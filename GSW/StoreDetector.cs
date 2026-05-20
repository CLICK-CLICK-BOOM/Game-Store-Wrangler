//<summary>[DO NOT REMOVE]StoreDetector.cs</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace GSWEngine
{
    public static class StoreDetector
    {
        // --- THE PRECISION GATE ---
        // Determines which processes belong to which store "Family"
        public static List<int> GetFamilyPids(string displayName, string processName, Dictionary<int, Process> cache)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Primary Configured Name (from master list)
            if (!string.IsNullOrEmpty(processName)) targets.Add(processName);

            // 2. Modern Process Aliases (2026 Fleet)
            // We use the normalized displayName for switching
            switch (displayName)
            {
                case "EA Desktop":
                case "EA":
                    targets.Add("EADesktop");
                    targets.Add("EABackgroundService");
                    targets.Add("EALauncher");
                    break;
                case "Ubisoft":
                case "Ubisoft Connect":
                    targets.Add("UbisoftConnect");
                    targets.Add("upc");
                    targets.Add("UbisoftConnectWebExperience");
                    targets.Add("Uplay");
                    break;
                case "Xbox":
                    targets.Add("XboxPcApp");
                    targets.Add("GamingApp");
                    targets.Add("gamelaunchhelper");
                    targets.Add("GamingServices");
                    targets.Add("GamingServicesNet");
                    targets.Add("XboxPcTray"); // Added tray helper
                    break;
                case "Steam":
                    targets.Add("steam");
                    targets.Add("steamwebhelper");
                    break;
                case "Epic Games":
                case "Epic":
                    targets.Add("EpicGamesLauncher");
                    break;
                case "Battle.net":
                    targets.Add("Battle.net");
                    targets.Add("Agent");
                    break;
                case "GOG Galaxy":
                    targets.Add("GalaxyClient");
                    targets.Add("GalaxyClientService");
                    break;
                case "Riot Games":
                    targets.Add("RiotClientServices");
                    targets.Add("RiotClient");
                    break;
                case "Rockstar":
                    targets.Add("SocialClubHelper");
                    targets.Add("RockstarService");
                    targets.Add("Launcher");
                    break;
                case "Amazon":
                    targets.Add("Amazon Games UI");
                    break;
                case "Itch.io":
                case "itch.io":
                    targets.Add("itch");
                    break;
            }

            var pids = new List<int>();
            foreach (var p in cache.Values)
            {
                try
                {
                    if (targets.Contains(p.ProcessName))
                    {
                        pids.Add(p.Id);
                    }
                }
                catch { continue; }
            }
            return pids;
        }

        public static List<int> GetAllLauncherPids(List<StoreTracker> trackers, Dictionary<int, Process> cache)
        {
            var allPids = new List<int>();
            foreach (var tracker in trackers)
            {
                allPids.AddRange(GetFamilyPids(tracker.DisplayName, tracker.ProcessName, cache));
            }
            return allPids;
        }

        public static string DetermineVisualState(List<int> pids, string displayName, Dictionary<int, Process> cache)
        {
            if (pids == null || pids.Count == 0) return "OFFLINE";

            bool mainUiFound = false;
            bool anyFound = false;

            foreach (var pid in pids)
            {
                try
                {
                    if (cache.TryGetValue(pid, out Process p))
                    {
                        anyFound = true;

                        // ANCHOR CHECK: Verify the core UI process is running
                        string pname = p.ProcessName;
                        if (string.Equals(displayName, "EA Desktop", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pname.Equals("EADesktop", StringComparison.OrdinalIgnoreCase)) mainUiFound = true;
                        }
                        else if (string.Equals(displayName, "Ubisoft", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pname.Equals("UbisoftConnect", StringComparison.OrdinalIgnoreCase)) mainUiFound = true;
                        }
                        else if (string.Equals(displayName, "Xbox", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pname.Equals("XboxPcApp", StringComparison.OrdinalIgnoreCase) ||
                                pname.Equals("Xbox", StringComparison.OrdinalIgnoreCase) ||
                                pname.Equals("XboxPcTray", StringComparison.OrdinalIgnoreCase))
                            {
                                mainUiFound = true;
                            }
                        }
                        else if (string.Equals(displayName, "Steam", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pname.Equals("steam", StringComparison.OrdinalIgnoreCase)) mainUiFound = true;
                        }
                        else if (string.Equals(displayName, "Epic", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pname.Equals("EpicGamesLauncher", StringComparison.OrdinalIgnoreCase)) mainUiFound = true;
                        }
                        else if (string.Equals(displayName, "Battle.net", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pname.Equals("Battle.net", StringComparison.OrdinalIgnoreCase)) mainUiFound = true;
                        }
                        else if (string.Equals(displayName, "Itch.io", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pname.Equals("itch", StringComparison.OrdinalIgnoreCase)) mainUiFound = true;
                        }
                        else
                        {
                            // For unknown/custom stores, if any process exists, we treat it as found
                            mainUiFound = true;
                        }
                    }
                }
                catch { /* Process exited or access denied */ }
            }

            if (!anyFound) return "OFFLINE";

            // If background services are running but the main app is closed, it's a GHOST
            if (!mainUiFound) return "GHOST";

            return "MIN";
        }

        public static bool IsStoreRunning(string displayName, Process[] allRunning)
        {
            // Stop guessing with Replace(" ", ""). 
            // We use the same alias logic as GetFamilyPids to be consistent.
            var cache = allRunning.ToDictionary(p => p.Id, p => p);
            var pids = GetFamilyPids(displayName, null, cache);

            return pids.Count > 0;
        }
    }
}