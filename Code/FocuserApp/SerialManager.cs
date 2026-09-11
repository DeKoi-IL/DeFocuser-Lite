/*
 * SerialManager.cs
 * Copyright (C) 2025 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 *
 * Manages the serial connection to the DeFocuser Lite hardware (ESP32).
 * Copy of the WinForms mediator's SerialManager, with an added SerialTraffic
 * event so the WPF Console card can show every command that flows over the wire.
 */

using ASCOM.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

namespace ASCOM.DeKoi.DeFocuserApp
{
    public enum SerialDirection { Tx, Rx }

    public class SerialTrafficEventArgs : EventArgs
    {
        public SerialDirection Direction { get; }
        public string Payload { get; }
        public SerialTrafficEventArgs(SerialDirection dir, string payload)
        {
            Direction = dir;
            Payload = payload;
        }
    }

    internal class SerialManager : IDisposable
    {
        private const string SEPARATOR = "\n";

        private const string DEVICE_GUID = "dfafe960-d19c-4abd-af4a-4dc5f49775a3";

        private const string OK = "OK";
        private const string NOK = "NOK";

        private const string TRUE = "TRUE";
        private const string FALSE = "FALSE";

        private const string COMMAND_PING = "COMMAND:PING";
        private const string RESULT_PING = "RESULT:PING:" + OK + ":";

        private const string COMMAND_INFO = "COMMAND:INFO";
        private const string RESULT_INFO_PREFIX = "RESULT:INFO:";

        private const string COMMAND_FOCUSER_GETBOARD = "COMMAND:FOCUSER:GETBOARD";
        private const string RESULT_FOCUSER_BOARD = "RESULT:FOCUSER:BOARD:";

        private const string COMMAND_FOCUSER_GETPOSITION = "COMMAND:FOCUSER:GETPOSITION";
        private const string RESULT_FOCUSER_POSITION = "RESULT:FOCUSER:POSITION:";

        private const string COMMAND_FOCUSER_GETMAXPOSITION = "COMMAND:FOCUSER:GETMAXPOSITION";
        private const string RESULT_FOCUSER_MAXPOSITION = "RESULT:FOCUSER:MAXPOSITION:";

        private const string COMMAND_FOCUSER_SETMAXPOSITION = "COMMAND:FOCUSER:SETMAXPOSITION:";
        private const string RESULT_FOCUSER_SETMAXPOSITION = "RESULT:FOCUSER:SETMAXPOSITION:";

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

        private const string COMMAND_FOCUSER_SETSPEED = "COMMAND:FOCUSER:SETSPEED:";
        private const string RESULT_FOCUSER_SETSPEED = "RESULT:FOCUSER:SETSPEED:";

        private const string COMMAND_FOCUSER_GETSPEED = "COMMAND:FOCUSER:GETSPEED";
        private const string RESULT_FOCUSER_GETSPEED = "RESULT:FOCUSER:GETSPEED:";

        private const string COMMAND_FOCUSER_SETSTALLTHRESHOLD = "COMMAND:FOCUSER:SETSTALLTHRESHOLD:";
        private const string RESULT_FOCUSER_SETSTALLTHRESHOLD = "RESULT:FOCUSER:SETSTALLTHRESHOLD:";

        private const string COMMAND_FOCUSER_GETSTALLTHRESHOLD = "COMMAND:FOCUSER:GETSTALLTHRESHOLD";
        private const string RESULT_FOCUSER_GETSTALLTHRESHOLD = "RESULT:FOCUSER:GETSTALLTHRESHOLD:";

        private const string COMMAND_FOCUSER_SETSTALLCOUNT = "COMMAND:FOCUSER:SETSTALLCOUNT:";
        private const string RESULT_FOCUSER_SETSTALLCOUNT = "RESULT:FOCUSER:SETSTALLCOUNT:";
        private const string COMMAND_FOCUSER_GETSTALLCOUNT = "COMMAND:FOCUSER:GETSTALLCOUNT";
        private const string RESULT_FOCUSER_GETSTALLCOUNT = "RESULT:FOCUSER:GETSTALLCOUNT:";

