// Nocturne Plus - ecrans et peripheriques son.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NocturnePlus
{
    static class Ecrans
    {
        public class Info
        {
            public string Peripherique, Nom; public int Ordinal; public bool Principal;
            public int X, Y, W, H, Hz;
            public override string ToString()
            {
                return string.Format("{0} : {1}x{2} @ {3} Hz{4}", Ordinal, W, H, Hz, Principal ? " (primary)" : "") + (string.IsNullOrEmpty(Nom) ? "" : " - " + Nom);
            }
        }

        /// <summary>Ecrans actifs, dans l'ordre de Windows (le meme que les sorties dgVoodoo sur un seul GPU).</summary>
        public static List<Info> Lister()
        {
            var l = new List<Info>();
            var dd = new Natif.DISPLAY_DEVICE(); dd.cb = Marshal.SizeOf(dd);
            for (int i = 0; Natif.EnumDisplayDevices(null, i, ref dd, 0); i++, dd.cb = Marshal.SizeOf(dd))
            {
                if ((dd.StateFlags & 0x1) == 0) continue; // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
                var dm = new Natif.DEVMODE(); dm.dmSize = (short)Marshal.SizeOf(dm);
                if (!Natif.EnumDisplaySettings(dd.DeviceName, -1, ref dm)) continue;
                var mon = new Natif.DISPLAY_DEVICE(); mon.cb = Marshal.SizeOf(mon);
                string nom = Natif.EnumDisplayDevices(dd.DeviceName, 0, ref mon, 0) ? mon.DeviceString : dd.DeviceString;
                l.Add(new Info
                {
                    Peripherique = dd.DeviceName, Nom = nom, Ordinal = l.Count + 1, Principal = (dd.StateFlags & 0x4) != 0,
                    X = dm.dmPositionX, Y = dm.dmPositionY, W = dm.dmPelsWidth, H = dm.dmPelsHeight, Hz = dm.dmDisplayFrequency
                });
            }
            return l;
        }

        /// <summary>Resolutions 32 bits distinctes proposees par un ecran.</summary>
        public static List<string> Modes(string peripherique)
        {
            var vus = new SortedSet<long>();
            var dm = new Natif.DEVMODE(); dm.dmSize = (short)Marshal.SizeOf(dm);
            for (int i = 0; Natif.EnumDisplaySettings(peripherique, i, ref dm); i++)
                if (dm.dmBitsPerPel == 32 && dm.dmPelsHeight >= 480) vus.Add((long)dm.dmPelsWidth * 100000 + dm.dmPelsHeight);
            var l = new List<string>();
            foreach (var v in vus) l.Add(string.Format("{0}x{1}", v / 100000, v % 100000));
            return l;
        }

        public static void RemettreAffichage()
        {
            Natif.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
            Natif.ClipCursor(IntPtr.Zero);
            Natif.ShowCursor(true);
        }
    }

    static class Son
    {
        /// <summary>Noms des peripheriques DirectSound, au format ecrit par le jeu ("DirectSound: ...").</summary>
        public static List<string> Peripheriques()
        {
            var l = new List<string>();
            try
            {
                Natif.DirectSoundEnumerateA(delegate (IntPtr g, IntPtr desc, IntPtr mod, IntPtr ctx)
                {
                    // chaine ANSI : on la decode en Windows-1252 pour retrouver les accents
                    int n = 0; while (Marshal.ReadByte(desc, n) != 0) n++;
                    var b = new byte[n]; Marshal.Copy(desc, b, 0, n);
                    l.Add("DirectSound: " + Installation.Win1252.GetString(b));
                    return true;
                }, IntPtr.Zero);
            }
            catch (Exception e) { Journal.Note("Enumeration DirectSound impossible : {0}", e.Message); }
            return l;
        }
    }
}
