/*
 * FocuserDriver.cs
 * Copyright (C) 2025 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Based on the original work of Julien Lecomte with his OAG Focuser.
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 *
 * This ASCOM driver is a thin IPC proxy. It does not own the serial connection.
 * Instead, it communicates with the DeFocuser Lite Mediator App via named pipes.
 * The mediator app owns the serial connection and can serve multiple ASCOM clients.
 */

using ASCOM.DeviceInterface;
using ASCOM.Utilities;

using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;

namespace ASCOM.DeKoi
{
    //
    // Your driver's DeviceID is ASCOM.DeKoi.DeFocuserLite
    //
    // The Guid attribute sets the CLSID for ASCOM.DeKoi.DeFocuserLite
    // The ClassInterface/None attribute prevents an empty interface called
    // _DeKoi from being created and used as the [default] interface
    //

    /// <summary>
    /// ASCOM Focuser Driver for DeKoi.
    /// This is a thin proxy that forwards commands to the DeFocuser Lite Mediator App via named pipes.
    /// </summary>
    [Guid("33af006c-fed0-4d90-907b-9e77ea4f4fef")]
    [ProgId("ASCOM.DeKoi.DeFocuserLite")]
    [ClassInterface(ClassInterfaceType.None)]
    public class Focuser : IFocuserV3
    {
        /// <summary>
        /// ASCOM DeviceID (COM ProgID) for this driver.
        /// The DeviceID is used by ASCOM applications to load the driver at runtime.
        /// </summary>
        internal static string driverID = "ASCOM.DeKoi.DeFocuserLite";

        /// <summary>
        /// Driver description that displays in the ASCOM Chooser.
        /// </summary>
        private static readonly string deviceName = "DeKoi DeFocuser Lite";

        // Constants used for Profile persistence
        internal static string traceStateProfileName = "Trace Level";
        internal static string traceStateDefault = "false";

        // Named pipe constants
        private const string PIPE_NAME = "DeFocuserLitePipe";
        private const string MEDIATOR_PROCESS_NAME = "ASCOM.DeKoi.DeFocuserApp";

        // Protocol constants (same as serial protocol)
        private const string OK = "OK";
        private const string TRUE = "TRUE";
        private const string FALSE = "FALSE";

        private const string COMMAND_FOCUSER_GETPOSITION = "COMMAND:FOCUSER:GETPOSITION";
        private const string RESULT_FOCUSER_POSITION = "RESULT:FOCUSER:POSITION:";

        private const string COMMAND_FOCUSER_GETMAXPOSITION = "COMMAND:FOCUSER:GETMAXPOSITION";
        private const string RESULT_FOCUSER_MAXPOSITION = "RESULT:FOCUSER:MAXPOSITION:";

        private const string COMMAND_FOCUSER_ISMOVING = "COMMAND:FOCUSER:ISMOVING";
        private const string RESULT_FOCUSER_ISMOVING = "RESULT:FOCUSER:ISMOVING:";

        private const string COMMAND_FOCUSER_SETREVERSE = "COMMAND:FOCUSER:SETREVERSE:";
        private const string RESULT_FOCUSER_SETREVERSE = "RESULT:FOCUSER:SETREVERSE:";

        private const string COMMAND_FOCUSER_ISREVERSE = "COMMAND:FOCUSER:ISREVERSE";
        private const string RESULT_FOCUSER_ISREVERSE = "RESULT:FOCUSER:ISREVERSE:";

        private const string COMMAND_FOCUSER_CALIBRATE = "COMMAND:FOCUSER:CALIBRATE";
        private const string RESULT_FOCUSER_CALIBRATE = "RESULT:FOCUSER:CALIBRATE:";

        private const string COMMAND_FOCUSER_ISCALIBRATING = "COMMAND:FOCUSER:ISCALIBRATING";
        private const string RESULT_FOCUSER_ISCALIBRATING = "RESULT:FOCUSER:ISCALIBRATING:";

