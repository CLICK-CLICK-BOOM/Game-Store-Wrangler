//<summary>[DO NOT REMOVE]Context.cs</summary>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;

namespace GSWEngine
{
    public class Context : ApplicationContext
    {
        public UserSettings Settings;
        public List<string> ExecutionHistory = new List<string>();
        public event EventHandler HistoryUpdated;
        public readonly object HistoryLock = new object();

        private readonly StateEngine _engine;
        private readonly UIManager _ui;
        private bool _isBusy = false;
        private HashSet<int> _knownGamePids = new HashSet<int>();
        private CancellationTokenSource _wardenCts = new CancellationTokenSource();

        // --- UI BRIDGES ---
        public List<StoreTracker> trackers => Settings.Trackers;
        public int CurrentTheme { get => Settings.ThemeIndex; set => Settings.ThemeIndex = value; }
        public bool IsAlwaysOnTop { get => Settings.TopMost; set => Settings.TopMost = value; }
        public int GracePeriod { get => Settings.GracePeriod; set => Settings.GracePeriod = value; }
        public int LastX { get => Settings.WindowX; set => Settings.WindowX = value; }
        public int LastY { get => Settings.WindowY; set => Settings.WindowY = value; }
        public int WindowX { get => Settings.WindowX; set => Settings.WindowX = value; }
        public int WindowY { get => Settings.WindowY; set => Settings.WindowY = value; }
        public int TweakerX { get => Settings.TweakerX; set => Settings.TweakerX = value; }
        public int TweakerY { get => Settings.TweakerY; set => Settings.TweakerY = value; }
        public int IntelX { get => Settings.IntelX; set => Settings.IntelX = value; }
        public int IntelY { get => Settings.IntelY; set => Settings.IntelY = value; }
        public bool IsUiVisible { get; set; } = false;
        public bool CloseUnnecessaryStores => Settings.CloseUnnecessaryStoresOnLaunch;

        public Context(string[] args)
        {
            InternalFunctions.Initialize();
            var saved = InternalFunctions.LoadSettings();
            Settings = saved ?? new UserSettings { Trackers = InternalFunctions.CreateDefaultTrackers() };

            StateEngine.IsDebug = false;
            StateEngine.GameDetectionEnabled = true;

            if (args.Any(a => a.Equals("-debug", StringComparison.OrdinalIgnoreCase)))
                StateEngine.IsDebug = true;
            if (args.Any(a => a.Equals("-nogamedetection", StringComparison.OrdinalIgnoreCase)))
                StateEngine.GameDetectionEnabled = false;

            // --- THE SOVEREIGN SYNC (Self-Healing Anchors) ---
            // This forces the JSON file to strictly obey the Master List in InternalFunctions.cs
            var masterDefs = InternalFunctions.GetAllPossibleStores();
            if (Settings.Trackers != null)
            {
                Settings.Trackers = Settings.Trackers.OrderBy(t => t.DisplayIndex).ToList();

                foreach (var tracker in Settings.Trackers)
                {
                    tracker.MaxTime = Settings.GracePeriod;

                    // Match on DisplayName so we can heal broken/changed ProcessNames
                    var master = masterDefs.FirstOrDefault(m =>
                        m.DisplayName.Equals(tracker.DisplayName, StringComparison.OrdinalIgnoreCase));

                    if (master != null)
                    {
                        tracker.ProcessName = master.ProcessName; // Instantly fixes old exes (e.g. UbisoftGameLauncher -> upc)
                        tracker.IsGallowsCompatible = master.IsGallowsCompatible;
                    }
                }

                // --- XBOX DEPENDENCY PILLAR FIX ---
                InternalFunctions.EnforceXboxDependencies(this);

                // Force a save right at boot so the file is guaranteed to be recreated/healed
                SaveSettings();
            }

            // Console.WriteLine("[System] Config Loaded. All Live States Reset to OFFLINE.");
            AuditLogger.Initialize();

            InternalFunctions.SetWindowsStartup(true);

            _engine = new StateEngine(this);
            _ui = new UIManager(this);

            _ = StartWardenEngineAsync();

            if (!args.Any(a => a.Equals("-quiet", StringComparison.OrdinalIgnoreCase)))
            {
                _ui.ShowMainHUD();
            }
        }

        public void AppendToHistory(string message)
        {
            lock (HistoryLock)
            {
                ExecutionHistory.Add(message);
                if (ExecutionHistory.Count > 500)
                {
                    ExecutionHistory.RemoveAt(0);
                }
            }
            // Fire the event safely outside the lock to avoid deadlocks
            HistoryUpdated?.Invoke(this, EventArgs.Empty);
        }

