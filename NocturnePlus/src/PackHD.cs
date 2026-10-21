// Nocturne Plus - generation du pack HD a partir des fichiers du jeu de l'utilisateur
// (portage C# de outils\decors_hd.py et outils\textures_hd.py, sans Python).
//   Decors : BACKDROP\*.RAW 640x480 -> Real-ESRGAN x4 -> 640x480 nettoye, palette d'origine.
//   Textures : ART\*.RAW 64/128/256 -> Real-ESRGAN x4 -> 256x256 (le moteur lit la taille
//   d'apres la longueur du fichier), transparence et .OPA preserves.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NocturnePlus
{
    class PackHD
    {
        // images 2D lues avec une taille fixe par le code : ne jamais les agrandir
        static readonly HashSet<string> Exclues = new HashSet<string> { "F0100", "F0300", "F0500", "F0700", "F0900", "F1100", "F1300", "F1500", "MENUCLD2" };
        static readonly Dictionary<int, int> TaillesTexture = new Dictionary<int, int> { { 4096, 64 }, { 16384, 128 }, { 65536, 256 } };

        readonly string jeu, sortie, travail;
        readonly Action<string> statut;

        public PackHD(string jeu, string sortie, Action<string> statut)
        {
            this.jeu = jeu; this.sortie = sortie; this.statut = statut;
            travail = Path.Combine(sortie, "_travail");
        }

        // ---------------------------------------------------------------- lecture des .POD (format POD2)

        class Entree { public string Pod; public int Taille, Offset; }

        static Dictionary<string, Entree> LirePod(string chemin)
        {
            var res = new Dictionary<string, Entree>(StringComparer.OrdinalIgnoreCase);
            using (var f = File.OpenRead(chemin))
            using (var br = new BinaryReader(f))
            {
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "POD2") return res;
                f.Position = 88; int n = br.ReadInt32();
                f.Position = 96;
                var ent = new int[n, 3];
                int maxNom = 0;
                for (int i = 0; i < n; i++)
                {
                    ent[i, 0] = br.ReadInt32(); ent[i, 1] = br.ReadInt32(); ent[i, 2] = br.ReadInt32(); br.ReadInt64();
                    maxNom = Math.Max(maxNom, ent[i, 0]);
                }
                var noms = br.ReadBytes(maxNom + 256);
                for (int i = 0; i < n; i++)
                {
                    int d = ent[i, 0], fin = Array.IndexOf(noms, (byte)0, d);
                    string nom = Installation.Win1252.GetString(noms, d, fin - d);
                    res[nom] = new Entree { Pod = chemin, Taille = ent[i, 1], Offset = ent[i, 2] };
                }
            }
            return res;
        }

        static byte[] Lire(Entree e, int taille = -1)
        {
            using (var f = File.OpenRead(e.Pod))
            {
                f.Position = e.Offset;
                var b = new byte[taille < 0 ? e.Taille : taille];
                f.Read(b, 0, b.Length);
                return b;
            }
        }

        // ---------------------------------------------------------------- images

        class Img
        {
            public int W, H; public float[] R, G, B;
            public Img(int w, int h) { W = w; H = h; R = new float[w * h]; G = new float[w * h]; B = new float[w * h]; }
        }

        static void EcrirePng(byte[] idx, byte[] pal, int w, int h, string chemin)
        {
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb))
            {
                var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                var px = new int[w * h];
                for (int i = 0; i < px.Length; i++) { int k = idx[i] * 3; px[i] = (pal[k] << 16) | (pal[k + 1] << 8) | pal[k + 2]; }
                for (int y = 0; y < h; y++) Marshal.Copy(px, y * w, d.Scan0 + y * d.Stride, w);
                bmp.UnlockBits(d);
                bmp.Save(chemin, ImageFormat.Png);
            }
        }

        /// <summary>Charge une image et la reduit a w x h (bicubique haute qualite).</summary>
        static Img ChargerReduit(string chemin, int w, int h)
        {
            using (var src = new Bitmap(chemin))
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppRgb))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var ia = new ImageAttributes())
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    ia.SetWrapMode(WrapMode.TileFlipXY); // pas de bord assombri
                    g.DrawImage(src, new Rectangle(0, 0, w, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, ia);
                }
                var d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                var px = new int[w * h];
                for (int y = 0; y < h; y++) Marshal.Copy(d.Scan0 + y * d.Stride, px, y * w, w);
                bmp.UnlockBits(d);
                var im = new Img(w, h);
                for (int i = 0; i < px.Length; i++) { im.R[i] = (px[i] >> 16) & 255; im.G[i] = (px[i] >> 8) & 255; im.B[i] = px[i] & 255; }
                return im;
            }
        }

        static float[] Flou(float[] c, int w, int h, float sigma)
        {
            int r = (int)Math.Ceiling(sigma * 3);
            var k = new float[2 * r + 1]; float s = 0;
            for (int i = -r; i <= r; i++) { k[i + r] = (float)Math.Exp(-i * i / (2 * sigma * sigma)); s += k[i + r]; }
            for (int i = 0; i < k.Length; i++) k[i] /= s;
            var t = new float[c.Length]; var o = new float[c.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = 0;
                    for (int i = -r; i <= r; i++) a += k[i + r] * c[y * w + Math.Min(w - 1, Math.Max(0, x + i))];
                    t[y * w + x] = a;
                }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float a = 0;
                    for (int i = -r; i <= r; i++) a += k[i + r] * t[Math.Min(h - 1, Math.Max(0, y + i)) * w + x];
                    o[y * w + x] = a;
                }
            return o;
        }

        /// <summary>Masque flou (comme PIL UnsharpMask) : rend le contraste local perdu a la reduction.</summary>
        static void Accentuer(Img im, float sigma, float force, float seuil)
        {
            foreach (var c in new[] { im.R, im.G, im.B })
            {
                var f = Flou(c, im.W, im.H, sigma);
                for (int i = 0; i < c.Length; i++)
                {
                    float d = c[i] - f[i];
                    if (Math.Abs(d) >= seuil) c[i] = c[i] + d * force;
                }
            }
        }

        /// <summary>Moyenne et ecart-type de chaque canal cales sur l'original (l'IA aplatit un peu la lumiere).</summary>
        static void CalerSur(Img im, byte[] idx, byte[] pal, float facteurEcart)
        {
            for (int ch = 0; ch < 3; ch++)
            {
                var c = ch == 0 ? im.R : ch == 1 ? im.G : im.B;
                double mo = 0, so = 0; foreach (var v in idx) mo += pal[v * 3 + ch]; mo /= idx.Length;
                foreach (var v in idx) { double e = pal[v * 3 + ch] - mo; so += e * e; } so = Math.Sqrt(so / idx.Length);
                double mi = c.Average(); double si = Math.Sqrt(c.Select(v => (v - mi) * (v - mi)).Average());
                double k = so * facteurEcart / Math.Max(si, 1);
                for (int i = 0; i < c.Length; i++) c[i] = (float)((c[i] - mi) * k + mo);
            }
        }

        /// <summary>Plus proche couleur de la palette, sans tramage ; "interdites" = entrees jamais choisies.</summary>
        static byte[] Remapper(Img im, byte[] pal, bool[] interdites)
        {
            var res = new byte[im.W * im.H];
            var cache = new Dictionary<int, byte>();
            for (int i = 0; i < res.Length; i++)
            {
                int r = Clamp(im.R[i]), g = Clamp(im.G[i]), b = Clamp(im.B[i]);
                int cle = (r << 16) | (g << 8) | b;
                byte meilleur;
                if (!cache.TryGetValue(cle, out meilleur))
                {
                    int dmin = int.MaxValue;
                    for (int p = 0; p < 256; p++)
                    {
                        if (interdites != null && interdites[p]) continue;
                        int dr = r - pal[p * 3], dg = g - pal[p * 3 + 1], db = b - pal[p * 3 + 2];
                        int d = dr * dr + dg * dg + db * db;
                        if (d < dmin) { dmin = d; meilleur = (byte)p; if (d == 0) break; }
                    }
                    cache[cle] = meilleur;
                }
                res[i] = meilleur;
            }
            return res;
        }

        static int Clamp(float v) { return v < 0 ? 0 : v > 255 ? 255 : (int)(v + 0.5f); }

        // ---------------------------------------------------------------- pipeline

        class Tache { public string Nom, Dossier; public byte[] Raw, Pal, Opa; public int Cote; public bool Decor; }

        public int Generer(string esrgan)
        {
            if (Directory.Exists(travail)) Directory.Delete(travail, true);
            string inD = Path.Combine(travail, "in"), outD = Path.Combine(travail, "x4");
            Directory.CreateDirectory(inD); Directory.CreateDirectory(outD);

            // 1. inventaire
            statut("Reading the game archives…");
            var taches = new Dictionary<string, Tache>();
            var ambigus = new HashSet<string>();
            foreach (var pod in Directory.GetFiles(jeu, "*.pod"))
            {
                Dictionary<string, Entree> idx;
                try { idx = LirePod(pod); } catch { continue; }
                foreach (var kv in idx)
                {
                    string cle = kv.Key.ToUpperInvariant();
                    if (!cle.EndsWith(".RAW")) continue;
                    Entree act; if (!idx.TryGetValue(kv.Key.Substring(0, kv.Key.Length - 4) + ".ACT", out act)) continue;
                    bool decor = cle.StartsWith("BACKDROP\\") && kv.Value.Taille == 640 * 480;
                    bool tex = cle.StartsWith("ART\\") && TaillesTexture.ContainsKey(kv.Value.Taille);
                    if (!decor && !tex) continue;
                    string nom = Path.GetFileNameWithoutExtension(cle);
                    if (tex && Exclues.Contains(nom)) continue;
                    string id = (decor ? "D_" : "T_") + nom;
                    var t = new Tache { Nom = nom, Decor = decor, Dossier = decor ? "BACKDROP" : "ART", Raw = Lire(kv.Value), Pal = Lire(act, 768) };
                    t.Cote = decor ? 0 : TaillesTexture[kv.Value.Taille];
                    Entree opa; if (tex && idx.TryGetValue(kv.Key.Substring(0, kv.Key.Length - 4) + ".OPA", out opa)) t.Opa = Lire(opa);
                    Tache deja;
                    if (taches.TryGetValue(id, out deja))
                    {
                        // meme nom dans plusieurs .POD avec des contenus differents : on ne choisit pas
                        if (!deja.Raw.SequenceEqual(t.Raw) || !deja.Pal.SequenceEqual(t.Pal)) ambigus.Add(id);
                        continue;
                    }
                    taches[id] = t;
                }
            }
            foreach (var a in ambigus) taches.Remove(a);
            Journal.Note("Pack HD : {0} decors, {1} textures ({2} ignorees car ambigues)", taches.Values.Count(t => t.Decor), taches.Values.Count(t => !t.Decor), ambigus.Count);

            // 2. images d'entree
            int n = 0;
            foreach (var kv in taches)
            {
                var t = kv.Value; int w = t.Decor ? 640 : t.Cote, h = t.Decor ? 480 : t.Cote;
                EcrirePng(t.Raw, t.Pal, w, h, Path.Combine(inD, kv.Key + ".png"));
                if (++n % 100 == 0) statut(string.Format("Preparing images {0}/{1}…", n, taches.Count));
            }

            // 3. Real-ESRGAN x4 sur le GPU (sans fenetre), avancement d'apres les fichiers produits
            var psi = new ProcessStartInfo(esrgan, string.Format("-i \"{0}\" -o \"{1}\" -n realesrgan-x4plus -m \"{2}\" -f png", inD, outD, Path.Combine(Path.GetDirectoryName(esrgan), "models")))
            { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(esrgan) };
            using (var p = Process.Start(psi))
            {
                while (!p.WaitForExit(1000))
                    statut(string.Format("AI upscaling {0}/{1}…", Directory.GetFiles(outD).Length, taches.Count));
                if (p.ExitCode != 0) throw new InvalidOperationException("Real-ESRGAN failed (code " + p.ExitCode + "). Is the graphics driver Vulkan-capable?");
            }

            // 4. retour au format du jeu
            foreach (var d in new[] { "BACKDROP", "ART" }) Directory.CreateDirectory(Path.Combine(sortie, d));
            int faits = 0; n = 0;
            foreach (var kv in taches)
            {
                var t = kv.Value; string x4 = Path.Combine(outD, kv.Key + ".png");
                if (++n % 50 == 0) statut(string.Format("Converting {0}/{1}…", n, taches.Count));
                if (!File.Exists(x4)) { Journal.Note("Pack HD : sortie IA manquante pour {0}", kv.Key); continue; }
                if (t.Decor) EcrireDecor(t, x4); else EcrireTexture(t, x4);
                faits++;
            }
            Directory.Delete(travail, true);
            Journal.Note("Pack HD genere : {0} fichiers images", faits);
            return faits;
        }

        void EcrireDecor(Tache t, string x4)
        {
            var im = ChargerReduit(x4, 640, 480);
            Accentuer(im, 1.6f, 0.7f, 2f);
            CalerSur(im, t.Raw, t.Pal, 1.05f);
            File.WriteAllBytes(Path.Combine(sortie, "BACKDROP", t.Nom + ".RAW"), Remapper(im, t.Pal, null));
        }

        void EcrireTexture(Tache t, string x4)
        {
            const int C = 256;
            var im = ChargerReduit(x4, C, C);
            Accentuer(im, 1.2f, 0.6f, 2f);
            CalerSur(im, t.Raw, t.Pal, 1f);
            // sans .OPA, le noir pur est transparent pour le moteur : aucun pixel visible ne doit y tomber
            var noirs = new bool[256];
            for (int p = 0; p < 256; p++) noirs[p] = t.Pal[p * 3] == 0 && t.Pal[p * 3 + 1] == 0 && t.Pal[p * 3 + 2] == 0;
            int premierNoir = Array.IndexOf(noirs, true);
            var idx = Remapper(im, t.Pal, premierNoir >= 0 ? noirs : null);
            if (t.Opa == null && premierNoir >= 0)
            {
                // masque d'origine agrandi au plus proche voisin
                for (int y = 0; y < C; y++)
                    for (int x = 0; x < C; x++)
                        if (noirs[t.Raw[(y * t.Cote / C) * t.Cote + x * t.Cote / C]]) idx[y * C + x] = (byte)premierNoir;
            }
            File.WriteAllBytes(Path.Combine(sortie, "ART", t.Nom + ".RAW"), idx);
            if (t.Opa != null && t.Opa.Length == t.Cote * t.Cote)
                File.WriteAllBytes(Path.Combine(sortie, "ART", t.Nom + ".OPA"), Agrandir(t.Opa, t.Cote, C));
        }

        /// <summary>Agrandissement bilineaire d'une carte d'opacite 8 bits.</summary>
        static byte[] Agrandir(byte[] a, int s, int c)
        {
            var r = new byte[c * c];
            for (int y = 0; y < c; y++)
                for (int x = 0; x < c; x++)
                {
                    float fx = Math.Max(0, (x + 0.5f) * s / c - 0.5f), fy = Math.Max(0, (y + 0.5f) * s / c - 0.5f);
                    int x0 = (int)fx, y0 = (int)fy, x1 = Math.Min(s - 1, x0 + 1), y1 = Math.Min(s - 1, y0 + 1);
                    float tx = fx - x0, ty = fy - y0;
                    float v = a[y0 * s + x0] * (1 - tx) * (1 - ty) + a[y0 * s + x1] * tx * (1 - ty) + a[y1 * s + x0] * (1 - tx) * ty + a[y1 * s + x1] * tx * ty;
                    r[y * c + x] = (byte)Clamp(v);
                }
            return r;
        }
    }
}
