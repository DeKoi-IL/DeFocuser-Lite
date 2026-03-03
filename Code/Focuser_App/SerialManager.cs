/*
 * SerialManager.cs
 * Copyright (C) 2025 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 *
 * Manages the serial connection to the DeFocuser Lite hardware (ESP32).
 * Extracted from FocuserDriver.cs to serve as the central serial owner
 * in the mediator architecture.
 */

using ASCOM.Utilities;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ASCOM.DeKoi.DeFocuserMediator
{
    internal class SerialManager : IDisposable
    {
        // Constants used to communicate with the device.
        // Make sure those values are identical to those in the Arduino Firmware.
        private const string SEPARATOR = "\n";

        private const string DEVICE_GUID = "dfafe960-d19c-4abd-af4a-4dc5f49775a3";

        private const string OK = "OK";
        private const string NOK = "NOK";

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

        private const string COMMAND_FOCUSER_SETLIMIT = "COMMAND:FOCUSER:SETLIMIT";
        private const string RESULT_FOCUSER_SETLIMIT = "RESULT:FOCUSER:SETLIMIT:";

        /// <summary>
        /// Object used to synchronize serial communication in a multi-threaded environment.
        /// </summary>
        private readonly object lockObject = new object();

        /// <summary>
        /// The ASCOM Serial object used to communicate with the device.
        /// </summary>
        private Serial objSerial;

        /// <summary>
        /// Whether the serial connection is currently established.
        /// </summary>
        private bool isConnected;

        /// <summary>
        /// Public property to check connection state.
        /// </summary>
        public bool IsConnected => isConnected;

        /// <summary>
        /// The COM port name we are currently connected to.
        /// </summary>
        public string ConnectedPortName => objSerial?.PortName;

        /// <summary>
        /// Event fired when connection state changes.
        /// </summary>
        public event Action<bool> ConnectionStateChanged;

        /// <summary>
        /// Connect to the focuser hardware on the specified COM port.
        /// </summary>
        /// <param name="comPort">COM port name, or null/empty for auto-detect.</param>
        /// <param name="autoDetect">If true, scan all COM ports to find the device.</param>
        public void Connect(string comPort, bool autoDetect)
        {
            if (isConnected)
                return;

            Serial serial = null;
            var comPorts = new List<string>(System.IO.Ports.SerialPort.GetPortNames());

            if (autoDetect)
            {
                foreach (string comPortName in comPorts)
                {
                    serial = ConnectToDevice(comPortName);
                    if (serial != null)
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(comPort) && comPorts.Contains(comPort))
            {
                serial = ConnectToDevice(comPort);
            }
            else
            {
                throw new ASCOM.InvalidValueException("Invalid COM port", comPort ?? "(null)",
                    string.Join(", ", System.IO.Ports.SerialPort.GetPortNames()));
            }

            if (serial != null)
            {
                objSerial = serial;
                isConnected = true;
                ConnectionStateChanged?.Invoke(true);
            }
            else
            {
                throw new ASCOM.NotConnectedException("Failed to connect to the DeFocuser Lite hardware.");
            }
        }

        /// <summary>
        /// Disconnect from the focuser hardware.
        /// </summary>
        public void Disconnect()
        {
            if (!isConnected)
                return;

            isConnected = false;

            if (objSerial != null)
            {
                objSerial.Connected = false;
                objSerial.Dispose();
                objSerial = null;
            }

            // Wait for the serial connection to be fully closed.
            System.Threading.Thread.Sleep(1000);

            ConnectionStateChanged?.Invoke(false);
        }

        /// <summary>
        /// Attempts to connect to the specified COM port.
        /// Returns a Serial object if successful, null otherwise.
        /// </summary>
        private Serial ConnectToDevice(string comPortName)
        {
            if (!System.IO.Ports.SerialPort.GetPortNames().Contains(comPortName))
                return null;

            Serial serial;

            try
            {
                serial = new Serial
                {
                    Speed = SerialSpeed.ps57600,
                    PortName = comPortName,
                    Connected = true,
                    ReceiveTimeout = 1
                };
            }
            catch (Exception)
            {
                return null;
            }

            // Wait for the serial connection to establish.
            System.Threading.Thread.Sleep(1000);

            serial.ClearBuffers();

            // Poll the device until successful, or until we've reached the retry limit.
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
                        // PortInUse or Timeout exceptions may happen here.
                    }
                }

                if (response == RESULT_PING + DEVICE_GUID)
                {
                    serial.ReceiveTimeout = 5;
                    return serial;
                }
            }

            serial.Connected = false;
            serial.Dispose();

            return null;
        }

        /// <summary>
        /// Send a raw command string to the device and return the full response string.
        /// This is the primary method used by the PipeServer to forward commands.
        /// </summary>
        /// <param name="command">The full command string (e.g. "COMMAND:FOCUSER:GETPOSITION")</param>
        /// <returns>The full response string from the device.</returns>
        public string SendRawCommand(string command)
        {
            if (!isConnected)
                throw new ASCOM.NotConnectedException("Not connected to hardware.");

            string response;

            lock (lockObject)
            {
                objSerial.Transmit(command + SEPARATOR);

                try
                {
                    response = objSerial.ReceiveTerminated(SEPARATOR).Trim();
                }
                catch (Exception e)
                {
                    throw new ASCOM.DriverException("Serial communication error: " + e.Message);
                }
            }

            return response;
        }

        /// <summary>
        /// Send a command to the device and parse the response by stripping the expected prefix.
        /// </summary>
        private string SendCommandToDevice(string command, string resultPrefix)
        {
            string response = SendRawCommand(command);

            if (!response.StartsWith(resultPrefix))
            {
                throw new ASCOM.DriverException("Invalid response from device: " + response);
            }

            return response.Substring(resultPrefix.Length);
        }

        // ---- Convenience Methods ----

        public int GetPosition()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_GETPOSITION, RESULT_FOCUSER_POSITION);
            return int.Parse(response);
        }

        public int GetMaxPosition()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_GETMAXPOSITION, RESULT_FOCUSER_MAXPOSITION);
            return int.Parse(response);
        }

        public bool GetIsMoving()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_ISMOVING, RESULT_FOCUSER_ISMOVING);
            return response == TRUE;
        }

        public void Move(int position)
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_MOVE + position.ToString(), RESULT_FOCUSER_MOVE);
            if (response != OK)
                throw new ASCOM.DriverException("Move command failed.");
        }

        public void Halt()
        {
            SendCommandToDevice(COMMAND_FOCUSER_HALT, RESULT_FOCUSER_HALT);
        }

        public void SetZeroPosition()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETZEROPOSITION, RESULT_FOCUSER_SETZEROPOSITION);
            if (response != OK)
                throw new ASCOM.DriverException("SetZeroPosition command failed.");
        }

        public void SetPosition(int position)
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETPOSITION + position.ToString(), RESULT_FOCUSER_SETPOSITION);
            if (response != OK)
                throw new ASCOM.DriverException("SetPosition command failed.");
        }

        public void SetReverse(bool reverse)
        {
            string val = reverse ? TRUE : FALSE;
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETREVERSE + val, RESULT_FOCUSER_SETREVERSE);
            if (response != OK)
                throw new ASCOM.DriverException("SetReverse command failed.");
        }

        public bool GetIsReverse()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_ISREVERSE, RESULT_FOCUSER_ISREVERSE);
            return response == TRUE;
        }

        public void Calibrate()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_CALIBRATE, RESULT_FOCUSER_CALIBRATE);
            if (response != OK)
                throw new ASCOM.DriverException("Calibrate command failed.");
        }

        public bool GetIsCalibrating()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_ISCALIBRATING, RESULT_FOCUSER_ISCALIBRATING);
            return response == TRUE;
        }

        public void SetLimit()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETLIMIT, RESULT_FOCUSER_SETLIMIT);
            if (response != OK)
                throw new ASCOM.DriverException("SetLimit command failed.");
        }

        public void Dispose()
        {
            if (isConnected)
            {
                Disconnect();
            }
        }
    }
}
