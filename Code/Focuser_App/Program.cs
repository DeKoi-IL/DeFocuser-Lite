/*
 * Program.cs
 * Copyright (C) 2022 - Present, Michael Levgold (DeKoi) - All Rights Reserved
 * Based on the original work of Julien Lecomte with his OAG Focuser.
 * Licensed under the MIT License. See the accompanying LICENSE file for terms.
 */

using System;
using System.Windows.Forms;

namespace ASCOM.DeKoi.DeFocuserStandalone
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
