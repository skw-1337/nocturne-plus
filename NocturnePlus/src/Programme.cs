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
            string jeu = TrouverJeu();
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
                inst.PreparerReserve();
                inst.InstallerWrapper(r.Mode);
                inst.EcrireIni(r, e);
                inst.InstallerDecors(r.DecorsHD);
                var actifs = inst.CorrectifsActifs(r);
                foreach (var c in Installation.Correctifs) inst.Appliquer(c, actifs.Contains(c.Id));
                return 0;
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

    /// <summary>Catalogue des correctifs connus (rempli au fil de l'analyse de nocturne.exe / tridx7.dll).</summary>
    static class Correctifs
    {
        public static void Enregistrer()
        {
            if (Installation.Correctifs.Count > 0) return;

            // Recherche des ressources (FUN_004c85f0) : le moteur enregistre d'abord le chercheur des
            // .POD (0x4F9250) puis celui du disque (0x456800) ; un fichier libre n'est lu que s'il
            // manque dans les .POD. Correctif : ordre inverse, les fichiers du dossier du jeu
            // (ex. BACKDROP\*.RAW des decors remasterises) passent avant les archives.
            Installation.Correctifs.Add(new Correctif
            {
                Id = Installation.IdFichiersLibres,
                Description = "Loose files override the .POD archives (needed for HD backgrounds)",
                Fichier = "nocturne.exe",
                Sha1Origine = Installation.Sha1NocturneExe
            }
            // 0x4C86DA : push 0x4F9250 (POD) -> push 0x456800 (disque)
            .Ajout(0xC7ADA, "6850924F00", "6800684500")
            // 0x4C86E7 : push 0x456800 (disque) -> push 0x4F9250 (POD)
            .Ajout(0xC7AE7, "6800684500", "6850924F00"));

            // Au demarrage, le moteur lit la memoire avec GlobalMemoryStatus : avec beaucoup de RAM,
            // un programme 32 bits de 1999 lit "0.0 Mo" de fichier d'echange et affiche un
            // avertissement (le shim GlobalMemoryStatusLie de nocturne.sdb le masquait).
            // Correctif : les deux tests (RAM < 60 Mo, echange < 200 Mo) sautent toujours l'avertissement.
            Installation.Correctifs.Add(new Correctif
            {
                Id = "memoire",
                Description = "No false low-memory warning at startup",
                Fichier = "nocturne.exe",
                Sha1Origine = Installation.Sha1NocturneExe
            }
            // 0x4C879E : jge (RAM >= 60 Mo) -> jmp, meme cible
            .Ajout(0xC7B9E, "0F8D6D020000", "E96E02000090")
            // 0x4C8A1B : jge (echange >= 200 Mo) -> jmp, meme cible
            .Ajout(0xC7E1B, "0F8D7A010000", "E97B01000090"));

            // Camera du moteur (FUN_00440010) : hauteur plafonnee a 480 et largeur = hauteur*4/3,
            // donc toujours une fenetre 3D de 640x480. Correctif : largeur = largeur du jeu *
            // hauteur camera / hauteur du jeu (640 en 640x480, 854 en 854x480). Applique par le
            // lanceur seulement quand une resolution large est choisie.
            Installation.Correctifs.Add(new Correctif
            {
                Id = Installation.IdCamera169,
                Description = "Widescreen camera (applied automatically for 16:9 game resolutions)",
                Fichier = "nocturne.exe",
                Sha1Origine = Installation.Sha1NocturneExe
            }
            // 0x440038 : lea edx,[esi*4] / mov esi,3 / mov eax,edx / sar edx,1Fh / idiv esi
            //         -> mov eax,[0x5B761C] / imul eax,esi / cdq / idiv dword [0x5B7620] / nop x4
            .Ajout(0x3F438, "8D14B500000000BE0300000089D0C1FA1FF7FE", "A11C765B000FAFC699F73D20765B0090909090"));

            // Horloge du moteur (FUN_00558a30) : elle cumulait a chaque appel le temps ecoule depuis
            // l'appel precedent, converti en unites de 1/1 179 648 s puis TRONQUE. Avec le
            // QueryPerformanceCounter a 10 MHz de Windows 10/11, un appel en boucle serree mesure
            // moins d'une unite, arrondie a 0 : le temps du jeu n'avance plus et ses boucles d'attente
            // tournent sans fin (gel en multi-coeur, menus lents, retour d'Alt+Tab lent).
            // Correctif : temps total depuis l'initialisation, calcule d'un bloc sur 64 bits.
            Installation.Correctifs.Add(new Correctif
            {
                Id = "horloge",
                Description = "Precise engine clock (fixes multi-core freeze, slow menus)",
                Fichier = "nocturne.exe",
                Sha1Origine = Installation.Sha1NocturneExe
            }
            // 0x55882C : l'init range le QPC de depart dans la base [0x2DE07D0] (au lieu de [0x2DE07D8])
            .Ajout(0x157C2C, "68D807DE02", "68D007DE02")
            // 0x558A5E : movsd movsd -> nop nop (la base n'est plus ecrasee a chaque appel)
            .Ajout(0x157E5E, "A5A5", "9090")
            // 0x558AC8 : fistp dword [esp+28h] -> fistp qword [esp+18h] (pas de debordement)
            .Ajout(0x157EC8, "DB5C2428", "DF7C2418")
            // 0x558ACC : mov eax,[esp+28h] -> mov eax,[esp+18h] (32 bits bas, qui bouclent comme avant)
            .Ajout(0x157ECC, "8B442428", "8B442418")
            // 0x558AD0 : add esi,eax -> mov esi,eax (le compteur prend le total au lieu d'ajouter)
            .Ajout(0x157ED0, "01C6", "89C6"));

            // En 3D, le moteur detruit tout son rendu quand il perd le focus (FUN_00553b70 :
            // arret du rendu + fenetre reduite) et le reconstruit au retour (FUN_00553ba0 :
            // device, surfaces, textures ; en cas d'echec il retombe sur le DirectDraw natif).
            // Sous dgVoodoo en fenetre sans bordure rien n'est perdu : on garde le rendu en place.
            // Le mode logiciel (Stable) ne passe pas par ces branches.
            Installation.Correctifs.Add(new Correctif
            {
                Id = "alttab3d",
                Description = "Fast Alt+Tab in 3D (renderer no longer torn down/rebuilt)",
                Fichier = "nocturne.exe",
                Sha1Origine = Installation.Sha1NocturneExe
            }
            // 0x553B80 : jne 0x553B83 -> nop nop (retour immediat, sans arret du rendu ni reduction)
            .Ajout(0x152F80, "7501", "9090")
            // 0x553BBD : mov edx,[0x5B7624] -> jmp 0x553C81 (va directement au SetFocus final)
            .Ajout(0x152FBD, "8B1524765B", "E9BF000000"));
        }
    }
}