        private const string COMMAND_FOCUSER_SETPOSITION = "COMMAND:FOCUSER:SETPOSITION:";
        private const string RESULT_FOCUSER_SETPOSITION = "RESULT:FOCUSER:SETPOSITION:";

        private const string COMMAND_FOCUSER_SETZEROPOSITION = "COMMAND:FOCUSER:SETZEROPOSITION";
        private const string RESULT_FOCUSER_SETZEROPOSITION = "RESULT:FOCUSER:SETZEROPOSITION:";

        private const string COMMAND_FOCUSER_MOVE = "COMMAND:FOCUSER:MOVE:";
        private const string RESULT_FOCUSER_MOVE = "RESULT:FOCUSER:MOVE:";

        private const string COMMAND_FOCUSER_HALT = "COMMAND:FOCUSER:HALT";
        private const string RESULT_FOCUSER_HALT = "RESULT:FOCUSER:HALT:";

        private const string COMMAND_FOCUSER_SETLIMIT = "COMMAND:FOCUSER:SETLIMIT";
        private const string RESULT_FOCUSER_SETLIMIT = "RESULT:FOCUSER:SETLIMIT:";

        /// <summary>
        /// Variable to hold the trace logger object
        /// </summary>
        internal TraceLogger tl;

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;

        /// <summary>
        /// Lock object for thread-safe pipe communication
        /// </summary>
        private readonly object lockObject = new object();

        /// <summary>
        /// Named pipe client for communicating with the mediator app
        /// </summary>
        private NamedPipeClientStream pipeClient;
        private StreamReader pipeReader;
        private StreamWriter pipeWriter;

        /// <summary>
        /// Initializes a new instance of the <see cref="Focuser"/> class.
        /// Must be public for COM registration.
        /// </summary>
        public Focuser()
        {
            tl = new TraceLogger("", "DeKoi");
            ReadProfile();
            LogMessage("Focuser", "Starting initialization");
            connectedState = false;
            LogMessage("Focuser", "Completed initialization");
        }

        //
        // PUBLIC COM INTERFACE IFocuserV3 IMPLEMENTATION
        //

        #region Common properties and methods.

