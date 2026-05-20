//<summary>[DO NOT REMOVE]Form1.Theme.cs</summary>

using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace GSWEngine
{
    public partial class StatsForm
    {
        private readonly int[] _timerPresets = { 90, 120, 180 };


        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        static extern bool HideCaret(IntPtr hWnd);

        // --- WINDOWS INTEROP & OS INTEGRATION ---
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            const int WM_NCACTIVATE = 0x0086;
            const int WM_ACTIVATEAPP = 0x001C;

            if (m.Msg == WM_NCACTIVATE || m.Msg == WM_ACTIVATEAPP)
            {
                bool active = (m.WParam != IntPtr.Zero);
                UpdateTitleBarOnly(active);
            }
        }

        private void UpdateTitleBarOnly(bool active)
        {
            if (this.Handle != IntPtr.Zero && Environment.OSVersion.Version.Major >= 10 && _currentColors != null)
            {
                try
                {
                    Color target = active ? _currentColors.TitleBarColor : _currentColors.TitleBarColorInactive;
                    int col = ColorTranslator.ToWin32(target);
                    DwmSetWindowAttribute(this.Handle, 35, ref col, sizeof(int));

                    Color textTarget = active ? _currentColors.TitleFg : Color.FromArgb(100, _currentColors.TitleFg.R, _currentColors.TitleFg.G, _currentColors.TitleFg.B);
                    int textCol = ColorTranslator.ToWin32(textTarget);
                    DwmSetWindowAttribute(this.Handle, 36, ref textCol, sizeof(int));
                }
                catch { }
            }
        }

        // --- UI COMPONENT THEMING ---
        private void UpdateOnTopVisuals()
        {
            if (chkAlwaysOnTop == null || _currentColors == null) return;

            chkAlwaysOnTop.Text = this.TopMost ? "On Top: YES" : "On Top: NO";
            Color baseColor = this.TopMost ? _currentColors.OnTopActiveBg : _currentColors.GeneralBtnBg;

            chkAlwaysOnTop.BackColor = baseColor;
            chkAlwaysOnTop.ForeColor = _currentColors.GeneralBtnFg;
            chkAlwaysOnTop.FlatAppearance.BorderColor = _currentColors.GeneralBtnBorder;
            chkAlwaysOnTop.FlatAppearance.MouseOverBackColor = _currentColors.GeneralBtnHover;
            chkAlwaysOnTop.FlatAppearance.MouseDownBackColor = _currentColors.OnTopActiveBg;
            chkAlwaysOnTop.TabStop = false;
        }

        private void UpdateCloseUnnecessaryVisuals()
        {
            if (chkCloseUnnecessary == null || _currentColors == null) return;

            bool isActive = _ctx.Settings.CloseUnnecessaryStoresOnLaunch;
            chkCloseUnnecessary.Text = isActive ? "Close unnecessary stores when a game runs: YES" : "Close unnecessary stores when a game runs: NO";

            Color baseColor = isActive ? _currentColors.OnTopActiveBg : _currentColors.GeneralBtnBg;

            chkCloseUnnecessary.BackColor = baseColor;
            chkCloseUnnecessary.ForeColor = _currentColors.GeneralBtnFg;
            chkCloseUnnecessary.FlatAppearance.BorderColor = _currentColors.GeneralBtnBorder;
            chkCloseUnnecessary.FlatAppearance.MouseOverBackColor = _currentColors.GeneralBtnHover;
            chkCloseUnnecessary.FlatAppearance.MouseDownBackColor = _currentColors.OnTopActiveBg;
            chkCloseUnnecessary.TabStop = false;
        }

        private void CycleTimerPresets()
        {
            int currentIndex = Array.IndexOf(_timerPresets, _ctx.GracePeriod);
            int nextIndex = (currentIndex + 1) % _timerPresets.Length;
            int newValue = _timerPresets[nextIndex];

            _ctx.GracePeriod = newValue;
            numTimer.Text = newValue.ToString();

            foreach (var t in _ctx.trackers)
            {
                t.MaxTime = newValue;
                // Prevent false flashing on OFFLINE/ACTIVE stores by syncing TimeLeft
                if (t.CurrentStatus != "MIN" && t.CurrentStatus != "MIN BUSY")
                {
                    t.TimeLeft = newValue;
                }
            }

            _ctx.SaveSettings();
        }

        // --- CORE DRAWING & GRAPHICS ---
        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            if (_ctx.trackers == null || _currentColors == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using (Font f = new Font("Segoe UI", 8, FontStyle.Bold))
            {
                int rowHeight = 25;
                int startY = 30;
                var visibleTrackers = _ctx.trackers.Take(12).ToList();

                if (_isSortMode)
                {
                    GridPainter.PaintSortColumnHighlight(g, startY, rowHeight, visibleTrackers.Count, _currentColors.GeneralBtnBg);
                }

                for (int i = 0; i < visibleTrackers.Count; i++)
                {
                    var t = visibleTrackers[i];
                    int y = startY + (i * rowHeight);

                    // This draws the background/tactical effects
                    GridPainter.PaintTacticalOverlay(g, y, rowHeight, canvas.Width, t, _scanOffset, _currentColors.GeneralBtnHover);

                    using (Brush storeBrush = new SolidBrush(_currentColors.StoreColFg))
                    using (Brush actionBrush = new SolidBrush(_currentColors.ActionTextFg))
                    using (Brush keepBrush = new SolidBrush(t.ManualOverride ? _currentColors.KeepOn : _currentColors.KeepOff))
                    {
                        // Simple state check: Is it offline? (Case-insensitive)
                        bool isOffline = string.Equals(t.CurrentStatus, "OFFLINE", StringComparison.OrdinalIgnoreCase);

                        Color statColor = isOffline ? _currentColors.Warning : _currentColors.Tick;
                        if (string.Equals(t.CurrentStatus, "MIN BUSY", StringComparison.OrdinalIgnoreCase))
                            statColor = _currentColors.StoreColFg;

                        using (Brush statusBrush = new SolidBrush(statColor))
                        {
                            g.DrawString(t.DisplayName, f, storeBrush, 10, y);

                            g.DrawString("[ X ]", f, actionBrush, 120, y);
                            g.DrawString(t.CurrentStatus, f, statusBrush, 165, y);
                            g.DrawString(t.CpuDisplay, f, Brushes.White, 255, y);
                            g.DrawString(t.DiskDisplay, f, Brushes.White, 305, y);

                            g.DrawString("[", f, Brushes.Gray, 365, y);
                            g.DrawString(t.ManualOverride ? "Y" : "N", f, keepBrush, 372, y);
                            g.DrawString("]", f, Brushes.Gray, 384, y);

                            string timeString = (t.CurrentStatus == "GAME CLOSE") ? $"{(int)Math.Max(0, t.TimeLeft)}s" : t.GraceDisplay;
                            int secondsLeft = (int)Math.Max(0, t.TimeLeft);
                            timeString = (t.CurrentStatus == "GAME CLOSE") ? $"{secondsLeft / 60:D2}:{secondsLeft % 60:D2}" : t.GraceDisplay;
                            g.DrawString(timeString, f, Brushes.White, 410, y);

                            // --- THE FINAL STOP BUTTON LOGIC ---
                            if (!t.ManualOverride)
                            {
                                if (!isOffline)
                                {
                                    // Store is active: Bright button
                                    g.DrawString("[ X ]", f, actionBrush, 453, y);
                                }
                                else
                                {
                                    // Store is offline: Dimmed button (Alpha 100)
                                    using (Brush dimBrush = new SolidBrush(Color.FromArgb(100, _currentColors.ActionTextFg)))
                                    {
                                        g.DrawString("[ X ]", f, dimBrush, 453, y);
                                    }
                                }
                            }
                        }
                    }
                }

                // Headers
                using (Brush hb = new SolidBrush(_currentColors.GridFg))
                {
                    string[] headers = { "STORE", "REMOVE", "STATUS", "CPU", "I/O", "KEEP", "TIME", "CLOSE" };
                    int[] xPos = { 10, 108, 165, 255, 305, 363, 404, 448 };
                    for (int i = 0; i < headers.Length; i++) g.DrawString(headers[i], f, hb, xPos[i], 5);

                    GridPainter.PaintSortIcons(g, f, _currentColors.GeneralBtnBorder, _currentColors.RequestorHighlightBg, _isSortMode, msg => _ctx.ExecutionHistory.Add(msg));

                    if (_isSortMode)
                    {
                        GridPainter.PaintSortStickers(g, startY, rowHeight, visibleTrackers, _sortQueue, _currentColors.Warning, _currentColors.Tick);
                    }
                }
            }
        }
        public void RefreshLog()
        {
            if (logBox == null || _ctx.ExecutionHistory == null) return;

            if (Application.OpenForms.OfType<LogTerminalForm>().Any())
            {
                // --- POP-OUT IS ACTIVE ---

                // 1. Clean string, no hardcoded spaces or newlines
                logBox.Text = ">>> EXPANDED LOG ACTIVE <<<";

                // 2. Force horizontal centering
                logBox.TextAlign = ContentAlignment.TopCenter;

                // 3. THE VERTICAL DIAL: Adjust the '20' below to move the text up or down by exact pixels
                logBox.Padding = new Padding(0, 22, 0, 0);
            }
            else
            {
                // --- POP-OUT IS CLOSED ---

                // Restore standard matrix alignment and padding
                logBox.TextAlign = ContentAlignment.TopLeft;
                logBox.Padding = new Padding(5, 0, 5, 5);

                try
                {
                    // ToList() takes a safe memory snapshot to avoid "Collection Modified" crashes
                    var snapshot = _ctx.ExecutionHistory.ToList();

                    logBox.Text = snapshot.Any()
                        ? InternalFunctions.FormatExecutionHistory(snapshot, 6)
                        : "TELEMETRY OUTPUT";
                }
                catch { /* Absorb the race condition. The 50ms timer will redraw it perfectly on the next tick. */ }
            }
        }

        // --- THEME APPLICATION ENGINE ---
        public void ApplyTheme()
        {
            if (_currentColors == null)
                _currentColors = ThemeManager.GetTheme(_ctx.CurrentTheme) ?? ThemeManager.GetTheme(0);

            UpdateTitleBarOnly(true);
            if (canvas == null || logBox == null) return;

            if (!string.IsNullOrEmpty(_currentColors.BgFile))
            {
                using (var stream = InternalFunctions.GetResource($"skins\\{_currentColors.BgFile}"))
                {
                    if (stream != null)
                    {
                        if (this.BackgroundImage != null) { var oldImg = this.BackgroundImage; this.BackgroundImage = null; oldImg.Dispose(); }
                        this.BackgroundImage = Image.FromStream(stream);
                        this.BackgroundImageLayout = ImageLayout.Stretch;
                    }
                    else if (this.BackgroundImage != null) { this.BackgroundImage.Dispose(); this.BackgroundImage = null; }
                }
            }
            else if (this.BackgroundImage != null) { this.BackgroundImage.Dispose(); this.BackgroundImage = null; }

            lblHeader.ForeColor = lblLogHeader.ForeColor = _currentColors.TitleFg;
            lblAward.ForeColor = _currentColors.TitleFg;
            logBox.ForeColor = _currentColors.LogFg;

            if (pnlTimerPill != null)
            {
                pnlTimerPill.BackColor = _currentColors.GeneralBtnBg;
                pnlTimerPill.BorderColor = _currentColors.GeneralBtnBorder;
                pnlTimerPill.BorderSize = 2;
                pnlTimerPill.Invalidate();

                foreach (Control child in pnlTimerPill.Controls)
                {
                    child.BackColor = (child is Label) ? Color.Transparent : _currentColors.GeneralBtnBg;
                    child.ForeColor = _currentColors.GeneralBtnFg;
                    if (child is TextBox tb) { tb.ReadOnly = true; tb.Enabled = true; tb.TabStop = false; tb.BorderStyle = BorderStyle.None; }
                }
            }

            foreach (Control c in settingsStrip.Controls) if (c is Label) c.ForeColor = _currentColors.GeneralBtnFg;

            btnAddStore.BackColor = btnThemes.BackColor = btnInfo.BackColor = _currentColors.GeneralBtnBg;
            btnAddStore.ForeColor = btnThemes.ForeColor = btnInfo.ForeColor = _currentColors.GeneralBtnFg;
            btnAddStore.FlatAppearance.BorderColor = btnThemes.FlatAppearance.BorderColor = btnInfo.FlatAppearance.BorderColor = _currentColors.GeneralBtnBorder;
            btnAddStore.FlatAppearance.BorderSize = btnThemes.FlatAppearance.BorderSize = btnInfo.FlatAppearance.BorderSize = 1;
            btnAddStore.FlatAppearance.MouseOverBackColor = btnThemes.FlatAppearance.MouseOverBackColor = btnInfo.FlatAppearance.MouseOverBackColor = _currentColors.GeneralBtnHover;

            btnExitApp.BackColor = _currentColors.ExitBtnBg;
            btnExitApp.ForeColor = _currentColors.ExitBtnFg;
            btnExitApp.FlatAppearance.BorderColor = _currentColors.ExitBtnBorder;
            btnExitApp.FlatAppearance.BorderSize = 1;

            // For the 3 Main Buttons
            btnAddStore.FlatAppearance.MouseDownBackColor =
            btnThemes.FlatAppearance.MouseDownBackColor =
            btnInfo.FlatAppearance.MouseDownBackColor = Color.FromArgb(120, _currentColors.GeneralBtnHover);

            // For the Exit Button
            btnExitApp.FlatAppearance.MouseDownBackColor = Color.FromArgb(120, _currentColors.ExitBtnBg);

            UpdateOnTopVisuals();
            UpdateCloseUnnecessaryVisuals(); // Refreshes text and theme coloring on reload
            this.Refresh();
        }
    }
}