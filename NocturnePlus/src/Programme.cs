// Nocturne Plus - point d'entree.
//   NocturnePlus.exe                 fenetre d'options
//   NocturnePlus.exe /lancer         lance directement avec les derniers reglages
//   NocturnePlus.exe /diag [s]       releve des fils du jeu en cours (gel)
//   NocturnePlus.exe /fps [s]        images distinctes par seconde du jeu en cours
//   NocturnePlus.exe /touche VK [s]  envoie une touche (hexa) au jeu et mesure la reaction
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NocturnePlus
{
    static class Programme
    {
        const string DossierParDefaut = @"C:\Jeux\ABWFR\Nocturne";

        [STAThread]
        static int Main(string[] args)
        {
            // /jeu <dossier> en tete : travailler sur un autre dossier de jeu (tests)
            string jeu = null;
            if (args.Length > 1 && args[0].ToLowerInvariant() == "/jeu")
            {
                jeu = args[1];
                args = args.Skip(2).ToArray();
            }
            if (jeu == null) jeu = TrouverJeu();
            if (jeu == null)
            {
                Application.EnableVisualStyles();
                using (var d = new FolderBrowserDialog { Description = "Nocturne folder (the one containing nocturne.exe)" })
                {
                    if (d.ShowDialog() != DialogResult.OK || !File.Exists(Path.Combine(d.SelectedPath, "nocturne.exe"))) return 1;
                    jeu = d.SelectedPath;
                }
            }
            var inst = new Installation(jeu);
            Journal.Fichier = Path.Combine(inst.Donnees, "journal.txt");
            Correctifs.Enregistrer();

            string cmd = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            if (cmd == "/diag" || cmd == "/fps" || cmd == "/touche" || cmd == "/modules") return Outil(cmd, args);
            if (cmd == "/preparer") return Preparer(inst, args);
            if (cmd == "/apercu") return Apercu(inst, args);
            if (cmd == "/genererpack")
            {
                try { inst.GenererPackHD(s => Journal.Note("Pack HD : {0}", s)); return 0; }
                catch (Exception ex) { Journal.Note("ERREUR pack HD : {0}", ex); return 4; }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // une erreur du lanceur va dans le journal et un message court, pas dans la boite .NET
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                Journal.Note("ERREUR du lanceur : {0}", e.Exception);
                MessageBox.Show("Internal launcher error, details in _NocturnePlus\\journal.txt:\n" + e.Exception.Message, "Nocturne Plus", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            Application.Run(new Fenetre(inst, Path.Combine(inst.Donnees, "NocturnePlus.ini"), cmd == "/lancer"));
            return 0;
        }

        static string TrouverJeu()
        {
            string ici = Path.GetDirectoryName(Application.ExecutablePath);
            foreach (var d in new[] { ici, Path.GetDirectoryName(ici), DossierParDefaut })
                if (!string.IsNullOrEmpty(d) && File.Exists(Path.Combine(d, "nocturne.exe"))) return d;
            return null;
        }

        /// <summary>/preparer ultra|stable|reparer : installe wrapper et .ini sans lancer le jeu.</summary>
        static int Preparer(Installation inst, string[] args)
        {
            string quoi = args.Length > 1 ? args[1].ToLowerInvariant() : "stable";
            var r = Reglages.Charger(Path.Combine(inst.Donnees, "NocturnePlus.ini"));
            var ecrans = Ecrans.Lister();
            var e = ecrans.FirstOrDefault(x => x.Peripherique == r.Ecran) ?? ecrans.FirstOrDefault(x => x.Principal);
            try
            {
                if (quoi == "reparer") { inst.Reparer(e); return 0; }
                r.Mode = quoi == "ultra" ? Mode.Ultra : Mode.Stable;
                Journal.Note("=== Preparation seule : mode {0} ===", r.Mode);
                // comme le bouton Launch : tout decoche = exe ni verifie ni touche ; exe non reconnu =
                // preparation "jouer quand meme" (sans correctif, mono-coeur, 640x480, sans decors HD)
                var actifs = inst.CorrectifsActifs(r);
                bool toucherExe = actifs.Count > 0, inconnu = false;
                if (toucherExe && inst.NonReconnus(actifs).Count > 0)
                {
                    Journal.Note("nocturne.exe non reconnu : {0}", Installation.MessageNonReconnu(inst.NonReconnus(actifs)).Replace("\n", " "));
                    Journal.Note("Jouer quand meme : aucun correctif, nocturne.exe laisse tel quel");
                    toucherExe = false; inconnu = true;
                    r.Coeurs = Coeurs.MonoCoeur; r.DecorsHD = false;
                    int lw, lh;
                    if (Reglages.LireRes(r.ResInterne, out lw, out lh) && lw * 3 > lh * 4) r.ResInterne = "640x480";
                }
                inst.PreparerReserve();
                inst.InstallerWrapper(r.Mode);
                inst.EcrireIni(r, e);
                inst.InstallerDecors(r.DecorsHD);
                if (toucherExe) foreach (var c in Installation.Correctifs) inst.Appliquer(c, actifs.Contains(c.Id));
                else if (!inconnu) Journal.Note("nocturne.exe : aucun correctif demande, exe ni verifie ni modifie");
                return inconnu ? 5 : 0;
            }
            catch (Exception ex) { Journal.Note("ERREUR : {0}", ex.Message); return 3; }
        }

        /// <summary>/apercu fichier.png [page] : image de la fenetre du lanceur seule, dessinee hors ecran.</summary>
        static int Apercu(Installation inst, string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var f = new Fenetre(inst, Path.Combine(inst.Donnees, "NocturnePlus.ini"), false))
            {
                f.StartPosition = FormStartPosition.Manual; f.Location = new System.Drawing.Point(-4000, -4000); f.ShowInTaskbar = false;
                f.Show(); Application.DoEvents();
                int page; if (args.Length > 2 && int.TryParse(args[2], out page)) f.AfficherPage(page);
                System.Threading.Thread.Sleep(1500); Application.DoEvents(); // laisse le temps a la verification de mise a jour
                using (var bmp = new System.Drawing.Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(args.Length > 1 ? args[1] : Path.Combine(inst.Donnees, "apercu.png"));
                }
                f.Close();
            }
            return 0;
        }

        static int Outil(string cmd, string[] args)
        {
            var p = Process.GetProcessesByName("nocturne").FirstOrDefault();
            if (p == null) { Journal.Note("{0} : aucun nocturne.exe en cours", cmd); return 2; }
            int s = 10;
            if (cmd == "/diag") { if (args.Length > 1) int.TryParse(args[1], out s); Diagnostic.Releve(p.Id, s * 20, 50); }
            else if (cmd == "/modules")
            {
                // preuve du chemin de rendu : tridx7.dll + dgVoodoo + d3d11 = 3D materielle
                IntPtr hp = Natif.OpenProcess(0x0410, false, p.Id);
                var carte = new CarteModules(hp); Natif.CloseHandle(hp);
                Journal.Note("Modules : {0}", string.Join(", ", carte.Liste.Select(m => m.Nom).ToArray()));
            }
            else if (cmd == "/fps") { if (args.Length > 1) int.TryParse(args[1], out s); Journal.Note("Mesure : {0}", Mesures.Fps(p.Id, s)); }
            else
            {
                ushort vk = Convert.ToUInt16(args.Length > 1 ? args[1] : "0D", 16);
                if (args.Length > 2) int.TryParse(args[2], out s);
                Journal.Note("Mesure : {0}", Mesures.Touche(p.Id, vk, s));
            }
            return 0;
        }
    }

    /// <summary>
    /// Catalogue des correctifs de nocturne.exe. Chaque site est retrouve par signature du code (les
    /// adresses absolues, propres a une compilation, sont des "??") ; les adresses entre parentheses
    /// sont celles du Patch 2 (build 1.00.52), pour reference.
    /// </summary>
    static class Correctifs
    {
        static Func<Lieu, bool?> Zone(int debut, string origine, string corrige)
        {
            return l =>
            {
                if (Correspond(l, debut, origine)) return false;
                if (Correspond(l, debut, corrige)) return true;
                return null;
            };
        }

        static bool Correspond(Lieu l, int debut, string motif)
        {
            var m = Signatures.Motif(motif);
            if (l.Pos + debut < 0 || l.Pos + debut + m.Length > l.Exe.Length) return false;
            for (int i = 0; i < m.Length; i++) if (m[i].HasValue && l.Octet(debut + i) != m[i].Value) return false;
            return true;
        }

        static byte[] Concat(params byte[][] b) { return b.SelectMany(x => x).ToArray(); }

        /// <summary>Site dont les octets corriges et d'origine sont fixes.</summary>
        static Site Fixe(string motif, int debut, string origine, string corrige)
        {
            return new Site
            {
                Motif = motif, Debut = debut, Longueur = Signatures.Hex(origine).Length,
                EstApplique = Zone(debut, origine, corrige),
                Nouveau = l => Signatures.Hex(corrige), Ancien = l => Signatures.Hex(origine)
            };
        }

        /// <summary>jge rel32 (6 octets) -> jmp vers la meme cible + nop, et retour.</summary>
        static Site JgeVersJmp(string motif, int debut)
        {
            return new Site
            {
                Motif = motif, Debut = debut, Longueur = 6,
                EstApplique = Zone(debut, "0F8D????????", "E9????????90"),
                Nouveau = l => Concat(new byte[] { 0xE9 }, Signatures.Le32(l.U32(debut + 2) + 1), new byte[] { 0x90 }),
                Ancien = l => Concat(new byte[] { 0x0F, 0x8D }, Signatures.Le32(l.U32(debut + 1) - 1))
            };
        }

        static byte[] Echanger(Lieu l, int debut)
        {
            var b = new byte[18];
            Array.Copy(l.Exe, l.Pos + debut, b, 0, 18);
            for (int i = 1; i <= 4; i++) { byte t = b[i]; b[i] = b[13 + i]; b[13 + i] = t; }
            return b;
        }

        // Horloge : adresses de la base (X) et du depart (Y) lues dans la fonction d'horloge elle-meme
        const string MotifHorloge = "5756 BF???????? BE???????? 68???????? ???? 2EFF15";

        public static void Enregistrer()
        {
            if (Installation.Correctifs.Count > 0) return;

            // Recherche des ressources (FUN_004c85f0) : le moteur enregistre d'abord le chercheur des
            // .POD (0x4F9250) puis celui du disque (0x456800) ; un fichier libre n'est lu que s'il
            // manque dans les .POD. Correctif : ordre inverse, les fichiers du dossier du jeu
            // (ex. BACKDROP\*.RAW des decors remasterises) passent avant les archives.
            // Site (0x4C86DA) : push chercheur1 / call enregistrer / add esp,4 / push chercheur2.
            const int fl = 20;
            Installation.Correctifs.Add(new Correctif
            {
                Id = Installation.IdFichiersLibres,
                Description = "Loose files override the .POD archives (needed for HD backgrounds)"
            }.Ajout(new Site
            {
                Motif = "BE020000005789 35???????? E8????????83C404 68????????E8????????83C404 68????????E8????????83C404",
                Debut = fl, Longueur = 18,
                EstApplique = l =>
                {
                    bool premierPod = Signatures.EstChercheurPod(l.Exe, l.U32(fl + 1)), secondPod = Signatures.EstChercheurPod(l.Exe, l.U32(fl + 14));
                    if (premierPod && !secondPod) return false;
                    if (!premierPod && secondPod) return true;
                    return null;
                },
                // les deux appels visent la meme fonction : on echange seulement les deux adresses poussees
                Nouveau = l => Echanger(l, fl), Ancien = l => Echanger(l, fl)
            }));

            // Au demarrage, le moteur lit la memoire avec GlobalMemoryStatus : avec beaucoup de RAM,
            // un programme 32 bits de 1999 lit "0.0 Mo" de fichier d'echange et affiche un
            // avertissement (le shim GlobalMemoryStatusLie de nocturne.sdb le masquait).
            // Correctif : les deux tests (RAM < 60 Mo, echange < 200 Mo) sautent toujours l'avertissement.
            Installation.Correctifs.Add(new Correctif
            {
                Id = "memoire",
                Description = "No false low-memory warning at startup"
            }
            // (0x4C879E) cmp [ram],3C00000h / jge -> jmp, meme cible
            .Ajout(JgeVersJmp("813D????????0000C003 ???????????? 68", 10))
            // (0x4C8A1B) cmp [echange],0C800000h / jge -> jmp, meme cible
            .Ajout(JgeVersJmp("813D????????0000800C ???????????? 68", 10)));

            // Camera du moteur (FUN_00440010) : hauteur plafonnee a 480 et largeur = hauteur*4/3,
            // donc toujours une fenetre 3D de 640x480. Correctif : largeur = largeur du jeu *
            // hauteur camera / hauteur du jeu (640 en 640x480, 854 en 854x480). Applique par le
            // lanceur seulement quand une resolution large est choisie.
            // (0x440038) lea edx,[esi*4] / mov esi,3 / mov eax,edx / sar edx,1Fh / idiv esi
            //         -> mov eax,[largeur] / imul eax,esi / cdq / idiv dword [hauteur] / nop x4
            // L'adresse de la hauteur du jeu (0x5B7620) est lue dans FUN_0043ff50, qui la compare
            // a 480 ; la largeur (0x5B761C) est rangee juste avant.
            const int cam = 19;
            const string CameraOrigine = "8D14B500000000BE0300000089D0C1FA1FF7FE";
            Installation.Correctifs.Add(new Correctif
            {
                Id = Installation.IdCamera169,
                Description = "Widescreen camera (applied automatically for 16:9 game resolutions)"
            }.Ajout(new Site
            {
                Motif = "81FEE0010000 7E05 BEE0010000 89B340010000 " + new string('?', 38) + " C7834C01000001000000",
                Debut = cam, Longueur = 19,
                EstApplique = Zone(cam, CameraOrigine, "A1????????0FAFC699F73D????????90909090"),
                Nouveau = l =>
                {
                    var ancre = Signatures.Unique(l.Exe, "6800000100 53 E8???????? 8B15???????? 83C408 81FAE0010000 0F8D");
                    if (ancre == null) return null;
                    uint h = ancre.U32(13), w = h - 4;
                    return Concat(new byte[] { 0xA1 }, Signatures.Le32(w), Signatures.Hex("0FAFC699F73D"), Signatures.Le32(h), Signatures.Hex("90909090"));
                },
                Ancien = l => Signatures.Hex(CameraOrigine)
            }));

            // Horloge du moteur (FUN_00558a30) : elle cumulait a chaque appel le temps ecoule depuis
            // l'appel precedent, converti en unites de 1/1 179 648 s puis TRONQUE. Avec le
            // QueryPerformanceCounter a 10 MHz de Windows 10/11, un appel en boucle serree mesure
            // moins d'une unite, arrondie a 0 : le temps du jeu n'avance plus et ses boucles d'attente
            // tournent sans fin (gel en multi-coeur, menus lents, retour d'Alt+Tab lent).
            // Correctif : temps total depuis l'initialisation, calcule d'un bloc sur 64 bits.
            Installation.Correctifs.Add(new Correctif
            {
                Id = "horloge",
                Description = "Precise engine clock (fixes multi-core freeze, slow menus)"
            }
            // (0x55882C) l'init range le QPC de depart dans la base X (au lieu de Y = X+8)
            .Ajout(new Site
            {
                Motif = "DF6DF0 DEC1 68???????? DD1D???????? 2EFF15", Debut = 5, Longueur = 5,
                EstApplique = l =>
                {
                    var f = Signatures.Unique(l.Exe, MotifHorloge);
                    if (f == null) return null;
                    uint v = l.U32(6);
                    if (v == f.U32(8)) return false;
                    if (v == f.U32(3)) return true;
                    return null;
                },
                Nouveau = l => { var f = Signatures.Unique(l.Exe, MotifHorloge); return f == null ? null : Concat(new byte[] { 0x68 }, Signatures.Le32(f.U32(3))); },
                Ancien = l => { var f = Signatures.Unique(l.Exe, MotifHorloge); return f == null ? null : Concat(new byte[] { 0x68 }, Signatures.Le32(f.U32(8))); }
            })
            // (0x558A5E) movsd movsd -> nop nop (la base n'est plus ecrasee a chaque appel)
            .Ajout(Fixe(MotifHorloge, 17, "A5A5", "9090"))
            // (0x558AC8) fistp dword [esp+28h] / mov eax,[esp+28h] / add esi,eax
            //         -> fistp qword [esp+18h] / mov eax,[esp+18h] / mov esi,eax (le compteur prend le total)
            .Ajout(Fixe("DDD9E8????????" + new string('?', 20) + "8935????????89F05E5F", 7, "DB5C24288B44242801C6", "DF7C24188B44241889C6")));

            // En 3D, le moteur detruit tout son rendu quand il perd le focus (FUN_00553b70 :
            // arret du rendu + fenetre reduite) et le reconstruit au retour (FUN_00553ba0 :
            // device, surfaces, textures ; en cas d'echec il retombe sur le DirectDraw natif).
            // Sous dgVoodoo en fenetre sans bordure rien n'est perdu : on garde le rendu en place.
            // Le mode logiciel (Stable) ne passe pas par ces branches.
            const int alt = 26;
            Installation.Correctifs.Add(new Correctif
            {
                Id = "alttab3d",
                Description = "Fast Alt+Tab in 3D (renderer no longer torn down/rebuilt)"
            }
            // (0x553B80) jne +1 -> nop nop (retour immediat, sans arret du rendu ni reduction)
            .Ajout(Fixe("833D????????007409833D????????00 ???? C353", 16, "7501", "9090"))
            // (0x553BBD) mov edx,[hauteur+4] -> jmp vers le SetFocus final (0x553C81)
            .Ajout(new Site
            {
                Motif = "833D????????000F84???????? 833D????????000F84???????? ???????????? 52 8B0D???????? 51 8B1D???????? 53",
                Debut = alt, Longueur = 5,
                EstApplique = Zone(alt, "8B15????????", "E9BF000000??"),
                Nouveau = l => Correspond(l, alt + 5 + 0xBF, "8B1D????????532EFF15") ? Signatures.Hex("E9BF000000") : null,
                // 5 octets reecrits : le 6e (octet haut de l'adresse) n'a jamais ete touche
                Ancien = l => Concat(new byte[] { 0x8B, 0x15 }, Signatures.Le32(l.U32(alt + 9) + 4).Take(3).ToArray())
            }));
        }
    }
}
