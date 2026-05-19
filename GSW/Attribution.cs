//<summary>[DO NOT REMOVE]Attribution.cs</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;
using System.Linq;

namespace GSWEngine
{
    public static class Attribution
    {
        public static string DetermineStoreOwner(GameSignal signal, List<StoreTracker> trackers = null)
        {
            // 1. Instant UWP/AppX mapping
            if (signal.IsAppX) return "Xbox";

            // 2. Explicit Path Mapping
            if (!string.IsNullOrEmpty(signal.ExePath))
            {
                if (signal.ExePath.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    signal.ExePath.IndexOf("Xbox", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Xbox";
                if (signal.ExePath.IndexOf("Steam", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Steam";
                if (signal.ExePath.IndexOf("Epic", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Epic Games";
                if (signal.ExePath.IndexOf("Ubisoft", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Ubisoft";
                if (signal.ExePath.IndexOf("EA Desktop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    signal.ExePath.IndexOf("Origin", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "EA Desktop";
                if (signal.ExePath.IndexOf("Battle.net", StringComparison.OrdinalIgnoreCase) >= 0)
                    return "Battle.net";
            }

            // 3. Fallback: Check parent processes if needed (Original logic)
            if (trackers != null)
            {
                var parentStore = ParentProcessUtilities.GetParentStore(signal, trackers);
                if (parentStore != null) return parentStore.DisplayName;
            }

            string orphanOwner = InterrogateOrphan(signal.Pid);
            if (!string.IsNullOrEmpty(orphanOwner)) return orphanOwner;

            return "Unknown";
        }

        // Move the InterrogateOrphan method here from Context.cs
        // Make it private static and have it accept an int (PID) instead of a Process object to safely manage disposal.
        private static string InterrogateOrphan(int pid)
        {
            try
            {
                using (var gameProcess = Process.GetProcessById(pid))
                {
                    try
                    {
                        using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                        {
                            if (key != null && key.GetValue("RunningAppID") is int appId && appId > 0)
                                return "Steam";
                        }
                    }
                    catch { }

                    try
                    {
                        string exePath = gameProcess.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            if (exePath.IndexOf("Amazon Games", StringComparison.OrdinalIgnoreCase) >= 0) return "Amazon";
                            if (exePath.IndexOf("itch.io", StringComparison.OrdinalIgnoreCase) >= 0) return "Itch.io";
                        }
                    }
                    catch { /* Handle Access Denied on MainModule */ }

                    // --- MODULE SCAN: The Check of Last Resort ---
                    try
                    {
                        foreach (ProcessModule m in gameProcess.Modules)
                        {
                            string mName = m.ModuleName.ToLowerInvariant();
                            if (mName == "gameoverlayrenderer64.dll" || mName == "gameoverlayrenderer.dll") return "Steam";
                            if (mName == "xgameruntime.dll" || mName == "xboxservices.api.dll") return "Xbox";
                            if (mName.Contains("uplay_") || mName.Contains("upc_")) return "Ubisoft";
                            if (mName == "galaxy64.dll" || mName == "galaxy.dll") return "GOG Galaxy";
                            if (mName == "eossdk-win64-shipping.dll") return "Epic";
                            if (mName == "battlenet_api.dll") return "Battle.net";
                            if (mName == "riotclientservices.exe") return "Riot Games";
                        }
                    }
                    catch { /* Handle Access Denied on modules for protected games */ }
                }
            }
            catch { }
            return null;
        }
    }
}