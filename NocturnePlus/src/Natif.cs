// Nocturne Plus - appels Win32 (P/Invoke). Compile en x86 : le jeu est 32 bits et
// GetThreadContext doit lire des contextes x86.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NocturnePlus
{
    static class Natif
    {
        public const uint CREATE_SUSPENDED = 0x4;
        public const uint THREAD_ALL = 0x1FFFFF;
        public const uint THREAD_QUERY_INFORMATION = 0x40;
        public const uint THREAD_SET_INFORMATION = 0x20;
        public const uint THREAD_SUSPEND_RESUME = 0x2;
        public const uint THREAD_GET_CONTEXT = 0x8;
        public const uint THREAD_SET_LIMITED_INFORMATION = 0x400;
        public const uint PROCESS_ALL = 0x1FFFFF;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcess(string app, StringBuilder cmd, IntPtr pa, IntPtr ta, bool inherit,
            uint flags, IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern uint ResumeThread(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern uint SuspendThread(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetProcessAffinityMask(IntPtr h, UIntPtr mask);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetProcessAffinityMask(IntPtr h, out UIntPtr proc, out UIntPtr sys);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern UIntPtr SetThreadAffinityMask(IntPtr h, UIntPtr mask);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenThread(uint access, bool inherit, uint tid);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetThreadContext(IntPtr h, IntPtr ctx);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool ReadProcessMemory(IntPtr h, IntPtr adr, byte[] buf, int n, out int lu);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetThreadTimes(IntPtr h, out long c, out long e, out long k, out long u);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetExitCodeProcess(IntPtr h, out uint code);

        // CPU Sets (Windows 10+)
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetSystemCpuSetInformation(IntPtr info, int len, out int retLen, IntPtr proc, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetProcessDefaultCpuSets(IntPtr proc, uint[] ids, int count);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetThreadSelectedCpuSets(IntPtr thread, uint[] ids, int count);

        // Toolhelp
        [StructLayout(LayoutKind.Sequential)]
        public struct THREADENTRY32 { public int dwSize, cntUsage, th32ThreadID, th32OwnerProcessID, tpBasePri, tpDeltaPri, dwFlags; }
        [DllImport("kernel32.dll", SetLastError = true)] public static extern IntPtr CreateToolhelp32Snapshot(uint flags, int pid);
        [DllImport("kernel32.dll")] public static extern bool Thread32First(IntPtr snap, ref THREADENTRY32 te);
        [DllImport("kernel32.dll")] public static extern bool Thread32Next(IntPtr snap, ref THREADENTRY32 te);

        [DllImport("ntdll.dll")] public static extern int NtQueryInformationThread(IntPtr h, int cls, out IntPtr info, int len, IntPtr retLen);

        [DllImport("psapi.dll", SetLastError = true)] public static extern bool EnumProcessModulesEx(IntPtr h, [Out] IntPtr[] mods, int cb, out int needed, uint filter);
        [DllImport("psapi.dll", CharSet = CharSet.Unicode)] public static extern int GetModuleBaseName(IntPtr h, IntPtr mod, StringBuilder name, int n);
        [StructLayout(LayoutKind.Sequential)] public struct MODULEINFO { public IntPtr lpBaseOfDll; public int SizeOfImage; public IntPtr EntryPoint; }
        [DllImport("psapi.dll")] public static extern bool GetModuleInformation(IntPtr h, IntPtr mod, out MODULEINFO mi, int cb);

        // Fenetres
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, EnumProc cb, IntPtr l);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out int pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsHungAppWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern bool ClipCursor(IntPtr r);
        [DllImport("user32.dll")] public static extern int ShowCursor(bool show);
        [DllImport("user32.dll")] public static extern int ChangeDisplaySettingsEx(string dev, IntPtr dm, IntPtr hwnd, uint flags, IntPtr p);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra; public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels; public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumDisplaySettings(string dev, int mode, ref DEVMODE dm);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString; public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumDisplayDevices(string dev, int i, ref DISPLAY_DEVICE dd, int flags);

        // Capture d'ecran (mesures)
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);

        // Clavier (mesure des menus)
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public int type; public KEYBDINPUT ki; public int pad1, pad2; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public short wVk, wScan; public int dwFlags, time; public IntPtr dwExtraInfo; }
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);

        // DirectSound : liste des peripheriques (noms ANSI, comme les ecrit le jeu)
        public delegate bool DSEnumCallback(IntPtr guid, IntPtr desc, IntPtr module, IntPtr ctx);
        [DllImport("dsound.dll")] public static extern int DirectSoundEnumerateA(DSEnumCallback cb, IntPtr ctx);

        [DllImport("shell32.dll")] public static extern bool IsUserAnAdmin();

        public static string Classe(IntPtr h) { var s = new StringBuilder(128); GetClassName(h, s, 128); return s.ToString(); }
        public static string Texte(IntPtr h) { var s = new StringBuilder(1024); GetWindowText(h, s, 1024); return s.ToString(); }

        public static List<IntPtr> FenetresDe(int pid)
        {
            var l = new List<IntPtr>();
            EnumWindows(delegate (IntPtr h, IntPtr x) { int p; GetWindowThreadProcessId(h, out p); if (p == pid) l.Add(h); return true; }, IntPtr.Zero);
            return l;
        }

        public static List<int> ThreadsDe(int pid)
        {
            var l = new List<int>();
            IntPtr snap = CreateToolhelp32Snapshot(0x4, 0);
            if (snap == new IntPtr(-1)) return l;
            var te = new THREADENTRY32(); te.dwSize = Marshal.SizeOf(typeof(THREADENTRY32));
            if (Thread32First(snap, ref te))
                do { if (te.th32OwnerProcessID == pid) l.Add(te.th32ThreadID); te.dwSize = Marshal.SizeOf(typeof(THREADENTRY32)); }
                while (Thread32Next(snap, ref te));
            CloseHandle(snap);
            return l;
        }

        public static uint StartAddress(IntPtr hThread)
        {
            IntPtr a; int st = NtQueryInformationThread(hThread, 9, out a, IntPtr.Size, IntPtr.Zero);
            return st == 0 ? (uint)a.ToInt64() : 0;
        }

        /// <summary>Identifiants CPU Set par index de processeur logique (groupe 0).</summary>
        public static Dictionary<int, uint> CpuSets()
        {
            var d = new Dictionary<int, uint>();
            int len;
            GetSystemCpuSetInformation(IntPtr.Zero, 0, out len, IntPtr.Zero, 0);
            if (len <= 0) return d;
            IntPtr buf = Marshal.AllocHGlobal(len);
            try
            {
                if (!GetSystemCpuSetInformation(buf, len, out len, IntPtr.Zero, 0)) return d;
                int off = 0;
                while (off < len)
                {
                    int size = Marshal.ReadInt32(buf, off);
                    uint id = (uint)Marshal.ReadInt32(buf, off + 8);
                    short grp = Marshal.ReadInt16(buf, off + 12);
                    int lp = Marshal.ReadByte(buf, off + 14);
                    if (grp == 0) d[lp] = id;
                    if (size <= 0) break;
                    off += size;
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
            return d;
        }
    }

    /// <summary>Modules charges dans le jeu, pour traduire une adresse en "module+decalage".</summary>
    class CarteModules
    {
        public struct Mod { public string Nom; public uint Base; public uint Taille; }
        public readonly List<Mod> Liste = new List<Mod>();

        public CarteModules(IntPtr hProc)
        {
            var mods = new IntPtr[1024]; int needed;
            if (!Natif.EnumProcessModulesEx(hProc, mods, mods.Length * IntPtr.Size, out needed, 3)) return;
            int n = Math.Min(needed / IntPtr.Size, mods.Length);
            for (int i = 0; i < n; i++)
            {
                var sb = new StringBuilder(260); Natif.GetModuleBaseName(hProc, mods[i], sb, 260);
                Natif.MODULEINFO mi; Natif.GetModuleInformation(hProc, mods[i], out mi, Marshal.SizeOf(typeof(Natif.MODULEINFO)));
                var m = new Mod(); m.Nom = sb.ToString(); m.Base = (uint)mi.lpBaseOfDll.ToInt64(); m.Taille = (uint)mi.SizeOfImage;
                Liste.Add(m);
            }
        }

        public string Nom(uint adr)
        {
            foreach (var m in Liste) if (adr >= m.Base && adr < m.Base + m.Taille) return m.Nom;
            return null;
        }

        public string Decrire(uint adr)
        {
            foreach (var m in Liste)
                if (adr >= m.Base && adr < m.Base + m.Taille)
                    return m.Nom.ToLowerInvariant() == "nocturne.exe" ? string.Format("nocturne.exe!{0:X8}", adr) : string.Format("{0}+0x{1:X}", m.Nom, adr - m.Base);
            return string.Format("{0:X8}", adr);
        }
    }
}
