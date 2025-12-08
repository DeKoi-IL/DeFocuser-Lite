/*
 * FocuserDriver.cs
 * Copyright (C) 2025 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Based on the original work of Julien Lecomte with his OAG Focuser.
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using ASCOM.DeviceInterface;
using ASCOM.Utilities;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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
        internal static string autoDetectComPortProfileName = "Auto-Detect COM Port";
        internal static string autoDetectComPortDefault = "false";

        internal static string comPortProfileName = "COM Port";
        internal static string comPortDefault = "COM1";

        internal static string lastComPortProfileName = "Last COM Port";
        internal static string lastComPortDefault = string.Empty;

        internal static string traceStateProfileName = "Trace Level";
        internal static string traceStateDefault = "false";

        // Variables to hold the current device configuration
        internal static bool autoDetectComPort = Convert.ToBoolean(autoDetectComPortDefault);
        internal static string comPortOverride = comPortDefault;

        /// <summary>
        /// Variable to hold the trace logger object (creates a diagnostic log file with information that you specify)
        /// </summary>
        internal TraceLogger tl;

        /// <summary>
        /// Private variable to hold the connected state
        /// </summary>
        private bool connectedState;

        /// <summary>
        // Object used to synchronize the serial communication with the device in a multi-threaded environment.
        /// </summary>
        private readonly object lockObject = new object();

        /// <summary>
        // The object used to communicate with the device using serial port communication.
        /// </summary>
        private Serial objSerial;

        // Constants used to communicate with the device
        // Make sure those values are identical to those in the Arduino Firmware.
        // (I could not come up with an easy way to share them across the two projects)
        private const string SEPARATOR = "\n";

        private const string DEVICE_GUID = "dfafe960-d19c-4abd-af4a-4dc5f49775a3";

        private const string OK = "OK";

        private const string TRUE = "TRUE";
        private const string FALSE = "FALSE";

        private const string COMMAND_PING = "COMMAND:PING";
        private const string RESULT_PING = "RESULT:PING:" + OK + ":";

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

        /// <summary>
        /// Initializes a new instance of the <see cref="DeKoi"/> class.
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
        /// If the user clicks the OK button to dismiss the form, then
        /// the new settings are saved, otherwise the old values are reloaded.
        /// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
        /// </summary>
        public void SetupDialog()
        {
            // consider only showing the setup dialog if not connected
            // or call a different dialog if connected
            if (IsConnected)
            {
                MessageBox.Show("Already connected, just press OK");
            }

            using (FocuserSetupDialogForm F = new FocuserSetupDialogForm(this))
            {
                var result = F.ShowDialog();
                if (result == DialogResult.OK)
                {
                    WriteProfile(); // Persist device configuration values to the ASCOM Profile store
                }
            }
        }

        public ArrayList SupportedActions
        {
            get
            {
                LogMessage("SupportedActions Get", "Returning [\"SetZeroPosition\"]");
                return new ArrayList()
                {
                    "SetZeroPosition"
                };
            }
        }

        public string Action(string actionName, string actionParameters)
        {
            string response;
            switch (actionName.ToUpper())
            {
                case "SETZEROPOSITION":
                    response = SendCommandToDevice("SetZeroPosition", COMMAND_FOCUSER_SETZEROPOSITION, RESULT_FOCUSER_SETZEROPOSITION);
                    if (response != OK)
                    {
                        LogMessage("SetZeroPosition", "Device responded with an error");
                        throw new DriverException("Device responded with an error");
                    }
                    return string.Empty;
                case "SETPOSITION":
                    int parameter = int.Parse(actionParameters);
                    response = SendCommandToDevice("SetPosition", COMMAND_FOCUSER_SETPOSITION + parameter.ToString(), RESULT_FOCUSER_SETPOSITION);
                    if (response != OK)
                    {
                        LogMessage("SetPosition", $"Device responded with an error for parameter {parameter} with response {response}");
                        throw new DriverException($"Device responded with an error for parameter {parameter} with response {response}");
                    }

                    return string.Empty;

                case "SETREVERSE":
                    bool setReverseParameter = bool.Parse(actionParameters);
                    response = SendCommandToDevice("SetReverse", COMMAND_FOCUSER_SETREVERSE + (setReverseParameter ? TRUE : FALSE), RESULT_FOCUSER_SETREVERSE);
                    if (response != OK)
                    {
                        LogMessage("SetPosition", $"Device responded with an error for parameter {setReverseParameter} with response {response}");
                        throw new DriverException($"Device responded with an error for parameter {setReverseParameter} with response {response}");
                    }

                    return string.Empty;

                case "ISREVERSE":
                    response = SendCommandToDevice("IsReverse", COMMAND_FOCUSER_ISREVERSE, RESULT_FOCUSER_ISREVERSE);
                    if (response != TRUE && response != FALSE)
                    {
                        LogMessage("IsReverse", $"Device responded with an error: {response}");
                        throw new DriverException($"Device responded with an error: {response}");
                    }
                    return response;

                case "CALIBRATE":
                    response = SendCommandToDevice("Calibrate", COMMAND_FOCUSER_CALIBRATE, RESULT_FOCUSER_CALIBRATE);
                    if (response != OK)
                    {
                        LogMessage("Calibrate", "Device responded with an error");
                        throw new DriverException("Device responded with an error");
                    }
                    return response;

                case "ISCALIBRATING":
                    response = SendCommandToDevice("IsCalibrating", COMMAND_FOCUSER_ISCALIBRATING, RESULT_FOCUSER_ISCALIBRATING);
                    if (response != TRUE && response != FALSE)
                    {
                        LogMessage("IsCalibrating", $"Device responded with an error: {response}");
                        throw new DriverException($"Device responded with an error: {response}");
                    }
                    return response;

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
                    LogMessage("Connected Set", "Connecting");

                    Debug.Assert(objSerial == null);

                    using (Profile driverProfile = new Profile() { DeviceType = "Focuser" })
                    {
                        Serial serial = null;

                        var comPorts = new List<string>(System.IO.Ports.SerialPort.GetPortNames());

                        if (autoDetectComPort)
                        {
                            // See if the last successfully connected COM port can be used first...
                            // This is a performance optimization that significantly reduces the time it takes to connect!
                            string lastComPort = driverProfile.GetValue(driverID, lastComPortProfileName, string.Empty, string.Empty);
                            if (!string.IsNullOrEmpty(lastComPort))
                            {
                                var i = comPorts.IndexOf(lastComPort);
                                if (i >= 0)
                                {
                                    // Move the last successfully connected COM port to the top of the list of available COM ports
                                    // (if it was found in that list to begin with...) so that we try that first.
                                    comPorts.RemoveAt(i);
                                    comPorts.Insert(0, lastComPort);
                                }
                            }

                            foreach (string comPortName in comPorts)
                            {
                                serial = ConnectToDevice(comPortName);
                                if (serial != null)
                                {
                                    break;
                                }
                            }
                        }
                        else if (comPorts.Contains(comPortOverride))
                        {
                            serial = ConnectToDevice(comPortOverride);
                        }
                        else
                        {
                            throw new InvalidValueException("Invalid COM port", comPortOverride, String.Join(", ", System.IO.Ports.SerialPort.GetPortNames()));
                        }

                        if (serial != null)
                        {
                            objSerial = serial;

                            // Persist the COM port name so that we try that first the next time
                            // we attempt to connect (see code above in this method)
                            driverProfile.WriteValue(driverID, lastComPortProfileName, serial.PortName);

                            LogMessage("Connected Set", "Connected to port {0}", serial.PortName);

                            connectedState = true;
                        }
                        else
                        {
                            throw new NotConnectedException("Failed to connect");
                        }
                    }
                }
                else
                {
                    connectedState = false;

                    LogMessage("Connected Set", "Disconnecting");

                    objSerial.Connected = false;
                    objSerial.Dispose();
                    objSerial = null;

                    // Wait for the serial connection to be fully closed...
                    // See https://stackoverflow.com/questions/6434297/why-thread-sleep-before-serialport-open-and-close
                    // TODO: Is there a better way?
                    System.Threading.Thread.Sleep(1000);
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
            SendCommandToDevice("Halt", COMMAND_FOCUSER_HALT, RESULT_FOCUSER_HALT);
            // Ignore whether the firmware responded with OK or NOK.
            // If the firmware responded with NOK, it's likely because
            // the focuser was not moving when the command was sent...
        }

        public bool IsMoving
        {
            get
            {
                string response = SendCommandToDevice("IsMoving", COMMAND_FOCUSER_ISMOVING, RESULT_FOCUSER_ISMOVING);
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
                string response = SendCommandToDevice("MaxStep", COMMAND_FOCUSER_GETMAXPOSITION, RESULT_FOCUSER_MAXPOSITION);
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
            string response = SendCommandToDevice("Move", COMMAND_FOCUSER_MOVE + Position.ToString(), RESULT_FOCUSER_MOVE);
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
                string response = SendCommandToDevice("Position", COMMAND_FOCUSER_GETPOSITION, RESULT_FOCUSER_POSITION);
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
        //
        /// <summary>
        /// Register or unregister the driver with the ASCOM Platform.
        /// This is harmless if the driver is already registered/unregistered.
        /// </summary>
        /// <param name="bRegister">If <c>true</c>, registers the driver, otherwise unregisters it.</param>
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

        /// <summary>
        /// This function registers the driver with the ASCOM Chooser and
        /// is called automatically whenever this class is registered for COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is successfully built.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During setup, when the installer registers the assembly for COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually register a driver with ASCOM.
        /// </remarks>
        [ComRegisterFunction]
        public static void RegisterASCOM(Type t)
        {
            RegUnregASCOM(true);
        }

        /// <summary>
        /// This function unregisters the driver from the ASCOM Chooser and
        /// is called automatically whenever this class is unregistered from COM Interop.
        /// </summary>
        /// <param name="t">Type of the class being registered, not used.</param>
        /// <remarks>
        /// This method typically runs in two distinct situations:
        /// <list type="numbered">
        /// <item>
        /// In Visual Studio, when the project is cleaned or prior to rebuilding.
        /// For this to work correctly, the option <c>Register for COM Interop</c>
        /// must be enabled in the project settings.
        /// </item>
        /// <item>During uninstall, when the installer unregisters the assembly from COM Interop.</item>
        /// </list>
        /// This technique should mean that it is never necessary to manually unregister a driver from ASCOM.
        /// </remarks>
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
        /// <param name="message"></param>
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
                autoDetectComPort = Convert.ToBoolean(driverProfile.GetValue(driverID, autoDetectComPortProfileName, string.Empty, autoDetectComPortDefault));
                comPortOverride = driverProfile.GetValue(driverID, comPortProfileName, string.Empty, comPortDefault);
            }
        }

        /// <summary>
        /// Write the device configuration to the  ASCOM  Profile store
        /// </summary>
        internal void WriteProfile()
        {
            using (Profile driverProfile = new Profile())
            {
                driverProfile.DeviceType = "Focuser";
                driverProfile.WriteValue(driverID, traceStateProfileName, tl.Enabled.ToString());
                driverProfile.WriteValue(driverID, autoDetectComPortProfileName, autoDetectComPort.ToString());
                if (comPortOverride != null)
                {
                    driverProfile.WriteValue(driverID, comPortProfileName, comPortOverride.ToString());
                }
            }
        }

        /// <summary>
        /// Attempts to connect to the specified COM port.
        /// Returns a Serial object if successful, null otherwise.
        /// </summary>
        /// <param name="comPortName"></param>
        internal Serial ConnectToDevice(string comPortName)
        {
            if (!System.IO.Ports.SerialPort.GetPortNames().Contains(comPortName))
            {
                throw new InvalidValueException("Invalid COM port", comPortName, String.Join(", ", System.IO.Ports.SerialPort.GetPortNames()));
            }

            Serial serial;

            LogMessage("ConnectToDevice", "Connecting to port {0}", comPortName);

            try
            {
                serial = new Serial
                {
                    Speed = SerialSpeed.ps57600,
                    PortName = comPortName,
                    Connected = true,
                    // Use a short timeout value to make polling fail fast in case this is the wrong port...
                    ReceiveTimeout = 1
                };
            }
            catch (Exception)
            {
                // If trying to connect to a port that is already in use, an exception will be thrown.
                return null;
            }

            // Wait for the serial connection to establish...
            // TODO: Is there a better way?
            System.Threading.Thread.Sleep(1000);

            serial.ClearBuffers();

            // Poll the device (with the short timeout value set above) until successful,
            // or until we've reached the retry count limit of 3...
            for (int retries = 3; retries >= 0; retries--)
            {
                string response = "";

                lock (lockObject)
                {
                    try
                    {
                        serial.Transmit(COMMAND_PING + SEPARATOR);
                        response = serial.ReceiveTerminated(SEPARATOR).Trim();
                    }
                    catch (Exception)
                    {
                        // PortInUse or Timeout exceptions may happen here!
                        // We ignore them.
                    }
                }

                if (response == RESULT_PING + DEVICE_GUID)
                {
                    // Restore default timeout value...
                    serial.ReceiveTimeout = 5;
                    return serial;
                }
            }

            serial.Connected = false;
            serial.Dispose();

            return null;
        }

        /// <summary>
        /// Send a command to the device and returns the response
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="command"></param>
        /// <param name="resultPrefix"></param>
        internal string SendCommandToDevice(string identifier, string command, string resultPrefix)
        {
            CheckConnected(identifier);

            string response;

            lock (lockObject)
            {
                LogMessage(identifier, "Sending command " + command + " to device...");
                objSerial.Transmit(command + SEPARATOR);
                LogMessage(identifier, "Waiting for response from device...");

                try
                {
                    response = objSerial.ReceiveTerminated(SEPARATOR).Trim();
                }
                catch (Exception e)
                {
                    LogMessage(identifier, "Exception: " + e.Message);
                    throw e;
                }
            }

            LogMessage(identifier, "Response from device: " + response);

            if (!response.StartsWith(resultPrefix))
            {
                LogMessage(identifier, "Invalid response from device: " + response);
                throw new DriverException("Invalid response from device: " + response);
            }

            string arg = response.Substring(resultPrefix.Length);
            return arg;
        }

        /// <summary>
        /// Log helper function that takes formatted strings and arguments
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="message"></param>
        /// <param name="args"></param>
        internal void LogMessage(string identifier, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            tl.LogMessage(identifier, msg);
        }

        #endregion
    }
}
