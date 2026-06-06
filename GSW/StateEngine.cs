//<summary>[DO NOT REMOVE]StateEngine.cs</summary>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GSWEngine
{
    public class StateEngine
    {
        public static class DiagnosticHUD
        {
            public static bool IsMinimized { get; set; }
            public static bool IsIdle { get; set; }
            public static bool IsRadarActive { get; set; }
            public static bool IsSentryArmed { get; set; }
            public static bool IsMeasuringCPU { get; set; }
            public static bool IsMeasuringIO { get; set; }
        }

        internal static bool IsDebug = false; // Toggle here manually for dev work
        internal static bool GameDetectionEnabled = true; // Always on for customers
        internal static bool RunTelemetryStressTest = false; // Trigger maximum payload injection once on startup

        private readonly Context _ctx;
        private Dictionary<string, int> _forensicCounters = new Dictionary<string, int>();
        private Dictionary<string, int> _namingDelayCounters = new Dictionary<string, int>();
        private Dictionary<int, string> _activeGameNames = new Dictionary<int, string>();
        private CancellationTokenSource _overwatchCts;

        // --- WIN32 TWO-STROKE PIPELINE INTERFACE ---
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_SHOWMINIMIZED = 2;

        public StateEngine(Context context)
        {
            _ctx = context;

            // Single-Shot Diagnostic Payload
            if (RunTelemetryStressTest)
            {
                AuditLogger.RunTelemetryStressTest(_ctx);
                RunTelemetryStressTest = false; // Instantly disarm to guarantee it never fires twice
            }
        }

        public void ProcessTick(HashSet<string> activeGames, double elapsedSec, Dictionary<int, Process> processCache, List<GameSignal> currentSignals)
        {
            // 1. PRE-TICK: Vitality Sync (Now using the Conductor's cache for maximum efficiency)
            foreach (var store in _ctx.Settings.Trackers)
            {
                SyncStoreVitality(store, processCache);
            }

            // The engine's true UI state determines both the MIN light and CPU light
            DiagnosticHUD.IsMinimized = !_ctx.IsUiVisible;

            // 1. Sentry is armed if the token exists
            DiagnosticHUD.IsSentryArmed = (_overwatchCts != null);

            // 2. Idle is true if ALL stores are OFFLINE
            DiagnosticHUD.IsIdle = _ctx.Settings.Trackers.All(t => t.CurrentStatus == "OFFLINE");

            // 3. Radar is ON only if we are actively managing/hunting (NOT in Deep Sleep)
            DiagnosticHUD.IsRadarActive = !DiagnosticHUD.IsSentryArmed && _ctx.Settings.Trackers.Any(t => t.CurrentStatus != "OFFLINE");

            // 4. CPU/IO lights are strictly suppressed if Sentry is armed (Deep Sleep)
            DiagnosticHUD.IsMeasuringCPU = !DiagnosticHUD.IsSentryArmed && _ctx.IsUiVisible && (_ctx.Settings.Trackers.Any(t => t.CurrentStatus != "OFFLINE"));
            DiagnosticHUD.IsMeasuringIO = !DiagnosticHUD.IsSentryArmed && _ctx.Settings.Trackers.Any(t => t.CurrentStatus != "OFFLINE");

            // 2. Game Detection & Attribution
            if (GameDetectionEnabled && currentSignals != null)
            {
                foreach (var signal in currentSignals)
                {
                    ProcessGameSignal(signal, activeGames, processCache);
                }
            }

            // 3. Status Transitions & Protective Gating
            bool isAnyGameActive = activeGames.Count > 0;
            foreach (var store in _ctx.Settings.Trackers)
            {
                // --- WARDEN ENGINE: GAME CLOSE UNNECESSARY STORES PURGE PIPELINE ---
                if (store.ManualOverride)
                {
                    // The individual Keep shield has absolute priority. Skip global automated enforcement.
                    if (store.CurrentStatus == "GAME CLOSE")
                    {
                        store.CurrentStatus = "MIN"; // Gracefully transition back to standard monitoring
                        store.TimeLeft = _ctx.GracePeriod;
                    }
                }
                else if (_ctx.Settings.CloseUnnecessaryStoresOnLaunch && isAnyGameActive)
                {
                    // --- XBOX BUDDY DEPENDENCY PROTECTION EXTENSION ---
                    bool isXboxKeepActive = _ctx.Settings.Trackers.Any(t => t.DisplayName.Equals("Xbox", StringComparison.OrdinalIgnoreCase) && t.ManualOverride);
                    bool isDependencyStore = store.DisplayName.Equals("Battle.net", StringComparison.OrdinalIgnoreCase) ||
                                             store.DisplayName.Equals("Ubisoft", StringComparison.OrdinalIgnoreCase) ||
                                             store.DisplayName.Equals("EA Desktop", StringComparison.OrdinalIgnoreCase);

                    if (isXboxKeepActive && isDependencyStore)
                    {
                        // If the user is forcing Xbox to stay open, protect its buddy dependency networks from automated execution
                        if (store.CurrentStatus == "GAME CLOSE")
                        {
                            store.CurrentStatus = "MIN";
                            store.TimeLeft = _ctx.GracePeriod;
                        }
                        continue;
                    }

                    // If a game is actively running and this specific store is NOT the verified owner
                    if (!activeGames.Contains(store.DisplayName) && store.CurrentStatus != "OFFLINE")
                    {
                        // --- THE XBOX BUDDY PROTECTION GATE ---
                        // If an Xbox game is currently active, protect Battle.net from the rapid-close sweeper loop
                        bool isXboxActive = activeGames.Contains("Xbox");
                        bool isBattleNet = store.DisplayName.Equals("Battle.net", StringComparison.OrdinalIgnoreCase);

                        if (isXboxActive && isBattleNet)
                        {
                            // Reset its tracking parameters and allow it to retain its standard baseline monitoring state
                            if (store.CurrentStatus == "GAME CLOSE")
                            {
                                store.CurrentStatus = "MIN";
                                store.TimeLeft = _ctx.Settings.GracePeriod; // Utilizing the correct schema master variable
                            }
                            continue; // Skip the rapid purge loop for this protected ally
                        }

                        // Initialize the rapid countdown state if it just entered scope
                        if (store.CurrentStatus != "GAME CLOSE")
                        {
                            store.CurrentStatus = "GAME CLOSE";
                            store.TimeLeft = 10; // Secure 10-second idle-buffered countdown window
                            store.TimeLeft = 20; // Expanded 20-second execution window
                            store.AccumulatedDiskBytes = 0;
                            store.AccumulationTicks = 0;

                            // STROKE 1: Fire soft-touch minimization across all family PIDs to force cache loops to freeze
                            foreach (var pid in store.LauncherPids)
                            {
                                try
                                {
                                    var proc = processCache.ContainsKey(pid) ? processCache[pid] : System.Diagnostics.Process.GetProcessById(pid);
                                    if (proc != null && proc.MainWindowHandle != IntPtr.Zero)
                                    {
                                        ShowWindow(proc.MainWindowHandle, SW_SHOWMINIMIZED);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Trace.WriteLine($"[GSW SILENT EXCEPTION] {ex.Message} | Source: {ex.StackTrace}");
                                }
                            }
                            _ctx.AppendToHistory($"[Engine] Game detected. Soft-minimizing {store.DisplayName}.");
                        }

                        // STROKE 2: Standard rolling aggregate I/O countdown handles the rest
                        double decrement = (elapsedSec > 0) ? elapsedSec : 1.0;
                        store.TimeLeft = (int)Math.Max(0, Math.Floor((store.TimeLeft - decrement) + 0.9));

                        store.AccumulatedDiskBytes += store.DeltaDiskBytes;
                        store.AccumulationTicks++;

                        // If it hit absolute zero, let the system handle the termination or pardon
                        if (store.TimeLeft <= 0)
                        {
                            long averageIo = store.AccumulationTicks > 0 ? (store.AccumulatedDiskBytes / store.AccumulationTicks) : 0;

                            // Using isolated variable identifier to resolve scope collision CS0128
                            long rapidIoThreshold = 512000;
                            if (store.DisplayName.Equals("Amazon", StringComparison.OrdinalIgnoreCase))
                            {
                                rapidIoThreshold = 5120000;
                            }

                            if (averageIo > rapidIoThreshold)
                            {
                                _ctx.AppendToHistory($"[Warden] Count reset. {store.DisplayName} is updating. Granting 20s pardon.");
                                // Compressed: Trimmed down calculations to save UI character space
                                _ctx.AppendToHistory($"[Warden] {store.DisplayName} busy: 20s pardon granted.");
                                store.TimeLeft = 20;
                                store.AccumulatedDiskBytes = 0;
                                store.AccumulationTicks = 0;
                            }
                            else
                            {
                                _ctx.AppendToHistory($"[Warden] Countdown complete on {store.DisplayName}. Closing store.");
                                var pidsToKill = store.LauncherPids.ToList();
                                System.Threading.Tasks.Task.Run(() => { Executioner.TerminateStore(store, pidsToKill, (msg) => _ctx.AppendToHistory(msg)); });

                                store.CurrentStatus = "OFFLINE";
                                store.TimeLeft = 0;
                            }
                        }
                        continue;
                    }
                }

                // Final Protection Check & State Commit
                if (CheckProtections(store, activeGames, processCache)) continue;

                ResolveXboxBuddyState(store);

                // WARDEN ACTIVITY CHECK: I/O based 
                if (store.BusyLabelTimer > 0)
                {
                    store.BusyLabelTimer--;
                }

                // --- THE FIX: Standard UI Visibility Check & Xbox Buddy Preservation ---
                string fallbackStatus = "MIN";

                if (WindowScanner.IsStoreOnScreen(store.LauncherPids, store.DisplayName))
                {
                    fallbackStatus = "ACTIVE";
                }
                else if (store.BusyLabelTimer > 0)
                {
                    fallbackStatus = "MIN BUSY";
                }
                else if (store.CurrentStatus == "Xbox Buddy")
                {
                    // Catch: If the Treaty block above granted Buddy status, do not overwrite it!
                    fallbackStatus = "Xbox Buddy";
                }

                UpdateStateWithHysteresis(store, fallbackStatus, elapsedSec, false);
            }

            // --- SENTRY ENGAGEMENT PROTOCOL ---
            // Sentry should only arm when a game is running AND all unnecessary stores are dead.
            if (_ctx.Settings.CloseUnnecessaryStoresOnLaunch && isAnyGameActive)
            {
                // Define the "cleared" perimeter: Are there any stores in an active/min/closing state that SHOULD be dead?
                bool isPerimeterClear = true;
                StoreTracker targetStore = null;

                foreach (var st in _ctx.Settings.Trackers)
                {
                    if (st.CurrentStatus == "GAME ACTIVE" || st.CurrentStatus == "GAME XBOX")
                    {
                        targetStore = st; // Identify the protector
                        continue;
                    }

                    // Xbox/Dependency Treaty rules still apply during perimeter check
                    bool isXboxKeepActive = _ctx.Settings.Trackers.Any(t => t.DisplayName.Equals("Xbox", StringComparison.OrdinalIgnoreCase) && t.ManualOverride);
                    bool isDependencyStore = st.DisplayName.Equals("Battle.net", StringComparison.OrdinalIgnoreCase) ||
                                             st.DisplayName.Equals("Ubisoft", StringComparison.OrdinalIgnoreCase) ||
                                             st.DisplayName.Equals("EA Desktop", StringComparison.OrdinalIgnoreCase);

                    if (isXboxKeepActive && isDependencyStore) continue;
                    if (st.CurrentStatus == "Xbox Buddy") continue;
                    if (st.ManualOverride) continue;

                    // If ANY store is still alive (not OFFLINE), the perimeter is NOT clear.
                    if (st.CurrentStatus != "OFFLINE")
                    {
                        isPerimeterClear = false;
                        break;
                    }
                }

                if (isPerimeterClear && _overwatchCts == null && targetStore != null)
                {
                    // Perimeter is clear. Arm the Sentry.
                    _overwatchCts = new CancellationTokenSource();
                    _ = RunTacticalOverwatchAsync(targetStore, _overwatchCts.Token);
                    _ctx.AppendToHistory("[Sentry] Perimeter clear. Sentry engaged.");
                }
            }
            else
            {
                // Disarm if no games are running or user toggles off mid-game
                if (_overwatchCts != null)
                {
                    _overwatchCts.Cancel();
                    _overwatchCts.Dispose();
                    _overwatchCts = null;
                    _ctx.AppendToHistory("[Sentry] Sentry disengaged.");
                }
            }
        }

        private void ResolveXboxBuddyState(StoreTracker store)
        {
            if (store.DisplayName != "Battle.net") return;
            var xbox = _ctx.Settings.Trackers.FirstOrDefault(t => t.DisplayName == "Xbox");
            bool xboxIsActuallyOnline = (xbox != null && xbox.LauncherPids.Count > 0 && xbox.IsPrimaryAppRunning);

            if (xboxIsActuallyOnline && store.LauncherPids.Count > 0)
            {
                if (store.CurrentStatus == "OFFLINE" || store.CurrentStatus == "MIN" || store.CurrentStatus == "OFFSCREEN" || store.CurrentStatus == "ACTIVE")
                {
                    if (!WindowScanner.IsStoreOnScreen(store.LauncherPids, store.DisplayName))
                    {
                        store.CurrentStatus = "Xbox Buddy";
                    }
                }
            }
            else if (store.CurrentStatus == "Xbox Buddy")
            {
                store.CurrentStatus = "MIN";
                if (store.TimeLeft <= 0) store.TimeLeft = 30;
            }
        }

        private void ProcessGameSignal(GameSignal signal, HashSet<string> activeGames, Dictionary<int, Process> cache)
        {
            // --- THE GHOST WINDOW GUARD ---
            // Prevents ping-pong log spam from lingering UWP/GDK container windows
            bool isAlive = cache.ContainsKey(signal.Pid);
            if (!isAlive)
            {
                try
                {
                    // Poke the PID directly. If it throws, the backing process is dead.
                    using (var p = Process.GetProcessById(signal.Pid)) { isAlive = true; }
                }
                catch { isAlive = false; }
            }

            // Drop the signal entirely if the process is gone, ignoring the zombie window.
            if (!isAlive) return;

            // Requirement: Enforce a strict 47-character limit on volatile game names to allow for terminal punctuation
            if (!string.IsNullOrEmpty(signal.ProcessName) && signal.ProcessName.Length > 47)
            {
                signal.ProcessName = signal.ProcessName.Substring(0, 44) + "...";
            }

            if (!string.IsNullOrEmpty(signal.WindowTitle) && signal.WindowTitle.Length > 47)
            {
                signal.WindowTitle = signal.WindowTitle.Substring(0, 44) + "...";
            }

            _activeGameNames[signal.Pid] = signal.ProcessName;

            // --- A. PERMANENT GLUE ---
            var existingLock = _ctx.Settings.Trackers.FirstOrDefault(t => t.ActiveGamePid == signal.Pid);
            if (existingLock != null)
            {
                if (existingLock.LauncherPids.Count == 0) { existingLock.ActiveGamePid = 0; return; }
                ApplyGameActiveState(existingLock, signal, activeGames);
                return;
            }

            // --- B. ATTRIBUTION ---
            string attributedStoreName = Attribution.DetermineStoreOwner(signal, _ctx.Settings.Trackers);
            if (!string.IsNullOrEmpty(attributedStoreName) && attributedStoreName != "Unknown")
            {
                var attributedStore = _ctx.Settings.Trackers.FirstOrDefault(t =>
                    string.Equals(t.DisplayName, attributedStoreName, StringComparison.OrdinalIgnoreCase) ||
                    (attributedStoreName == "EA Desktop" && t.DisplayName == "EA"));

                if (attributedStore != null && attributedStore.LauncherPids.Count > 0)
                {
                    // THE PING-PONG SHIELD: If this store is already tracking a valid game, ignore secondary windows
                    if (attributedStore.ActiveGamePid > 0 && attributedStore.ActiveGamePid != signal.Pid)
                    {
                        if (cache.ContainsKey(attributedStore.ActiveGamePid)) return;
                    }

                    // Action: Delay naming logic by 5 ticks to allow window title to settle
                    if ((DateTime.Now - attributedStore.LastLaunchTime).TotalSeconds > 60)
                    {
                        _namingDelayCounters[attributedStore.DisplayName] = 5;
                    }

                    // Process Delayed Naming
                    if (_namingDelayCounters.ContainsKey(attributedStore.DisplayName))
                    {
                        _namingDelayCounters[attributedStore.DisplayName]--;
                        if (_namingDelayCounters[attributedStore.DisplayName] <= 0)
                        {
                            _ctx.AppendToHistory($"Target identified: {signal.WindowTitle}.");

                            if (!string.IsNullOrEmpty(signal.WindowTitle))
                            {
                                if (attributedStore.DetectedGames == null) attributedStore.DetectedGames = new Dictionary<string, int>();
                                if (!attributedStore.DetectedGames.ContainsKey(signal.WindowTitle)) attributedStore.DetectedGames[signal.WindowTitle] = 0;
                                attributedStore.DetectedGames[signal.WindowTitle]++;
                            }
                            attributedStore.LaunchCount++;
                            attributedStore.LastLaunchTime = DateTime.Now;
                            _ctx.SaveSettings();
                            _namingDelayCounters.Remove(attributedStore.DisplayName);
                        }
                    }

                    // STATE PROTECTION: Apply state lock BEFORE logging
                    attributedStore.CurrentStatus = "LOCKED";
                    ApplyGameActiveState(attributedStore, signal, activeGames);

                    // TELEMETRY CHECKPOINT: State Shift
                    _ctx.AppendToHistory($"[State] {attributedStore.DisplayName} locked: Game running.");
                }
            }
        }

        private void ApplyGameActiveState(StoreTracker store, GameSignal signal, HashSet<string> activeGames)
        {
            store.CurrentStatus = (store.CurrentStatus == "GAME XBOX") ? "GAME XBOX" : "GAME ACTIVE";
            store.TimeLeft = store.MaxTime;
            store.ActiveGamePid = signal.Pid;
            store.GameActiveGraceTicks = 15;
            activeGames.Add(store.DisplayName);
        }

        private void SyncStoreVitality(StoreTracker store, Dictionary<int, Process> cache)
        {
            if (store.LauncherPids.Count == 0)
            {
                store.IsPrimaryAppRunning = false;
                store.ActiveGamePid = 0;
                return;
            }

            bool uiFound = false;
            foreach (var pid in store.LauncherPids)
            {
                if (cache.TryGetValue(pid, out Process p))
                {
                    string processName;
                    try
                    {
                        processName = p.ProcessName;
                    }
                    catch (ArgumentException) { continue; }
                    catch (InvalidOperationException) { continue; }

                    if (processName.Equals(store.ProcessName, StringComparison.OrdinalIgnoreCase) ||
                       (store.DisplayName == "Xbox" && (processName.Equals("Xbox", StringComparison.OrdinalIgnoreCase) ||
                                                        processName.Equals("XboxPcTray", StringComparison.OrdinalIgnoreCase))))
                    {
                        uiFound = true;
                        break;
                    }
                }
            }
            store.IsPrimaryAppRunning = uiFound;
        }

        private bool CheckProtections(StoreTracker store, HashSet<string> activeGames, Dictionary<int, Process> cache)
        {
            if (store.LauncherPids.Count == 0)
            {
                UpdateStateWithHysteresis(store, "OFFLINE", 0, true);
                return true;
            }

            // Block "Ghost" minimized states for services only
            if (!store.IsPrimaryAppRunning && store.ActiveGamePid == 0 && store.CurrentStatus != "Xbox Buddy")
            {
                UpdateStateWithHysteresis(store, "OFFLINE", 0, true);
                return true;
            }

            // --- THE PERMANENT GLUE & TOTAL SCRUB ---
            if (store.ActiveGamePid > 0)
            {
                bool isAlive = cache.ContainsKey(store.ActiveGamePid);

                // THE SANDBOX FALLBACK: GDK/UWP games like 'Mio' hide from GetProcesses().
                if (!isAlive)
                {
                    try
                    {
                        // Poke the PID directly. 
                        using (var p = Process.GetProcessById(store.ActiveGamePid)) { isAlive = true; }
                    }
                    catch (ArgumentException)
                    {
                        isAlive = false; // ArgumentException means it's truly dead and gone.
                    }
                    catch (Exception)
                    {
                        // Win32Exception or Access Denied means it IS alive, just heavily armored!
                        isAlive = true;
                    }
                }

                if (isAlive)
                {
                    // Still alive: Keep the status
                    string status = (store.CurrentStatus == "GAME XBOX") ? "GAME XBOX" : "GAME ACTIVE";
                    LogForensics(store, cache); // This will safely ignore invisible games without crashing
                    UpdateStateWithHysteresis(store, status, 0, true);
                    activeGames.Add(store.DisplayName);
                    return true;
                }
                else
                {
                    // GAME EXIT DETECTED: Scrub this PID from EVERY store immediately
                    int deadPid = store.ActiveGamePid;

                    // Stand down the Sentry
                    if (_overwatchCts != null)
                    {
                        _overwatchCts.Cancel();
                        _overwatchCts.Dispose();
                        _overwatchCts = null;
                    }

                    if (_activeGameNames.ContainsKey(deadPid))
                    {
                        // TELEMETRY CHECKPOINT: Process Exit
                        _ctx.AppendToHistory($"[Monitor] Game exited: {_activeGameNames[deadPid]}.");
                        _activeGameNames.Remove(deadPid);
                    }

                    store.ActiveGamePid = 0;
                    store.GameActiveGraceTicks = 0;

                    if (store.LauncherPids.Count == 0)
                    {
                        store.CurrentStatus = "OFFLINE";
                    }
                    else
                    {
                        store.CurrentStatus = "MIN";
                        ResolveXboxBuddyState(store);
                        _ctx.AppendToHistory($"[State] {store.DisplayName} clear for management.");
                    }
                    // Console.WriteLine($"[RELEASE] Scrubbing PID {deadPid} from {store.DisplayName}");
                }
            }

            if (activeGames.Contains(store.DisplayName))
            {
                UpdateStateWithHysteresis(store, "GAME ACTIVE", 0, true);
                return true;
            }

            if (store.CurrentStatus == "Xbox Buddy")
            {
                UpdateStateWithHysteresis(store, "Xbox Buddy", 0, true);

                if (store.GameActiveGraceTicks > 0)
                {
                    store.GameActiveGraceTicks--;
                    UpdateStateWithHysteresis(store, "GAME ACTIVE", 0, true);
                    return true;
                }

                if (WindowScanner.IsStoreOnScreen(store.LauncherPids, store.DisplayName))
                {
                    UpdateStateWithHysteresis(store, "ACTIVE", 0, true);
                    return true;
                }

                return false;
            }

            return false;
        }

        private void UpdateStateWithHysteresis(StoreTracker store, string newStatus, double elapsedSec, bool force = false)
        {
            if (force || store.CurrentStatus == newStatus)
            {
                store.CurrentStatus = newStatus;
                if (newStatus != "MIN" && newStatus != "MIN BUSY" && newStatus != "OFFLINE")
                    store.TimeLeft = store.MaxTime;
            }
            else
            {
                if (store.PendingStatus == newStatus)
                {
                    if (++store.PendingTicks >= 3)
                    {
                        store.CurrentStatus = newStatus;
                        if (newStatus == "MIN" || newStatus == "MIN BUSY") store.TimeLeft = store.MaxTime;
                        store.PendingStatus = null;
                        store.PendingTicks = 0;
                    }
                }
                else
                {
                    store.PendingStatus = newStatus;
                    store.PendingTicks = 1;
                }
            }

            // --- THE FIX: Timer Management & The Warden's Audit ---
            if (store.CurrentStatus == "MIN" || store.CurrentStatus == "MIN BUSY")
            {
                if (store.ManualOverride)
                {
                    store.TimeLeft = store.MaxTime;
                    store.AccumulatedDiskBytes = 0;
                    store.AccumulationTicks = 0;
                }
                else if (store.TimeLeft > 0)
                {
                    // The timer ticks down uninterrupted
                    double decrement = (elapsedSec > 0) ? elapsedSec : 1.0;
                    store.TimeLeft = (int)Math.Max(0, Math.Floor((store.TimeLeft - decrement) + 0.9));

                    // Secretly accumulate the I/O data during the countdown
                    store.AccumulatedDiskBytes += store.DeltaDiskBytes;
                    store.AccumulationTicks++;
                }

                // The Warden's Audit (Triggered only at Zero)
                if (store.TimeLeft <= 0 && !store.ManualOverride)
                {
                    long averageIo = store.AccumulationTicks > 0 ? (store.AccumulatedDiskBytes / store.AccumulationTicks) : 0;
                    long ioThreshold = 512000; // 500 KB/s average over the entire grace period

                    long averageKb = averageIo / 1024;
                    long thresholdKb = ioThreshold / 1024;

                    if (averageIo > ioThreshold)
                    {
                        // Store is genuinely busy downloading/updating. Grant a pardon and reset.
                        _ctx.AppendToHistory($"[Warden] Timer reset. {store.DisplayName} is busy. Avg I/O > threshold over {store.MaxTime}s).");
                        // Compressed: Removed heavy math readout from real-time view
                        _ctx.AppendToHistory($"[Warden] {store.DisplayName} busy: Resetting timer.");
                        store.TimeLeft = store.MaxTime;
                        store.AccumulatedDiskBytes = 0;
                        store.AccumulationTicks = 0;
                    }
                    else
                    {
                        // Just telemetry slop. Drop the axe.
                        _ctx.AppendToHistory($"[Warden] {store.DisplayName} idle {store.MaxTime}s. Terminating.");

                        var pidsToKill = store.LauncherPids.ToList();
                        Task.Run(() =>
                        {
                            Executioner.TerminateStore(store, pidsToKill, (msg) =>
                            {
                                _ctx.AppendToHistory(msg);
                            });
                        }).ContinueWith(t =>
                        {
                            if (store.ActiveGamePid > 0)
                            {
                                _ctx.DropKnownGamePid(store.ActiveGamePid);
                            }
                            store.CurrentStatus = "OFFLINE";
                            store.AccumulatedDiskBytes = 0;
                            store.AccumulationTicks = 0;
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
            }
        }

        private async Task RunTacticalOverwatchAsync(StoreTracker activeStore, CancellationToken token)
        {
            // The Sentry Loop: Pulses every 5 seconds while the game is locked
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, token);

                    if (!_ctx.Settings.CloseUnnecessaryStoresOnLaunch) continue; // Respect user consent dynamically

                    // Lightweight scan for resurrected stores (excluding the active/attributed store)
                    var rogueStoresDetected = false;
                    foreach (var tracker in _ctx.Settings.Trackers.Where(t => t != activeStore))
                    {
                        // Dependency Exception: Protect Battle.net if the active store is Xbox
                        if (activeStore.DisplayName == "Xbox" && tracker.DisplayName == "Battle.net")
                            continue;

                        // Check if the rogue store's executable is currently running
                        var rogueProcesses = Process.GetProcessesByName(tracker.ProcessName);
                        if (rogueProcesses.Any())
                        {
                            rogueStoresDetected = true;
                            _ctx.AppendToHistory($"[Sentry] Rogue process detected: {tracker.DisplayName}. Executing.");
                            // Trigger the existing Executioner/Warden termination logic here
                            var pidsToKill = rogueProcesses.Select(p => p.Id).ToList();
                            Executioner.TerminateStore(tracker, pidsToKill, (msg) => _ctx.AppendToHistory(msg));
                        }
                    }

                    if (rogueStoresDetected)
                    {
                        _ctx.AppendToHistory("[Sentry] Perimeter secured. Resuming overwatch.");
                    }
                }
                catch (TaskCanceledException)
                {
                    // The game ended, Sentry thread cleanly aborted
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[GSW Sentry Exception] {ex.Message}");
                }
            }
        }

        private void LogForensics(StoreTracker store, Dictionary<int, Process> cache)
        {
            if (!_forensicCounters.ContainsKey(store.DisplayName)) _forensicCounters[store.DisplayName] = 0;

            _forensicCounters[store.DisplayName]++;

            if (_forensicCounters[store.DisplayName] >= 5)
            {
                _forensicCounters[store.DisplayName] = 0;
                string safeName = "Unknown";

                // ZERO SLOP: Only query the safe cache. Do not hit the OS again.
                if (cache.TryGetValue(store.ActiveGamePid, out Process p))
                {
                    try { safeName = p.ProcessName; }
                    catch { return; } // Silent skip if property fetch fails
                }
                else { return; } // Process has already died since the snapshot

                Console.WriteLine($"[Forensics] Store: {store.DisplayName} | Tracking Game PID: {store.ActiveGamePid} | Process Name: {safeName}");
            }
        }
    }
}