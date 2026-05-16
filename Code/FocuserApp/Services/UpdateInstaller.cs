/*
 * UpdateInstaller.cs
 * Downloads the latest installer to %TEMP%, runs it silently with the existing
 * uninstall + relaunch flow that Setup.iss already implements, then shuts the
 * current app down so the installer can replace the EXE on disk.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.DeKoi.DeFocuserApp.Services
{
    public class UpdateDownloadProgress
    {
        public long BytesReceived { get; set; }
        public long? TotalBytes { get; set; }
        public double Percent => TotalBytes.HasValue && TotalBytes.Value > 0
            ? (double)BytesReceived / TotalBytes.Value * 100.0
            : 0.0;
    }

    public static class UpdateInstaller
    {
        private static readonly HttpClient http = CreateClient();

        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        }

        public static async Task<string> DownloadInstallerAsync(
            string url,
            Version version,
            IProgress<UpdateDownloadProgress> progress,
            CancellationToken ct)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DeFocuserLite-Update");
            Directory.CreateDirectory(tempDir);

            string fileName = "DeFocuserLite-Setup-" + (version?.ToString() ?? "latest") + ".exe";
            string path = Path.Combine(tempDir, fileName);

            using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                long? total = resp.Content.Headers.ContentLength;

                using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var dst = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920];
                    long received = 0;
                    int read;
                    while ((read = await src.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await dst.WriteAsync(buffer, 0, read, ct).ConfigureAwait(false);
                        received += read;
                        progress?.Report(new UpdateDownloadProgress { BytesReceived = received, TotalBytes = total });
                    }
                }
            }

            return path;
        }

        // Launches the installer with silent flags and asks WPF to shut down so
        // the installer can replace the running .exe. Setup.iss has a [Run]
        // entry that relaunches the app after install completes.
        public static void LaunchAndExit(string installerPath)
        {
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
                UseShellExecute = true,
            };

            Process.Start(psi);

            // Give the installer a moment to take a process handle before we exit.
            Thread.Sleep(500);

            System.Windows.Application.Current.Shutdown();
        }
    }
}
