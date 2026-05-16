/*
 * FirmwareFlasher.cs
 * Downloads the released firmware .bin and writes it to the ESP32-C3 over the
 * same serial port the hub normally uses. Wraps the bundled esptool.exe so we
 * don't need to ship a Python runtime. Caller must Disconnect() the serial
 * port first — esptool requires exclusive access.
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

        public static string ResolveEsptoolPath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string installed = Path.Combine(baseDir, "tools", "esptool.exe");
            if (File.Exists(installed)) return installed;

            // Dev fallback: walk up to repo root and look in Tools/esptool.
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

            return null;
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
