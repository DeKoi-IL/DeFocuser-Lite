/*
 * PipeServer.cs
 * Copyright (C) 2025 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 *
 * Named pipe server that accepts connections from ASCOM driver instances
 * and routes commands to the SerialManager (which owns the serial port).
 * This enables the mediator architecture where multiple ASCOM clients
 * can share a single serial connection.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.DeKoi.DeFocuserMediator
{
    internal class PipeServer : IDisposable
    {
        public const string PIPE_NAME = "DeFocuserLitePipe";

        private const string DEVICE_GUID = "dfafe960-d19c-4abd-af4a-4dc5f49775a3";

        private readonly SerialManager serialManager;
        private readonly List<int> activeClientIds = new List<int>();
        private readonly object clientLock = new object();

        private CancellationTokenSource cts;
        private bool isRunning;
        private int nextClientId = 1;

        /// <summary>
        /// The number of ASCOM driver clients currently connected via pipes.
        /// </summary>
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

        /// <summary>
        /// Event fired when the connected client count changes.
        /// The int parameter is the new client count.
        /// </summary>
        public event Action<int> ClientCountChanged;

        public PipeServer(SerialManager serialManager)
        {
            this.serialManager = serialManager;
        }

        /// <summary>
        /// Start accepting pipe connections from ASCOM driver instances.
        /// </summary>
        public void Start()
        {
            if (isRunning)
                return;

            isRunning = true;
            cts = new CancellationTokenSource();

            Task.Run(() => AcceptLoop(cts.Token));
        }

        /// <summary>
        /// Stop accepting connections and disconnect all clients.
        /// </summary>
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

        /// <summary>
        /// Main loop that continuously creates pipe server instances and
        /// accepts incoming connections. Each client gets its own handler thread.
        /// </summary>
        private async Task AcceptLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream pipe = null;

                try
                {
                    pipe = new NamedPipeServerStream(
                        PIPE_NAME,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    // Wait for a client connection
                    await Task.Factory.FromAsync(pipe.BeginWaitForConnection, pipe.EndWaitForConnection, null);

                    if (token.IsCancellationRequested)
                    {
                        pipe.Dispose();
                        break;
                    }

                    // Spawn a handler for this client (don't await - run concurrently).
                    // ContinueWith catches any exception that escapes HandleClient's
                    // own try/catch, preventing unobserved task exceptions.
                    var clientPipe = pipe;
                    pipe = null; // Prevent disposal in finally
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
                    // Pipe error, retry
                    await Task.Delay(100, token);
                }
                finally
                {
                    pipe?.Dispose();
                }
            }
        }

        /// <summary>
        /// Handle a single connected ASCOM driver client.
        /// Reads commands line by line, routes them to the SerialManager,
        /// and writes responses back.
        /// </summary>
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
                            break; // Pipe broken
                        }

                        if (line == null)
                            break; // Client disconnected

                        string response = ProcessCommand(line.Trim());

                        try
                        {
                            await writer.WriteLineAsync(response);
                        }
                        catch (Exception)
                        {
                            break; // Pipe broken
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Client handler crashed - clean up
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

        /// <summary>
        /// Process a single command from an ASCOM driver client.
        /// IPC-specific commands are handled internally.
        /// Device commands are forwarded to the SerialManager.
        /// </summary>
        private string ProcessCommand(string command)
        {
            // Handle IPC-specific commands
            if (command == "IPC:CONNECT")
            {
                return "IPC:CONNECT:OK";
            }

            if (command == "IPC:DISCONNECT")
            {
                return "IPC:DISCONNECT:OK";
            }

            if (command == "IPC:ISCONNECTED")
            {
                return "IPC:ISCONNECTED:" + (serialManager.IsConnected ? "TRUE" : "FALSE");
            }

            // Handle PING locally (the mediator itself can respond since motor might not be connected yet)
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

            // All other COMMAND: prefixed messages go to the serial device
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
