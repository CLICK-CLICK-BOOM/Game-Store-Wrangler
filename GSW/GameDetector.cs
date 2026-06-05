//<summary>[DO NOT REMOVE]GameDetector.cs</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GSWEngine
{
    public class GameSignal
    {
        public int Pid { get; set; }
        public string ProcessName { get; set; }
        public string WindowTitle { get; set; }
        public string ExePath { get; set; } // Raw data passed to Attribution
        public bool IsAppX { get; set; } // Flag to tell Attribution it's a UWP container
    }

    public static class GameDetector
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private static string GetExecutablePathSafe(int pid)
        {
            // Primary: Win32 API (Bypasses 32/64-bit boundaries and standard Access Denied errors)
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    StringBuilder sb = new StringBuilder(1024);
                    uint size = (uint)sb.Capacity;
                    if (QueryFullProcessImageName(hProcess, 0, sb, ref size)) return sb.ToString();
                }
                finally { CloseHandle(hProcess); }
            }

            return "";
        }

        private static int GetUwpRealPid(IntPtr frameHwnd)
        {
            int realPid = 0;
            EnumChildWindows(frameHwnd, (childHwnd, lParam) =>
            {
                StringBuilder className = new StringBuilder(256);
                GetClassName(childHwnd, className, 256);
                if (className.ToString() == "Windows.UI.Core.CoreWindow")
                {
                    GetWindowThreadProcessId(childHwnd, out realPid);
                    return false; // Stop enumeration once found
                }
                return true; // Continue searching
            }, IntPtr.Zero);
            return realPid;
        }

        private static Queue<IntPtr> _recentFocusStack = new Queue<IntPtr>();

        private static DateTime _lastCheckTime = DateTime.MinValue;
        private static List<GameSignal> _cachedSignals = new List<GameSignal>();
        private static int _lastVerifiedGamePid = 0;
        private static GameSignal _lastVerifiedGameSignal = null;

        private static readonly HashSet<string> _manualBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // --- System & Essentials ---
            "StartMenuExperienceHost", "explorer", "Taskmgr", "SearchHost", "ShellExperienceHost",
            "ApplicationFrameHost", "SystemSettings", "snippingtool", "ctfmon", "conhost", "svchost",
            "CalculatorApp", "Microsoft.WindowsCalculator", "Microsoft.ZuneVideo", "Microsoft.ZuneMusic", "Microsoft.Notes",

            // --- Adobe Creative Cloud ---
            "photoshop", "adobe", "creative cloud", "premiere", "afterfx", "acrobat",
            "illustrator", "indesign", "audition", "bridge", "lightroom", "charactanimator",

            // --- 3D, CAD & Office ---
            "blender", "autocad", "revit", "fusion360", "3dsmax", "maya", "sketchup",
            "solidworks", "keyshot", "zbrush", "winword", "excel", "powerpnt", "outlook",

            // --- Development & Engineering ---
            "node", "npm", "docker", "devenv", "code", "pycharm", "intellij", "webstorm",
            "sourcetree", "postman", "wireshark", "fiddler",

            // --- Communication & Web ---
            "teams", "slack", "zoom", "webex", "discord", "thunderbird", "chrome", "msedge", "firefox", "spotify", "whatsapp.root",
            
            // --- UI, Overlays & Services ---
            "gamingservicesui", "GameBar", "GameBarFTServer", "XboxGameBarWidgets", "XboxGameBar", "TextInputHost", "XboxPcApp", "gamingservices", "DCv2", "nvcplui",

            // --- Unity & Crash Handlers ---
            "UnityCrashHandler64", "UnityCrashHandler32", "crashpad_handler",

            // --- Self-Shield ---
            "GSW", "GSWEngine", "GameStoreWrangler"
        };

        public static List<GameSignal> GetActiveGames(List<StoreTracker> trackers = null)
        {
            IntPtr foregroundWin = GetForegroundWindow();
            if (foregroundWin != IntPtr.Zero)
            {
                if (_recentFocusStack.Count == 0 || _recentFocusStack.LastOrDefault() != foregroundWin)
                {
                    _recentFocusStack.Enqueue(foregroundWin);
                    if (_recentFocusStack.Count > 5) _recentFocusStack.Dequeue();
                }
            }

            // THE GOVERNOR: Throttle checks to prevent CPU burn
            if ((DateTime.Now - _lastCheckTime).TotalMilliseconds < 800) return _cachedSignals;
            _lastCheckTime = DateTime.Now;

            var signals = new List<GameSignal>();
            var processedPids = new HashSet<int>();

            foreach (var hwnd in _recentFocusStack.ToList())
            {
                GetWindowThreadProcessId(hwnd, out int pid);
                if (pid <= 0 || !processedPids.Add(pid)) continue;

                if (pid == Process.GetCurrentProcess().Id) continue;

                // --- LAUNCHER EXCLUSION: Shield Store Processes from Game Scoring ---
                if (trackers != null)
                {
                    var activeStore = trackers.FirstOrDefault(t => t.LauncherPids != null && t.LauncherPids.Contains(pid));
                    if (activeStore != null) continue;
                }

                bool isUwpContainer = false;

                // --- LAYER 1: THE UWP PIERCER ---
                try
                {
                    using (var tempP = Process.GetProcessById(pid))
                    {
                        string tempProcessName;
                        try { tempProcessName = tempP.ProcessName; }
                        catch (ArgumentException) { continue; }
                        catch (InvalidOperationException) { continue; }

                        if (tempProcessName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                        {
                            int uwpPid = GetUwpRealPid(hwnd);
                            if (uwpPid > 0)
                            {
                                pid = uwpPid;
                                processedPids.Add(pid);
                                isUwpContainer = true; // Flag for the UWP Passport

                                // 1. Initialize with safe defaults immediately
                                string windowTitle = string.Empty;
                                string finalProcessName = "UWP Game";
                                string exePath = GetExecutablePathSafe(pid);

                                try
                                {
                                    using (var realP = Process.GetProcessById(pid))
                                    {
                                        finalProcessName = realP.ProcessName;
                                        windowTitle = realP.MainWindowTitle;
                                    }
                                }
                                catch (ArgumentException)
                                {
                                    // ZERO SLOP: Process is dead. Do not resurrect.
                                    continue;
                                }
                                catch (Exception)
                                {
                                    // Access Denied / Shielded. Process is alive. Keep default "UWP Game".
                                }

                                // THE BLACKLIST FIX: Enforce the blacklist BEFORE adding the signal
                                if (_manualBlacklist.Contains(finalProcessName)) continue;

                                signals.Add(new GameSignal
                                {
                                    Pid = pid,
                                    ProcessName = finalProcessName,
                                    WindowTitle = windowTitle,
                                    ExePath = exePath,
                                    IsAppX = true
                                });

                                continue;
                            }
                        }
                    }
                }
                catch { }

                if (IsBlacklistedClass(hwnd)) continue;

                // --- LAYER 2: THE CASCADE EVALUATION ---
                try
                {
                    using (var p = Process.GetProcessById(pid))
                    {
                        string processName = "Unknown";
                        string windowTitle = string.Empty;
                        try
                        {
                            if (p.HasExited) continue;
                            processName = p.ProcessName;
                            windowTitle = p.MainWindowTitle;
                        }
                        catch (ArgumentException)
                        {
                            // ZERO SLOP: Process is dead.
                            continue;
                        }
                        catch (Exception)
                        {
                            // Catch InvalidOperation or Win32Exception (Access Denied)
                            if (isUwpContainer) processName = "UWP Game";
                            else continue;
                        }

                        // --- STAGE 1: THE FIREWALL (Instant Rejection) ---
                        if (_manualBlacklist.Contains(processName)) continue;

                        bool isGame = false;

                        // --- STAGE 2: THE SNIPER (Instant Confirmation) ---
                        string exePath = GetExecutablePathSafe(pid);

                        if (isUwpContainer || HasXboxSignature(exePath) || IsSteamRegistryActive(p))
                        {
                            isGame = true;
                        }
                        else
                        {
                            // --- STAGE 3: THE JURY (Weighted Scoring) ---
                            int score = 0;
                            int threshold = 60;

                            if (!string.IsNullOrEmpty(exePath))
                            {
                                string[] segments = exePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                                // Added "common" as it is the default Steam game installation folder
                                string[] targetFolders = { "Games", "WindowsApps", "Xboxgames", "xbox", "Steam", "Epic", "Riot", "Ubisoft", "GOG", "Origin", "Gaming", "common" };
                                if (segments.Any(s => targetFolders.Any(tf => s.IndexOf(tf, StringComparison.OrdinalIgnoreCase) >= 0)))
                                {
                                    score += 70;
                                }
                            }

                            if (IsRendering3D(p, isUwpContainer))
                            {
                                score += 30;
                            }

                            if (score >= threshold)
                            {
                                isGame = true;
                            }
                        }

                        if (isGame)
                        {
                            signals.Add(new GameSignal
                            {
                                Pid = pid,
                                ProcessName = processName,
                                WindowTitle = windowTitle,
                                ExePath = exePath,
                                IsAppX = isUwpContainer || HasXboxSignature(exePath)
                            });
                        }
                    }
                }
                catch { /* Process exited or access denied on non-game */ }
            }

            // --- UWP & SANDBOX PERSISTENCE PRESERVATION ---
            if (signals.Any())
            {
                // Capture the primary active game signal for backup tracking
                var primaryGame = signals.First();
                _lastVerifiedGamePid = primaryGame.Pid;
                _lastVerifiedGameSignal = primaryGame;
            }
            else if (_lastVerifiedGamePid > 0 && _lastVerifiedGameSignal != null)
            {
                // The focus stack is empty, check if our last tracked game is still physically running
                bool gameStillAlive = false;
                try
                {
                    using (var p = Process.GetProcessById(_lastVerifiedGamePid))
                    {
                        if (!p.HasExited)
                        {
                            gameStillAlive = true;
                        }
                    }
                }
                catch
                {
                    // If access is denied, the process is armored but alive
                    if (System.Runtime.InteropServices.Marshal.GetLastWin32Error() != 0)
                    {
                        gameStillAlive = _lastVerifiedGameSignal.IsAppX;
                    }
                }

                if (gameStillAlive)
                {
                    // Re-inject the verified signal to bridge across temporary window focus drops
                    signals.Add(new GameSignal
                    {
                        Pid = _lastVerifiedGameSignal.Pid,
                        ProcessName = _lastVerifiedGameSignal.ProcessName,
                        WindowTitle = _lastVerifiedGameSignal.WindowTitle,
                        ExePath = _lastVerifiedGameSignal.ExePath,
                        IsAppX = _lastVerifiedGameSignal.IsAppX
                    });
                }
                else
                {
                    // Process is truly dead, clear the tracking lock
                    _lastVerifiedGamePid = 0;
                    _lastVerifiedGameSignal = null;
                }
            }

            _cachedSignals = signals;
            return signals;
        }

        private static bool IsBlacklistedClass(IntPtr hwnd)
        {
            StringBuilder className = new StringBuilder(256);
            if (GetClassName(hwnd, className, 256) > 0)
            {
                string cName = className.ToString();
                if (cName.Equals("Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase) ||
                    cName.Equals("MozillaWindowClass", StringComparison.OrdinalIgnoreCase) ||
                    cName.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                    cName.Equals("Notepad", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsRendering3D(Process p, bool isUwpContainer)
        {
            try
            {
                foreach (ProcessModule m in p.Modules)
                {
                    string mName = m.ModuleName.ToLowerInvariant();
                    if (mName == "d3d11.dll" || mName == "d3d12.dll" ||
                        mName == "vulkan-1.dll" || mName == "opengl32.dll")
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                if (isUwpContainer) return true;
            }
            return false;
        }

        private static bool HasXboxSignature(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return false;

            return exePath.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   exePath.IndexOf("xbox", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSteamRegistryActive(Process p)
        {
            try
            {
                if (p.ProcessName.Equals("Steam", StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.Equals("steamwebhelper", StringComparison.OrdinalIgnoreCase)) return false;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (key != null && key.GetValue("RunningAppID") is int appId && appId > 0)
                    {
                        foreach (ProcessModule m in p.Modules)
                        {
                            string mName = m.ModuleName.ToLowerInvariant();
                            if (mName.Contains("opengl32.dll") || mName.Contains("d3d9.dll") ||
                                mName.Contains("d3d11.dll") || mName.Contains("d3d12.dll")) return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }
    }
}