        /// <summary>
        /// Displays the Setup Dialog form.
        /// All configuration is handled by the DeFocuser Lite Controller app.
        /// This method does nothing heavy — the mediator app is launched
        /// only when Connected=true is called.
        /// </summary>
        public void SetupDialog()
        {
            System.Windows.Forms.MessageBox.Show(
                "No setup needed, click connect :)",
                "DeFocuser Lite", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
        }

        public ArrayList SupportedActions
        {
            get
            {
                LogMessage("SupportedActions Get", "Returning supported actions");
                return new ArrayList()
                {
                    "SetZeroPosition",
                    "SetLimit"
                };
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            string response;
            string pipeResponse;

            switch (actionName.ToUpper())
            {
                case "SETZEROPOSITION":
                    pipeResponse = SendPipeCommand(COMMAND_FOCUSER_SETZEROPOSITION);
                    response = ParseResponse(pipeResponse, RESULT_FOCUSER_SETZEROPOSITION);
                    if (response != OK)
                    {
                        LogMessage("SetZeroPosition", "Device responded with an error");
                        throw new DriverException("Device responded with an error");
                    }
                    return string.Empty;

                case "SETPOSITION":
                    int parameter = int.Parse(actionParameters);
                    pipeResponse = SendPipeCommand(COMMAND_FOCUSER_SETPOSITION + parameter.ToString());
                    response = ParseResponse(pipeResponse, RESULT_FOCUSER_SETPOSITION);
                    if (response != OK)
                    {
                        LogMessage("SetPosition", $"Device responded with an error for parameter {parameter} with response {response}");
                        throw new DriverException($"Device responded with an error for parameter {parameter} with response {response}");
                    }
                    return string.Empty;

                case "SETREVERSE":
                    bool setReverseParameter = bool.Parse(actionParameters);
                    pipeResponse = SendPipeCommand(COMMAND_FOCUSER_SETREVERSE + (setReverseParameter ? TRUE : FALSE));
                    response = ParseResponse(pipeResponse, RESULT_FOCUSER_SETREVERSE);
                    if (response != OK)
                    {
                        LogMessage("SetReverse", $"Device responded with an error for parameter {setReverseParameter} with response {response}");
                        throw new DriverException($"Device responded with an error for parameter {setReverseParameter} with response {response}");
                    }
                    return string.Empty;

                case "ISREVERSE":
                    pipeResponse = SendPipeCommand(COMMAND_FOCUSER_ISREVERSE);
                    response = ParseResponse(pipeResponse, RESULT_FOCUSER_ISREVERSE);
                    if (response != TRUE && response != FALSE)
                    {
                        LogMessage("IsReverse", $"Device responded with an error: {response}");
                        throw new DriverException($"Device responded with an error: {response}");
                    }
                    return response;

                case "CALIBRATE":
                    pipeResponse = SendPipeCommand(COMMAND_FOCUSER_CALIBRATE);
                    response = ParseResponse(pipeResponse, RESULT_FOCUSER_CALIBRATE);
                    if (response != OK)
                    {
                        LogMessage("Calibrate", "Device responded with an error");
                        throw new DriverException("Device responded with an error");
                    }
                    return response;

                case "ISCALIBRATING":
                    pipeResponse = SendPipeCommand(COMMAND_FOCUSER_ISCALIBRATING);
                    response = ParseResponse(pipeResponse, RESULT_FOCUSER_ISCALIBRATING);
                    if (response != TRUE && response != FALSE)
                    {
                        LogMessage("IsCalibrating", $"Device responded with an error: {response}");
                        throw new DriverException($"Device responded with an error: {response}");
                    }
                    return response;

                case "SETLIMIT":
                    pipeResponse = SendPipeCommand(COMMAND_FOCUSER_SETLIMIT);
                    response = ParseResponse(pipeResponse, RESULT_FOCUSER_SETLIMIT);
                    if (response != OK)
                    {
                        LogMessage("SetLimit", "Device responded with an error");
                        throw new DriverException("Device responded with an error");
                    }
                    return string.Empty;

                default:
                    LogMessage("", "Action {0} not implemented", actionName);
                    throw new ActionNotImplementedException("Action " + actionName + " is not implemented by this driver");
            }
        }

        public void CommandBlind(string command, bool raw)
        {
            CheckConnected("CommandBlind");
            throw new ASCOM.MethodNotImplementedException("CommandBlind");
        }

        public bool CommandBool(string command, bool raw)
        {
            CheckConnected("CommandBool");
            throw new ASCOM.MethodNotImplementedException("CommandBool");
        }

        public string CommandString(string command, bool raw)
        {
            CheckConnected("CommandString");
            throw new ASCOM.MethodNotImplementedException("CommandString");
        }

        public void Dispose()
        {
            if (connectedState)
            {
                try { SendPipeCommand("IPC:DISCONNECT"); } catch { }
                connectedState = false;
            }
            CleanupPipe();

            tl.Enabled = false;
            tl.Dispose();
            tl = null;
        }

        public bool Connected
        {
            get
            {
                LogMessage("Connected", "Get {0}", IsConnected);
                return IsConnected;
            }
            set
            {
                LogMessage("Connected", "Set {0}", value);
                if (value == IsConnected)
                    return;

                if (value)
                {
                    LogMessage("Connected Set", "Connecting to mediator app");

                    // 1. Launch mediator app if not running
                    EnsureMediatorAppRunning();

                    // 2. Connect to named pipe
                    try
                    {
                        pipeClient = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut);
                        pipeClient.Connect(10000); // 10 second timeout
                        pipeReader = new StreamReader(pipeClient);
                        pipeWriter = new StreamWriter(pipeClient) { AutoFlush = true };
                    }
                    catch (TimeoutException)
                    {
                        CleanupPipe();
                        throw new NotConnectedException("Timed out connecting to the DeFocuser Lite Mediator application.");
                    }
                    catch (Exception ex)
                    {
                        CleanupPipe();
                        throw new NotConnectedException("Failed to connect to the DeFocuser Lite Mediator: " + ex.Message);
                    }

                    // 3. Register as client
                    string registerResponse = SendPipeCommand("IPC:CONNECT");
                    if (registerResponse != "IPC:CONNECT:OK")
                    {
                        CleanupPipe();
                        throw new NotConnectedException("Failed to register with the mediator application.");
                    }

                    // 4. Verify device connectivity
                    string connResponse = SendPipeCommand("IPC:ISCONNECTED");
                    if (connResponse != "IPC:ISCONNECTED:TRUE")
                    {
                        CleanupPipe();
                        throw new NotConnectedException(
                            "The DeFocuser Lite Mediator is not connected to the hardware. " +
                            "Please open the DeFocuser Lite app, select a COM port, and click Connect.");
                    }

                    connectedState = true;
                    LogMessage("Connected Set", "Connected via mediator app");
                }
                else
                {
                    LogMessage("Connected Set", "Disconnecting from mediator app");
                    connectedState = false;

                    try
                    {
                        SendPipeCommand("IPC:DISCONNECT");
                    }
                    catch (Exception) { }

                    CleanupPipe();
                }
            }
        }

        public string Description
        {
            get
            {
                LogMessage("Description Get", deviceName);
                return deviceName;
            }
        }

        public string DriverInfo
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverInfo = deviceName + " ASCOM Driver Version " + String.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                LogMessage("DriverInfo Get", driverInfo);
                return driverInfo;
            }
        }

