//<summary>[DO NOT REMOVE]IntelForm.cs</summary>

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;

namespace GSWEngine
{
    public class IntelForm : Form
    {
        private readonly Context _ctx;
        private readonly string[] _themeNames = { "Cubic", "Alarm", "Cyber", "Deep", "Arctic" };
        private ThemeColors _currentColors;
        private RichTextBox rtbIntel;
        private Panel pnlContainer;
        private Panel customScroll;
        private Panel scrollThumb;
        private bool _isScrolling = false;
        private int _scrollClickY = 0;
        private int _contentHeight = 0;
        private Image _themeBg;
        private RoundedButton btnSupport;

        // DWM Imports
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        public static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, ref POINT lParam);

        const int EM_GETSCROLLPOS = 0x04DD;
        const int EM_SETSCROLLPOS = 0x04DE;
        const int WM_VSCROLL = 0x115;
        const int SB_LINEUP = 0;
        const int SB_LINEDOWN = 1;
        const int EM_SETMARGINS = 0xd3;
        const int EC_LEFTMARGIN = 0x1;
        const int EC_RIGHTMARGIN = 0x2;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public IntelForm(Context ctx, GSWEngine.ThemeColors colors, Form parentForm)
        {
            _ctx = ctx;
            _currentColors = colors;

            // --- VISUAL DNA ---
            this.ClientSize = new Size(515, 685);
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // Action 1: Standard Border
            this.Text = "App info";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.DoubleBuffered = true; // Action 2: Double-Buffering
            this.BackColor = colors.GeneralBtnBg; // Action 1: Color Sync

            // Action 1: The Rounded Corners
            int attribute = 33; // DWMWA_WINDOW_CORNER_PREFERENCE;
            int preference = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(this.Handle, attribute, ref preference, sizeof(int));

            int captionAttr = 35; // DWMWA_CAPTION_COLOR
            int captionColor = ColorTranslator.ToWin32(_currentColors.TitleBarColor);
            DwmSetWindowAttribute(this.Handle, captionAttr, ref captionColor, sizeof(int));

            int textAttr = 36;
            int textColor = ColorTranslator.ToWin32(_currentColors.TitleFg);
            DwmSetWindowAttribute(this.Handle, textAttr, ref textColor, sizeof(int));

            // Icon Logic
            this.Icon = parentForm.Icon;

            InitializeComponents();
            ApplyTheme(); // Action 4: Logic

            // Action 2: Persistence & Boundary Check
            if (_ctx.Settings.IntelX != -1 && _ctx.Settings.IntelY != -1)
            {
                Point target = new Point(_ctx.Settings.IntelX, _ctx.Settings.IntelY);
                // Action 3: Boundary Check
                if (Screen.AllScreens.Any(s => s.WorkingArea.Contains(target)))
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = target;
                }
            }

