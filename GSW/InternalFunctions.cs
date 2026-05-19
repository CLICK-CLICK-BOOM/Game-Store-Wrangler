//<summary>[DO NOT REMOVE]InternalFunctions.cs</summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Drawing;

namespace GSWEngine
{
    public class StatusData
    {
        public List<string> LudicrousLines { get; set; } = new List<string>();
        public List<string> SupporterLines { get; set; } = new List<string>();
    }

    public static class InternalFunctions
    {
        public const string AppNameFull = "Game Store Wrangler";
        public const string AppNameShort = "GSW";
        public const string AppAuthor = "CLICK CLICK BOOM";

        public static string AppVersion
        {
            get
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return $"v{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", $"{AppNameShort}_config.json");
        private static readonly string StatusPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Status.json");
        private const string StartupRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static readonly string AppIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppIcon.ico");
        public static readonly string LockIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lock_icon.png");
        public static readonly string QuickGuidePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Intel_manifest.rtf");

        public static Stream GetResource(string path)
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            // Converts "skins\Cubic_read.jpg" to "GSWEngine.skins.Cubic_read.jpg"
            string resourceName = $"GSWEngine.{path.Replace('\\', '.').Replace('/', '.')}";
            return assembly.GetManifestResourceStream(resourceName);
        }

