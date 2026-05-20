using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;

namespace GSWEngine
{
    public partial class StatsForm : Form
    {
        private string Fit(string text) => text.Length > 64 ? text.Substring(0, 61) + "..." : text;

        private string FormatRam(long bytes)
        {
            if (bytes > 1073741824) return $"{bytes / 1073741824.0:F2}GB";
            return $"{bytes / 1048576.0:F0}MB";
        }

        private void HandleHeartbeat()
        {
            _scanOffset += 0.02f;
            if (_scanOffset > 1.0f) _scanOffset = 0;

            RefreshLog();
            canvas?.Invalidate();

            if (lblAward == null || _ctx == null || _ctx.trackers == null || _currentColors == null) return;

            if (!_isEngineInitialized)
            {
                _isEngineInitialized = true;
                _currentPhase = 0; // Startup
                lblAward.Text = "Status: Scanning...";
                lblAward.Visible = true;
                return;
            }

            int maxTicks = _statusDisplaySeconds * 20;
            if (_engineTicks >= maxTicks)
            {
                _engineTicks = 0;

                _statusBeatCount++;

                // Every 8th beat (modulo 8 equals 0)
                if (_statusBeatCount % 10 == 0 && _statusData != null && _statusData.SupporterLines != null && _statusData.SupporterLines.Count > 0)
                {
                    // Sequential grab
                    string supporter = _statusData.SupporterLines[_supporterIndex];

                    // Wrap around to the start of the list if we reach the end
                    _supporterIndex = (_supporterIndex + 1) % _statusData.SupporterLines.Count;

                    // Distinct prefix so it isn't confused with system telemetry
                    lblAward.Text = Fit($"{supporter}");
                    _currentPhase = 3; // Keep the support color and interactivity
                }
                else
                {
                    // The Beat Advancer
                    if (_currentPhase == 0)
                    {
                        _currentPhase = _statusBeat[_beatIndex];
                    }
                    else
                    {
                        _beatIndex++;
                        if (_beatIndex >= _statusBeat.Length) _beatIndex = 0;
                        _currentPhase = _statusBeat[_beatIndex];
                    }

                    // The Payload Delivery
                    if (_currentPhase == 1) PickLudicrousLine();
                    else if (_currentPhase == 2) PickForensicLine();
                    else if (_currentPhase == 3) lblAward.Text = "Support your local Sheriff - Help GSW below TODAY! Thankyou";
                }
            }

            // --- COLOR & FLASHING LOGIC ---
            Color targetColor;

            if (_currentPhase == 1) targetColor = _currentColors.ActionTextFg;             // Funny
            else if (_currentPhase == 3) targetColor = _currentColors.Tick;         // Support
            else targetColor = _currentColors.StoreColFg;                              // Forensic & Startup

            // Flashing Rule: Phase 0 (Startup), Phase 2 (Forensic)
            bool shouldFlash = (_currentPhase == 0 || _currentPhase == 2);

            if (shouldFlash)
            {
                bool isBright = (_engineTicks % 20 < 10);
                Color dimColor = Color.FromArgb(255, targetColor.R / 3, targetColor.G / 3, targetColor.B / 3);
                lblAward.ForeColor = isBright ? targetColor : dimColor;
            }
            else
            {
                lblAward.ForeColor = targetColor;
            }

            lblAward.Cursor = (_currentPhase == 3) ? Cursors.Hand : Cursors.Default;
            lblAward.Visible = true;
            lblAward.Refresh();
            _engineTicks++;
            _pulseTick++;
        }

        private void PickLudicrousLine()
        {
            if (_statusData == null || _statusData.LudicrousLines == null || _statusData.LudicrousLines.Count == 0) lblAward.Text = "Status: Scanning...";
            else lblAward.Text = Fit(_statusData.LudicrousLines[new Random().Next(_statusData.LudicrousLines.Count)]);
        }

        private void PickForensicLine()
        {
            var validStats = GetValidForensicStats();
            if (validStats.Count > 0)
            {
                lblAward.Text = Fit(validStats[_forensicIndex % validStats.Count]);
                _forensicIndex++;
            }
            else { PickLudicrousLine(); _currentPhase = 1; }
        }

        private string EnforceLength(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Length <= maxLength ? input : input.Substring(0, maxLength - 3) + "...";
        }

        private List<string> GetValidForensicStats()
        {
            var stats = new List<string>();
            if (_ctx == null || _ctx.trackers == null) return stats;

            int sessionKills = _ctx.trackers.Sum(t => t.SessionKills);
            int totalKills = _ctx.trackers.Sum(t => t.LifetimeKills);
            long totalBytes = _ctx.trackers.Sum(t => t.TotalMemoryReclaimed);
            int activeStores = _ctx.trackers.Count(t => t.CurrentStatus != "OFFLINE");
            int shieldedStores = _ctx.trackers.Count(t => t.ManualOverride);
            double totalCpu = _ctx.trackers.Sum(t => t.CpuUsage);
            TimeSpan uptime = DateTime.Now - _appStartTime;

            var topGameEntry = _ctx.trackers
                .Where(t => t.DetectedGames != null)
                .SelectMany(t => t.DetectedGames)
                .GroupBy(k => k.Key)
                .Select(g => new { Name = g.Key, Count = g.Sum(x => x.Value) })
                .OrderByDescending(x => x.Count).FirstOrDefault();

            string safeGameName = EnforceLength(topGameEntry?.Name, 25);
            string mostWanted = topGameEntry != null && topGameEntry.Count > 0 ? $"{safeGameName} ({topGameEntry.Count} sessions)" : "Awaiting target data...";

            var allDetectedGames = _ctx.trackers.Where(t => t.DetectedGames != null).SelectMany(t => t.DetectedGames).ToList();
            int totalSessions = allDetectedGames.Sum(g => g.Value);
            int uniqueGames = allDetectedGames.Select(g => g.Key).Distinct().Count();

            var storeUsage = _ctx.trackers
                .Select(t => new { Name = t.DisplayName, Sessions = t.DetectedGames?.Sum(g => g.Value) ?? 0 })
                .Where(s => s.Sessions > 0).OrderByDescending(s => s.Sessions).ToList();

            string safeMostUsedStore = EnforceLength(storeUsage.FirstOrDefault()?.Name, 25) ?? "Awaiting Data";
            string safeLeastUsedStore = EnforceLength(storeUsage.LastOrDefault()?.Name, 25) ?? "Awaiting Data";

            string mostUsedTheme = "Awaiting Data";

            // Routed to _ctx.Settings
            if (_ctx.Settings.ThemeUsage != null && _ctx.Settings.ThemeUsage.Any())
                mostUsedTheme = EnforceLength(_ctx.Settings.ThemeUsage.OrderByDescending(t => t.Value).First().Key, 25);

            stats.Add($"{sessionKills} Stores Closed this Session.");
            stats.Add($"All-Time Store Close Count: {totalKills}");
            stats.Add($"Memory Reclaimed: {FormatRam(totalBytes)}");
            stats.Add($"Most Launched Game: {mostWanted}");
            stats.Add($"Uptime: {uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s.");
            stats.Add($"{activeStores} Store(s) Currently On Desktop.");
            stats.Add($"{shieldedStores} Protected Store(s) [x]");
            stats.Add($"Total CPU Load: {totalCpu:F1}%.");
            stats.Add($"Most Popular UI Theme: {mostUsedTheme}.");

            return stats;
        }
    }
}