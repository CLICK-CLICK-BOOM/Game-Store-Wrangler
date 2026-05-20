//<summary>[DO NOT REMOVE]StoreSelector.cs</summary>

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;

namespace GSWEngine
{
    public static class StoreSelectors
    {
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void OpenStorePicker(Form parent, Context ctx, ThemeColors colors, List<StoreTracker> availableStores)
        {
            using (Form f = new Form
            {
                Text = "SELECT TARGET STORE",
                Width = 300,
                Height = 400,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                TopMost = true
            })
            {

                ListBox list = new ListBox { Dock = DockStyle.Top, Height = 300, BorderStyle = BorderStyle.None };

                // Populate list sorted alphabetically
                foreach (var s in availableStores.OrderBy(x => x.DisplayName))
                {
                    list.Items.Add(s.DisplayName);
                }

                Button btn = new Button
                {
                    Text = "Add to Surveillance",
                    Dock = DockStyle.Bottom,
                    Height = 40,
                    FlatStyle = FlatStyle.Flat
                };

                btn.Click += (s, e) =>
                {
                    f.ActiveControl = null;
                    if (list.SelectedItem != null)
                    {
                        string selectedDisplayName = list.SelectedItem.ToString();

                        // 1. CAPACITY LOCK (12 Slots)
                        if (ctx.trackers.Count >= 12)
                        {
                            MessageBox.Show("Maximum Surveillance Capacity (12) Reached.", "SYSTEM LIMIT");
                        }
                        // 2. DUPLICATE CHECK (Case-Insensitive)
                        else if (!ctx.trackers.Any(t => t.DisplayName.Equals(selectedDisplayName, StringComparison.OrdinalIgnoreCase)))
                        {
                            // 3. THE FIX: Search template using Case-Insensitive logic
                            // This ensures "Itch.io" (UI) matches "itch.io" (Master) so the flag clones correctly.
                            var template = availableStores.FirstOrDefault(t =>
                                t.DisplayName.Equals(selectedDisplayName, StringComparison.OrdinalIgnoreCase));

                            if (template != null)
                            {
                                var newTracker = new StoreTracker(template.DisplayName, template.ProcessName, ctx.Settings.GracePeriod)
                                {
                                    IsGallowsCompatible = template.IsGallowsCompatible,
                                    DisplayIndex = ctx.trackers.Count
                                };

                                ctx.trackers.Add(newTracker);

                                // --- XBOX DEPENDENCY PILLAR FIX ---
                                InternalFunctions.EnforceXboxDependencies(ctx);

                                ctx.SaveSettings();

                                if (parent is StatsForm main) main.UpdateUI();
                            }
                        }
                        f.Close();
                    }
                };

                f.Controls.Add(list);
                f.Controls.Add(btn);

                ApplyRequestorTheme(f, colors, list, btn);
                f.TopMost = parent.TopMost;
                f.ShowDialog(parent);
            }
        }

        public static void OpenThemePicker(Form parent, Context ctx, ThemeColors colors, string[] themes, Action onThemeChanged)
        {
            using (Form f = new Form
            {
                Text = "SELECT INTERFACE THEME",
                Width = 300,
                Height = 250,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                TopMost = true
            })
            {

                ListBox list = new ListBox { Dock = DockStyle.Top, Height = 150, BorderStyle = BorderStyle.None };
                var sortedThemes = themes.OrderBy(t => t).ToArray();
                list.Items.AddRange(sortedThemes);

                if (ctx.Settings.ThemeIndex >= 0 && ctx.Settings.ThemeIndex < themes.Length)
                {
                    string currentThemeName = themes[ctx.Settings.ThemeIndex];
                    list.SelectedIndex = Array.IndexOf(sortedThemes, currentThemeName);
                }

                Button btn = new Button
                {
                    Text = "Apply Theme",
                    Dock = DockStyle.Bottom,
                    Height = 40,
                    FlatStyle = FlatStyle.Flat
                };

                btn.Click += (s, e) =>
                {
                    f.ActiveControl = null;
                    if (list.SelectedItem != null)
                    {
                        string selectedName = list.SelectedItem.ToString();
                        int originalIndex = Array.IndexOf(themes, selectedName);
                        if (originalIndex >= 0) ctx.CurrentTheme = originalIndex;
                    }
                    ctx.SaveSettings();
                    onThemeChanged?.Invoke();
                    f.Close();
                };

                f.Controls.Add(list);
                f.Controls.Add(btn);

                ApplyRequestorTheme(f, colors, list, btn);
                f.TopMost = parent.TopMost;
                f.ShowDialog(parent);
            }
        }

        private static void ApplyRequestorTheme(Form f, ThemeColors colors, ListBox list, Button btn)
        {
            // Windows Title Bar DWM Sync
            if (Environment.OSVersion.Version.Major >= 10)
            {
                int col = ColorTranslator.ToWin32(colors.RequestorBg);
                DwmSetWindowAttribute(f.Handle, 35, ref col, sizeof(int));
            }

            f.BackColor = colors.RequestorBg;

            list.BackColor = colors.RequestorBg;
            list.ForeColor = colors.RequestorListFg;
            list.BorderStyle = BorderStyle.None;
            list.DrawMode = DrawMode.OwnerDrawFixed;

            btn.BackColor = colors.GeneralBtnBg;
            btn.ForeColor = colors.GeneralBtnFg;
            btn.FlatAppearance.BorderColor = colors.GeneralBtnBorder;
            btn.FlatAppearance.BorderSize = 1;

            list.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;

                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                Color bgCol = isSelected ? colors.RequestorHighlightBg : colors.RequestorBg;
                Color txCol = isSelected ? colors.RequestorHighlightFg : colors.RequestorListFg;

                using (SolidBrush b = new SolidBrush(bgCol))
                    e.Graphics.FillRectangle(b, e.Bounds);

                TextRenderer.DrawText(e.Graphics, list.Items[e.Index].ToString(), list.Font, e.Bounds, txCol, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };
        }
    }
}