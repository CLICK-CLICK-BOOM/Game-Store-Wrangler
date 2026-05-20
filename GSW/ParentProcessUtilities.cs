//<summary>[DO NOT REMOVE]ParentProcessUtiilities.cs</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
namespace GSWEngine
{
    public static class ParentProcessUtilities
    {
        private static Dictionary<int, int> _lineageCache = new Dictionary<int, int>();

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, ref PROCESS_BASIC_INFORMATION processInformation, int processInformationLength, out int returnLength);

        public static void UpdateCache(int child, int parent)
        {
            if (child > 0 && parent > 0)
            {
                if (_lineageCache.Count > 500) _lineageCache.Clear();
                _lineageCache[child] = parent;
            }
        }

        /// <summary>
        /// Climbs the process tree starting from the GameSignal to find an attributing StoreTracker.
        /// Uses WMI to identify parent processes and prioritizes Vassal stores (EA, Ubi) over Hosts (Xbox).
        /// </summary>
        public static StoreTracker GetParentStore(GameSignal signal, List<StoreTracker> trackers)
        {
            if (signal == null || trackers == null) return null;

            int currentPid = signal.Pid;
            var treeMatches = new List<StoreTracker>();

            // 1. THE TREE CLIMB: Physical Parentage (Highest Truth)
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    int parentPid = _lineageCache.ContainsKey(currentPid) ? _lineageCache[currentPid] : GetParentPid(currentPid);
                    if (parentPid <= 0) break;

                    string parentName = GetProcessName(parentPid);

                    // Match Check: Is this parent one of our known Store Launchers?
                    var match = trackers.FirstOrDefault(t =>
                        (t.LauncherPids != null && t.LauncherPids.Contains(parentPid)) ||
                        t.ProcessName.Equals(parentName, StringComparison.OrdinalIgnoreCase) ||
                        IsHelperForStore(parentName, t.DisplayName));

                    if (match != null)
                    {
                        return match;
                    }

                    // Exit Condition: Stop if we hit the OS root
                    if (IsSystemProcess(parentName)) break;

                    currentPid = parentPid;
                }
                catch { break; }
            }

            return null;
        }

        public static int GetParentPid(int pid)
        {
            if (_lineageCache.ContainsKey(pid)) return _lineageCache[pid];

            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                    int status = NtQueryInformationProcess(p.Handle, 0, ref pbi, Marshal.SizeOf(pbi), out int returnLength);
                    if (status == 0)
                    {
                        int parentId = pbi.InheritedFromUniqueProcessId.ToInt32();
                        _lineageCache[pid] = parentId;
                        return parentId;
                    }
                }
            }
            catch { }

            return -1;
        }

        private static string GetProcessName(int pid)
        {
            try { using (var p = Process.GetProcessById(pid)) return p.ProcessName; } catch { return ""; }
        }

        private static bool IsSystemProcess(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            return n == "explorer" || n == "services" || n == "system" || n == "idle" || n == "wininit" || n == "svchost" || n == "csrss" || n == "lsass" || n == "winlogon";
        }

        private static bool IsHelperForStore(string processName, string storeName)
        {
            if (string.IsNullOrEmpty(processName)) return false;
            string n = processName.ToLowerInvariant();

            return storeName switch
            {
                "Xbox" => n == "gamelaunchhelper" || n == "gamingservices" || n == "gamingservicesnet",
                "Ubisoft" => n == "uplaywebcore" || n == "upc" || n == "ubisoftconnect",
                "Steam" => n == "steamwebhelper" || n == "steam",
                "EA Desktop" => n == "eabackgroundservice" || n == "eadesktop",
                "Battle.net" => n == "agent",
                "Epic" => n == "epicgameslauncher",
                _ => false
            };
        }

        private static string GetProcessPath(int pid)
        {
            try { using (var p = Process.GetProcessById(pid)) return p.MainModule?.FileName ?? ""; } catch { return ""; }
        }
    }
}