// Nocturne Plus - outils tiers : cnc-ddraw (licence MIT, integre a l'exe) et telechargements
// depuis les releases officielles (dgVoodoo 2 de Dege, Real-ESRGAN de xinntao), verifies par SHA-256.
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;

namespace NocturnePlus
{
    static class Depots
    {
        // Versions validees avec Nocturne : ne pas suivre "latest" sans retester.
        public const string UrlDgVoodoo = "https://github.com/dege-diosg/dgVoodoo2/releases/download/v2.87.5/dgVoodoo2_87_5.zip";
        const string ShaDgVoodoo = "5FFDE6927F7355CA3FDD5D785B581256A8E6539FA13E395A891ADE6BA1040850";
        public const string UrlRealEsrgan = "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.5.0/realesrgan-ncnn-vulkan-20220424-windows.zip";
        const string ShaRealEsrgan = "ABC02804E17982A3BE33675E4D471E91EA374E65B70167ABC09E31ACB412802D";

        // ---------------------------------------------------------------- cnc-ddraw (integre)

        static void Ressource(string nom, string cible)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(nom))
            {
                if (s == null) throw new InvalidOperationException("Ressource absente de l'exe : " + nom);
                Directory.CreateDirectory(Path.GetDirectoryName(cible));
                using (var f = File.Create(cible)) s.CopyTo(f);
            }
        }

        /// <summary>Extrait cnc-ddraw v7.1 (MIT) de l'exe : DLL et licence dans la reserve, .ini et filtre dans le jeu s'ils manquent.</summary>
        public static void ExtraireCnc(string reserve, string jeu)
        {
            Ressource("cnc.ddraw.dll", Path.Combine(reserve, "ddraw.dll"));
            Ressource("cnc.LICENSE.txt", Path.Combine(reserve, "LICENSE.txt"));
            if (!File.Exists(Path.Combine(jeu, "ddraw.ini"))) Ressource("cnc.ddraw.ini", Path.Combine(jeu, "ddraw.ini"));
            string filtre = Path.Combine(jeu, @"Shaders\interpolation\lanczos2-sharp.glsl");
            if (!File.Exists(filtre)) Ressource("cnc.lanczos2-sharp.glsl", filtre);
            Journal.Note("cnc-ddraw v7.1 (licence MIT) extrait du lanceur");
        }

        // ---------------------------------------------------------------- telechargements

        static string Telecharger(string url, string sha256, string dossier, Action<string> statut)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2 pour GitHub
            Directory.CreateDirectory(dossier);
            string zip = Path.Combine(dossier, Path.GetFileName(new Uri(url).LocalPath));
            if (!File.Exists(zip) || Sha256(zip) != sha256)
            {
                if (statut != null) statut("Downloading " + Path.GetFileName(zip) + "…");
                Journal.Note("Telechargement : {0}", url);
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "NocturnePlus/" + MiseAJour.VersionTexte;
                    wc.DownloadFile(url, zip + ".part");
                }
                if (File.Exists(zip)) File.Move(zip, zip + ".ancien." + DateTime.Now.ToString("yyyyMMddHHmmss"));
                File.Move(zip + ".part", zip);
            }
            string h = Sha256(zip);
            if (h != sha256)
                throw new InvalidOperationException("Downloaded file does not match the expected SHA-256 (" + Path.GetFileName(zip) + "). Nothing was installed.");
            return zip;
        }

        static string Sha256(string f)
        {
            using (var s = SHA256.Create()) using (var fs = File.OpenRead(f))
                return BitConverter.ToString(s.ComputeHash(fs)).Replace("-", "");
        }

        static void Extraire(ZipArchive z, string entree, string cible)
        {
            var e = z.Entries.FirstOrDefault(x => string.Equals(x.FullName.Replace('\\', '/'), entree, StringComparison.OrdinalIgnoreCase));
            if (e == null) throw new InvalidOperationException("Archive incomplete : " + entree);
            Directory.CreateDirectory(Path.GetDirectoryName(cible));
            e.ExtractToFile(cible, true);
        }

        /// <summary>dgVoodoo 2.87.5 (Dege, http://dege.fw.hu) depuis sa release GitHub officielle : DDraw.dll + D3DImm.dll x86.</summary>
        public static void TelechargerDgVoodoo(string reserve, string jeu, string telechargements, Action<string> statut)
        {
            string zip = Telecharger(UrlDgVoodoo, ShaDgVoodoo, telechargements, statut);
            using (var z = ZipFile.OpenRead(zip))
            {
                Extraire(z, "MS/x86/DDraw.dll", Path.Combine(reserve, "ddraw.dll"));
                Extraire(z, "MS/x86/D3DImm.dll", Path.Combine(reserve, "D3DImm.dll"));
                if (!File.Exists(Path.Combine(jeu, "dgVoodoo.conf"))) Extraire(z, "dgVoodoo.conf", Path.Combine(jeu, "dgVoodoo.conf"));
            }
            File.WriteAllText(Path.Combine(reserve, "SOURCE.txt"),
                "dgVoodoo 2.87.5 by Dege - http://dege.fw.hu - https://github.com/dege-diosg/dgVoodoo2\r\n" +
                "Downloaded by Nocturne Plus from the official release (" + UrlDgVoodoo + ").\r\n" +
                "All rights reserved by its author; not redistributed by Nocturne Plus.\r\n");
            Journal.Note("dgVoodoo 2.87.5 (Dege) telecharge depuis la release officielle");
        }

        /// <summary>Real-ESRGAN ncnn (xinntao, licence BSD-3) : exe, runtime OpenMP et modele realesrgan-x4plus.</summary>
        public static string TelechargerRealEsrgan(string dossier, string telechargements, Action<string> statut)
        {
            string exe = Path.Combine(dossier, "realesrgan-ncnn-vulkan.exe");
            if (File.Exists(exe) && File.Exists(Path.Combine(dossier, @"models\realesrgan-x4plus.bin"))) return exe;
            string zip = Telecharger(UrlRealEsrgan, ShaRealEsrgan, telechargements, statut);
            using (var z = ZipFile.OpenRead(zip))
            {
                foreach (var nom in new[] { "realesrgan-ncnn-vulkan.exe", "vcomp140.dll", "models/realesrgan-x4plus.bin", "models/realesrgan-x4plus.param" })
                    Extraire(z, nom, Path.Combine(dossier, nom.Replace('/', '\\')));
            }
            Journal.Note("Real-ESRGAN ncnn (xinntao, BSD-3) telecharge depuis la release officielle");
            return exe;
        }
    }
}
