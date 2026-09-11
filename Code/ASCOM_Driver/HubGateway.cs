/*
 * HubGateway.cs
 * Copyright (C) 2025 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 *
 * Locates the hub this driver should talk to. The driver used to ask the user
 * for a COM port in its own Setup dialog; the hub owns that choice now and
 * writes it into this driver's ASCOM Profile, so all this has to do is find
 * the matching pipe — or, failing that, the only hub that is actually running.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;

namespace ASCOM.DeKoi
{
    internal static class HubGateway
    {
        public const string PIPE_PREFIX = "DeFocuserLitePipe";

        public static string BuildPipeName(string portName)
        {
            return PIPE_PREFIX + (string.IsNullOrWhiteSpace(portName)
                ? string.Empty
                : "_" + portName.Trim().ToUpperInvariant());
        }

        /// <summary>
        /// COM ports of every hub currently serving a pipe. Enumerating the
        /// pipe filesystem is the only way to see hubs we were not told about.
        /// </summary>
        public static List<string> RunningHubPorts()
        {
            var ports = new List<string>();

            try
            {
                foreach (string path in Directory.GetFiles(@"\\.\pipe\"))
                {
                    string name = Path.GetFileName(path);
                    if (name == null) continue;
                    if (!name.StartsWith(PIPE_PREFIX + "_", StringComparison.OrdinalIgnoreCase)) continue;

                    string port = name.Substring(PIPE_PREFIX.Length + 1);
                    if (port.Length > 0) ports.Add(port.ToUpperInvariant());
                }
            }
            catch
            {
                // Pipe enumeration is best-effort; an empty list just means we
                // fall back to the configured port.
            }

            return ports.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
        }

        public static bool IsHubRunning(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName)) return false;
            return RunningHubPorts().Any(p => string.Equals(p, portName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Which port to connect to, given what the hub wrote into the Profile.
        /// Preference order: the claimed port if its hub is up, else the single
        /// running hub. Returns null when the caller has to launch one.
        /// </summary>
        /// <param name="ambiguous">
        /// Set when several hubs are running and none of them is the claimed
        /// one — the caller must not guess, it has to tell the user to pick.
        /// </param>
        public static string ResolvePort(string claimedPort, out List<string> ambiguous)
        {
            ambiguous = null;

            var running = RunningHubPorts();

            if (!string.IsNullOrWhiteSpace(claimedPort)
                && running.Any(p => string.Equals(p, claimedPort.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return claimedPort.Trim().ToUpperInvariant();
            }

            if (running.Count == 1) return running[0];

            if (running.Count > 1)
            {
                ambiguous = running;
                return null;
            }

            // Nothing running. A claimed port still tells us what to launch.
            return string.IsNullOrWhiteSpace(claimedPort) ? null : claimedPort.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Asks a running hub to bring its window up. Returns false when no hub
        /// is serving that port, which the caller treats as "launch one".
        /// </summary>
        public static bool TryShowHubWindow(string portName, int timeoutMs = 1000)
        {
            try
            {
                using (var pipe = new NamedPipeClientStream(".", BuildPipeName(portName), PipeDirection.InOut))
                {
                    pipe.Connect(timeoutMs);
                    using (var reader = new StreamReader(pipe))
                    using (var writer = new StreamWriter(pipe) { AutoFlush = true })
                    {
                        writer.WriteLine("IPC:SHOW");
                        string response = reader.ReadLine();
                        return response == "IPC:SHOW:OK";
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
