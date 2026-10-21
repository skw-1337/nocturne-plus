// Nocturne Plus - mesures sans toucher au jeu : images distinctes par seconde (approximation
// des FPS par capture d'ecran) et temps de reaction d'un menu a une touche.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace NocturnePlus
{
    static class Mesures
    {
        /// <summary>Empreinte de quelques zones de l'ecran du jeu (capture via le compositeur).</summary>
        class Sonde : IDisposable
        {
            readonly Rectangle zone; readonly Bitmap bmp; readonly Graphics g;
            public Sonde(Rectangle fenetre)
            {
                // bande horizontale au centre : elle traverse la scene 3D et les menus
                zone = new Rectangle(fenetre.X + fenetre.Width / 8, fenetre.Y + fenetre.Height / 2 - 32, fenetre.Width * 3 / 4, 64);
                bmp = new Bitmap(zone.Width, zone.Height, PixelFormat.Format32bppRgb);
                g = Graphics.FromImage(bmp);
            }
            public ulong Empreinte()
            {
                g.CopyFromScreen(zone.Location, Point.Empty, zone.Size);
                var d = bmp.LockBits(new Rectangle(Point.Empty, bmp.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
                try
                {
                    int n = d.Stride * d.Height; var buf = new byte[n]; Marshal.Copy(d.Scan0, buf, 0, n);
                    ulong h = 1469598103934665603UL;
                    for (int i = 0; i < n; i += 4) { h ^= BitConverter.ToUInt32(buf, i); h *= 1099511628211UL; }
                    return h;
                }
                finally { bmp.UnlockBits(d); }
            }
            public void Dispose() { g.Dispose(); bmp.Dispose(); }
        }

        static Rectangle RectJeu(int pid)
        {
            foreach (var h in Natif.FenetresDe(pid))
                if (Natif.IsWindowVisible(h) && Natif.Classe(h) != "#32770")
                {
                    Natif.RECT r; Natif.GetWindowRect(h, out r);
                    if (r.Right - r.Left <= 100) continue;
                    // on ne capture que si c'est bien le jeu qui est affiche a cet endroit (jamais le bureau)
                    var c = new Natif.POINT { X = (r.Left + r.Right) / 2, Y = (r.Top + r.Bottom) / 2 };
                    int p; Natif.GetWindowThreadProcessId(Natif.WindowFromPoint(c), out p);
                    if (p != pid) continue;
                    return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                }
            return Rectangle.Empty;
        }

        /// <summary>Compte les images distinctes affichees chaque seconde (borne basse des FPS).</summary>
        public static string Fps(int pid, int secondes)
        {
            var rect = RectJeu(pid); if (rect.IsEmpty) return "fenetre du jeu introuvable";
            var parSeconde = new List<int>();
            using (var s = new Sonde(rect))
            {
                ulong prec = s.Empreinte(); var w = Stopwatch.StartNew(); int n = 0; long fin = 1000;
                while (w.ElapsedMilliseconds < secondes * 1000L)
                {
                    ulong e = s.Empreinte(); if (e != prec) { n++; prec = e; }
                    if (w.ElapsedMilliseconds >= fin) { parSeconde.Add(n); n = 0; fin += 1000; }
                }
            }
            if (parSeconde.Count == 0) return "aucune mesure";
            return string.Format("images distinctes/s : moy {0:F1}, min {1}, max {2} ({3} s, zone {4}x64) [{5}]",
                parSeconde.Average(), parSeconde.Min(), parSeconde.Max(), parSeconde.Count, rect.Width * 3 / 4, string.Join(" ", parSeconde.Select(x => x.ToString()).ToArray()));
        }

        /// <summary>Envoie une touche au jeu (au premier plan) et mesure le temps jusqu'a ce que l'image se stabilise.</summary>
        public static string Touche(int pid, ushort vk, int maxSecondes)
        {
            var rect = RectJeu(pid); if (rect.IsEmpty) return "fenetre du jeu introuvable";
            using (var s = new Sonde(rect))
            {
                ulong avant = s.Empreinte();
                Appuyer(vk);
                var w = Stopwatch.StartNew();
                long premier = -1, dernierChangement = 0; ulong prec = avant;
                while (w.ElapsedMilliseconds < maxSecondes * 1000L)
                {
                    ulong e = s.Empreinte();
                    if (e != prec) { if (premier < 0) premier = w.ElapsedMilliseconds; dernierChangement = w.ElapsedMilliseconds; prec = e; }
                    if (premier >= 0 && w.ElapsedMilliseconds - dernierChangement > 1500) break;
                    Thread.Sleep(2);
                }
                if (premier < 0) return string.Format("touche 0x{0:X2} : aucun changement visible en {1} s", vk, maxSecondes);
                return string.Format("touche 0x{0:X2} : premiere reaction {1} ms, image stable a {2} ms", vk, premier, dernierChangement);
            }
        }

        public static void Appuyer(ushort vk)
        {
            var e = new Natif.INPUT[2];
            for (int i = 0; i < 2; i++)
            {
                e[i].type = 1; e[i].ki.wVk = (short)vk; e[i].ki.wScan = (short)Natif.MapVirtualKey(vk, 0);
                e[i].ki.dwFlags = (i == 1 ? 0x2 : 0) | 0x8; // KEYUP, SCANCODE (le jeu lit les codes clavier)
            }
            Natif.SendInput(1, new[] { e[0] }, Marshal.SizeOf(typeof(Natif.INPUT)));
            Thread.Sleep(60);
            Natif.SendInput(1, new[] { e[1] }, Marshal.SizeOf(typeof(Natif.INPUT)));
        }
    }
}
