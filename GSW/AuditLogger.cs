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
                _sessionLogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", $"{InternalFunctions.AppNameShort}_Log_{timestamp}.txt");

                string logDir = Path.GetDirectoryName(_sessionLogFile);
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
    }
}
