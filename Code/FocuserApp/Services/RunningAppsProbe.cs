/*
 * RunningAppsProbe.cs
 * Asks the Restart Manager which processes currently hold our installed files.
 *
 * The hub runs the installer with /SILENT, and in silent mode Inno Setup closes
 * those processes itself without showing the "applications must be closed" page
 * it would display interactively. An ASCOM client with the driver DLL loaded --
 * N.I.N.A., SGP -- therefore used to vanish mid-session with no warning. Inno
 * uses the Restart Manager for that, so querying it here yields the same list
 * and lets us ask first.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ASCOM.DeKoi.DeFocuserApp.Services
{
    public static class RunningAppsProbe
    {
        private const int RmRebootReasonNone = 0;
        private const int CCH_RM_MAX_APP_NAME = 255;
        private const int CCH_RM_MAX_SVC_NAME = 63;
        private const int CCH_RM_SESSION_KEY = 32;
        private const int ERROR_MORE_DATA = 234;

        [StructLayout(LayoutKind.Sequential)]
        private struct RM_UNIQUE_PROCESS
        {
            public int dwProcessId;
            public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RM_PROCESS_INFO
        {
            public RM_UNIQUE_PROCESS Process;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
            public string strAppName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
            public string strServiceShortName;
            public int ApplicationType;
            public uint AppStatus;
            public uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bRestartable;
        }

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(
            uint pSessionHandle,
            uint nFiles, string[] rgsFilenames,
            uint nApplications, RM_UNIQUE_PROCESS[] rgApplications,
            uint nServices, string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(
            uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
            ref uint lpdwRebootReasons);

        /// <summary>
        /// Files the installer overwrites: the hub executable and the ASCOM
        /// driver DLL that sits beside it. Anything holding these gets closed.
        /// </summary>
        private static string[] InstalledFiles()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "ASCOM.DeKoi.DeFocuserApp.exe"),
                Path.Combine(baseDir, "ASCOM.DeKoi.DeFocuserLite.dll"),
            };
            return candidates.Where(File.Exists).ToArray();
        }

        /// <summary>
        /// Display names of other processes holding our installed files, most
        /// notably ASCOM clients with the driver loaded. Excludes this process:
        /// the hub shuts itself down as part of the update anyway. Returns an
        /// empty list on any failure — a warning we can't build must not block
        /// the update.
        /// </summary>
        public static IReadOnlyList<string> ProcessesHoldingInstalledFiles()
        {
            var results = new List<string>();

            string[] files = InstalledFiles();
            if (files.Length == 0) return results;

            var key = new StringBuilder(CCH_RM_SESSION_KEY + 1);
            if (RmStartSession(out uint session, 0, key) != 0) return results;

            try
            {
                if (RmRegisterResources(session, (uint)files.Length, files, 0, null, 0, null) != 0)
                    return results;

                uint arrayCount = 0;
                uint rebootReasons = RmRebootReasonNone;

                // First call sizes the array, second fills it.
                int rc = RmGetList(session, out uint needed, ref arrayCount, null, ref rebootReasons);
                if (rc != ERROR_MORE_DATA || needed == 0) return results;

                arrayCount = needed;
                var infos = new RM_PROCESS_INFO[arrayCount];
                if (RmGetList(session, out needed, ref arrayCount, infos, ref rebootReasons) != 0)
                    return results;

                int selfId = Process.GetCurrentProcess().Id;

                for (int i = 0; i < arrayCount; i++)
                {
                    int pid = infos[i].Process.dwProcessId;
                    if (pid == selfId) continue;

                    string name = infos[i].strAppName;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        try { name = Process.GetProcessById(pid).ProcessName; }
                        catch { continue; } // already gone
                    }

                    results.Add(name + " (PID " + pid + ")");
                }
            }
            catch
            {
                return new List<string>();
            }
            finally
            {
                RmEndSession(session);
            }

            return results.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