        private async Task StartWardenEngineAsync()
        {
            // 1-second interval for the Warden's heartbeat
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            try
            {
                while (await timer.WaitForNextTickAsync(_wardenCts.Token))
                {
                    await TickAsync();
                }
            }
            catch (OperationCanceledException)
            {
                // Engine stopped gracefully.
            }
        }

        public void SaveSettings() => InternalFunctions.SaveSettings(Settings);

        public async Task TickAsync()
        {
            if (_isBusy) return;
            _isBusy = true;

            Dictionary<int, Process> processCache = null;
            try
            {
                // 1. Get real elapsed time
                double elapsedSec = ActivityMonitor.GetElapsedSeconds();
                if (elapsedSec < 0.1) elapsedSec = 1.0;

                // 2. Process Snapshot
                processCache = new Dictionary<int, Process>();
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (!processCache.ContainsKey(p.Id)) processCache[p.Id] = p;
                        else p.Dispose();
                    }
                    catch (ArgumentException) { p.Dispose(); continue; }
                    catch (InvalidOperationException) { p.Dispose(); continue; }
                }

                // 3. Update Core Stats
                foreach (var tracker in Settings.Trackers)
                {
                    tracker.MaxTime = Settings.GracePeriod;

                    // Anchor check: Get the family PIDs using the case-insensitive detector
                    tracker.LauncherPids = StoreDetector.GetFamilyPids(tracker.DisplayName, tracker.ProcessName, processCache);

                    if (IsUiVisible)
                        tracker.CpuUsage = ActivityMonitor.GetTotalCpuUsage(tracker.LauncherPids, elapsedSec * 1000, processCache);
                    else
                        tracker.CpuUsage = 0;

                    tracker.DeltaDiskBytes = ActivityMonitor.GetTotalDiskBytes(tracker.LauncherPids, processCache);

                    // Visual & Ghost State Analysis
                    string vState = StoreDetector.DetermineVisualState(tracker.LauncherPids, tracker.DisplayName, processCache);
                    tracker.IsGhost = (vState == "GHOST");

                    // The UI redraw will now pick up the flag we synced in the constructor
                }

                // 4. Active Games Check
                var activeGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var currentSignals = new List<GameSignal>();
                var currentPids = new HashSet<int>();

                if (StateEngine.GameDetectionEnabled)
                {
                    var activeSignals = GameDetector.GetActiveGames(Settings.Trackers);
                    foreach (var signal in activeSignals)
                    {
                        currentPids.Add(signal.Pid);
                        if (!string.IsNullOrEmpty(signal.ProcessName))
                            activeGames.Add(signal.ProcessName);

                        bool isNewGame = !_knownGamePids.Contains(signal.Pid);
                        if (isNewGame)
                        {
                            _knownGamePids.Add(signal.Pid);
                            AppendToHistory($"[Radar] Game: {signal.ProcessName} (PID: {signal.Pid})");
                        }

                        string attributedStore = Attribution.DetermineStoreOwner(signal, Settings.Trackers);

                        if (isNewGame && !string.IsNullOrEmpty(attributedStore) && attributedStore != "Unknown")
                        {
                            AppendToHistory($"[Switchboard] Game attributed to {attributedStore}.");
                        }

                        currentSignals.Add(signal);
                    }
                    _knownGamePids.IntersectWith(currentPids);
                }

                // 5. State Engine Processing
                _engine.ProcessTick(activeGames, elapsedSec, processCache, currentSignals);

                if (StateEngine.IsDebug)
                {
                    foreach (var store in Settings.Trackers)
                    {
                        if (store.CurrentStatus == "MIN" || store.CurrentStatus == "MIN BUSY")
                            Console.WriteLine($"[DEBUG] {store.DisplayName} | Status: {store.CurrentStatus} | Timer: {store.TimeLeft} | CPU: {store.CpuUsage:F1}%");
                    }
                }
            }
            catch (Exception ex)
            {
                if (StateEngine.IsDebug)
                {
                    Console.WriteLine($"[DEBUG] TickAsync Error: {ex.Message}");
                }
            }
            finally
            {
                if (processCache != null)
                {
                    foreach (var p in processCache.Values) p.Dispose();
                    processCache.Clear();
                }
                _isBusy = false;
            }
        }

        public void DropKnownGamePid(int pid)
        {
            if (pid > 0 && _knownGamePids.Contains(pid))
            {
                _knownGamePids.Remove(pid);
            }
        }

        public void Exit()
        {
            _wardenCts.Cancel();
            SaveSettings(); // Ensure our state is permanently recorded

            if (Settings.Trackers.Any(t => t.SessionKills > 0))
            {
                AuditLogger.LogSummary(Settings.Trackers);
            }
            _ui.DisposeTray();
            Application.Exit();
        }
    }
}