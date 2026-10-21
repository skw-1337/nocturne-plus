// Nocturne Plus - lancement du jeu (affinite, premier plan) et garde-fou contre le gel.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NocturnePlus
{
    class Partie
    {
        public event Action<IntPtr> FenetreVisible;
        public event Action<string> Termine;

        readonly Installation inst; readonly Reglages r;
        Natif.PROCESS_INFORMATION pi;
        Process proc;
        DateTime debut;
        uint masqueJeu, masqueTout;
        Dictionary<int, uint> cpuSets;
        readonly HashSet<int> filsVus = new HashSet<int>();
        readonly HashSet<IntPtr> dialoguesVus = new HashSet<IntPtr>();
        public int Pid { get { return pi.dwProcessId; } }

        public Partie(Installation inst, Reglages r) { this.inst = inst; this.r = r; }

        public void Demarrer()
        {
            string exe = inst.P("nocturne.exe");
            var si = new Natif.STARTUPINFO(); si.cb = Marshal.SizeOf(si);
            var cmd = new StringBuilder("\"" + exe + "\"");
            if (!Natif.CreateProcess(exe, cmd, IntPtr.Zero, IntPtr.Zero, false, Natif.CREATE_SUSPENDED, IntPtr.Zero, inst.Jeu, ref si, out pi))
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 740) throw new InvalidOperationException("nocturne.exe requires administrator elevation (error 740). Remove the \"Run as administrator\" option from nocturne.exe: elevation loses the CPU affinity.");
                throw new InvalidOperationException("Could not start nocturne.exe (Windows error " + err + ").");
            }
            debut = DateTime.Now;

            UIntPtr pm, sm; Natif.GetProcessAffinityMask(pi.hProcess, out pm, out sm);
            masqueTout = (uint)sm.ToUInt64();
            masqueJeu = 1u << r.CoeurJeu;
            if ((masqueJeu & masqueTout) == 0) masqueJeu = 1;

            switch (r.Coeurs)
            {
                case Coeurs.MonoCoeur:
                    Natif.SetProcessAffinityMask(pi.hProcess, new UIntPtr(masqueJeu));
                    Journal.Note("Affinite : processus entier sur CPU{0} (masque 0x{1:X})", r.CoeurJeu, masqueJeu);
                    break;
                case Coeurs.JeuEpingle:
                    // le fil principal sur un coeur, en dur, avant meme sa premiere instruction ;
                    // tout nouveau fil demarre sur ce coeur (CPU Set par defaut), puis les fils du
                    // rendu (dgVoodoo, D3D11, pilote) sont liberes sur les autres coeurs
                    Natif.SetThreadAffinityMask(pi.hThread, new UIntPtr(masqueJeu));
                    cpuSets = Natif.CpuSets();
                    uint id;
                    if (cpuSets.TryGetValue(r.CoeurJeu, out id))
                    {
                        bool ok = Natif.SetProcessDefaultCpuSets(pi.hProcess, new[] { id }, 1);
                        Journal.Note("Affinite : fils du jeu sur CPU{0}, rendu libre (CPU Set par defaut {1} : {2})", r.CoeurJeu, id, ok ? "ok" : "erreur " + Marshal.GetLastWin32Error());
                    }
                    filsVus.Add(pi.dwThreadId);
                    break;
                case Coeurs.MultiCoeur:
                    Journal.Note("Affinite : aucune restriction (multi-coeur)");
                    break;
            }

            Natif.AllowSetForegroundWindow(pi.dwProcessId);
            Natif.ResumeThread(pi.hThread);
            proc = Process.GetProcessById(pi.dwProcessId);
            Journal.Note("Jeu demarre : PID {0}", pi.dwProcessId);
            new Thread(Surveiller) { IsBackground = true, Name = "garde-fou" }.Start();
        }

        // ------------------------------------------------------------------ surveillance

        void Surveiller()
        {
            IntPtr fen = IntPtr.Zero;
            int msGel = 0; bool diagFait = false, premierPlanFait = false;
            var horloge = Stopwatch.StartNew();
            long dernier = 0;
            try
            {
                while (!proc.HasExited)
                {
                    bool rapide = r.Coeurs == Coeurs.JeuEpingle && horloge.ElapsedMilliseconds < 30000;
                    Thread.Sleep(rapide ? 5 : 100);
                    if (r.Coeurs == Coeurs.JeuEpingle) EpinglerNouveauxFils();

                    long t = horloge.ElapsedMilliseconds; if (t - dernier < 100) continue;
                    int pas = (int)(t - dernier); dernier = t;

                    if (fen == IntPtr.Zero || !Natif.IsWindowVisible(fen))
                    {
                        fen = FenetrePrincipale();
                        if (fen != IntPtr.Zero && !premierPlanFait)
                        {
                            premierPlanFait = true;
                            Natif.SetForegroundWindow(fen);
                            Journal.Note("Fenetre du jeu visible ({0:F1} s), mise au premier plan : {1}", (DateTime.Now - debut).TotalSeconds,
                                Natif.GetForegroundWindow() == fen ? "ok" : "refusee par Windows");
                            var h = FenetreVisible; if (h != null) h(fen);
                        }
                    }

                    VerifierDialogues();
                    if (fen != IntPtr.Zero) GarderActif(fen, pas);

                    if (fen != IntPtr.Zero && Natif.IsHungAppWindow(fen)) msGel += pas; else
                    {
                        if (msGel >= 5000) Journal.Note("Le jeu repond de nouveau apres {0:F1} s de blocage", msGel / 1000.0);
                        msGel = 0; diagFait = false;
                    }
                    if (msGel >= 5000 && !diagFait)
                    {
                        diagFait = true;
                        Journal.Note("GEL : le jeu ne traite plus ses messages depuis {0:F0} s ({1:F0} s apres le lancement)", msGel / 1000.0, (DateTime.Now - debut).TotalSeconds);
                        if (r.DiagGel) Diagnostic.Releve(pi.dwProcessId, 40, 50);
                    }
                    if (r.DelaiGel > 0 && msGel >= r.DelaiGel * 1000)
                    {
                        Journal.Note("GEL : arret force apres {0} s sans reponse", r.DelaiGel);
                        try { proc.Kill(); } catch { }
                        Thread.Sleep(500);
                        Ecrans.RemettreAffichage();
                        Journal.Note("Souris et affichage remis a zero. Si DirectDraw refuse ensuite de demarrer (erreurs wddvmem.cpp), redemarrez le PC.");
                        break;
                    }
                }
            }
            catch (Exception e) { Journal.Note("Garde-fou : erreur {0}", e.Message); }

            proc.WaitForExit();
            uint code; Natif.GetExitCodeProcess(pi.hProcess, out code);
            Natif.CloseHandle(pi.hThread); Natif.CloseHandle(pi.hProcess);
            Ecrans.RemettreAffichage();
            string bilan = string.Format("Jeu ferme apres {0:hh\\:mm\\:ss}, code de sortie {1} (0x{1:X})", DateTime.Now - debut, code);
            Journal.Note(bilan);
            var fin = Termine; if (fin != null) fin(bilan);
        }

        // Drapeau "application active" du moteur (nocturne.exe, image fixe en 0x400000), mis a jour
        // par WM_ACTIVATEAPP : a 0, la boucle principale se met en pause et ne dessine plus rien.
        static readonly IntPtr AdrActif = new IntPtr(0x005C1660);
        int msInactifAuPremierPlan; bool etaitPremierPlan;

        /// <summary>
        /// Apres un Alt+Tab, Windows rend parfois le premier plan au jeu sans qu'il recoive
        /// WM_ACTIVATEAPP : le moteur reste en pause et sa fenetre laisse voir le bureau.
        /// On le detecte et on relance l'activation.
        /// </summary>
        void GarderActif(IntPtr fen, int pas)
        {
            if (Natif.GetForegroundWindow() != fen)
            {
                if (etaitPremierPlan) Journal.Note("Jeu passe a l'arriere-plan (Alt+Tab)");
                etaitPremierPlan = false; msInactifAuPremierPlan = 0; return;
            }
            if (!etaitPremierPlan) Journal.Note("Jeu revenu au premier plan");
            etaitPremierPlan = true;
            var b = new byte[4]; int lu;
            if (!Natif.ReadProcessMemory(pi.hProcess, AdrActif, b, 4, out lu) || lu != 4) return;
            if (BitConverter.ToInt32(b, 0) != 0) { msInactifAuPremierPlan = 0; return; }
            msInactifAuPremierPlan += pas;
            if (msInactifAuPremierPlan < 400) return;
            msInactifAuPremierPlan = -2000; // laisse au jeu le temps de repartir avant de recommencer
            Natif.SetWindowPos(fen, IntPtr.Zero, 0, 0, 0, 0, 0x0003 | 0x0040); // HWND_TOP, NOSIZE|NOMOVE|SHOWWINDOW
            Natif.PostMessage(fen, 0x0006, new IntPtr(1), IntPtr.Zero);       // WM_ACTIVATE (WA_ACTIVE)
            Natif.PostMessage(fen, 0x001C, new IntPtr(1), IntPtr.Zero);       // WM_ACTIVATEAPP (actif)
            Journal.Note("Jeu au premier plan mais moteur en pause : activation renvoyee");
        }

        IntPtr FenetrePrincipale()
        {
            foreach (var h in Natif.FenetresDe(pi.dwProcessId))
                if (Natif.IsWindowVisible(h) && Natif.Classe(h) != "#32770") return h;
            return IntPtr.Zero;
        }

        void VerifierDialogues()
        {
            foreach (var h in Natif.FenetresDe(pi.dwProcessId))
            {
                if (Natif.Classe(h) != "#32770" || !Natif.IsWindowVisible(h) || dialoguesVus.Contains(h)) continue;
                dialoguesVus.Add(h);
                var sb = new StringBuilder(Natif.Texte(h));
                Natif.EnumChildWindows(h, delegate (IntPtr c, IntPtr l)
                {
                    if (Natif.Classe(c) == "Static") { string t = Natif.Texte(c); if (t.Length > 0) sb.Append(" | ").Append(t.Replace("\r", " ").Replace("\n", " ")); }
                    return true;
                }, IntPtr.Zero);
                Journal.Note("MESSAGE DU JEU : {0}", sb);
                Natif.SetForegroundWindow(h); // pour qu'on puisse cliquer OK par-dessus le jeu
            }
        }

        static readonly string[] modulesRendu = { "ddraw.dll", "d3dimm.dll", "d3d11.dll", "dxgi.dll", "d3d12.dll", "d3d12core.dll", "dxcore.dll", "d3d10warp.dll", "ntdll.dll", "opengl32.dll" };

        void EpinglerNouveauxFils()
        {
            List<int> fils = Natif.ThreadsDe(pi.dwProcessId);
            if (fils.All(filsVus.Contains)) return;
            CarteModules carte = null;
            foreach (int tid in fils)
            {
                if (filsVus.Contains(tid)) continue;
                IntPtr h = Natif.OpenThread(Natif.THREAD_QUERY_INFORMATION | Natif.THREAD_SET_INFORMATION | Natif.THREAD_SET_LIMITED_INFORMATION, false, (uint)tid);
                if (h == IntPtr.Zero) continue;
                uint start = Natif.StartAddress(h);
                if (carte == null) carte = new CarteModules(proc.Handle);
                string mod = carte.Nom(start);
                if (mod == null && start != 0) { carte = new CarteModules(proc.Handle); mod = carte.Nom(start); }
                if (mod == null && start != 0) { Natif.CloseHandle(h); continue; } // module pas encore liste : on reessaiera
                filsVus.Add(tid);
                string m = (mod ?? "").ToLowerInvariant();
                bool rendu = modulesRendu.Contains(m) || m.StartsWith("nv");
                if (rendu)
                {
                    uint libres = masqueTout & ~masqueJeu;
                    var ids = cpuSets.Where(kv => kv.Key != r.CoeurJeu && ((1u << kv.Key) & masqueTout) != 0).Select(kv => kv.Value).ToArray();
                    Natif.SetThreadSelectedCpuSets(h, ids, ids.Length);
                    Natif.SetThreadAffinityMask(h, new UIntPtr(libres));
                }
                else Natif.SetThreadAffinityMask(h, new UIntPtr(masqueJeu));
                Journal.Note("  fil {0} ({1}) -> {2}", tid, carte.Decrire(start), rendu ? "libre" : "coeur du jeu");
                Natif.CloseHandle(h);
            }
        }

        public void Tuer()
        {
            try { if (proc != null && !proc.HasExited) { Journal.Note("Arret demande par l'utilisateur"); proc.Kill(); } } catch { }
        }
    }

    /// <summary>Releve des threads du jeu : ou tourne chaque fil pendant un gel.</summary>
    static class Diagnostic
    {
        public static void Releve(int pid, int n, int intervalleMs)
        {
            IntPtr hp = Natif.OpenProcess(0x0410, false, pid); // QUERY_INFORMATION | VM_READ
            if (hp == IntPtr.Zero) { Journal.Note("Releve impossible : processus inaccessible"); return; }
            var carte = new CarteModules(hp);
            var fils = Natif.ThreadsDe(pid);
            var eips = new Dictionary<int, Dictionary<string, int>>();
            var piles = new Dictionary<int, string>();
            var cpu0 = new Dictionary<int, long>();
            var debuts = new Dictionary<int, string>();
            IntPtr ctx = Marshal.AllocHGlobal(0x2CC + 16);
            IntPtr ctxA = new IntPtr((ctx.ToInt64() + 15) & ~15L);
            try
            {
                foreach (int tid in fils)
                {
                    IntPtr h = Natif.OpenThread(Natif.THREAD_QUERY_INFORMATION, false, (uint)tid);
                    if (h == IntPtr.Zero) continue;
                    long c, e, k, u; Natif.GetThreadTimes(h, out c, out e, out k, out u); cpu0[tid] = k + u;
                    debuts[tid] = carte.Decrire(Natif.StartAddress(h));
                    Natif.CloseHandle(h);
                }
                for (int i = 0; i < n; i++)
                {
                    foreach (int tid in fils)
                    {
                        IntPtr h = Natif.OpenThread(Natif.THREAD_SUSPEND_RESUME | Natif.THREAD_GET_CONTEXT | Natif.THREAD_QUERY_INFORMATION, false, (uint)tid);
                        if (h == IntPtr.Zero) continue;
                        if (Natif.SuspendThread(h) != 0xFFFFFFFF)
                        {
                            for (int z = 0; z < 0x2CC; z += 4) Marshal.WriteInt32(ctxA, z, 0);
                            Marshal.WriteInt32(ctxA, 0, 0x10007); // CONTEXT_i386 | CONTROL | INTEGER | SEGMENTS
                            if (Natif.GetThreadContext(h, ctxA))
                            {
                                uint eip = (uint)Marshal.ReadInt32(ctxA, 0xB8), esp = (uint)Marshal.ReadInt32(ctxA, 0xC4);
                                string lieu = carte.Decrire(eip);
                                Dictionary<string, int> d;
                                if (!eips.TryGetValue(tid, out d)) eips[tid] = d = new Dictionary<string, int>();
                                d[lieu] = (d.ContainsKey(lieu) ? d[lieu] : 0) + 1;
                                if (i == n - 1) piles[tid] = Pile(hp, carte, esp);
                            }
                            Natif.ResumeThread(h);
                        }
                        Natif.CloseHandle(h);
                    }
                    Thread.Sleep(intervalleMs);
                }
                Journal.Note("--- Releve des fils ({0} echantillons, {1} ms) ---", n, intervalleMs);
                foreach (int tid in fils)
                {
                    if (!eips.ContainsKey(tid)) continue;
                    IntPtr h = Natif.OpenThread(Natif.THREAD_QUERY_INFORMATION, false, (uint)tid);
                    long c, e, k, u; Natif.GetThreadTimes(h, out c, out e, out k, out u); Natif.CloseHandle(h);
                    double cpuMs = ((k + u) - (cpu0.ContainsKey(tid) ? cpu0[tid] : 0)) / 10000.0;
                    var top = eips[tid].OrderByDescending(kv => kv.Value).Take(4).Select(kv => string.Format("{0} {1}%", kv.Key, kv.Value * 100 / n));
                    Journal.Note("fil {0} [debut {1}] CPU {2:F0} ms : {3}", tid, debuts.ContainsKey(tid) ? debuts[tid] : "?", cpuMs, string.Join(", ", top.ToArray()));
                    string pile; if (piles.TryGetValue(tid, out pile)) Journal.Note("      pile : {0}", pile);
                }
                Journal.Note("--- fin du releve ---");
            }
            finally { Marshal.FreeHGlobal(ctx); Natif.CloseHandle(hp); }
        }

        /// <summary>Adresses de retour probables sur la pile (mots pointant dans un module charge).</summary>
        static string Pile(IntPtr hp, CarteModules carte, uint esp)
        {
            var buf = new byte[4096]; int lu;
            if (!Natif.ReadProcessMemory(hp, new IntPtr(esp), buf, buf.Length, out lu)) return "(illisible)";
            var l = new List<string>();
            for (int i = 0; i + 4 <= lu && l.Count < 12; i += 4)
            {
                uint v = BitConverter.ToUInt32(buf, i);
                string m = carte.Nom(v);
                if (m != null) l.Add(carte.Decrire(v));
            }
            return string.Join(" < ", l.ToArray());
        }
    }
}
