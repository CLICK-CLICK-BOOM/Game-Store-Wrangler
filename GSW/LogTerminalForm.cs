//<summary>[DO NOT REMOVE]LogTerminalForm.cs</summary>
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.Runtime.InteropServices;

namespace GSWEngine
{
    public class LogTerminalForm : Form
    {
        private readonly Context _ctx;
        private ThemeColors _currentColors;
        private Image _themeBg;
        private Font _logFont;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public LogTerminalForm(Context ctx, ThemeColors colors, Form parentForm)
        {
            _ctx = ctx;
            _currentColors = colors;
            _logFont = new Font("Consolas", 9);

            this.Text = "GSW LIVE LOG";
            this.ClientSize = new Size(515, 685);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = true;
            this.MaximizeBox = false;
            this.DoubleBuffered = true;
            this.BackColor = colors.GeneralBtnBg;

            string[] themeNames = { "Cubic", "Alarm", "Cyber", "Deep", "Arctic" };
            string themeName = (_ctx.Settings.ThemeIndex >= 0 && _ctx.Settings.ThemeIndex < themeNames.Length) ? themeNames[_ctx.Settings.ThemeIndex] : "Void";

            using (var stream = InternalFunctions.GetResource($"skins\\{themeName}_read.jpg"))
            {
                if (stream != null)
                {
                    _themeBg = new Bitmap(Image.FromStream(stream));
                }
            }

            if (_ctx.Settings.LogX != -1 && _ctx.Settings.LogY != -1)
            {
                Point target = new Point(_ctx.Settings.LogX, _ctx.Settings.LogY);
                if (Screen.AllScreens.Any(s => s.WorkingArea.Contains(target)))
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = target;
                }
            }

            int attribute = 33; int preference = 2; // Rounded corners
            DwmSetWindowAttribute(this.Handle, attribute, ref preference, sizeof(int));

            int captionAttr = 35; // DWMWA_CAPTION_COLOR
            int captionColor = ColorTranslator.ToWin32(_currentColors.TitleBarColor);
            DwmSetWindowAttribute(this.Handle, captionAttr, ref captionColor, sizeof(int));

            int textAttr = 36;
            int textColor = ColorTranslator.ToWin32(_currentColors.TitleFg);
            DwmSetWindowAttribute(this.Handle, textAttr, ref textColor, sizeof(int));

            this.ShowIcon = true;
            this.Icon = parentForm.Icon;

            // Fix: lblTerminalColumns removal pass completed.

            _ctx.HistoryUpdated += Ctx_HistoryUpdated;
        }

        private void Ctx_HistoryUpdated(object sender, EventArgs e)
        {
            if (this.IsHandleCreated && !this.IsDisposed)
            {
                this.BeginInvoke(new Action(() => this.Refresh()));
            }
        }

        public void ApplyTheme()
        {
            _currentColors = ThemeManager.GetTheme(_ctx.CurrentTheme);
            if (_currentColors == null) return;

            UpdateTitleBarOnly(true);

            string[] themeNames = { "Cubic", "Alarm", "Cyber", "Deep", "Arctic" };
            string themeName = (_ctx.Settings.ThemeIndex >= 0 && _ctx.Settings.ThemeIndex < themeNames.Length) ? themeNames[_ctx.Settings.ThemeIndex] : "Void";

            using (var stream = InternalFunctions.GetResource($"skins\\{themeName}_read.jpg"))
            {
                if (stream != null)
                {
                    if (_themeBg != null) { _themeBg.Dispose(); _themeBg = null; }
                    _themeBg = new Bitmap(Image.FromStream(stream));
                }
            }

            this.BackColor = _currentColors.GeneralBtnBg;

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (this.Handle == IntPtr.Zero || !this.IsHandleCreated || _currentColors == null) return;

            base.OnPaint(e);
            Graphics g = e.Graphics;

            // 1. Draw Background JPG with a 30-pixel offset to match the Main App's ClientArea
            if (_themeBg != null)
            {
                g.DrawImage(_themeBg, this.ClientRectangle);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(_currentColors.GeneralBtnBg))
                    g.FillRectangle(b, this.ClientRectangle);
            }

            int gap = 15;
            Rectangle tacticalFrame = new Rectangle(gap, gap, this.ClientRectangle.Width - (gap * 2) - 1, this.ClientRectangle.Height - (gap * 2) - 1);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(180, _currentColors.RequestorBg))) g.FillRectangle(brush, tacticalFrame);

            // 3. Render Log Text Natively via GDI+ to prevent Release optimization blanking
            try
            {
                List<string> snapshot;
                lock (_ctx.HistoryLock)
                {
                    snapshot = _ctx.ExecutionHistory.ToList();
                }

                var rollingSnapshot = snapshot.Skip(Math.Max(0, snapshot.Count - 46)).ToList();
                string logText = rollingSnapshot.Any()
                    ? AuditLogger.FormatExecutionHistory(rollingSnapshot, 46)
                    : "TELEMETRY OUTPUT";

                Rectangle textBounds = new Rectangle(17, 17, 481, 650); // Adjusted for the removed title bar offset
                TextRenderer.DrawText(g, logText, _logFont, textBounds, _currentColors.LogFg, TextFormatFlags.WordBreak | TextFormatFlags.Top | TextFormatFlags.Left);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[GSW SILENT EXCEPTION] {ex.Message} | Source: {ex.StackTrace}");
            }

            using (Pen p = new Pen(_currentColors.GeneralBtnBorder, 1)) g.DrawRectangle(p, tacticalFrame);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
        }

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
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[GSW SILENT EXCEPTION] {ex.Message} | Source: {ex.StackTrace}");
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (this.WindowState == FormWindowState.Normal)
            {
                _ctx.Settings.LogX = this.Location.X;
                _ctx.Settings.LogY = this.Location.Y;
                _ctx.SaveSettings();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _themeBg?.Dispose();
            base.OnFormClosed(e);

            // Explicitly push the update command to the end of the message queue
            // to ensure this form is completely removed from OpenForms before evaluation.
            if (this.Owner is StatsForm mainHud)
            {
                mainHud.BeginInvoke(new Action(() => mainHud.UpdateUI()));
            }
            else if (Application.OpenForms.OfType<StatsForm>().FirstOrDefault() is StatsForm mainApp)
            {
                mainApp.BeginInvoke(new Action(() => mainApp.UpdateUI()));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_ctx != null)
                {
                    _ctx.HistoryUpdated -= Ctx_HistoryUpdated;
                }
                _logFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}