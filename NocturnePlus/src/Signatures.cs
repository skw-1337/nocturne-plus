// Nocturne Plus - correctifs de nocturne.exe localises par signature du code, pas par adresse fixe.
// Chaque emplacement est decrit par un motif (octets hexadecimaux, "??" = octet quelconque, pour les
// adresses absolues qui changent d'une compilation a l'autre). Le motif doit apparaitre UNE seule fois ;
// les nouveaux octets sont calcules a partir de ceux trouves (sauts recalcules, adresses relues).
using System;
using System.Collections.Generic;
using System.Linq;

namespace NocturnePlus
{
    /// <summary>Un emplacement trouve dans l'exe : position du motif et octets autour.</summary>
    class Lieu
    {
        public byte[] Exe; public int Pos;
        public uint U32(int decalage) { return BitConverter.ToUInt32(Exe, Pos + decalage); }
        public byte Octet(int decalage) { return Exe[Pos + decalage]; }
    }

    /// <summary>Un emplacement a corriger : motif unique, zone a reecrire, et les deux sens de conversion.</summary>
    class Site
    {
        public string Motif;                    // contexte + zone, "??" pour les octets variables
        public int Debut, Longueur;             // zone reecrite, relative au debut du motif
        public Func<Lieu, bool?> EstApplique;   // true = deja corrige, false = d'origine, null = inattendu
        public Func<Lieu, byte[]> Nouveau;      // octets corriges
        public Func<Lieu, byte[]> Ancien;       // octets d'origine (pour retirer le correctif)
    }

    static class Signatures
    {
        public static byte?[] Motif(string m)
        {
            var t = m.Replace(" ", "");
            var r = new byte?[t.Length / 2];
            for (int i = 0; i < r.Length; i++)
            {
                string o = t.Substring(i * 2, 2);
                r[i] = o == "??" ? (byte?)null : Convert.ToByte(o, 16);
            }
            return r;
        }

        /// <summary>Toutes les positions du motif dans les donnees.</summary>
        public static List<int> Chercher(byte[] d, string motif)
        {
            var m = Motif(motif);
            var res = new List<int>();
            int premier = Array.FindIndex(m, x => x.HasValue);
            byte p = m[premier].Value;
            for (int i = 0; i + m.Length <= d.Length; i++)
            {
                if (d[i + premier] != p) continue;
                bool ok = true;
                for (int j = 0; j < m.Length; j++)
                    if (m[j].HasValue && d[i + j] != m[j].Value) { ok = false; break; }
                if (ok) res.Add(i);
            }
            return res;
        }

        public static Lieu Unique(byte[] d, string motif)
        {
            var p = Chercher(d, motif);
            return p.Count == 1 ? new Lieu { Exe = d, Pos = p[0] } : null;
        }

        public static byte[] Hex(string s) { return Motif(s).Select(x => x.Value).ToArray(); }

        public static byte[] Le32(uint v) { return BitConverter.GetBytes(v); }

        // ---------------------------------------------------------------- adresses <-> fichier (PE)

        /// <summary>Decalage fichier d'une adresse virtuelle, d'apres la table des sections ; -1 si hors fichier.</summary>
        public static int VersFichier(byte[] exe, uint va)
        {
            int pe = BitConverter.ToInt32(exe, 0x3C);
            uint base_ = BitConverter.ToUInt32(exe, pe + 0x34);
            int n = BitConverter.ToUInt16(exe, pe + 6), opt = BitConverter.ToUInt16(exe, pe + 20);
            uint rva = va - base_;
            for (int i = 0; i < n; i++)
            {
                int s = pe + 24 + opt + i * 40;
                uint vAdr = BitConverter.ToUInt32(exe, s + 12), vTaille = BitConverter.ToUInt32(exe, s + 8);
                uint brut = BitConverter.ToUInt32(exe, s + 20), bTaille = BitConverter.ToUInt32(exe, s + 16);
                if (rva >= vAdr && rva < vAdr + Math.Max(vTaille, bTaille) && rva - vAdr < bTaille) return (int)(brut + rva - vAdr);
            }
            return -1;
        }

        /// <summary>Vrai si le code a cette adresse commence comme le chercheur de fichiers des .POD.</summary>
        public static bool EstChercheurPod(byte[] exe, uint va)
        {
            int o = VersFichier(exe, va);
            if (o < 0 || o + 11 > exe.Length) return false;
            // mov edx,[pod] ; test edx,edx ; jne +3 ; xor eax,eax ; ret
            return exe[o] == 0x8B && exe[o + 1] == 0x15 && exe[o + 6] == 0x85 && exe[o + 7] == 0xD2 &&
                   exe[o + 8] == 0x75 && exe[o + 9] == 0x03 && exe[o + 10] == 0x31;
        }

        // ---------------------------------------------------------------- etats

        public enum Etat { Absent, Applique, Partiel, Inconnu }

        /// <summary>Localise chaque site ; null si un site est introuvable, ambigu ou dans un etat inattendu.</summary>
        public static List<Tuple<Site, Lieu, bool>> Localiser(byte[] exe, Correctif c)
        {
            var res = new List<Tuple<Site, Lieu, bool>>();
            foreach (var s in c.Sites)
            {
                var l = Unique(exe, s.Motif);
                if (l == null) return null;
                bool? a = s.EstApplique(l);
                if (a == null) return null;
                res.Add(Tuple.Create(s, l, a.Value));
            }
            return res;
        }

        public static Etat EtatDe(byte[] exe, Correctif c)
        {
            var l = Localiser(exe, c);
            if (l == null) return Etat.Inconnu;
            if (l.All(x => x.Item3)) return Etat.Applique;
            if (l.All(x => !x.Item3)) return Etat.Absent;
            return Etat.Partiel;
        }

        /// <summary>Applique ou retire le correctif dans le tableau (en memoire). Faux si l'exe n'est pas reconnu.</summary>
        public static bool Modifier(byte[] exe, Correctif c, bool actif)
        {
            var lieux = Localiser(exe, c);
            if (lieux == null) return false;
            // tous les nouveaux octets sont calcules avant la moindre ecriture
            var ecritures = new List<Tuple<int, byte[]>>();
            foreach (var x in lieux)
            {
                if (x.Item3 == actif) continue;
                var b = actif ? x.Item1.Nouveau(x.Item2) : x.Item1.Ancien(x.Item2);
                if (b == null || b.Length != x.Item1.Longueur) return false;
                ecritures.Add(Tuple.Create(x.Item2.Pos + x.Item1.Debut, b));
            }
            foreach (var e in ecritures) Array.Copy(e.Item2, 0, exe, e.Item1, e.Item2.Length);
            return true;
        }
    }
}
