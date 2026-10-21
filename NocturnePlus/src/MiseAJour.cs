// Nocturne Plus - verification de mise a jour : compare la version du lanceur au dernier
// "release" du depot GitHub. Ne telecharge rien : l'utilisateur ouvre la page s'il le souhaite.
using System;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace NocturnePlus
{
    static class MiseAJour
    {
        public const string DepotParDefaut = "skw-1337/nocturne-plus";

        public static Version VersionLanceur { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public static string VersionTexte { get { var v = VersionLanceur; return string.Format("{0}.{1}.{2}", v.Major, v.Minor, v.Build); } }

        public enum Etat { Verification, AJour, Disponible, Impossible }

        public class Resultat
        {
            public Etat Etat; public string Derniere; public string Page; public string Detail;
        }

        /// <summary>Verifie en arriere-plan, rappelle "fin" sur le fil d'appel de l'interface via le contexte donne.</summary>
        public static void Verifier(string depot, Action<Resultat> fin)
        {
            var ctx = SynchronizationContext.Current;
            new Thread(() =>
            {
                Resultat r = Interroger(string.IsNullOrEmpty(depot) ? DepotParDefaut : depot.Trim());
                if (ctx != null) ctx.Post(_ => fin(r), null); else fin(r);
            }) { IsBackground = true, Name = "mise-a-jour" }.Start();
        }

        static Resultat Interroger(string depot)
        {
            var res = new Resultat { Page = "https://github.com/" + depot + "/releases" };
            try
            {
                // .NET 4 n'active pas TLS 1.2 par defaut ; GitHub l'exige
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "NocturnePlus/" + VersionTexte;
                    wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                    string json = wc.DownloadString("https://api.github.com/repos/" + depot + "/releases/latest");
                    var tag = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    var url = Regex.Match(json, "\"html_url\"\\s*:\\s*\"(https://github.com/[^\"]+/releases/tag/[^\"]+)\"");
                    if (!tag.Success) { res.Etat = Etat.Impossible; res.Detail = "no release found"; return res; }
                    res.Derniere = tag.Groups[1].Value;
                    if (url.Success) res.Page = url.Groups[1].Value;
                    Version distante;
                    var m = Regex.Match(res.Derniere, @"(\d+)(\.\d+){0,3}");
                    if (!m.Success || !Version.TryParse(m.Value.Contains(".") ? m.Value : m.Value + ".0", out distante))
                    { res.Etat = Etat.Impossible; res.Detail = "unreadable tag " + res.Derniere; return res; }
                    res.Etat = Normaliser(distante) > Normaliser(VersionLanceur) ? Etat.Disponible : Etat.AJour;
                }
            }
            catch (WebException e)
            {
                res.Etat = Etat.Impossible;
                var rep = e.Response as HttpWebResponse;
                res.Detail = rep != null && rep.StatusCode == HttpStatusCode.NotFound ? "no release published yet" : "offline";
            }
            catch (Exception e) { res.Etat = Etat.Impossible; res.Detail = e.Message; }
            return res;
        }

        static Version Normaliser(Version v)
        {
            return new Version(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build));
        }
    }
}