        public string DriverVersion
        {
            get
            {
                Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                string driverVersion = string.Format(CultureInfo.InvariantCulture, "{0}.{1}", version.Major, version.Minor);
                LogMessage("DriverVersion Get", driverVersion);
                return driverVersion;
            }
        }

        public short InterfaceVersion
        {
            get
            {
                LogMessage("InterfaceVersion Get", "3");
                return Convert.ToInt16("3");
            }
        }

        public string Name
        {
            get
            {
                LogMessage("Name Get", deviceName);
                return deviceName;
            }
        }

        #endregion

        #region IFocuser Implementation

        // This is an absolute positioning focuser.
        public bool Absolute
        {
            get
            {
                LogMessage("Absolute Get", true.ToString());
                return true;
            }
        }

        public void Halt()
        {
            string response = SendPipeCommand(COMMAND_FOCUSER_HALT);
            ParseResponse(response, RESULT_FOCUSER_HALT);
            // Ignore whether the firmware responded with OK or NOK.
        }

        public bool IsMoving
        {
            get
            {
                string pipeResponse = SendPipeCommand(COMMAND_FOCUSER_ISMOVING);
                string response = ParseResponse(pipeResponse, RESULT_FOCUSER_ISMOVING);
                if (response != TRUE && response != FALSE)
                {
                    LogMessage("IsMoving", "Invalid response from device: " + response);
                    throw new DriverException("Invalid response from device: " + response);
                }
                return (response == TRUE);
            }
        }

        // Direct function to the connected method, the Link method is just here for backwards compatibility
        public bool Link
        {
            get
            {
                LogMessage("Link Get", this.Connected.ToString());
                return this.Connected;
            }
            set
            {
                LogMessage("Link Set", value.ToString());
                this.Connected = value;
            }
        }

        // Maximum change in one move.
        public int MaxIncrement
        {
            get
            {
                int maxStep = MaxStep;
                LogMessage("MaxIncrement Get", maxStep.ToString());
                return maxStep;
            }
        }

        // Maximum extent of the focuser.
        public int MaxStep
        {
            get
            {
                string pipeResponse = SendPipeCommand(COMMAND_FOCUSER_GETMAXPOSITION);
                string response = ParseResponse(pipeResponse, RESULT_FOCUSER_MAXPOSITION);
                int value;
                try
                {
                    value = int.Parse(response);
                }
                catch (FormatException)
                {
                    LogMessage("MaxStep", "Invalid max step value received from device: " + response);
                    throw new DriverException("Invalid max step value received from device: " + response);
                }
                return value;
            }
        }

