using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Net.Cache;

namespace Frosty.Core
{
    public class Release
    {
        public string Name;

        // [DAI fix] release tags are the build date (e.g. 2026.11.20.1), a leading 'v' is allowed
        [JsonProperty(PropertyName = "tag_name")]
        public string Tag;

        public Version Version => Version.TryParse(Tag?.TrimStart('v', 'V'), out Version version) ? version : null;
    }

    public static class UpdateCheckerUtils
    {
        // [DAI fix] this fork's releases instead of J-Lyt's
        public const string Repository = "dduttonnc/FrostyToolsuite";
        public const string ReleasesUrl = "https://github.com/" + Repository + "/releases/latest";
        public const string ChangeLogUrl = "https://raw.githubusercontent.com/" + Repository + "/refs/heads/main/FrostyEditor/ChangeLog.txt";

        // [DAI fix] no release yet, no connection or an unreadable answer all count as "no update"
        private static Release GetLatestRelease(string url, bool isArray = true)
        {
            try
            {
                WebClient client = new WebClient()
                {
                    CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore)
                };
                client.Headers.Add(HttpRequestHeader.UserAgent, "request");

                return isArray
                    ? JsonConvert.DeserializeObject<List<Release>>(client.DownloadString(url))?.FirstOrDefault()
                    : JsonConvert.DeserializeObject<Release>(client.DownloadString(url));
            }
            catch (Exception e)
            {
                App.Logger?.Log("Update check skipped: {0}", e.Message);
                return null;
            }
        }

        public static bool CheckVersion(bool checkPrerelease, Version localVersion)
        {
            Release release = checkPrerelease
                ? GetLatestRelease("https://api.github.com/repos/" + Repository + "/releases")
                : GetLatestRelease("https://api.github.com/repos/" + Repository + "/releases/latest", false);

            if (release?.Version == null)
                return false;

            bool isLocalPrerelease = false;
#if FROSTY_ALPHA
                isLocalPrerelease = true;
#elif FROSTY_BETA
                isLocalPrerelease = true;
#endif

            // check if local version isn't the latest
            if (localVersion.CompareTo(release.Version) < 0)
            {
                if (isLocalPrerelease)
                {
                    string version = release.Version.ToString();
                    int preVersion = int.Parse(version.Substring(version.Length - 1));

                    // check if local beta/alpha version isn't the latest
                    if (Frosty.Core.App.MinorVersion < preVersion)
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }
    }
}
