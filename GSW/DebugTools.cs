//<summary>[DO NOT REMOVE]DebugTools.cs</summary>
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Linq;
using System.Text;

namespace GSWEngine
{
    public class DebugTools : Form
    {
        private StatsForm _main;
        private Context _ctx;
        private ComboBox _propSelector;
        private TrackBar _r, _g, _b;
        private Label _valLabel;
        private System.Windows.Forms.Timer _debugHudTimer;
        private Panel panLightMin, panLightIdle, panLightRadar, panLightSentry, panLightCpu, panLightIo;

        // --- SURGERY: Context first, Form optional ---
        public DebugTools(Context ctx, StatsForm main = null)
        {
            _ctx = ctx;
            _main = main;

            this.Text = "HUD LIVE TWEAKER";
            this.Size = new Size(320, 520);
            this.TopMost = true;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            _propSelector = new ComboBox
            {
                Dock = DockStyle.Top,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.Black,
                ForeColor = Color.Lime,
                FlatStyle = FlatStyle.Flat
            };

            var colorProps = typeof(ThemeColors).GetProperties()
                                .Where(p => p.PropertyType == typeof(Color));

            foreach (var p in colorProps) _propSelector.Items.Add(p.Name);
            _propSelector.SelectedIndexChanged += (s, e) => SyncSlidersToCurrentTheme();

            _r = CreateSlider("RED", 50, Color.Red);
            _g = CreateSlider("GREEN", 120, Color.Green);
            _b = CreateSlider("BLUE", 190, Color.Blue);

            _valLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Consolas", 10),
                ForeColor = Color.Yellow
            };

            Button btnCopyAll = new Button
            {
                Text = "COPY ENTIRE THEME BLOCK",
                Dock = DockStyle.Bottom,
                Height = 50,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 80, 0),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };

            btnCopyAll.Click += (s, e) =>
            {
                // SAFETY: Ensure we have a form to copy from
                if (_main == null || _main._currentColors == null) return;

                StringBuilder sb = new StringBuilder();
                var props = typeof(ThemeColors).GetProperties();
                foreach (var p in props)
                {
                    var val = p.GetValue(_main._currentColors);

                    if (p.PropertyType == typeof(Color))
                    {
                        Color c = (Color)val;
                        sb.AppendLine($"{p.Name} = Color.FromArgb({c.R}, {c.G}, {c.B}),");
                    }
                    else if (p.PropertyType == typeof(string))
                    {
                        sb.AppendLine($"{p.Name} = \"{val}\",");
                    }
                }

                string result = sb.ToString().TrimEnd(',', '\r', '\n');
                Clipboard.SetText(result);

                _ctx.ExecutionHistory.Add("SYSTEM: Raw Theme Settings Copied.");
                _main.UpdateUI();
            };

            this.Controls.Add(_propSelector);
            this.Controls.Add(btnCopyAll);
            this.Controls.Add(_valLabel);

            InitializeDiagnosticHUD();

            if (_propSelector.Items.Count > 0) _propSelector.SelectedIndex = 0;
        }

        private void InitializeDiagnosticHUD()
        {
            FlowLayoutPanel hudPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = Color.FromArgb(15, 15, 15),
                WrapContents = false,
                Padding = new Padding(5, 5, 0, 0)
            };

            panLightMin = CreateLight("MIN", hudPanel);
            panLightIdle = CreateLight("IDLE", hudPanel);
            panLightRadar = CreateLight("RADAR", hudPanel);
            panLightSentry = CreateLight("SENTRY", hudPanel);
            panLightCpu = CreateLight("CPU", hudPanel);
            panLightIo = CreateLight("I/O", hudPanel);

            this.Controls.Add(hudPanel);

            _debugHudTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _debugHudTimer.Tick += DebugHudTimer_Tick;
            _debugHudTimer.Start();
        }

        private Panel CreateLight(string name, FlowLayoutPanel parent)
        {
            Panel pnl = new Panel { Width = 45, Height = 45, Margin = new Padding(2) };
            Label lbl = new Label
            {
                Text = name,
                Dock = DockStyle.Top,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 6, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 12
            };
            Panel light = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.DarkGreen,
                Margin = new Padding(8, 4, 8, 8)
            };
            pnl.Controls.Add(light);
            pnl.Controls.Add(lbl);
            parent.Controls.Add(pnl);
            return light;
        }

        private void DebugHudTimer_Tick(object sender, EventArgs e)
        {
            Color onColor = Color.LimeGreen;
            Color offColor = Color.DarkGreen;

            panLightMin.BackColor = StateEngine.DiagnosticHUD.IsMinimized ? onColor : offColor;
            panLightIdle.BackColor = StateEngine.DiagnosticHUD.IsIdle ? onColor : offColor;
            panLightRadar.BackColor = StateEngine.DiagnosticHUD.IsRadarActive ? onColor : offColor;
            panLightSentry.BackColor = StateEngine.DiagnosticHUD.IsSentryArmed ? onColor : offColor;
            panLightCpu.BackColor = StateEngine.DiagnosticHUD.IsMeasuringCPU ? onColor : offColor;
            panLightIo.BackColor = StateEngine.DiagnosticHUD.IsMeasuringIO ? onColor : offColor;
        }

        // --- NEW: Helper to link the form later ---
        public void LinkForm(StatsForm form)
        {
            if (form == null) return;
            _main = form;
            SyncSlidersToCurrentTheme();
        }

        private TrackBar CreateSlider(string name, int y, Color trackColor)
        {
            Label lbl = new Label { Text = name, Location = new Point(10, y), AutoSize = true, ForeColor = trackColor };
            TrackBar tb = new TrackBar
            {
                Location = new Point(10, y + 20),
                Width = 280,
                Minimum = 0,
                Maximum = 255,
                TickStyle = TickStyle.None,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            tb.Scroll += (s, e) => UpdateHUD();
            this.Controls.Add(lbl);
            this.Controls.Add(tb);
            return tb;
        }

        private void SyncSlidersToCurrentTheme()
        {
            if (_main == null || _propSelector.SelectedItem == null || _main._currentColors == null) return;
            var prop = typeof(ThemeColors).GetProperty(_propSelector.SelectedItem.ToString());
            Color c = (Color)prop.GetValue(_main._currentColors);
            _r.Value = c.R; _g.Value = c.G; _b.Value = c.B;
            _valLabel.Text = $"SYNCED: {_propSelector.SelectedItem}";
        }

        private void UpdateHUD()
        {
            // SAFETY: No form? No update.
            if (_main == null || _propSelector.SelectedItem == null || _main._currentColors == null) return;
            var prop = typeof(ThemeColors).GetProperty(_propSelector.SelectedItem.ToString());
            Color newCol = Color.FromArgb(_r.Value, _g.Value, _b.Value);
            prop.SetValue(_main._currentColors, newCol);
            _valLabel.Text = $"EDITING: {_propSelector.SelectedItem}\nRGB({newCol.R}, {newCol.G}, {newCol.B})";
            _main.ApplyTheme();
        }
    }
}