        private const string COMMAND_FOCUSER_SETSTALLWINDOW = "COMMAND:FOCUSER:SETSTALLWINDOW:";
        private const string RESULT_FOCUSER_SETSTALLWINDOW = "RESULT:FOCUSER:SETSTALLWINDOW:";
        private const string COMMAND_FOCUSER_GETSTALLWINDOW = "COMMAND:FOCUSER:GETSTALLWINDOW";
        private const string RESULT_FOCUSER_GETSTALLWINDOW = "RESULT:FOCUSER:GETSTALLWINDOW:";

        private const string COMMAND_FOCUSER_SETSTALLGRACE = "COMMAND:FOCUSER:SETSTALLGRACE:";
        private const string RESULT_FOCUSER_SETSTALLGRACE = "RESULT:FOCUSER:SETSTALLGRACE:";
        private const string COMMAND_FOCUSER_GETSTALLGRACE = "COMMAND:FOCUSER:GETSTALLGRACE";
        private const string RESULT_FOCUSER_GETSTALLGRACE = "RESULT:FOCUSER:GETSTALLGRACE:";

        private const string COMMAND_FOCUSER_SETSTALLENABLED = "COMMAND:FOCUSER:SETSTALLENABLED:";
        private const string RESULT_FOCUSER_SETSTALLENABLED = "RESULT:FOCUSER:SETSTALLENABLED:";
        private const string COMMAND_FOCUSER_GETSTALLENABLED = "COMMAND:FOCUSER:GETSTALLENABLED";
        private const string RESULT_FOCUSER_GETSTALLENABLED = "RESULT:FOCUSER:GETSTALLENABLED:";

        public const int StallThresholdMin = 128;
        public const int StallThresholdMax = 255;
        public const int StallThresholdDefault = 211;

        public const int StallCountMin = 1;
        public const int StallCountMax = 20;
        public const int StallCountDefault = 2;

        public const int StallWindowMin = 50;
        public const int StallWindowMax = 5000;
        public const int StallWindowDefault = 300;

        public const int StallGraceMin = 0;
        public const int StallGraceMax = 10000;
        public const int StallGraceDefault = 1000;

        public const bool StallEnabledDefault = true;

        private readonly object lockObject = new object();

        private Serial objSerial;
        private bool isConnected;

        public bool IsConnected => isConnected;

        public string ConnectedPortName => objSerial?.PortName;

        public event Action<bool> ConnectionStateChanged;

        public event EventHandler<SerialTrafficEventArgs> SerialTraffic;

        private void RaiseTraffic(SerialDirection dir, string payload)
        {
            try
            {
                SerialTraffic?.Invoke(this, new SerialTrafficEventArgs(dir, payload));
            }
            catch
            {
                // Never let a logger crash the serial path.
            }
        }

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

            System.Threading.Thread.Sleep(1000);

            ConnectionStateChanged?.Invoke(false);
        }

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

            System.Threading.Thread.Sleep(1000);

            serial.ClearBuffers();

            for (int retries = 3; retries >= 0; retries--)
            {
                string response = "";

                lock (lockObject)
                {
                    try
                    {
                        serial.Transmit(COMMAND_PING + SEPARATOR);
                        RaiseTraffic(SerialDirection.Tx, COMMAND_PING);
                        response = serial.ReceiveTerminated(SEPARATOR).Trim();
                        RaiseTraffic(SerialDirection.Rx, response);
                    }
                    catch (Exception)
                    {
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

        public string SendRawCommand(string command)
        {
            if (!isConnected)
                throw new ASCOM.NotConnectedException("Not connected to hardware.");

            string response;

            lock (lockObject)
            {
                objSerial.Transmit(command + SEPARATOR);
                RaiseTraffic(SerialDirection.Tx, command);

                try
                {
                    response = objSerial.ReceiveTerminated(SEPARATOR).Trim();
                    RaiseTraffic(SerialDirection.Rx, response);
                }
                catch (Exception e)
                {
                    throw new ASCOM.DriverException("Serial communication error: " + e.Message);
                }
            }

            return response;
        }

        private string SendCommandToDevice(string command, string resultPrefix)
        {
            string response = SendRawCommand(command);

            if (!response.StartsWith(resultPrefix))
            {
                throw new ASCOM.DriverException("Invalid response from device: " + response);
            }

            return response.Substring(resultPrefix.Length);
        }

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

        public void SetMaxPosition(int maxPosition)
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETMAXPOSITION + maxPosition.ToString(), RESULT_FOCUSER_SETMAXPOSITION);
            if (response != OK)
                throw new ASCOM.DriverException("SetMaxPosition command failed.");
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

        public void SetSpeed(string speed)
        {
            // Firmware accepts "FAST" / "NORMAL" / "SLOW".
            string upper = (speed ?? "FAST").ToUpperInvariant();
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETSPEED + upper, RESULT_FOCUSER_SETSPEED);
            if (response != OK)
                throw new ASCOM.DriverException("SetSpeed command failed (response: " + response + ").");
        }

        public string GetSpeed()
        {
            return SendCommandToDevice(COMMAND_FOCUSER_GETSPEED, RESULT_FOCUSER_GETSPEED);
        }

        public void SetStallThreshold(int value)
        {
            if (value < StallThresholdMin) value = StallThresholdMin;
            if (value > StallThresholdMax) value = StallThresholdMax;
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETSTALLTHRESHOLD + value.ToString(),
                                                  RESULT_FOCUSER_SETSTALLTHRESHOLD);
            if (response != OK)
                throw new ASCOM.DriverException("SetStallThreshold command failed (response: " + response + ").");
        }

