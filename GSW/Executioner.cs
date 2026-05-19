//<summary>[DO NOT REMOVE]Executioner.cs</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GSWEngine
{
    public static class Executioner
    {
        public static void TerminateStore(StoreTracker store, IEnumerable<int> pids, Action<string> log)
        {
            if (store.ManualOverride) return;

            long ramSaved = pids.Sum(pid => { try { return Process.GetProcessById(pid).WorkingSet64; } catch { return 0; } });

            string storeName = store.DisplayName;
            string[] targets = GetStoreTargets(storeName);

            // We use /F (Force) and /T (Tree) to kill the process and everything it spawned.
            foreach (var exeName in targets)
            {
                RunTaskKill($"/F /IM {exeName}.exe /T", log);
            }

            // Double-tap the specific PIDs detected by the StoreDetector
            foreach (var pid in pids)
            {
                try
                {
                    string pName = Process.GetProcessById(pid).ProcessName;
                    if (pName.Equals("GamingServices", StringComparison.OrdinalIgnoreCase) ||
                        pName.Equals("GamingServicesNet", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Protect system drivers from termination loops
                    }
                }
                catch { }

                RunTaskKill($"/F /PID {pid} /T", log);
            }

            store.KillCount++;
            store.LifetimeKills++;
            store.SessionKills++;
            store.TotalMemoryReclaimed += ramSaved;
            log($"[MCP] {storeName} Closed down. Reclaimed {ramSaved / 1024 / 1024} MB.");
        }

        private static void RunTaskKill(string args, Action<string> log)
        {
            try
            {
                using (Process proc = new Process())
                {
                    proc.StartInfo.FileName = "taskkill";
                    proc.StartInfo.Arguments = args;
                    proc.StartInfo.UseShellExecute = false;
                    proc.StartInfo.CreateNoWindow = true;
                    proc.Start();
                    proc.WaitForExit(2000);
                }
            }
            catch (Exception ex) { log($"[KILL FAIL] {ex.Message}"); }
        }

        private static string[] GetStoreTargets(string store)
        {
            return store switch
            {
                // We target the main executable and rely on the /T (Tree) switch to kill children.
                // We also explicitly target known background services that might not be children.
                "EA Desktop" => new[] { "EADesktop", "EABackgroundService", "Link2EA", "EAConnect_UI" },
                "Ubisoft" => new[] { "UbisoftConnect", "UplayService", "UbisoftConnectWebUI" },
                "Steam" => new[] { "steam", "steamwebhelper" },
                "Epic" => new[] { "EpicGamesLauncher" },
                "Battle.net" => new[] { "Battle.net", "Agent" },
                "Xbox" => new[] { "XboxPcApp", "GamingApp", "Gameloop" },
                "Amazon" => new[] { "Amazon Games UI" },
                "Itch.io" => new[] { "itch" },
                "GOG Galaxy" => new[] { "GalaxyClient", "GalaxyClientService" },
                "Riot Games" => new[] { "RiotClientServices", "RiotClient" },
                "Rockstar" => new[] { "SocialClubHelper", "RockstarService", "Launcher" },
                _ => Array.Empty<string>()
            };
        }
    }
}