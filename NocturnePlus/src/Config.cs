// Nocturne Plus - reglages du lanceur, edition des .ini, journal.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NocturnePlus
{
    enum Mode { Ultra, Stable }
    enum Coeurs { MonoCoeur, JeuEpingle, MultiCoeur }

    /// <summary>Reglages choisis dans la fenetre, enregistres dans NocturnePlus.ini.</summary>
    class Reglages
    {
        public Mode Mode = Mode.Stable;
        public string Ecran = "";                 // \\.\DISPLAY1 ...
        public string SortieUltra = "auto";       // "auto" (4:3 a la hauteur de l'ecran) ou "LxH"
        public string ResInterne = "640x480";     // resolution du moteur en mode Ultra
        public bool Etirer169 = false;            // false : 4:3 avec barres
        public bool PleinEcran = false;           // false : fenetre sans bordure (Alt+Tab facile)
        public int FpsMax = 60;
        public bool VSync = true;
        public int Anisotrope = 16;               // 0 = laisse le jeu
        public string Antialiasing = "off";       // off, 2x, 4x, 8x
        public bool MipmapsAuto = true;
        public bool TexturesMax = false;
        public bool Lissage2D = true;             // decor 2D (854x480) agrandi x3 en bilineaire plutot qu'en blocs de 3x3
        public bool DecorsHD = true;              // decors precalcules remasterises (si le pack existe)             // menus, texte et HUD agrandis en bilineaire plutot qu'en gros pixels
        public string PeripheriqueSon = "";       // vide = ne pas toucher
        public bool SonCoupe = false;
        public Coeurs Coeurs = Coeurs.MonoCoeur;
        public int CoeurJeu = 2;                  // index de processeur logique (2 = CPU2, masque 4)
        public int DelaiGel = 30;                 // s avant arret force (0 = jamais)
        public bool DiagGel = true;               // releve des threads au gel
        public string Correctifs = "horloge,memoire";
        public string Depot = MiseAJour.DepotParDefaut;  // depot GitHub verifie pour les mises a jour (proprietaire/nom)            // identifiants des correctifs actifs, separes par des virgules

        static readonly string[] Cles = { "Mode", "Ecran", "SortieUltra", "ResInterne", "Etirer169", "PleinEcran", "FpsMax", "VSync",
            "Anisotrope", "Antialiasing", "MipmapsAuto", "TexturesMax", "Lissage2D", "DecorsHD", "PeripheriqueSon", "SonCoupe", "Coeurs", "CoeurJeu", "DelaiGel", "DiagGel", "Correctifs", "Depot" };

        public static Reglages Charger(string fichier)
        {
            var r = new Reglages();
            if (!File.Exists(fichier)) return r;
            foreach (var l in File.ReadAllLines(fichier, Encoding.UTF8))
            {
                int i = l.IndexOf('='); if (i <= 0) continue;
                string k = l.Substring(0, i).Trim(), v = l.Substring(i + 1).Trim();
                try
                {
                    switch (k)
                    {
                        case "Mode": r.Mode = (Mode)Enum.Parse(typeof(Mode), v); break;
                        case "Ecran": r.Ecran = v; break;
                        case "SortieUltra": r.SortieUltra = v; break;
                        case "ResInterne": r.ResInterne = v; break;
                        case "Etirer169": r.Etirer169 = v == "1"; break;
                        case "PleinEcran": r.PleinEcran = v == "1"; break;
                        case "FpsMax": r.FpsMax = int.Parse(v); break;
                        case "VSync": r.VSync = v == "1"; break;
                        case "Anisotrope": r.Anisotrope = int.Parse(v); break;
                        case "Antialiasing": r.Antialiasing = v; break;
                        case "MipmapsAuto": r.MipmapsAuto = v == "1"; break;
                        case "TexturesMax": r.TexturesMax = v == "1"; break;
                        case "Lissage2D": r.Lissage2D = v == "1"; break;
                        case "DecorsHD": r.DecorsHD = v == "1"; break;
                        case "PeripheriqueSon": r.PeripheriqueSon = v; break;
                        case "SonCoupe": r.SonCoupe = v == "1"; break;
                        case "Coeurs": r.Coeurs = (Coeurs)Enum.Parse(typeof(Coeurs), v); break;
                        case "CoeurJeu": r.CoeurJeu = int.Parse(v); break;
                        case "DelaiGel": r.DelaiGel = int.Parse(v); break;
                        case "DiagGel": r.DiagGel = v == "1"; break;
                        case "Correctifs": r.Correctifs = v; break;
                        case "Depot": r.Depot = v; break;
                    }
                }
                catch { }
            }
            return r;
        }

        public void Enregistrer(string fichier)
        {
            var sb = new StringBuilder("; Nocturne Plus - reglages du lanceur\r\n");
            foreach (var kv in EnPaires()) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("\r\n");
            File.WriteAllText(fichier, sb.ToString(), new UTF8Encoding(false));
        }

        public List<KeyValuePair<string, string>> EnPaires()
        {
            Func<bool, string> b = x => x ? "1" : "0";
            var vals = new object[] { Mode, Ecran, SortieUltra, ResInterne, b(Etirer169), b(PleinEcran), FpsMax, b(VSync), Anisotrope,
                Antialiasing, b(MipmapsAuto), b(TexturesMax), b(Lissage2D), b(DecorsHD), PeripheriqueSon, b(SonCoupe), Coeurs, CoeurJeu, DelaiGel, b(DiagGel), Correctifs, Depot };
            var l = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < Cles.Length; i++) l.Add(new KeyValuePair<string, string>(Cles[i], Convert.ToString(vals[i], CultureInfo.InvariantCulture)));
            return l;
        }

        public static bool LireRes(string s, out int w, out int h)
        {
            w = h = 0;
            var p = (s ?? "").ToLowerInvariant().Split('x');
            return p.Length == 2 && int.TryParse(p[0], out w) && int.TryParse(p[1], out h) && w > 0 && h > 0;
        }
    }

    /// <summary>
    /// Edition ligne a ligne d'un .ini en gardant tout le reste intact (commentaires, alignement,
    /// sections dupliquees). L'encodage est impose par l'appelant : Windows-1252 pour nocturne.ini.
    /// </summary>
    class FichierIni
    {
        readonly string chemin; readonly Encoding enc;
        string finLigne = "\r\n";
        readonly List<string> lignes = new List<string>();
        public readonly List<string> Changements = new List<string>();

        public FichierIni(string chemin, Encoding enc)
        {
            this.chemin = chemin; this.enc = enc;
            if (File.Exists(chemin))
            {
                string t = File.ReadAllText(chemin, enc);
                if (!t.Contains("\r\n") && t.Contains("\n")) finLigne = "\n";
                lignes.AddRange(t.Replace("\r\n", "\n").Split('\n'));
                if (lignes.Count > 0 && lignes[lignes.Count - 1] == "") lignes.RemoveAt(lignes.Count - 1);
            }
        }

        static string Section(string l)
        {
            string t = l.Trim();
            return t.StartsWith("[") && t.Contains("]") ? t.Substring(1, t.IndexOf(']') - 1).Trim() : null;
        }

        static string Cle(string l)
        {
            string t = l.TrimStart();
            if (t.StartsWith(";") || t.StartsWith("#")) return null;
            int i = t.IndexOf('='); return i > 0 ? t.Substring(0, i).Trim() : null;
        }

        public string Lire(string section, string cle)
        {
            string cur = null;
            foreach (var l in lignes)
            {
                string s = Section(l); if (s != null) { cur = s; continue; }
                if (cur != null && string.Equals(cur, section, StringComparison.OrdinalIgnoreCase) && string.Equals(Cle(l), cle, StringComparison.OrdinalIgnoreCase))
                    return l.Substring(l.IndexOf('=') + 1).Trim();
            }
            return null;
        }

        /// <summary>Remplace la valeur dans la PREMIERE section du nom donne (ajoute la cle/section si absente).</summary>
        public void Ecrire(string section, string cle, string valeur)
        {
            int debut = -1, derniereCle = -1;
            for (int i = 0; i < lignes.Count; i++)
            {
                string s = Section(lignes[i]);
                if (s != null)
                {
                    if (debut >= 0) break;
                    if (string.Equals(s, section, StringComparison.OrdinalIgnoreCase)) { debut = i; derniereCle = i; }
                    continue;
                }
                if (debut < 0) continue;
                string k = Cle(lignes[i]);
                if (k == null) continue;
                derniereCle = i;
                if (string.Equals(k, cle, StringComparison.OrdinalIgnoreCase))
                {
                    string l = lignes[i]; int eq = l.IndexOf('=');
                    string avant = l.Substring(eq + 1);
                    // garde l'espacement d'origine (dgVoodoo aligne ses "=")
                    string nouv = l.Substring(0, eq + 1) + (avant.StartsWith(" ") ? " " : "") + valeur;
                    if (avant.Trim() != valeur) Changements.Add(string.Format("[{0}] {1}: {2} -> {3}", section, cle, avant.Trim(), valeur));
                    lignes[i] = nouv;
                    return;
                }
            }
            if (debut < 0)
            {
                lignes.Add(""); lignes.Add("[" + section + "]"); lignes.Add(cle + "=" + valeur);
            }
            else lignes.Insert(derniereCle + 1, cle + "=" + valeur);
            Changements.Add(string.Format("[{0}] {1}: (absent) -> {2}", section, cle, valeur));
        }

        public void Enregistrer()
        {
            if (Changements.Count == 0 && File.Exists(chemin)) return;
            File.WriteAllText(chemin, string.Join(finLigne, lignes.ToArray()) + finLigne, enc);
        }
    }

    /// <summary>Journal texte horodate dans _NocturnePlus\journal.txt.</summary>
    static class Journal
    {
        public static string Fichier;
        public static event Action<string> Ligne;
        static readonly object verrou = new object();

        public static void Note(string format, params object[] args)
        {
            string t = args.Length > 0 ? string.Format(CultureInfo.InvariantCulture, format, args) : format;
            string l = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + t;
            lock (verrou)
            {
                try { if (Fichier != null) File.AppendAllText(Fichier, l + "\r\n", new UTF8Encoding(false)); } catch { }
            }
            var h = Ligne; if (h != null) h(l);
        }
    }
}
