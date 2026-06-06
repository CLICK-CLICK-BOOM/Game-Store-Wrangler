//<summary>[DO NOT REMOVE]AuditLogger.cs</summary>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GSWEngine
{
    public static class AuditLogger
    {
        private static readonly object _lock = new object();
        private static bool _initialized = false;
        private static string _sessionLogFile;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logDir = Path.Combine(localAppData, "GSWEngine", "Logs");
                _sessionLogFile = Path.Combine(logDir, $"{InternalFunctions.AppNameShort}_Log_{timestamp}.txt");

                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

                try
                {
                    string searchPattern = $"{InternalFunctions.AppNameShort}_Log_*";
                    string[] existingLogs = Directory.GetFiles(logDir, searchPattern);
                    DateTime cutoff = DateTime.Now.AddDays(-21);

                    foreach (string file in existingLogs)
                    {
                        try
                        {
                            if (File.GetCreationTime(file) < cutoff)
                            {
                                File.Delete(file);
                            }
                        }
                        catch { } // Ignore locked files and proceed to the next
                    }
                }
                catch { }

                lock (_lock)
                {
                    File.AppendAllText(_sessionLogFile, $"[{DateTime.Now}] {InternalFunctions.AppNameShort} {InternalFunctions.AppVersion} Service Started.{Environment.NewLine}");
                }
            }
            catch { }
        }

        public static void LogSummary(List<StoreTracker> trackers)
        {
            if (trackers == null) return;

            // Action 3: Logic Check - Only log if kills > 0
            int totalKills = trackers.Sum(t => t.SessionKills);
            if (totalKills == 0) return;

            try
            {
                var lines = new List<string>();
                lines.Add($"\n--- SESSION END SUMMARY [{DateTime.Now}] ---");
                foreach (var t in trackers.Where(x => x.SessionKills > 0))
                {
                    lines.Add($"[{t.DisplayName}] Kills: {t.SessionKills} | Reclaimed: {t.TotalMemoryReclaimed / 1024 / 1024} MB");
                }
                lines.Add("------------------------------------------");

                lock (_lock)
                {
                    File.AppendAllText(_sessionLogFile, string.Join(Environment.NewLine, lines) + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void RunTelemetryStressTest(Context ctx)
        {
            // Variables packed to their absolute mathematical limits
            string store = "STORESTORE"; // 10 chars
            string game = "GAMEGAMEGAMEGAMEGAMEGAMEGAMEGAMEGAMEGAMEGAMEGAM"; // 47 chars
            string time = "999"; // 3 chars
            string pid = "99999"; // 5 chars

            ctx.AppendToHistory("--- STRESS TEST INITIATED ---");

            // Core Engine & System
            ctx.AppendToHistory($"[Engine] Game detected. Soft-minimizing {store}.");
            ctx.AppendToHistory($"[System] Xbox dependent stores added.");

            // Radar & Identification
            ctx.AppendToHistory($"[Radar] Game: {game} (PID: {pid}).");
            ctx.AppendToHistory($"[Switchboard] Game attributed to {store}.");
            ctx.AppendToHistory($"Target identified: {game}.");

            // State & Monitor
            ctx.AppendToHistory($"[State] {store} locked: Game running.");
            ctx.AppendToHistory($"[State] {store} clear for management.");
            ctx.AppendToHistory($"[Monitor] Game exited: {game}.");

            // The Warden's Full Uncompressed Suite
            ctx.AppendToHistory($"[Warden] {store} busy: 20s pardon granted.");
            ctx.AppendToHistory($"[Warden] {store} busy: Resetting timer.");
            ctx.AppendToHistory($"[Warden] {store} idle {time}s. Terminating.");
            ctx.AppendToHistory($"[Warden] Count reset. {store} is updating. Granting 20s pardon.");
            ctx.AppendToHistory($"[Warden] Countdown complete on {store}. Closing store.");
            ctx.AppendToHistory($"[Warden] Timer reset. {store} is busy. Avg I/O > threshold over {time}s).");

            ctx.AppendToHistory("--- STRESS TEST COMPLETE ---");
        }

        public static string FormatExecutionHistory(List<string> history, int maxLines = 100)
        {
            // Requirement 2: Unified telemetry fallback greeting
            if (history == null || history.Count == 0) return "TELEMETRY OUTPUT";

            var formattedLines = new List<string>();
            var targets = history.Skip(Math.Max(0, history.Count - maxLines)).ToList();

            foreach (var rawLine in targets)
            {
                if (string.IsNullOrWhiteSpace(rawLine)) continue;

                // Handle manual pipeline breaks
                string[] segments = rawLine.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var segment in segments)
                {
                    string cleanSegment = segment.Trim();

                    // Requirement 3: Strip any legacy or hardcoded 3-dots from the string before evaluation
                    if (cleanSegment.EndsWith("..."))
                    {
                        cleanSegment = cleanSegment.Substring(0, cleanSegment.Length - 3).TrimEnd();
                    }

                    // 1. Establish the baseline limit for the visible UI payload
                    int dynamicLimit = 67;

                    // 2. Calculate the "invisible" weight of the category tag (e.g., "[Warden] ")
                    if (cleanSegment.StartsWith("[") && cleanSegment.Contains("] "))
                    {
                        // Offset the limit by the exact length of the tag + the trailing space
                        int tagLength = cleanSegment.IndexOf("] ") + 2;
                        dynamicLimit += tagLength;
                    }

                    // 3. Enforce the context-aware structural ceiling
                    if (cleanSegment.Length > dynamicLimit)
                    {
                        // Truncate and append the hyphen, accounting for the dynamic ceiling
                        cleanSegment = cleanSegment.Substring(0, dynamicLimit - 1) + "-";
                    }

                    // Now, strip the tag from the (potentially truncated) segment for final display.
                    int tagEndIndex = cleanSegment.IndexOf(']');
                    string finalSegment = tagEndIndex != -1
                        ? cleanSegment.Substring(tagEndIndex + 1).TrimStart()
                        : cleanSegment.Trim();

                    formattedLines.Add(finalSegment);
                }
            }

            return string.Join(Environment.NewLine, formattedLines);
        }
    }
}