        public static void Initialize()
        {
            string[] folders = { "Config", "Logs" };
            foreach (var folder in folders)
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            }
        }

        // THE CLEAN SAVE: Takes the specific settings object
        public static void SaveSettings(UserSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[SAVE ERROR] {ex.Message}");
            }
        }

        public static UserSettings LoadSettings()
        {
            if (!File.Exists(SettingsPath)) return null;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                string json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<UserSettings>(json, options);

                if (loaded != null)
                {
                    // Reset transient states that shouldn't persist
                    foreach (var t in loaded.Trackers)
                    {
                        t.CurrentStatus = "OFFLINE";
                        t.CpuUsage = 0;
                        t.TimeLeft = loaded.GracePeriod;
                        t.MaxTime = loaded.GracePeriod;
                        t.ActiveGamePid = 0;
                        t.LauncherPids = new List<int>();
                        t.GameActiveGraceTicks = 0;
                    }
                }
                return loaded;
            }
            catch { return null; }
        }

        public static StatusData LoadStatusData()
        {
            var defaults = new StatusData { LudicrousLines = new List<string> { "Status: Scanning..." } };

            try
            {
                // Target the embedded resource. 
                // Ensure Status.json is set to "Embedded Resource" in the .csproj!
                using (var stream = GetResource("Status.json"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            string json = reader.ReadToEnd();
                            var data = JsonSerializer.Deserialize<StatusData>(json);
                            if (data != null) return data;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[JSON ERROR] Failed to load embedded status lines: {ex.Message}");
            }

            return defaults;
        }

        public static void SetWindowsStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupRegistryPath, true))
                {
                    if (key == null) return;
                    if (enable) key.SetValue(AppNameShort, $"\"{Application.ExecutablePath}\" -quiet");
                    else key.DeleteValue(AppNameShort, false);
                }
            }
            catch { }
        }

        public static void RefreshUI(StatsForm window)
        {
            if (window != null && !window.IsDisposed)
            {
                if (window.InvokeRequired) window.Invoke(new Action(() => window.UpdateUI()));
                else window.UpdateUI();
            }
        }

        public static List<StoreTracker> CreateDefaultTrackers(int defaultGrace = 90)
        {
            string[] defaults = { "Steam", "Xbox", "Epic", "Ubisoft", "EA Desktop", "Battle.net" };
            var all = GetAllPossibleStores();
            var filtered = all.Where(t => defaults.Contains(t.DisplayName)).ToList();

            // Apply the requested grace period to the defaults
            for (int i = 0; i < filtered.Count; i++)
            {
                filtered[i].TimeLeft = filtered[i].MaxTime = defaultGrace;
                filtered[i].DisplayIndex = i;
            }

            return filtered;
        }

        public static void EnforceXboxDependencies(Context ctx)
        {
            if (ctx.trackers.Any(t => t.DisplayName.Equals("Xbox", StringComparison.OrdinalIgnoreCase)))
            {
                string[] dependencies = { "EA Desktop", "Ubisoft", "Battle.net" };
                bool dependenciesAdded = false;
                var masterDefs = GetAllPossibleStores();

                foreach (string dep in dependencies)
                {
                    if (ctx.trackers.Count >= 12) break;

                    if (!ctx.trackers.Any(t => t.DisplayName.Equals(dep, StringComparison.OrdinalIgnoreCase)))
                    {
                        var depTemplate = masterDefs.FirstOrDefault(t => t.DisplayName.Equals(dep, StringComparison.OrdinalIgnoreCase));
                        if (depTemplate != null)
                        {
                            ctx.trackers.Add(new StoreTracker(depTemplate.DisplayName, depTemplate.ProcessName, ctx.GracePeriod)
                            {
                                IsGallowsCompatible = depTemplate.IsGallowsCompatible,
                                DisplayIndex = ctx.trackers.Count
                            });
                            dependenciesAdded = true;
                        }
                    }
                }

                if (dependenciesAdded)
                {
                    ctx.AppendToHistory("[System] Xbox dependent stores added.");
                }
            }
        }

        public static List<StoreTracker> GetAllPossibleStores()
        {
            int g = 180;
            return new List<StoreTracker> {
                new StoreTracker("Steam", "steam", g),
                new StoreTracker("Epic", "EpicGamesLauncher", g),
                new StoreTracker("Xbox", "XboxPcApp", g),
                new StoreTracker("EA Desktop", "EADesktop", g),
                new StoreTracker("Ubisoft", "upc", g),
                new StoreTracker("Battle.net", "Battle.net", g),
                new StoreTracker("GOG Galaxy", "GalaxyClient", g),
                new StoreTracker("Rockstar", "SocialClubHelper", g),
                new StoreTracker("Riot Games", "RiotClientServices", g),
                // MARK THE SOVEREIGNS
                new StoreTracker("Amazon", "Amazon Games UI", g) { IsGallowsCompatible = true },
                new StoreTracker("Itch.io", "itch", g) { IsGallowsCompatible = true }
            };
        }

        public static string FormatExecutionHistory(List<string> history, int maxLines = 100)
        {
            // Requirement 2: Unified telemetry fallback greeting
            if (history == null || history.Count == 0) return "TELEMETRY OUTPUT";

            var formattedLines = new System.Collections.Generic.List<string>();
            var targets = history.Skip(Math.Max(0, history.Count - maxLines)).ToList();

            foreach (var rawLine in targets)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;

                // Strip the legacy [Subsystem] tag entirely
                int tagEndIndex = rawLine.IndexOf(']');
                string fullMessage = tagEndIndex != -1
                    ? rawLine.Substring(tagEndIndex + 1).TrimStart()
                    : rawLine.Trim();

                // Handle manual pipeline breaks
                string[] segments = fullMessage.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var segment in segments)
                {
                    string cleanSegment = segment.Trim();

                    // Requirement 3: Strip any legacy or hardcoded 3-dots from the string before evaluation
                    if (cleanSegment.EndsWith("..."))
                    {
                        cleanSegment = cleanSegment.Substring(0, cleanSegment.Length - 3).TrimEnd();
                    }

                    // Requirement 3: Enforce strict, mid-word truncation at exactly 55 characters with a single-dash indicator
                    if (cleanSegment.Length > 55)
                        // Enforce a strict, word-blind 67-character structural ceiling (66 text + 1 dash)
                        if (cleanSegment.Length > 67)
                        {
                            cleanSegment = cleanSegment.Substring(0, 54) + "-";
                            cleanSegment = cleanSegment.Substring(0, 66) + "-";
                        }

                    formattedLines.Add(cleanSegment);
                }
            }

            return string.Join(Environment.NewLine, formattedLines);
        }

        public static Color BrightenColor(Color baseColor, float percentage = 0.30f)
        {
            // Multiply each channel by 1.30 (to add 30%) and clamp it at 255
            int r = Math.Min(255, (int)(baseColor.R + (baseColor.R * percentage)));
            int g = Math.Min(255, (int)(baseColor.G + (baseColor.G * percentage)));
            int b = Math.Min(255, (int)(baseColor.B + (baseColor.B * percentage)));

            return Color.FromArgb(baseColor.A, r, g, b);
        }
    }
}