            // Action 4: Ghost Focus
            this.Shown += (s, e) => { this.ActiveControl = null; };
        }

        private void InitializeComponents()
        {
            // The container for the RichTextBox is explicitly positioned to match the standard layout.
            pnlContainer = new Panel { Location = new Point(17, 17), Size = new Size(481, 651), BackColor = _currentColors.GeneralBtnBg, Padding = new Padding(0) };

            // Action 1: The Bypass (Custom Scrollbar)
            customScroll = new Panel { Width = 10, Height = pnlContainer.Height, Dock = DockStyle.Right, BackColor = _currentColors.TitleBarColor };
            scrollThumb = new Panel { Width = 10, Height = 40, BackColor = _currentColors.GeneralBtnBorder, Top = 0, Cursor = Cursors.Hand };

            scrollThumb.MouseDown += (s, e) => { _isScrolling = true; _scrollClickY = e.Y; };
            scrollThumb.MouseUp += (s, e) => { _isScrolling = false; };
            scrollThumb.MouseMove += ScrollThumb_MouseMove;

            customScroll.Controls.Add(scrollThumb);

            // RichTextBox
            rtbIntel = new RichTextBox
            {
                Location = new Point(0, 0),
                Height = pnlContainer.Height,
                Dock = DockStyle.Fill,
                ReadOnly = true,
                TabStop = false,
                WordWrap = true, // RESTORE NATURAL PARAGRAPH WRAPPING
                BorderStyle = BorderStyle.None,
                BackColor = _currentColors.GeneralBtnBg,
                ForeColor = _currentColors.GeneralBtnFg,
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ScrollBars = RichTextBoxScrollBars.None
            };

            // Action 3: Text Padding
            SendMessage(rtbIntel.Handle, EM_SETMARGINS, EC_LEFTMARGIN | EC_RIGHTMARGIN, (12 << 16) | 5);

            rtbIntel.ContentsResized += (s, e) => { _contentHeight = e.NewRectangle.Height; UpdateScrollThumb(); };
            rtbIntel.MouseWheel += RtbIntel_MouseWheel;
            rtbIntel.LinkClicked += RtbIntel_LinkClicked;

            // Docking Order: Add RTB first (Fill), then CustomScroll (Right) so CustomScroll takes precedence
            pnlContainer.Controls.Add(rtbIntel);
            pnlContainer.Controls.Add(customScroll);

            this.Controls.Add(pnlContainer);

            LoadManifest();

            // [RESERVED FOR FUTURE USE: Original Support Button]
            /*
            btnSupport = new RoundedButton
            {
                Text = "Support your local Sheriff",
                Location = new Point(129, this.Height - 60),
                Size = new Size(257, 35),
                FlatStyle = FlatStyle.Flat
            };
            btnSupport.FlatAppearance.BorderSize = 1;
            btnSupport.TabStop = false;

            ThemeManager.StripFocus(btnSupport);

            btnSupport.Click += (s, e) =>
            {
                this.ActiveControl = null;

                // PLACEHOLDER: URL to be replaced with actual Stripe Checkout / Payment Link later
                string stripeUrl = "https://buy.stripe.com/dRm9AS275dFo1tc8rP6sw00";

                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = stripeUrl,
                        UseShellExecute = true // Required in modern .NET to launch URLs
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[IntelForm] Failed to launch Support URL: {ex.Message}");
                }
            };

            this.Controls.Add(btnSupport);
            */
        }

        private void RtbIntel_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = e.LinkText,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to open link: {ex.Message}");
            }
        }

        // Action 3: The Logic Glue (Mouse Wheel)
        private void RtbIntel_MouseWheel(object sender, MouseEventArgs e)
        {
            int lines = Math.Abs(e.Delta) / 40;
            int direction = e.Delta > 0 ? SB_LINEUP : SB_LINEDOWN;
            for (int i = 0; i < lines; i++) SendMessage(rtbIntel.Handle, WM_VSCROLL, direction, 0);
            UpdateScrollThumb();
        }

        // Action 3: The Logic Glue (Thumb Sync)
        private void UpdateScrollThumb()
        {
            POINT pt = new POINT();
            SendMessage(rtbIntel.Handle, EM_GETSCROLLPOS, 0, ref pt);

            int clientHeight = rtbIntel.ClientSize.Height;
            if (_contentHeight <= clientHeight)
            {
                scrollThumb.Visible = false;
                return;
            }
            scrollThumb.Visible = true;

            int trackHeight = customScroll.Height;
            int thumbHeight = Math.Max(20, (int)((float)clientHeight / _contentHeight * trackHeight));
            scrollThumb.Height = thumbHeight;

            int maxScroll = _contentHeight - clientHeight;
            int thumbTop = (int)((float)pt.Y / maxScroll * (trackHeight - thumbHeight));

            if (thumbTop < 0) thumbTop = 0;
            if (thumbTop > trackHeight - thumbHeight) thumbTop = trackHeight - thumbHeight;

            scrollThumb.Top = thumbTop;
        }

        // Action 3: The Logic Glue (Drag)
        private void ScrollThumb_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isScrolling) return;

            int delta = e.Y - _scrollClickY;
            int newTop = scrollThumb.Top + delta;
            int trackHeight = customScroll.Height;
            int thumbHeight = scrollThumb.Height;

            if (newTop < 0) newTop = 0;
            if (newTop > trackHeight - thumbHeight) newTop = trackHeight - thumbHeight;

            scrollThumb.Top = newTop;

            // Calculate Scroll
            int clientHeight = rtbIntel.ClientSize.Height;
            int maxScroll = _contentHeight - clientHeight;

            if (maxScroll > 0)
            {
                float percent = (float)newTop / (trackHeight - thumbHeight);
                int targetY = (int)(percent * maxScroll);

                POINT pt = new POINT { X = 0, Y = targetY };
                SendMessage(rtbIntel.Handle, EM_SETSCROLLPOS, 0, ref pt);
            }
        }

        private void LoadManifest()
        {
            try
            {
                using (Stream stream = InternalFunctions.GetResource("intel_manifest.rtf"))
                {
                    if (stream != null)
                        rtbIntel.LoadFile(stream, RichTextBoxStreamType.RichText);
                    else
                        rtbIntel.Text = "Mission Briefing Missing. Reinstalling Intel Manifest...";
                }
            }
            catch { rtbIntel.Text = "Mission Briefing Missing. Reinstalling Intel Manifest..."; }
        }

        // Action 2: The OnPaint Override
        protected override void OnPaint(PaintEventArgs e)
        {
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

            // Action 1: The Border Logic Variables
            int gap = 15;
            Rectangle tacticalFrame = new Rectangle(gap, gap, this.ClientRectangle.Width - (gap * 2) - 1, this.ClientRectangle.Height - (gap * 2) - 1);

            // Action 3: Visual Cleanup (Wash)
            using (SolidBrush brush = new SolidBrush(Color.FromArgb(180, _currentColors.RequestorBg)))
                g.FillRectangle(brush, tacticalFrame);

            // Action 1: The Border Logic
            using (Pen p = new Pen(_currentColors.GeneralBtnBorder, 1))
                g.DrawRectangle(p, tacticalFrame);
        }

        // Action 4: The Hit-Box (Mouse Logic)
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
                catch { }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (this.WindowState == FormWindowState.Normal)
            {
                _ctx.Settings.IntelX = this.Location.X;
                _ctx.Settings.IntelY = this.Location.Y;
                _ctx.SaveSettings();
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _themeBg?.Dispose();
            base.OnFormClosed(e);
        }

        // Action 4: The Logic (ApplyTheme)
        public void ApplyTheme()
        {
            _currentColors = ThemeManager.GetTheme(_ctx.CurrentTheme);
            if (_currentColors == null) return;

            // 1. Map the Index to the correct File Name
            string themeName = "Void";
            if (_ctx.Settings.ThemeIndex >= 0 && _ctx.Settings.ThemeIndex < _themeNames.Length)
                themeName = _themeNames[_ctx.Settings.ThemeIndex];

            // 2. Load the specific Backdrop
            using (var stream = InternalFunctions.GetResource($"skins\\{themeName}_read.jpg"))
            {
                if (stream != null)
                {
                    if (_themeBg != null) { _themeBg.Dispose(); _themeBg = null; }
                    _themeBg = new Bitmap(Image.FromStream(stream));
                }
            }

            // 3. Apply the Colors & Force the "Ink" (SelectionColor)
            this.BackColor = _currentColors.GeneralBtnBg;
            if (pnlContainer != null) pnlContainer.BackColor = _currentColors.GeneralBtnBg;

            rtbIntel.BackColor = _currentColors.GeneralBtnBg;

            // This forces the RTF text to match your theme's "Ink"
            rtbIntel.SelectAll();
            rtbIntel.SelectionColor = _currentColors.GeneralBtnFg;
            rtbIntel.DeselectAll();

            // 4. Sync the Tactical Scrollbar
            if (customScroll != null) customScroll.BackColor = _currentColors.TitleBarColor;
            if (scrollThumb != null) scrollThumb.BackColor = _currentColors.GeneralBtnBorder;

            /*
            if (btnSupport != null)
            {
                btnSupport.BackColor = _currentColors.GeneralBtnBg;
                btnSupport.ForeColor = _currentColors.GeneralBtnFg;
                btnSupport.FlatAppearance.BorderColor = _currentColors.GeneralBtnBorder;
                btnSupport.FlatAppearance.MouseOverBackColor = _currentColors.GeneralBtnHover;
                btnSupport.FlatAppearance.MouseDownBackColor = Color.FromArgb(120, _currentColors.GeneralBtnHover);
            }
            */

            // 5. Handle the 'Always on Top' State
            this.TopMost = _ctx.Settings.TopMost;

            UpdateTitleBarOnly(true);
            this.Invalidate(); // Redraw the Title Bar and the GeneralBtnBorder frame
        }
    }
}