        public int GetStallThreshold()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_GETSTALLTHRESHOLD, RESULT_FOCUSER_GETSTALLTHRESHOLD);
            return int.Parse(response);
        }

        public void SetStallCount(int value)
        {
            if (value < StallCountMin) value = StallCountMin;
            if (value > StallCountMax) value = StallCountMax;
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETSTALLCOUNT + value.ToString(),
                                                  RESULT_FOCUSER_SETSTALLCOUNT);
            if (response != OK)
                throw new ASCOM.DriverException("SetStallCount command failed (response: " + response + ").");
        }

        public int GetStallCount()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_GETSTALLCOUNT, RESULT_FOCUSER_GETSTALLCOUNT);
            return int.Parse(response);
        }

        public void SetStallWindow(int valueMs)
        {
            if (valueMs < StallWindowMin) valueMs = StallWindowMin;
            if (valueMs > StallWindowMax) valueMs = StallWindowMax;
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETSTALLWINDOW + valueMs.ToString(),
                                                  RESULT_FOCUSER_SETSTALLWINDOW);
            if (response != OK)
                throw new ASCOM.DriverException("SetStallWindow command failed (response: " + response + ").");
        }

        public int GetStallWindow()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_GETSTALLWINDOW, RESULT_FOCUSER_GETSTALLWINDOW);
            return int.Parse(response);
        }

        public void SetStallGrace(int valueMs)
        {
            if (valueMs < StallGraceMin) valueMs = StallGraceMin;
            if (valueMs > StallGraceMax) valueMs = StallGraceMax;
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETSTALLGRACE + valueMs.ToString(),
                                                  RESULT_FOCUSER_SETSTALLGRACE);
            if (response != OK)
                throw new ASCOM.DriverException("SetStallGrace command failed (response: " + response + ").");
        }

        public int GetStallGrace()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_GETSTALLGRACE, RESULT_FOCUSER_GETSTALLGRACE);
            return int.Parse(response);
        }

        public void SetStallEnabled(bool enabled)
        {
            string val = enabled ? TRUE : FALSE;
            string response = SendCommandToDevice(COMMAND_FOCUSER_SETSTALLENABLED + val, RESULT_FOCUSER_SETSTALLENABLED);
            if (response != OK)
                throw new ASCOM.DriverException("SetStallEnabled command failed (response: " + response + ").");
        }

        public bool GetStallEnabled()
        {
            string response = SendCommandToDevice(COMMAND_FOCUSER_GETSTALLENABLED, RESULT_FOCUSER_GETSTALLENABLED);
            return response == TRUE;
        }

        // Board revision the running firmware was built for ("esp32c3-old", ...).
        // Firmware older than 2.4.0 doesn't know this command and answers
        // ERROR:INVALID_COMMAND, which surfaces here as a DriverException —
        // callers treat that as "unknown board".
        public string GetBoard()
        {
            return SendCommandToDevice(COMMAND_FOCUSER_GETBOARD, RESULT_FOCUSER_BOARD);
        }

        // Returns the firmware identification string (e.g. "DeKoi's DeFocuser Lite Firmware v1.0").
        // Used by the update flow to compare against the released firmware version.
        public string GetFirmwareInfo()
        {
            return SendCommandToDevice(COMMAND_INFO, RESULT_INFO_PREFIX);
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