        public void Move(int Position)
        {
            int maxStep = MaxStep;
            if (Position < 0 || Position > maxStep)
            {
                throw new InvalidValueException("Position", Position.ToString(), "0", maxStep.ToString());
            }
            string pipeResponse = SendPipeCommand(COMMAND_FOCUSER_MOVE + Position.ToString());
            string response = ParseResponse(pipeResponse, RESULT_FOCUSER_MOVE);
            if (response != OK)
            {
                LogMessage("Move", "Device responded with an error");
                throw new DriverException("Device responded with an error");
            }
        }

        public int Position
        {
            get
            {
                string pipeResponse = SendPipeCommand(COMMAND_FOCUSER_GETPOSITION);
                string response = ParseResponse(pipeResponse, RESULT_FOCUSER_POSITION);
                int value;
                try
                {
                    value = int.Parse(response);
                }
                catch (FormatException)
                {
                    LogMessage("Position", "Invalid position value received from device: " + response);
                    throw new DriverException("Invalid position value received from device: " + response);
                }
                return value;
            }
        }

        public double StepSize
        {
            get
            {
                LogMessage("StepSize Get", "Not implemented");
                throw new PropertyNotImplementedException("StepSize", false);
            }
        }

        public bool TempComp
        {
            get
            {
                LogMessage("TempComp Get", false.ToString());
                return false;
            }
            set
            {
                LogMessage("TempComp Set", "Not implemented");
                throw new PropertyNotImplementedException("TempComp", false);
            }
        }

        public bool TempCompAvailable
        {
            get
            {
                LogMessage("TempCompAvailable Get", false.ToString());
                return false;
            }
        }

        public double Temperature
        {
            get
            {
                LogMessage("Temperature Get", "Not implemented");
                throw new PropertyNotImplementedException("Temperature", false);
            }
        }

        #endregion

        #region Private properties and methods

        #region ASCOM Registration

        // Register or unregister driver for ASCOM. This is harmless if already
        // registered or unregistered.
        private static void RegUnregASCOM(bool bRegister)
        {
            using (var P = new Profile())
            {
                P.DeviceType = "Focuser";
                if (bRegister)
                {
                    P.Register(driverID, deviceName);
                }
                else
                {
                    P.Unregister(driverID);
                }
            }
        }

        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        [ComUnregisterFunction]
        public static void UnregisterASCOM(Type t)
        {
            RegUnregASCOM(false);
        }

        #endregion

        /// <summary>
        /// Returns true if there is a valid connection to the driver hardware
        /// </summary>
        private bool IsConnected
        {
            get
            {
                return connectedState;
            }
        }

        /// <summary>
        /// Use this function to throw an exception if we aren't connected to the hardware
        /// </summary>
        private void CheckConnected(string message)
        {
            if (!IsConnected)
            {
                throw new NotConnectedException(message);
            }
        }

