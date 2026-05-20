﻿//<summary>[DO NOT REMOVE]Form1.Designer.cs</summary>

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Linq;

namespace GSWEngine
{
    public class RoundedButton : Button
    {
        public int BorderRadius { get; set; } = 6;
        public int TextOffsetY { get; set; } = 0; // Surgical 1px alignment
        private bool _isHovered = false;
        private Color _themeBackColor = Color.FromArgb(45, 45, 48);

        public RoundedButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.Cursor = Cursors.Hand;
            // Strictly solid rendering. No transparency lies.
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public override Color BackColor
        {
            get => _themeBackColor;
            set { _themeBackColor = value; this.Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; this.Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; this.Invalidate(); base.OnMouseLeave(e); }

        private GraphicsPath GetRoundPath(Rectangle rect)
        {
            int d = BorderRadius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // THE GUILLOTINE 2.0: Shrink the physical OS window by 1px to cut off the dark pixel halo
            using (GraphicsPath path = GetRoundPath(new Rectangle(1, 1, this.Width - 2, this.Height - 2)))
            {
                this.Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = _isHovered && this.FlatAppearance.MouseOverBackColor != Color.Empty ? this.FlatAppearance.MouseOverBackColor : _themeBackColor;

            // 1. The Fill Rect: Fills the entire OS Region (Width - 2)
            Rectangle fillRect = new Rectangle(1, 1, this.Width - 2, this.Height - 2);
            using (GraphicsPath fillPath = GetRoundPath(fillRect))
            {
                using (SolidBrush brush = new SolidBrush(bg)) g.FillPath(brush, fillPath);
            }

            // 2. The Border Rect: Shrunk by 1 extra pixel (Width - 3) so the right/bottom pen strokes aren't chopped off
            if (this.FlatAppearance.BorderSize > 0)
            {
                Rectangle borderRect = new Rectangle(1, 1, this.Width - 3, this.Height - 3);
                using (GraphicsPath borderPath = GetRoundPath(borderRect))
                {
                    using (Pen pen = new Pen(this.FlatAppearance.BorderColor, 2f)) g.DrawPath(pen, borderPath);
                }
            }

            // Apply the surgical Y-axis offset for perfect visual centering
            Rectangle textRect = new Rectangle(0, TextOffsetY, this.Width, this.Height);
            TextRenderer.DrawText(g, this.Text, this.Font, textRect, this.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class RoundedPanel : Panel
    {
        public int BorderRadius { get; set; } = 6;
        public Color BorderColor { get; set; } = Color.FromArgb(100, 100, 100);
        public int BorderSize { get; set; } = 2;

        private Color _themeBackColor = Color.FromArgb(30, 30, 30);

        public RoundedPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public override Color BackColor
        {
            get => _themeBackColor;
            set { _themeBackColor = value; this.Invalidate(); }
        }

        private GraphicsPath GetRoundPath(Rectangle rect)
        {
            int d = BorderRadius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            using (GraphicsPath path = GetRoundPath(new Rectangle(1, 1, this.Width - 2, this.Height - 2)))
            {
                this.Region = new Region(path);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. The Fill Rect
            Rectangle fillRect = new Rectangle(1, 1, this.Width - 2, this.Height - 2);
            using (GraphicsPath fillPath = GetRoundPath(fillRect))
            {
                using (SolidBrush brush = new SolidBrush(_themeBackColor)) g.FillPath(brush, fillPath);
            }

            // 2. The Border Rect
            if (BorderSize > 0)
            {
                Rectangle borderRect = new Rectangle(1, 1, this.Width - 3, this.Height - 3);
                using (GraphicsPath borderPath = GetRoundPath(borderRect))
                {
                    using (Pen pen = new Pen(BorderColor, BorderSize)) g.DrawPath(pen, borderPath);
                }
            }
        }
    }

    public class DoubleBufferPanel : Panel
    {
        public DoubleBufferPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
    }

    public partial class StatsForm
    {
        private Panel settingsStrip;
        private DoubleBufferPanel canvas;
        private TextBox numTimer = new TextBox(); // Ensure this is not RichTextBox
        private RoundedPanel pnlTimerPill;
        private RoundedButton btnAddStore, btnThemes, btnExitApp, btnInfo;
        private RoundedButton chkCloseUnnecessary;
        private Label lblHeader, lblAward, lblLogHeader, logBox;
        private RoundedButton chkAlwaysOnTop = new RoundedButton();
        private PictureBox btnExpandLog;

        private void InitializeManualComponents()
        {
            this.Text = $"{InternalFunctions.AppNameShort} {InternalFunctions.AppVersion}";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.ClientSize = new Size(515, 685);
            this.MaximizeBox = false;


            lblHeader = new Label
            {
                Text = "GAME STORE WRANGLER",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                Location = new Point(0, 5),
                Width = 515,
                Height = 35,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            settingsStrip = new Panel { Location = new Point(0, 45), Width = 515, Height = 40, BackColor = Color.Transparent };

            // --- EQUALIZED 3-BUTTON HEADER ROW ---
            pnlTimerPill = new RoundedPanel
            {
                Name = "pnlTimerPill",
                Size = new Size(115, 24),
                Location = new Point(12, 5),
                BackColor = Color.FromArgb(30, 30, 30),
                Cursor = Cursors.Hand
            };

            Label lblTimerLabel = new Label
            {
                Text = "CLOSE TIMER:",
                Font = new Font("Segoe UI", 7, FontStyle.Bold),
                Location = new Point(5, 6),
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            numTimer.Size = new Size(35, 20);
            numTimer.Location = new Point(75, 4);
            numTimer.BorderStyle = BorderStyle.None;
            numTimer.TextAlign = HorizontalAlignment.Center;
            numTimer.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            numTimer.ReadOnly = true;
            numTimer.Enabled = true;
            numTimer.ShortcutsEnabled = false;
            numTimer.Cursor = Cursors.Hand;
            numTimer.Text = _ctx.GracePeriod.ToString();
            numTimer.GotFocus += (s, e) => { HideCaret(numTimer.Handle); pnlTimerPill.Focus(); };

            // Map all clicks to the cycle method in Theme.cs
            pnlTimerPill.Click += (s, e) => CycleTimerPresets();
            lblTimerLabel.Click += (s, e) => CycleTimerPresets();
            numTimer.Click += (s, e) => CycleTimerPresets();

            pnlTimerPill.Controls.Add(lblTimerLabel);
            pnlTimerPill.Controls.Add(numTimer);

            // CENTRAL TOGGLE
            chkCloseUnnecessary = new RoundedButton
            {
                Text = "Close unnecessary stores when a game runs: YES",
                Size = new Size(249, 24),
                Location = new Point(133, 5), // Balanced gap center space
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                TabStop = false
            };
            chkCloseUnnecessary.FlatAppearance.BorderSize = 1;
            chkCloseUnnecessary.TextOffsetY = -1;
            chkCloseUnnecessary.Click += (s, e) =>
            {
                this.ActiveControl = null;
                _ctx.Settings.CloseUnnecessaryStoresOnLaunch = !_ctx.Settings.CloseUnnecessaryStoresOnLaunch;
                UpdateCloseUnnecessaryVisuals();
                _ctx.SaveSettings();
            };
            ThemeManager.StripFocus(chkCloseUnnecessary);

            // RIGHT TOGGLE
            chkAlwaysOnTop.Text = "On Top: NO";
            chkAlwaysOnTop.Size = new Size(115, 24);
            chkAlwaysOnTop.Location = new Point(388, 5); // Corrected semicolon syntax
            chkAlwaysOnTop.FlatStyle = FlatStyle.Flat;
            chkAlwaysOnTop.FlatAppearance.BorderSize = 1;
            chkAlwaysOnTop.TextOffsetY = -1;
            chkAlwaysOnTop.TextAlign = ContentAlignment.MiddleCenter;
            chkAlwaysOnTop.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            chkAlwaysOnTop.Click += (s, e) => { this.ActiveControl = null; chkAlwaysOnTop_Click(s, e); };
            chkAlwaysOnTop.TabStop = false;
            ThemeManager.StripFocus(chkAlwaysOnTop);

            settingsStrip.Controls.Add(pnlTimerPill);
            settingsStrip.Controls.Add(chkCloseUnnecessary);
            settingsStrip.Controls.Add(chkAlwaysOnTop);

            canvas = new DoubleBufferPanel { Location = new Point(12, 95), Width = 490, Height = 330, BackColor = Color.Transparent };
            canvas.Paint += Canvas_Paint;
            canvas.MouseClick += Canvas_MouseClick;

            lblLogHeader = new Label { Text = "SYSTEM ENGAGEMENT LOG", Font = new Font("Segoe UI", 11, FontStyle.Bold), Location = new Point(0, 430), Width = 515, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent };

            // The Data Label
            logBox = new Label
            {
                Location = new Point(12, 459),
                Size = new Size(490, 95),
                Font = new Font("Consolas", 9),
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(5, 0, 5, 5),
                ForeColor = Color.DarkGray
            };

            lblAward = new Label { Text = "Scanning...", Location = new Point(0, 555), Width = 515, Height = 35, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent, Font = new Font("Segoe UI", 10, FontStyle.Bold) };

            btnExpandLog = new PictureBox
            {
                Location = new Point(480, 428),
                Size = new Size(20, 20),
                BackColor = Color.Transparent,
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor = Cursors.Hand
            };

            // Load the PNGs cleanly into the PictureBox Image property
            try
            {
                using (var normalStream = InternalFunctions.GetResource("Log_Popout_icon.png"))
                {
                    if (normalStream != null) btnExpandLog.Image = Image.FromStream(normalStream);
                }

                btnExpandLog.MouseEnter += (s, e) =>
                {
                    using (var hoverStream = InternalFunctions.GetResource("Log_Popout_Hover_icon.png")) { if (hoverStream != null) btnExpandLog.Image = Image.FromStream(hoverStream); }
                };
                btnExpandLog.MouseLeave += (s, e) =>
                {
                    using (var normalStream = InternalFunctions.GetResource("Log_Popout_icon.png")) { if (normalStream != null) btnExpandLog.Image = Image.FromStream(normalStream); }
                };
            }
            catch { }

            // THE FAILSAFE: If the PNGs fail to load, manually draw the [+]
            btnExpandLog.Paint += (s, e) =>
            {
                if (btnExpandLog.Image == null)
                {
                    TextRenderer.DrawText(e.Graphics, "[+]", new Font("Segoe UI", 9, FontStyle.Bold), new Point(0, 0), Color.White);
                }
            };
            // Bind the picture box to the label to fix WinForms alpha-clipping
            btnExpandLog.Parent = lblLogHeader;
            // Shift the relative coordinates since it is now a child of the label
            btnExpandLog.Location = new Point(480, 2);

            ThemeManager.StripFocus(btnExpandLog);
            btnExpandLog.Click += (s, e) =>
            {
                this.ActiveControl = null;
                if (Application.OpenForms.OfType<LogTerminalForm>().Any()) return;
                _logTerminalForm = new LogTerminalForm(_ctx, _currentColors, this);
                _logTerminalForm.FormClosed += (s, ev) => { _logTerminalForm = null; };
                _logTerminalForm.TopMost = this.TopMost;

                // Position Logic: Use saved coordinates, or fallback to docking to the right
                if (_ctx.Settings.LogX == -1 || _ctx.Settings.LogY == -1)
                {
                    _logTerminalForm.StartPosition = FormStartPosition.Manual;
                    _logTerminalForm.Location = new Point(this.Right + 5, this.Top);
                }
                _logTerminalForm.Show(this);
            };

            btnAddStore = new RoundedButton { Text = "ADD STORE", Location = new Point(12, 595), Size = new Size(157, 35), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1 } };
            btnAddStore.TabStop = false;
            ThemeManager.StripFocus(btnAddStore);
            btnAddStore.Click += (s, e) => { this.ActiveControl = null; btnAddStore_Click(s, e); };

            // Clean single instantiation with the click event wired
            btnInfo = new RoundedButton { Text = "APP INFO", Location = new Point(179, 595), Size = new Size(157, 35), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1 } };
            btnInfo.TabStop = false;
            ThemeManager.StripFocus(btnInfo);
            btnInfo.Click += btnInfo_Click;

            btnThemes = new RoundedButton { Text = "SELECT THEME", Location = new Point(346, 595), Size = new Size(157, 35), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1 } };
            btnThemes.TabStop = false;
            ThemeManager.StripFocus(btnThemes);
            btnThemes.Click += (s, e) => { this.ActiveControl = null; btnThemes_Click(s, e); };

            btnExitApp = new RoundedButton { Text = StateEngine.IsDebug ? "Exit Application" : "Support your local Sheriff", Location = new Point(129, 640), Size = new Size(257, 35), FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 1 } };
            btnExitApp.TabStop = false;
            ThemeManager.StripFocus(btnExitApp);
            btnExitApp.Click += (s, e) =>
            {
                this.ActiveControl = null;
                if (StateEngine.IsDebug)
                {
                    _ctx.Exit();
                }
                else
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://buy.stripe.com/dRm9AS275dFo1tc8rP6sw00",
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to launch Support URL: {ex.Message}");
                    }
                }
            };

            this.Controls.Add(lblAward);
            this.Controls.Add(logBox);
            this.Controls.Add(lblLogHeader);
            this.Controls.Add(canvas);
            this.Controls.Add(settingsStrip);
            this.Controls.Add(lblHeader);
            this.Controls.Add(btnAddStore);
            this.Controls.Add(btnInfo);
            this.Controls.Add(btnThemes);
            this.Controls.Add(btnExitApp);
        }
    }
}