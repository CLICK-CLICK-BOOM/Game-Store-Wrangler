//<summary>[DO NOT REMOVE]WindowScanner.cs</summary>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GSWEngine
{
    public static class WindowScanner
    {
        // --- WIN32 API ---
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
        [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private const int DWMWA_CLOAKED = 14;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        public static bool IsStoreOnScreen(List<int> familyPids, string displayName)
        {
            bool found = false;
            HashSet<uint> pids = new HashSet<uint>();
            foreach (int id in familyPids) pids.Add((uint)id);

            EnumWindows((hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
                    if (cloaked == 0)
                    {
                        GetWindowThreadProcessId(hWnd, out uint windowPid);
                        bool isTargetWindow = pids.Contains(windowPid);

                        StringBuilder sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, 256);
                        string title = sb.ToString();

                        // --- THE STEAM, XBOX & RIOT OVERRIDE ---
                        if (!isTargetWindow)
                        {
                            if (displayName == "Xbox" && title.IndexOf("Xbox", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                StringBuilder className = new StringBuilder(256);
                                GetClassName(hWnd, className, 256);
                                if (className.ToString() == "ApplicationFrameWindow")
                                {
                                    isTargetWindow = true;
                                }
                            }
                            else if (displayName == "Steam" && title.Equals("Steam", StringComparison.OrdinalIgnoreCase))
                            {
                                isTargetWindow = true;
                            }
                            else if (displayName.IndexOf("Riot", StringComparison.OrdinalIgnoreCase) >= 0 && title.IndexOf("Riot Client", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isTargetWindow = true;
                            }
                        }

                        if (isTargetWindow)
                        {
                            if (displayName == "EA Desktop" && string.IsNullOrWhiteSpace(title)) return true; // Skip empty EA rendering canvases

                            GetWindowRect(hWnd, out RECT rect);
                            int width = rect.Right - rect.Left;
                            int height = rect.Bottom - rect.Top;

                            // Filter out small tooltips or background ghost windows. 
                            // (Title check removed to allow Riot's blank CEF rendering window to pass)
                            if (width > 160 && height > 160 && rect.Left > -32000)
                            {
                                found = true;
                                return false; // Stop enumerating
                            }
                        }
                    }
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        public static bool IsProcessVisible(List<int> pids, string displayName)
        {
            // We use the advanced store-on-screen logic to ensure accuracy
            return IsStoreOnScreen(pids, displayName);
        }
    }
}