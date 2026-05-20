//<summary>[DO NOT REMOVE]GridPainter.cs</summary>

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GSWEngine
{
    public static class GridPainter
    {
        // TACTICAL PALETTE
        public static Color ColorActive = Color.FromArgb(0, 255, 150);     // Neon Green
        public static Color ColorGameActive = Color.FromArgb(0, 200, 255); // Neon Blue
        public static Color ColorGallows = Color.FromArgb(255, 50, 50);    // Target Red
        public static Color ColorGrace = Color.FromArgb(255, 170, 0);      // Warning Amber
        public static Color ColorWarning = Color.OrangeRed;               // CPU Danger

        private static Image _alphaIcon;
        private static Image _customIcon;
        private static bool _sortIconsLoaded = false;
        public static int PressedIcon { get; set; } = 0;


        public static float TickThickness = 2.5f;
        public static double IdleCpuThreshold = 1.5;

        public static void PaintTacticalOverlay(Graphics g, int y, int rowHeight, int canvasWidth, StoreTracker t, float pulse, Color themeColor)
        {
            Rectangle rowRect = new Rectangle(2, y - 5, canvasWidth - 5, rowHeight - 2);

            // 1. THE ENGAGEMENT SWEEP (Store is actively running a GAME)
            if (t.CurrentStatus.Contains("GAME"))
            {
                using (LinearGradientBrush br = new LinearGradientBrush(rowRect, Color.Transparent, Color.Transparent, 0f))
                {
                    ColorBlend cb = new ColorBlend();
                    cb.Positions = new[] { 0f, 0.5f, 1f };

                    int alpha = (int)(75 + (Math.Sin(pulse * Math.PI * 2) * 20));
                    cb.Colors = new[] {
                Color.Transparent,
                Color.FromArgb(alpha, themeColor),
                Color.Transparent
            };

                    br.InterpolationColors = cb;
                    float sweepWidth = rowRect.Width * 2f;
                    br.TranslateTransform((pulse * sweepWidth) - rowRect.Width, 0);
                    g.FillRectangle(br, rowRect);
                }

                using (Pen p = new Pen(Color.FromArgb(30, Color.White), 1f))
                {
                    g.DrawLine(p, rowRect.Left, rowRect.Top, rowRect.Right, rowRect.Top);
                }
            }
            // 2. THE COUNTDOWN ALERT (Store is on "Death Row")
            // This no longer cares if it's MIN or ACTIVE. If the timer is ticking, it pulses.
            else if (t.TimeLeft < t.MaxTime && t.TimeLeft > 0 && !t.ManualOverride)
            {
                // Pumped intensity: Floor 70, Peak 130
                int alpha = (int)(100 + (Math.Sin(pulse * Math.PI * 4) * 30));

                using (var br = new SolidBrush(Color.FromArgb(alpha, themeColor)))
                {
                    g.FillRectangle(br, rowRect);
                }
            }
        }


        private static Color GetStatusColor(string status)
        {
            return status switch
            {
                "ACTIVE" => ColorActive,
                "MIN" => Color.FromArgb(100, 100, 110),
                "OFFLINE" => Color.FromArgb(40, 40, 45),
                "Xbox Buddy" => Color.MediumPurple,
                _ => Color.White
            };
        }

        public static void PaintSortColumnHighlight(Graphics g, int startY, int rowHeight, int rowCount, Color highlightColor)
        {
            if (rowCount <= 0) return;
            using (Brush b = new SolidBrush(Color.FromArgb(150, highlightColor)))
            {
                g.FillRectangle(b, 2, startY - 5, 102, rowCount * rowHeight);
            }
        }

        public static void PaintSortIcons(Graphics g, Font f, Color defaultColor, Color activeColor, bool isSortMode, Action<string> log = null)
        {
            if (!_sortIconsLoaded)
            {
                try
                {
                    var assembly = typeof(Context).Assembly;
                    bool missing = false;

                    using (var alphaStream = assembly.GetManifestResourceStream("GSWEngine.Sort_Alpha_icon.png"))
                    {
                        if (alphaStream != null) _alphaIcon = Image.FromStream(alphaStream);
                        else missing = true;
                    }

                    using (var customStream = assembly.GetManifestResourceStream("GSWEngine.Sort_Custom_icon.png"))
                    {
                        if (customStream != null) _customIcon = Image.FromStream(customStream);
                        else missing = true;
                    }

                    if (missing && log != null) log("[System] Warning: Icon resource missing. Using placeholders.");
                }
                catch { }
                _sortIconsLoaded = true;
            }

            Rectangle alphaRect = new Rectangle(72, 4, 16, 16);
            if (PressedIcon == 1) alphaRect.Inflate(3, 3); // 1.5x scale (30x30)

            if (_alphaIcon != null)
            {
                g.DrawImage(_alphaIcon, alphaRect);
            }
            else
            {
                using (Brush b = new SolidBrush(defaultColor))
                    g.DrawString("@", f, b, alphaRect.X + 2, alphaRect.Y + 2);
            }

            Rectangle customRect = new Rectangle(86, 4, 16, 16);
            if (PressedIcon == 2) customRect.Inflate(3, 3); // 1.5x scale (30x30)

            if (_customIcon != null)
            {
                g.DrawImage(_customIcon, customRect);
            }
            else
            {
                using (Brush b = new SolidBrush(isSortMode ? activeColor : defaultColor))
                    g.DrawString("#", f, b, customRect.X + 2, customRect.Y + 2);
            }
        }

        public static void PaintSortStickers(Graphics g, int startY, int rowHeight, List<StoreTracker> visibleTrackers, List<StoreTracker> sortQueue, Color stampColor, Color textColor)
        {
            if (sortQueue == null || sortQueue.Count == 0) return;
            var originalSmoothingMode = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = 0; i < sortQueue.Count; i++)
            {
                var t = sortQueue[i];
                int rowIndex = visibleTrackers.IndexOf(t);
                if (rowIndex < 0) continue;

                int y = startY + (rowIndex * rowHeight);
                int stampNumber = i + 1;

                Rectangle stampRect = new Rectangle(82, y - 3, 19, 19);
                using (Brush circleBrush = new SolidBrush(stampColor))
                {
                    g.FillEllipse(circleBrush, stampRect);
                }
                using (Brush textBrush = new SolidBrush(Color.White))
                using (Font f = new Font("Segoe UI", 8, FontStyle.Bold))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    Rectangle textRect = new Rectangle(stampRect.X + 1, stampRect.Y + 1, stampRect.Width, stampRect.Height + 1);
                    g.DrawString(stampNumber.ToString(), f, textBrush, textRect, sf);
                }
            }
            g.SmoothingMode = originalSmoothingMode;
        }
    }
}