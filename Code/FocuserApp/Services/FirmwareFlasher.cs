/*
 * FirmwareFlasher.cs
 * Downloads the released firmware .bin and writes it to the ESP32-C3 over the
 * same serial port the hub normally uses. Wraps the bundled esptool.exe so we
 * don't need to ship a Python runtime. Caller must Disconnect() the serial
 * port first — esptool requires exclusive access.
 *
 * If the installer didn't bundle esptool.exe (dev builds, or the user's
 * Setup.iss source was missing the binary), the resolver falls back to a
 * one-time download from espressif/esptool releases into LocalAppData.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.DeKoi.DeFocuserApp.Services
{
    public class FlashProgress
    {
        public string Line { get; set; }
        public bool IsError { get; set; }
    }

    public static class FirmwareFlasher
    {
        private static readonly HttpClient http = CreateClient();

        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        }

        // Search order: installer-bundled (next to the exe), repo Tools dir
        // (dev), then the LocalAppData cache populated by EnsureEsptoolAsync.
        public static string ResolveEsptoolPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string installed = Path.Combine(baseDir, "tools", "esptool.exe");
            if (File.Exists(installed)) return installed;

            try
            {
                var dir = new DirectoryInfo(baseDir);
                for (int i = 0; i < 6 && dir != null; i++)
                {
                    string candidate = Path.Combine(dir.FullName, "Tools", "esptool", "esptool.exe");
                    if (File.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            catch { }

            string cached = LocalCachePath();
            if (File.Exists(cached)) return cached;

            return null;
        }

        public static string LocalCachePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "DeFocuserLite", "tools", "esptool.exe");
        }

        // Pinned fallback used when the latest release's asset names don't
        // match our pattern (e.g. espressif changes the naming convention).
        // Last verified working: esptool 5.2.0, windows-amd64.
        private const string PinnedFallbackZipUrl =
            "https://github.com/espressif/esptool/releases/download/v5.2.0/esptool-v5.2.0-windows-amd64.zip";

        // Fetches the latest espressif/esptool Windows release, extracts
        // esptool.exe to LocalAppData, returns the resolved path. Caller
        // should pump `onLine` into the existing console pane so the user
        // sees what's happening.
        public static async Task<string> EnsureEsptoolAsync(Action<string> onLine, CancellationToken ct)
        {
            string existing = ResolveEsptoolPath();
            if (existing != null) return existing;

            onLine?.Invoke("esptool.exe not bundled — fetching from GitHub...");

            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "DeFocuser-Lite-Updater/" + Assembly.GetExecutingAssembly().GetName().Version);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

                string zipUrl = null;
                try
                {
                    zipUrl = await FindLatestWindowsZipUrl(client).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    onLine?.Invoke("Release lookup failed (" + ex.Message + ") — using pinned fallback.");
                }

                if (zipUrl == null)
                {
                    onLine?.Invoke("No Windows asset matched in latest release — using pinned fallback.");
                    zipUrl = PinnedFallbackZipUrl;
                }

                onLine?.Invoke("Downloading " + zipUrl);

                string tempZip = Path.Combine(Path.GetTempPath(), "esptool-" + Guid.NewGuid().ToString("N") + ".zip");
                try
                {
                    using (var resp = await client.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                    {
                        resp.EnsureSuccessStatusCode();
                        using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var dst = new FileStream(tempZip, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await src.CopyToAsync(dst, 81920, ct).ConfigureAwait(false);
                        }
                    }

                    string destPath = LocalCachePath();
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath));

                    onLine?.Invoke("Extracting esptool.exe...");
                    using (var zip = ZipFile.OpenRead(tempZip))
                    {
                        ZipArchiveEntry entry = null;
                        foreach (var e in zip.Entries)
                        {
                            if (e.Name.Equals("esptool.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                entry = e;
                                break;
                            }
                        }
                        if (entry == null) throw new InvalidOperationException("esptool.exe not found inside downloaded zip.");
                        entry.ExtractToFile(destPath, overwrite: true);
                    }

                    onLine?.Invoke("esptool ready at " + destPath);
                    return destPath;
                }
                finally
                {
                    try { File.Delete(tempZip); } catch { }
                }
            }
        }

        // Returns the first asset URL whose name contains a Windows marker
        // AND an x64-ish marker AND ends in .zip. Covers both the legacy
        // 'esptool-vX-win64.zip' and the current 'esptool-vX-windows-amd64.zip'
        // schemes, plus any future variant on the same axis.
        private static async Task<string> FindLatestWindowsZipUrl(HttpClient client)
        {
            using (var stream = await client.GetStreamAsync(
                "https://api.github.com/repos/espressif/esptool/releases/latest").ConfigureAwait(false))
            {
                var release = (EsptoolReleaseDto)new DataContractJsonSerializer(typeof(EsptoolReleaseDto)).ReadObject(stream);
                if (release?.assets == null) return null;

                foreach (var a in release.assets)
                {
                    if (a?.name == null || a.browser_download_url == null) continue;
                    if (!a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;

                    string n = a.name.ToLowerInvariant();
                    bool hasWindows = n.Contains("windows") || n.Contains("win64") || n.Contains("win32");
                    bool has64Bit = n.Contains("amd64") || n.Contains("x64") || n.Contains("win64");
                    if (hasWindows && has64Bit) return a.browser_download_url;
                }
                return null;
            }
        }

        [DataContract]
        private class EsptoolReleaseDto
        {
            [DataMember(Name = "assets", EmitDefaultValue = false)]
            public EsptoolAssetDto[] assets { get; set; }
        }

        [DataContract]
        private class EsptoolAssetDto
        {
            [DataMember(Name = "name", EmitDefaultValue = false)]
            public string name { get; set; }
            [DataMember(Name = "browser_download_url", EmitDefaultValue = false)]
            public string browser_download_url { get; set; }
        }

        public static async Task<string> DownloadFirmwareAsync(string url, Version version, CancellationToken ct)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DeFocuserLite-Firmware");
            Directory.CreateDirectory(tempDir);

            string fileName = "firmware-" + (version?.ToString() ?? "latest") + ".bin";
            string path = Path.Combine(tempDir, fileName);

            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var dst = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await src.CopyToAsync(dst, 81920, ct).ConfigureAwait(false);
                }
            }
            return path;
        }

        // Returns true on exit code 0. Streams every stdout/stderr line to
        // `onLine` so the caller can mirror it in the UI console.
        public static Task<bool> FlashAsync(
            string esptoolPath,
            string comPort,
            string binPath,
            Action<FlashProgress> onLine,
            CancellationToken ct)
        {
            if (string.IsNullOrEmpty(esptoolPath) || !File.Exists(esptoolPath))
                throw new FileNotFoundException("esptool.exe not found", esptoolPath ?? "(null)");
            if (string.IsNullOrEmpty(comPort))
                throw new ArgumentException("COM port is required", nameof(comPort));
            if (!File.Exists(binPath))
                throw new FileNotFoundException("Firmware .bin not found", binPath);

            var args =
                "--chip esp32c3 " +
                "--port " + comPort + " " +
                "--baud 921600 " +
                "--before default_reset --after hard_reset " +
                "write_flash 0x0 \"" + binPath + "\"";

            var psi = new ProcessStartInfo
            {
                FileName = esptoolPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var tcs = new TaskCompletionSource<bool>();
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            p.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) onLine?.Invoke(new FlashProgress { Line = e.Data, IsError = false });
            };
            p.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) onLine?.Invoke(new FlashProgress { Line = e.Data, IsError = true });
            };
            p.Exited += (s, e) =>
            {
                try { tcs.TrySetResult(p.ExitCode == 0); }
                finally { p.Dispose(); }
            };

            ct.Register(() =>
            {
                try { if (!p.HasExited) p.Kill(); } catch { }
                tcs.TrySetCanceled();
            });

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            return tcs.Task;
        }
    }
}
