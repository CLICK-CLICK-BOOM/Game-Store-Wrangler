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
        private Label lblTerminal;
        private System.Windows.Forms.Timer _refreshTimer;
        private Image _themeBg;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        public LogTerminalForm(Context ctx, ThemeColors colors, Size parentSize)
        {
            _ctx = ctx;
            _currentColors = colors;

            this.Size = parentSize;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterParent;
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

            int gap = 15; int titleHeight = 30;
            this.Padding = new Padding(gap + 1, titleHeight + gap + 1, gap + 1, gap + 1);

            // Fix: lblTerminalColumns removal pass completed.

            lblTerminal = new Label
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Location = new Point(gap + 10, titleHeight + gap + 10), // Pushed to the top of the frame box
                Size = new Size(this.Width - ((gap + 10) * 2), this.Height - (titleHeight + gap + 10) - 14 - (gap + 1)),
                Font = new Font("Consolas", 9),
                BackColor = Color.Transparent,
                ForeColor = _currentColors.LogFg,
                Padding = new Padding(0, 0, 0, 10),
                AutoSize = false
            };

            this.Controls.Add(lblTerminal);

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _refreshTimer.Tick += (s, e) => RefreshTerminal();
            _refreshTimer.Start();
        }

        private void RefreshTerminal()
        {
            try
            {
                var snapshot = _ctx.ExecutionHistory.ToList();
                lblTerminal.Text = snapshot.Any()
                    ? InternalFunctions.FormatExecutionHistory(snapshot, 100)
                    : "TELEMETRY OUTPUT";
            }
            catch { }
        }

        public void ApplyTheme()
        {
            _currentColors = ThemeManager.GetTheme(_ctx.CurrentTheme);
            if (_currentColors == null) return;

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

            if (lblTerminal != null)
                lblTerminal.ForeColor = _currentColors.LogFg;

            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            // 1. Draw Background JPG with a 30-pixel offset to match the Main App's ClientArea
            if (_themeBg != null)
            {
                // We start at Y=30, and reduce the height by 30 to prevent vertical distortion
                Rectangle trueClientArea = new Rectangle(0, 30, this.Width, this.Height - 30);
                g.DrawImage(_themeBg, trueClientArea);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(_currentColors.GeneralBtnBg))
                    g.FillRectangle(b, this.ClientRectangle);
            }

            int gap = 15; int titleHeight = 30;
            Rectangle tacticalFrame = new Rectangle(gap, titleHeight + gap, this.Width - (gap * 2) - 1, this.Height - titleHeight - (gap * 2) - 1);
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(180, _currentColors.RequestorBg))) g.FillRectangle(brush, tacticalFrame);
            using (SolidBrush b = new SolidBrush(_currentColors.TitleBarColor)) g.FillRectangle(b, new Rectangle(0, 0, this.Width, 30));

            // 2. Updated Window Title
            TextRenderer.DrawText(g, "SYSTEM ENGAGEMENT LOG - POP OUT EXPANSION", this.Font, new Point(10, 8), _currentColors.TitleFg);
            TextRenderer.DrawText(g, "X", this.Font, new Rectangle(this.Width - 40, 0, 40, 30), _currentColors.TitleFg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using (Pen p = new Pen(_currentColors.TitleBarColor, 2)) g.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
            using (Pen p = new Pen(_currentColors.GeneralBtnBorder, 1)) g.DrawRectangle(p, tacticalFrame);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.X >= this.Width - 45 && e.Y <= 45) { this.Close(); return; }
            if (e.Y <= 30) { ReleaseCapture(); SendMessage(this.Handle, 0xA1, 0x2, 0); }
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
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _refreshTimer?.Dispose();
            base.Dispose(disposing);
        }
    }
}