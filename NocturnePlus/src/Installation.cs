// Nocturne Plus - preparation du dossier du jeu : sauvegardes, bascule des wrappers,
// ecriture des .ini, correctifs binaires reversibles.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NocturnePlus
{
    /// <summary>
    /// Un correctif de nocturne.exe : une liste de sites reperes par signature du code (voir Signatures.cs),
    /// donc independant de l'adresse exacte. Rien n'est ecrit si un seul site est introuvable ou ambigu.
    /// </summary>
    class Correctif
    {
        public string Id, Description;
        public string Fichier = "nocturne.exe";   // relatif au dossier du jeu
        public List<Site> Sites = new List<Site>();
        public Correctif Ajout(Site s) { Sites.Add(s); return this; }
    }

    /// <summary>nocturne.exe n'est pas une version reconnue : les correctifs demandes ne s'y appliquent pas.</summary>
    class ExeNonReconnu : Exception
    {
        public ExeNonReconnu(string m) : base(m) { }
    }

    class Installation
    {
        public readonly string Jeu, Donnees;
        public static readonly Encoding Win1252 = Encoding.GetEncoding(1252);
        string dossierSauvegarde;

        // Empreinte de l'exe de reference (Patch 2, build 1.00.52, version francaise Abandonware France).
        // Les correctifs ne s'appuient pas dessus (recherche par signature) ; elle ne sert qu'au journal.
        public const string Sha1NocturneExe = "c07307239723bd6db62acecde9d92f0597190d89";

        /// <summary>Patch officiel a installer quand l'exe n'est pas reconnu.</summary>
        public const string PatchOfficiel = "Nocturne Patch 2 (Build 1.00.52)";
        public const string LienPatchOfficiel = "https://www.patches-scrolls.de/patch/2952/7/28592";

        public Installation(string jeu)
        {
            Jeu = jeu;
            Donnees = Path.Combine(jeu, "_NocturnePlus");
            Directory.CreateDirectory(Donnees);
        }

        public string P(string relatif) { return Path.Combine(Jeu, relatif); }

        public static string Sha1(string fichier)
        {
            using (var s = SHA1.Create()) using (var f = File.OpenRead(fichier))
                return BitConverter.ToString(s.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
        }

        static bool MemeContenu(string a, string b)
        {
            if (!File.Exists(a) || !File.Exists(b)) return false;
            if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
            return Sha1(a) == Sha1(b);
        }

        /// <summary>Famille d'une DLL de wrapper d'apres ses informations de version.</summary>
        public static string Famille(string dll)
        {
            if (!File.Exists(dll)) return null;
            var v = FileVersionInfo.GetVersionInfo(dll);
            string p = (v.ProductName ?? "") + " " + (v.FileDescription ?? "");
            if (p.Contains("cnc-ddraw")) return "cnc";
            if (p.Contains("dgVoodoo")) return "dgv";
            if (p.Contains("DDrawCompat")) return "ddc";
            return "inconnu";
        }

        public static string Version(string dll)
        {
            if (!File.Exists(dll)) return "absent";
            var v = FileVersionInfo.GetVersionInfo(dll);
            return string.Format("{0} {1}", v.FileDescription ?? v.ProductName, v.FileVersion);
        }

        // ---------------------------------------------------------------- sauvegardes

        /// <summary>Copie un fichier dans le dossier de sauvegarde de ce lancement avant de le toucher.</summary>
        public void Sauvegarder(string relatif)
        {
            string src = P(relatif);
            if (!File.Exists(src)) return;
            if (dossierSauvegarde == null)
            {
                dossierSauvegarde = Path.Combine(Donnees, "sauvegardes", DateTime.Now.ToString("yyyy-MM-dd_HHmmss"));
                Directory.CreateDirectory(dossierSauvegarde);
            }
            string dst = Path.Combine(dossierSauvegarde, relatif);
            if (File.Exists(dst)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst);
        }

        public string DossierSauvegarde { get { return dossierSauvegarde; } }

        /// <summary>Range un fichier a l'ecart par renommage (jamais de suppression).</summary>
        string Ecarter(string relatif, string suffixe)
        {
            string src = P(relatif);
            string dst = src + "." + suffixe;
            if (File.Exists(dst))
            {
                // le nom de rangement est pris : on deplace dans _NocturnePlus\remplaces
                string dir = Path.Combine(Donnees, "remplaces");
                Directory.CreateDirectory(dir);
                dst = Path.Combine(dir, Path.GetFileName(src) + "." + suffixe + "." + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            }
            File.Move(src, dst);
            Journal.Note("  rangement : {0} -> {1}", relatif, dst.Substring(Jeu.Length + 1));
            return dst;
        }

        // ---------------------------------------------------------------- wrappers

        string Reserve(string famille) { return Path.Combine(Donnees, "wrappers", famille); }

        /// <summary>
        /// Constitue la reserve de wrappers (_NocturnePlus\wrappers\...) a partir des fichiers deja
        /// presents dans le dossier du jeu. Copie seulement : les fichiers d'origine restent ou ils sont.
        /// </summary>
        public void PreparerReserve()
        {
            string[] candidatsDdraw = { "ddraw.dll", "ddraw.dll.cnc", "DDraw.dll.dgv", "ddraw.dll.dgv", "ddraw.dll.ddc" };
            foreach (var c in candidatsDdraw)
            {
                string f = P(c), fam = Famille(f);
                if (fam == null || fam == "inconnu" || fam == "ddc") continue;
                string dst = Path.Combine(Reserve(fam), "ddraw.dll");
                if (File.Exists(dst)) continue;
                Directory.CreateDirectory(Reserve(fam));
                File.Copy(f, dst);
                Journal.Note("Reserve : {0} copie depuis {1} ({2})", fam, c, Version(f));
            }
            foreach (var c in new[] { "D3DImm.dll", "D3DImm.dll.dgv" })
            {
                string f = P(c);
                if (Famille(f) != "dgv") continue;
                string dst = Path.Combine(Reserve("dgv"), "D3DImm.dll");
                if (File.Exists(dst)) continue;
                Directory.CreateDirectory(Reserve("dgv"));
                File.Copy(f, dst);
                Journal.Note("Reserve : D3DImm dgVoodoo copie depuis {0} ({1})", c, Version(f));
            }
        }

        public bool ReserveDisponible(string famille)
        {
            return File.Exists(Path.Combine(Reserve(famille), "ddraw.dll")) &&
                   (famille != "dgv" || File.Exists(Path.Combine(Reserve(famille), "D3DImm.dll")));
        }

        /// <summary>Met en place le bon ddraw.dll (et D3DImm.dll) pour le mode.</summary>
        public void InstallerWrapper(Mode mode, Action<string> statut = null)
        {
            string fam = mode == Mode.Ultra ? "dgv" : "cnc";
            // aucun wrapper dans le dossier du jeu : cnc-ddraw vient de l'exe, dgVoodoo de sa release officielle
            if (!ReserveDisponible(fam))
            {
                if (fam == "cnc") Depots.ExtraireCnc(Reserve("cnc"), Jeu);
                else Depots.TelechargerDgVoodoo(Reserve("dgv"), Jeu, Path.Combine(Donnees, "telechargements"), statut);
            }

            Poser("ddraw.dll", fam);
            if (mode == Mode.Ultra) Poser("D3DImm.dll", "dgv");
            else if (File.Exists(P("D3DImm.dll"))) Ecarter("D3DImm.dll", Famille(P("D3DImm.dll")));

            // DSOAL faisait planter le menu : on garde le DirectSound de Windows
            if (File.Exists(P("dsound.dll"))) { Sauvegarder("dsound.dll"); Ecarter("dsound.dll", "off"); }

            Journal.Note("Wrapper actif : ddraw.dll = {0}{1}", Version(P("ddraw.dll")),
                mode == Mode.Ultra ? " ; D3DImm.dll = " + Version(P("D3DImm.dll")) : "");
        }

        /// <summary>
        /// Active la DLL "nom" de la famille voulue par simples renommages : la DLL active est rangee
        /// sous nom.famille (ddraw.dll.cnc, ddraw.dll.dgv...) et la DLL rangee reprend sa place.
        /// La reserve de _NocturnePlus ne sert que si la DLL rangee manque.
        /// </summary>
        void Poser(string nom, string fam)
        {
            string cible = P(nom), rangee = cible + "." + fam;
            if (Famille(cible) == fam) return;
            if (File.Exists(cible)) Ecarter(nom, Famille(cible));
            if (File.Exists(rangee) && Famille(rangee) == fam)
            {
                File.Move(rangee, cible);
                Journal.Note("  active : {0}.{1} -> {0}", nom, fam);
            }
            else
            {
                File.Copy(Path.Combine(Reserve(fam), nom.ToLowerInvariant() == "ddraw.dll" ? "ddraw.dll" : "D3DImm.dll"), cible);
                Journal.Note("  active : {0} copie depuis la reserve _NocturnePlus\\wrappers\\{1}", nom, fam);
            }
        }

        // ---------------------------------------------------------------- .ini

        public void EcrireIni(Reglages r, Ecrans.Info ecran)
        {
            // nocturne.ini : Windows-1252 obligatoire (accents de DeviceName)
            Sauvegarder(@"SYSTEM\nocturne.ini");
            var noc = new FichierIni(P(@"SYSTEM\nocturne.ini"), Win1252);
            int w = 640, h = 480;
            if (r.Mode == Mode.Ultra) Reglages.LireRes(r.ResInterne, out w, out h);
            noc.Ecrire("Graphics", "useDirect3D", r.Mode == Mode.Ultra ? "1" : "0");
            noc.Ecrire("Graphics", "gamePIXX", w.ToString());
            noc.Ecrire("Graphics", "gamePIXY", h.ToString());
            noc.Ecrire("Graphics", "gameBPP", "32");
            noc.Ecrire("Graphics", "rendererDLLPath", "tridx7.dll");
            noc.Ecrire("Graphics", "haloMode", "2");
            noc.Ecrire("Graphics", "shadowFlag", "1");
            if (!string.IsNullOrEmpty(r.PeripheriqueSon)) noc.Ecrire("Sound", "DeviceName", r.PeripheriqueSon);
            noc.Ecrire("Sound", "Mute", r.SonCoupe ? "1" : "0");
            noc.Ecrire("Sound", "HwMixingEnabled", "0");
            noc.Enregistrer();
            Rapporter("nocturne.ini", noc);

            Sauvegarder(@"SYSTEM\RENDER.INI");
            var ren = new FichierIni(P(@"SYSTEM\RENDER.INI"), Win1252);
            ren.Ecrire("Graphics", "useHoldBuffer", "1");
            if (r.DecorsHD && PackTexturesDisponible)
            {
                // presque toutes les textures 3D passent en 256x256 : de la place pour les garder en memoire
                ren.Ecrire("Textures", "maxTextures64", "64");
                ren.Ecrire("Textures", "maxTextures128", "16");
                ren.Ecrire("Textures", "maxTextures256", "256");
            }
            else if (r.Mode == Mode.Ultra && r.TexturesMax)
            {
                ren.Ecrire("Textures", "maxTextures64", "256");
                ren.Ecrire("Textures", "maxTextures128", "64");
                ren.Ecrire("Textures", "maxTextures256", "64");
            }
            else
            {
                ren.Ecrire("Textures", "maxTextures64", "192");
                ren.Ecrire("Textures", "maxTextures128", "4");
                ren.Ecrire("Textures", "maxTextures256", "32");
            }
            ren.Enregistrer();
            Rapporter("RENDER.INI", ren);

            if (r.Mode == Mode.Stable) EcrireCncDdraw(r, ecran);
            else EcrireDgVoodoo(r, ecran);
        }

        void EcrireCncDdraw(Reglages r, Ecrans.Info ecran)
        {
            Sauvegarder("ddraw.ini");
            var ini = new FichierIni(P("ddraw.ini"), Win1252);
            const string S = "ddraw";
            ini.Ecrire(S, "renderer", "opengl");
            ini.Ecrire(S, "fullscreen", "true");
            ini.Ecrire(S, "windowed", r.PleinEcran ? "false" : "true");
            ini.Ecrire(S, "border", "false");
            ini.Ecrire(S, "width", "0");
            ini.Ecrire(S, "height", "0");
            ini.Ecrire(S, "maintas", r.Etirer169 ? "false" : "true");
            ini.Ecrire(S, "boxing", "false");
            ini.Ecrire(S, "maxfps", r.FpsMax.ToString());
            ini.Ecrire(S, "vsync", r.VSync ? "true" : "false");
            ini.Ecrire(S, "adjmouse", "false");
            // cnc-ddraw sait se mettre seul en mono-coeur ; dans les autres modes il ecraserait l'epinglage
            ini.Ecrire(S, "singlecpu", r.Coeurs == Coeurs.MonoCoeur ? "true" : "false");
            ini.Ecrire(S, "nonexclusive", "true");
            ini.Ecrire(S, "shader", @"Shaders\interpolation\lanczos2-sharp.glsl");
            ini.Ecrire(S, "savesettings", "0");
            if (!r.PleinEcran && ecran != null)
            {
                // fenetre sans bordure plein ecran sur l'ecran choisi
                ini.Ecrire(S, "posX", ecran.X.ToString());
                ini.Ecrire(S, "posY", ecran.Y.ToString());
            }
            ini.Enregistrer();
            Rapporter("ddraw.ini (cnc-ddraw)", ini);
        }

        void EcrireDgVoodoo(Reglages r, Ecrans.Info ecran)
        {
            Sauvegarder("dgVoodoo.conf");
            var ini = new FichierIni(P("dgVoodoo.conf"), Win1252);
            ini.Ecrire("General", "OutputAPI", "d3d11_fl11_0");
            ini.Ecrire("General", "Adapters", "all");
            ini.Ecrire("General", "FullScreenOutput", ecran != null ? ecran.Ordinal.ToString() : "default");
            // JAMAIS de plein ecran exclusif en multi-ecrans (ecran noir, souris piegee)
            ini.Ecrire("General", "FullScreenMode", "false");
            ini.Ecrire("General", "ScalingMode", r.Etirer169 ? "stretched" : "stretched_ar");
            ini.Ecrire("General", "KeepWindowAspectRatio", "true");
            ini.Ecrire("General", "CaptureMouse", "false");
            ini.Ecrire("GeneralExt", "FPSLimit", r.FpsMax.ToString());
            ini.Ecrire("DirectX", "Filtering", r.Anisotrope > 1 ? r.Anisotrope.ToString() : "appdriven");
            ini.Ecrire("DirectX", "Antialiasing", r.Antialiasing);
            // le moteur dessine texte, menus et HUD en 640/854x480 : lissage a l'agrandissement
            ini.Ecrire("DirectX", "Bilinear2DOperations", r.Lissage2D ? "true" : "false");
            ini.Ecrire("DirectX", "Resolution", ResolutionUltra(r, ecran));
            ini.Ecrire("DirectX", "ForceVerticalSync", r.VSync ? "true" : "false");
            // Plein ecran : le jeu demande son plein ecran, dgVoodoo le simule (FullScreenMode=false).
            // Fenetre sans bordure : dgVoodoo force le fenetre, a la taille de l'ecran et sans bordure
            // (sans "fullscreensize", la fenetre garderait la taille 0x0 creee par le jeu).
            ini.Ecrire("DirectX", "AppControlledScreenMode", r.PleinEcran ? "true" : "false");
            ini.Ecrire("GeneralExt", "WindowedAttributes", r.PleinEcran ? "" : "borderless, fullscreensize");
            ini.Ecrire("DirectX", "DisableAltEnterToToggleScreenMode", "true");
            ini.Ecrire("DirectX", "dgVoodooWatermark", "false");
            ini.Ecrire("DirectX", "Mipmapping", r.MipmapsAuto ? "autogen_bilinear" : "appdriven");
            // "forcealways" simule un affichage 16 bits de 1999 (couleurs reduites + trame agrandie
            // a l'ancienne resolution) : pixels en blocs dans les degrades sombres. On reste en 32 bits.
            ini.Ecrire("DirectXExt", "Dithering", "disabled");
            ini.Enregistrer();
            Rapporter("dgVoodoo.conf", ini);
        }

        /// <summary>Resolution de rendu forcee de dgVoodoo : la 3D est vraiment rendue a cette taille.</summary>
        public static string ResolutionUltra(Reglages r, Ecrans.Info ecran)
        {
            int w, h;
            if (r.SortieUltra == "auto" || !Reglages.LireRes(r.SortieUltra, out w, out h))
            {
                h = ecran != null ? ecran.H : 1440;
                // la 3D garde le ratio de la resolution du jeu (4:3 en 640x480, 16:9 en 854x480)
                int wi, hi;
                if (!Reglages.LireRes(r.ResInterne, out wi, out hi)) { wi = 640; hi = 480; }
                w = r.Etirer169 && ecran != null ? ecran.W : (int)Math.Round(h * (double)wi / hi);
                // 854/480 n'est pas exactement 16:9 : on cale sur la largeur de l'ecran si l'ecart est infime
                if (ecran != null && Math.Abs(w - ecran.W) <= ecran.W / 100) w = ecran.W;
            }
            return string.Format("h:{0}, v:{1}", w, h);
        }

        static void Rapporter(string nom, FichierIni ini)
        {
            if (ini.Changements.Count == 0) Journal.Note("{0} : deja conforme", nom);
            foreach (var c in ini.Changements) Journal.Note("{0} : {1}", nom, c);
        }

        // ---------------------------------------------------------------- correctifs

        public static readonly List<Correctif> Correctifs = new List<Correctif>();
        public const string IdCamera169 = "camera169";
        public const string IdFichiersLibres = "fichierslibres";

        /// <summary>Correctifs a appliquer : ceux choisis, plus ceux que demandent le format et les decors HD.</summary>
        public HashSet<string> CorrectifsActifs(Reglages r)
        {
            var actifs = new HashSet<string>((r.Correctifs ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            int w, h;
            bool large = r.Mode == Mode.Ultra && Reglages.LireRes(r.ResInterne, out w, out h) && w * 3 > h * 4;
            if (large) actifs.Add(IdCamera169); else actifs.Remove(IdCamera169);
            if (r.DecorsHD && PackDecorsDisponible) actifs.Add(IdFichiersLibres); else actifs.Remove(IdFichiersLibres);
            return actifs;
        }

        // ---------------------------------------------------------------- decors remasterises

        // Le pack vit dans _NocturnePlus\hd\<dossier> ; actif, chaque dossier est deplace (renommage,
        // meme disque) en <jeu>\<dossier>, ou le moteur le lit avant les .POD grace au correctif
        // "fichiers libres". BACKDROP = decors precalcules, ART = textures 3D agrandies en 256x256.
        static readonly string[] DossiersPack = { "BACKDROP", "ART" };
        string PackRange(string d) { return Path.Combine(Donnees, "hd", d); }

        bool PackPresent(string d) { return Directory.Exists(PackRange(d)) || Directory.Exists(P(d)); }
        public bool PackDecorsDisponible { get { return DossiersPack.Any(PackPresent); } }
        public bool PackTexturesDisponible { get { return PackPresent("ART"); } }

        /// <summary>
        /// Genere le pack HD depuis les .POD de l'utilisateur (Real-ESRGAN telecharge au besoin).
        /// Un pack existant est mis de cote dans _NocturnePlus\hd_ancien_<date>, jamais supprime.
        /// </summary>
        public int GenererPackHD(Action<string> statut)
        {
            InstallerDecors(false);
            string hd = Path.Combine(Donnees, "hd");
            if (Directory.Exists(hd))
            {
                string cote = hd + "_ancien_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                Directory.Move(hd, cote);
                Journal.Note("Ancien pack HD mis de cote : {0}", cote.Substring(Jeu.Length + 1));
            }
            string esrgan = Depots.TelechargerRealEsrgan(Path.Combine(Donnees, "outils", "realesrgan"), Path.Combine(Donnees, "telechargements"), statut);
            return new PackHD(Jeu, hd, statut).Generer(esrgan);
        }

        public void InstallerDecors(bool actif)
        {
            foreach (var d in DossiersPack)
            {
                if (actif && Directory.Exists(PackRange(d)) && !Directory.Exists(P(d)))
                {
                    Directory.Move(PackRange(d), P(d));
                    Journal.Note("Pack HD active : {0} fichiers dans {1}\\", Directory.GetFiles(P(d)).Length, d);
                }
                else if (!actif && Directory.Exists(P(d)) && !Directory.Exists(PackRange(d)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(PackRange(d)));
                    Directory.Move(P(d), PackRange(d));
                    Journal.Note("Pack HD desactive : {0}\\ range dans _NocturnePlus\\hd", d);
                }
            }
        }

        public string CopieOriginale(string relatif) { return Path.Combine(Donnees, "originaux", relatif); }

        /// <summary>Etat d'un correctif : "absent", "applique", "partiel" ou "incompatible" (exe non reconnu).</summary>
        public string Etat(Correctif c)
        {
            string f = P(c.Fichier);
            if (!File.Exists(f)) return "incompatible";
            return Nom(Signatures.EtatDe(File.ReadAllBytes(f), c));
        }

        static string Nom(Signatures.Etat e)
        {
            return e == Signatures.Etat.Applique ? "applique" : e == Signatures.Etat.Absent ? "absent" : e == Signatures.Etat.Partiel ? "partiel" : "incompatible";
        }

        /// <summary>Correctifs demandes que l'exe present ne permet pas d'appliquer (version non reconnue).</summary>
        public List<Correctif> NonReconnus(IEnumerable<string> ids)
        {
            var liste = Correctifs.Where(c => ids.Contains(c.Id)).ToList();
            if (liste.Count == 0) return liste;
            var d = File.Exists(P("nocturne.exe")) ? File.ReadAllBytes(P("nocturne.exe")) : new byte[0];
            return liste.Where(c => d.Length < 0x400 || Signatures.EtatDe(d, c) == Signatures.Etat.Inconnu).ToList();
        }

        public static string MessageNonReconnu(IEnumerable<Correctif> liste)
        {
            return "Your nocturne.exe is not a version Nocturne Plus recognises, so these engine fixes cannot be applied:\n\n" +
                string.Join("\n", liste.Select(c => "• " + c.Description).ToArray()) +
                "\n\nNocturne Plus is made for the latest official patch, " + PatchOfficiel + ": install it (" + LienPatchOfficiel +
                "), or use a copy of the game that already includes it.";
        }

        /// <summary>Applique (actif) ou retire un correctif. L'exe n'est ecrit que si le contenu change.</summary>
        public void Appliquer(Correctif c, bool actif)
        {
            string src = P(c.Fichier);
            var data = File.ReadAllBytes(src);
            var etat = Signatures.EtatDe(data, c);
            if (etat == Signatures.Etat.Inconnu)
            {
                if (!actif) return;   // rien a retirer d'un exe qu'on ne reconnait pas : on n'y touche pas
                throw new ExeNonReconnu(MessageNonReconnu(new[] { c }));
            }
            if (actif && etat == Signatures.Etat.Applique) return;
            if (!actif && etat == Signatures.Etat.Absent) return;

            // copie de l'original avant la toute premiere modification : l'exe sans aucun correctif,
            // reconstitue en retirant ceux qui seraient deja poses
            string orig = CopieOriginale(c.Fichier);
            if (!File.Exists(orig))
            {
                var vierge = (byte[])data.Clone();
                bool ok = Correctifs.Where(x => x.Fichier == c.Fichier).All(x => Signatures.EtatDe(vierge, x) == Signatures.Etat.Inconnu || Signatures.Modifier(vierge, x, false));
                if (!ok) throw new InvalidOperationException(c.Fichier + ": the original copy could not be rebuilt, nothing written.");
                Directory.CreateDirectory(Path.GetDirectoryName(orig));
                File.WriteAllBytes(orig, vierge);
                Journal.Note("Copie d'origine conservee : {0}", orig.Substring(Jeu.Length + 1));
            }
            if (!Signatures.Modifier(data, c, actif)) throw new InvalidOperationException("Patch " + c.Id + ": unexpected bytes in " + c.Fichier + ", nothing written.");
            Sauvegarder(c.Fichier);
            File.WriteAllBytes(src, data);
            Journal.Note("Correctif {0} {1} dans {2}", c.Id, actif ? "applique" : "retire", c.Fichier);
        }

        // ---------------------------------------------------------------- reparation

        static bool EstVierge(byte[] exe) { return Correctifs.All(c => Signatures.EtatDe(exe, c) == Signatures.Etat.Absent); }

        /// <summary>Remet la configuration connue-bonne (mode Stable, aucun correctif).</summary>
        public void Reparer(Ecrans.Info ecran)
        {
            Journal.Note("=== Reparation : retour a la configuration connue-bonne (mode Stable) ===");
            PreparerReserve();
            InstallerDecors(false);
            foreach (var c in Correctifs) if (Etat(c) != "absent" && Etat(c) != "incompatible") Appliquer(c, false);
            // exe encore different de la copie d'origine (correctif d'une ancienne version...) : on remet
            // la copie, si elle est bien un exe reconnu et vierge ; l'exe actuel est range, pas supprime
            string orig = CopieOriginale("nocturne.exe");
            if (File.Exists(orig) && File.Exists(P("nocturne.exe")) && Sha1(P("nocturne.exe")) != Sha1(orig) && EstVierge(File.ReadAllBytes(orig)))
            {
                Sauvegarder("nocturne.exe"); Ecarter("nocturne.exe", "modifie");
                File.Copy(orig, P("nocturne.exe"));
                Journal.Note("nocturne.exe d'origine remis en place");
            }
            var r = new Reglages();
            InstallerWrapper(Mode.Stable);
            EcrireIni(r, ecran);
            // dgVoodoo.conf aussi remis en valeurs sures, meme s'il ne sert pas en Stable
            EcrireDgVoodoo(new Reglages { Mode = Mode.Ultra }, ecran);
            var noc = new FichierIni(P(@"SYSTEM\nocturne.ini"), Win1252);
            noc.Ecrire("Graphics", "currentBoard", "0");
            noc.Enregistrer();
            if (File.Exists(P(@"video\opening.avi"))) Journal.Note("Attention : video\\opening.avi est present (codec Indeo absent de Windows 11).");
            Journal.Note("Reparation terminee. Sauvegarde des fichiers remplaces : {0}", dossierSauvegarde ?? "(rien a sauvegarder)");
        }
    }
}