        /// <summary>
        /// Read the device configuration from the ASCOM Profile store
        /// </summary>
        internal void ReadProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";
                tl.Enabled = Convert.ToBoolean(driverProfile.GetValue(driverID, traceStateProfileName, string.Empty, traceStateDefault));
            }
        }

        /// <summary>
        /// Write the device configuration to the ASCOM Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";
                driverProfile.WriteValue(driverID, traceStateProfileName, tl.Enabled.ToString());
            }
        }

        /// <summary>
        /// Launches the mediator app if not already running.
        /// Does NOT wait for the pipe to become available (non-blocking).
        /// Used by SetupDialog to avoid freezing the caller.
        /// </summary>
        private void LaunchMediatorApp()
        {
            var processes = Process.GetProcessesByName(MEDIATOR_PROCESS_NAME);
            if (processes.Length > 0)
            {
                LogMessage("LaunchMediatorApp", "Mediator app is already running");
                return;
            }

            string driverDir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            string appPath = Path.Combine(driverDir, MEDIATOR_PROCESS_NAME + ".exe");

            if (!File.Exists(appPath))
            {
                throw new DriverException("DeFocuser Lite Mediator application not found at: " + appPath);
            }

            LogMessage("LaunchMediatorApp", "Launching mediator app from: " + appPath);
            Process.Start(appPath);
        }

        /// <summary>
        /// Ensures the mediator app is running and its pipe server is available.
        /// Launches the app if needed, then blocks until the pipe is reachable.
        /// Fails immediately if the mediator process exits (user closed the app).
        /// </summary>
        private void EnsureMediatorAppRunning()
        {
            LaunchMediatorApp();

            // Wait for the pipe to become available
            int retries = 30; // 30 * 500ms = 15 seconds
            while (retries > 0)
            {
                // If the mediator process has exited (user closed it), fail immediately
                var processes = Process.GetProcessesByName(MEDIATOR_PROCESS_NAME);
                if (processes.Length == 0)
                {
                    throw new DriverException(
                        "The DeFocuser Lite Controller was closed. " +
                        "Please connect to the focuser in the Controller app before connecting in N.I.N.A.");
                }

                try
                {
                    using (var testPipe = new NamedPipeClientStream(".", PIPE_NAME, PipeDirection.InOut))
                    {
                        testPipe.Connect(500);
                        LogMessage("EnsureMediatorAppRunning", "Mediator pipe is available");
                        return; // Pipe is available
                    }
                }
                catch (TimeoutException) { }
                catch (IOException) { }

                retries--;
                Thread.Sleep(100);
            }

            throw new DriverException(
                "The DeFocuser Lite Controller is running but not connected to the focuser. " +
                "Please select a COM port and click Connect in the Controller app first.");
        }

        /// <summary>
        /// Send a command over the named pipe and return the response.
        /// </summary>
        private string SendPipeCommand(string command)
        {
            // IPC commands don't require hardware connection
            if (!command.StartsWith("IPC:"))
            {
                CheckConnected("SendPipeCommand: " + command);
            }

            lock (lockObject)
            {
                LogMessage("SendPipeCommand", "Sending: " + command);

                try
                {
                    pipeWriter.WriteLine(command);
                    string response = pipeReader.ReadLine();

                    LogMessage("SendPipeCommand", "Received: " + response);

                    if (response == null)
                    {
                        throw new DriverException("Mediator app pipe connection was closed unexpectedly.");
                    }

                    // Check for error responses from the mediator
                    if (response.StartsWith("ERROR:"))
                    {
                        string errorMsg = response.Substring(6);
                        if (errorMsg == "NOT_CONNECTED")
                        {
                            throw new NotConnectedException(
                                "The DeFocuser Lite Mediator is not connected to the hardware. " +
                                "Please open the DeFocuser Lite app, select a COM port, and click Connect.");
                        }
                        throw new DriverException("Mediator error: " + errorMsg);
                    }

                    return response;
                }
                catch (IOException ex)
                {
                    connectedState = false;
                    throw new NotConnectedException("Lost connection to the DeFocuser Lite Mediator: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Parse a response from the pipe, stripping the expected prefix.
        /// </summary>
        private string ParseResponse(string response, string expectedPrefix)
        {
            if (!response.StartsWith(expectedPrefix))
            {
                LogMessage("ParseResponse", "Invalid response: " + response + " (expected prefix: " + expectedPrefix + ")");
                throw new DriverException("Invalid response from device: " + response);
            }
            return response.Substring(expectedPrefix.Length);
        }

        /// <summary>
        /// Clean up pipe resources
        /// </summary>
        private void CleanupPipe()
        {
            try { pipeWriter?.Dispose(); } catch { }
            try { pipeReader?.Dispose(); } catch { }
            try { pipeClient?.Dispose(); } catch { }
            pipeWriter = null;
            pipeReader = null;
            pipeClient = null;
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        internal void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            tl.LogMessage(identifier, msg);
        }

        #endregion
    }
}
