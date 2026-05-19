//<summary>[DO NOT REMOVE]ThemeEditor.cs</summary>
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Linq;
using System.Text;

namespace GSWEngine
{
    public class ThemeEditor : Form
    {
        private StatsForm _main;
        private Context _ctx;
        private ComboBox _propSelector;
        private TrackBar _r, _g, _b;
        private Label _valLabel;

        // --- SURGERY: Context first, Form optional ---
        public ThemeEditor(Context ctx, StatsForm main = null)
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

            if (_propSelector.Items.Count > 0) _propSelector.SelectedIndex = 0;
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