/*
 * PipeServer.cs
 * Copyright (C) 2025 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 *
 * Named pipe server that accepts connections from ASCOM driver instances
 * and routes commands to the SerialManager (which owns the serial port).
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.DeKoi.DeFocuserApp
{
    internal class PipeServer : IDisposable
    {
        // Pipe names are per-COM-port so multiple hub instances (one focuser
        // each) can coexist. Each instance serves "DeFocuserLitePipe_<COMx>".
        // The ASCOM driver builds the same name from the port chosen in its
        // Setup dialog. Keep this prefix in sync with the driver.
        public const string PIPE_PREFIX = "DeFocuserLitePipe";

        public static string BuildPipeName(string portName)
        {
            string suffix = string.IsNullOrWhiteSpace(portName)
                ? string.Empty
                : "_" + portName.Trim().ToUpperInvariant();
            return PIPE_PREFIX + suffix;
        }

        private const string DEVICE_GUID = "dfafe960-d19c-4abd-af4a-4dc5f49775a3";

        private readonly SerialManager serialManager;
        private readonly List<int> activeClientIds = new List<int>();
        private readonly object clientLock = new object();

        private CancellationTokenSource cts;
        private bool isRunning;
        private int nextClientId = 1;
        private string pipeName;

        public int ConnectedClientCount
        {
            get
            {
                lock (clientLock)
                {
                    return activeClientIds.Count;
                }
            }
        }

        public event Action<int> ClientCountChanged;

        /// <summary>
        /// Raised when a driver asks this hub to come to the foreground —
        /// the ASCOM client's Setup button, which no longer opens a dialog of
        /// its own. Fired on a pipe worker thread; marshal before touching UI.
        /// </summary>
        public event Action ShowRequested;

        public PipeServer(SerialManager serialManager)
        {
            this.serialManager = serialManager;
        }

        /// <summary>
        /// Explicit DACL granting authenticated users read/write access and the
        /// right to create additional pipe instances. Without this the pipe gets
        /// a restrictive default DACL and ASCOM clients in a separate process
        /// (e.g. N.I.N.A.) can hit "Access to the path is denied" on connect.
        ///
        /// NOTE: this controls the DACL only — it does NOT lower the pipe's
        /// mandatory integrity label. A client running at a LOWER integrity than
        /// this app (app elevated, client not) is still blocked by Windows
        /// "no write-up". Run the hub and the ASCOM client at the same elevation.
        /// </summary>
        private static readonly PipeSecurity PipeAccessSecurity = BuildPipeSecurity();

        private static PipeSecurity BuildPipeSecurity()
        {
            var security = new PipeSecurity();
            var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            security.AddAccessRule(new PipeAccessRule(
                authenticatedUsers,
                PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
                AccessControlType.Allow));
            return security;
        }

        public void Start(string portName)
        {
            if (isRunning)
                return;

            pipeName = BuildPipeName(portName);
            isRunning = true;
            cts = new CancellationTokenSource();

            Task.Run(() => AcceptLoop(cts.Token));
        }

        public void Stop()
        {
            if (!isRunning)
                return;

            isRunning = false;
            cts?.Cancel();

            lock (clientLock)
            {
                activeClientIds.Clear();
            }

            ClientCountChanged?.Invoke(0);
        }

        private async Task AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;

                try
                {
                    pipe = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        0,
                        0,
                        PipeAccessSecurity);

                    await Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null);

                    if (token.IsCancellationRequested)
                    {
                        pipe.Dispose();
                        break;
                    }

                    var clientPipe = pipe;
                    pipe = null;
                    _ = Task.Run(() => HandleClient(clientPipe, token))
                        .ContinueWith(t =>
                        {
                            try
                            {
                                clientPipe.Dispose();
                            }
                            catch { }
                        }, TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(100, token);
                }
                finally
                {
                    pipe?.Dispose();
                }
            }
        }

        private async Task HandleClient(NamedPipeServerStream pipe, CancellationToken token)
        {
            int clientId;
            lock (clientLock)
            {
                clientId = nextClientId++;
                activeClientIds.Add(clientId);
            }
            ClientCountChanged?.Invoke(ConnectedClientCount);

            try
            {
                using (var reader = new StreamReader(pipe))
                using (var writer = new StreamWriter(pipe) { AutoFlush = true })
                {
                    string line;
                    while (!token.IsCancellationRequested && pipe.IsConnected)
                    {
                        try
                        {
                            line = await reader.ReadLineAsync();
                        }
                        catch (Exception)
                        {
                            break;
                        }

                        if (line == null)
                            break;

                        string response = ProcessCommand(line.Trim());

                        try
                        {
                            await writer.WriteLineAsync(response);
                        }
                        catch (Exception)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                lock (clientLock)
                {
                    activeClientIds.Remove(clientId);
                }
                ClientCountChanged?.Invoke(ConnectedClientCount);

                try { pipe.Dispose(); }
                catch { }
            }
        }

        private string ProcessCommand(string command)
        {
            if (command == "IPC:CONNECT")
            {
                return "IPC:CONNECT:OK";
            }

            if (command == "IPC:DISCONNECT")
            {
                return "IPC:DISCONNECT:OK";
            }

            if (command == "IPC:SHOW")
            {
                try { ShowRequested?.Invoke(); } catch { }
                return "IPC:SHOW:OK";
            }

            if (command == "IPC:ISCONNECTED")
            {
                return "IPC:ISCONNECTED:" + (serialManager.IsConnected ? "TRUE" : "FALSE");
            }

            if (command == "COMMAND:PING")
            {
                if (serialManager.IsConnected)
                {
                    return "RESULT:PING:OK:" + DEVICE_GUID;
                }
                else
                {
                    return "ERROR:NOT_CONNECTED";
                }
            }

            if (command.StartsWith("COMMAND:"))
            {
                if (!serialManager.IsConnected)
                {
                    return "ERROR:NOT_CONNECTED";
                }

                try
                {
                    return serialManager.SendRawCommand(command);
                }
                catch (Exception ex)
                {
                    return "ERROR:" + ex.Message;
                }
            }

            return "ERROR:INVALID_COMMAND";
        }

        public void Dispose()
        {
            Stop();
            cts?.Dispose();
        }
    }
}
