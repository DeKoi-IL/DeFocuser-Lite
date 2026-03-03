/*
 * Program.cs
 * Copyright (C) 2022 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Based on the original work of Julien Lecomte with his OAG Focuser.
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ASCOM.DeKoi.DeFocuserMediator
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// The main entry point for the application.
        /// Uses a named mutex to ensure only one instance runs at a time.
        /// If another instance is already running, bring it to the front.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "DeFocuserLiteMediator_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already running - bring it to front
                    var existingProcess = Process.GetProcessesByName("ASCOM.DeKoi.DeFocuserMediator").FirstOrDefault();
                    if (existingProcess != null && existingProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(existingProcess.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(existingProcess.MainWindowHandle);
                    }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }
}
