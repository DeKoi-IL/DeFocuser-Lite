/*
 * UpdateChecker.cs
 * Polls the GitHub Releases API for the latest stable release of DeFocuser Lite
 * and reports whether the installed app (and connected firmware) are out of date.
 * No external JSON dependency — we hand-parse the two or three fields we need.
 */

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ASCOM.DeKoi.DeFocuserApp.Services
{
    public class UpdateInfo
    {
        public bool HubAvailable { get; set; }
        public Version LatestVersion { get; set; }
        public Version CurrentVersion { get; set; }
        public string HubInstallerUrl { get; set; }
        public long HubInstallerSize { get; set; }
        public string FirmwareBinUrl { get; set; }
        public Version FirmwareVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string TagName { get; set; }
    }

    public static class UpdateChecker
    {
        private const string ReleasesUrl = "https://api.github.com/repos/DeKoi-IL/DeFocuser-Lite/releases/latest";

        private static readonly HttpClient http = CreateClient();

        private static HttpClient CreateClient()
        {
            // .NET 4.8's default protocol set on older Windows builds may not
            // include TLS 1.2 — GitHub requires it.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            c.DefaultRequestHeaders.UserAgent.ParseAdd("DeFocuser-Lite-Updater/" + v);
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return c;
        }

        public static async Task<UpdateInfo> CheckAsync()
        {
            using (var resp = await http.GetAsync(ReleasesUrl).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                using (var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    var release = (ReleaseDto)new DataContractJsonSerializer(typeof(ReleaseDto)).ReadObject(stream);
                    return Build(release);
                }
            }
        }

        private static UpdateInfo Build(ReleaseDto release)
        {
            var info = new UpdateInfo
            {
                CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version,
                TagName = release?.tag_name,
                ReleaseNotes = release?.body ?? string.Empty,
            };

            if (release == null || release.draft || release.prerelease)
                return info;

            info.LatestVersion = ParseTag(release.tag_name);

            if (release.assets != null)
            {
                foreach (var asset in release.assets)
                {
                    if (asset?.name == null) continue;
                    var n = asset.name;

                    if (info.HubInstallerUrl == null && IsHubInstaller(n))
                    {
                        info.HubInstallerUrl = asset.browser_download_url;
                        info.HubInstallerSize = asset.size;
                    }
                    else if (info.FirmwareBinUrl == null && IsFirmwareBin(n))
                    {
                        info.FirmwareBinUrl = asset.browser_download_url;
                        info.FirmwareVersion = ParseFirmwareVersionFromAsset(n);
                    }
                }
            }

            info.HubAvailable = info.LatestVersion != null
                                && info.HubInstallerUrl != null
                                && info.CurrentVersion != null
                                && Normalize(info.LatestVersion).CompareTo(Normalize(info.CurrentVersion)) > 0;

            return info;
        }

        private static bool IsHubInstaller(string name)
        {
            // Pattern: "DeKoi DeFocuser Lite Setup-2.0.7.exe"
            return name.StartsWith("DeKoi DeFocuser Lite Setup-", StringComparison.OrdinalIgnoreCase)
                   && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFirmwareBin(string name)
        {
            // Pattern: "DeFocuser-Lite-Firmware-2.0.7-esp32c3.bin"
            return name.StartsWith("DeFocuser-Lite-Firmware-", StringComparison.OrdinalIgnoreCase)
                   && name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase);
        }

        private static Version ParseTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            string trimmed = tag.TrimStart('v', 'V');
            return TryParse(trimmed);
        }

        private static Version ParseFirmwareVersionFromAsset(string name)
        {
            var m = Regex.Match(name, @"Firmware-(\d+\.\d+(?:\.\d+)?)-", RegexOptions.IgnoreCase);
            return m.Success ? TryParse(m.Groups[1].Value) : null;
        }

        internal static Version TryParse(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            // System.Version requires at least major.minor; pad 1.0 -> 1.0
            // and accept 1, 1.2, 1.2.3, 1.2.3.4.
            if (!raw.Contains(".")) raw = raw + ".0";
            return Version.TryParse(raw, out var v) ? v : null;
        }

        // Compare on 3 parts so 2.0.6 (assembly 2.0.6.0) matches release tag "2.0.6".
        internal static Version Normalize(Version v)
        {
            if (v == null) return new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, v.Build < 0 ? 0 : v.Build);
        }

        // ---- DTOs for DataContractJsonSerializer ----
        [DataContract]
        private class ReleaseDto
        {
            [DataMember(Name = "tag_name", IsRequired = false, EmitDefaultValue = false)]
            public string tag_name { get; set; }

            [DataMember(Name = "body", IsRequired = false, EmitDefaultValue = false)]
            public string body { get; set; }

            [DataMember(Name = "draft", IsRequired = false, EmitDefaultValue = false)]
            public bool draft { get; set; }

            [DataMember(Name = "prerelease", IsRequired = false, EmitDefaultValue = false)]
            public bool prerelease { get; set; }

            [DataMember(Name = "assets", IsRequired = false, EmitDefaultValue = false)]
            public AssetDto[] assets { get; set; }
        }

        [DataContract]
        private class AssetDto
        {
            [DataMember(Name = "name", IsRequired = false, EmitDefaultValue = false)]
            public string name { get; set; }

            [DataMember(Name = "browser_download_url", IsRequired = false, EmitDefaultValue = false)]
            public string browser_download_url { get; set; }

            [DataMember(Name = "size", IsRequired = false, EmitDefaultValue = false)]
            public long size { get; set; }
        }
    }
}
