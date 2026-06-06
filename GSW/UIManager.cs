//UIManager.cs

using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GSWEngine
{
    public class UIManager
    {
        private readonly Context _ctx;
        private NotifyIcon _trayIcon;
        private StatsForm _statsWindow;
        private DebugTools _debugEditor;

        public UIManager(Context context)
        {
            _ctx = context;
            InitializeTray();

            // Re-planting the Tweaker Deployment logic
            if (StateEngine.IsDebug)
            {
                InitializeTweaker();
            }
        }

        private void InitializeTray()
        {
            Icon targetIcon = null;
            try
            {
                // Single-File profile safe path resolution: pulls the actual native path of the running executable payload
                string executablePath = System.Environment.ProcessPath;

                if (!string.IsNullOrEmpty(executablePath) && System.IO.File.Exists(executablePath))
                {
                    targetIcon = Icon.ExtractAssociatedIcon(executablePath);
                }
                else
                {
                    // Primary directory fallback using the compiler's recommended base target
                    string pathFallback = System.IO.Path.Combine(System.AppContext.BaseDirectory, "GSW.exe");
                    targetIcon = System.IO.File.Exists(pathFallback) ? Icon.ExtractAssociatedIcon(pathFallback) : SystemIcons.Application;
                }
            }
            catch
            {
                // Final contingency safety check for local relative execution
                targetIcon = System.IO.File.Exists("AppIcon.ico") ? new Icon("AppIcon.ico") : SystemIcons.Application;
            }

            _trayIcon = new NotifyIcon()
            {
                Icon = targetIcon,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = InternalFunctions.AppNameShort
            };

            _trayIcon.ContextMenuStrip.Items.Add("Show Stats", null, (s, e) => ShowMainHUD());
            // This link is critical: calls the Exit method in Context
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => _ctx.Exit());
            _trayIcon.DoubleClick += (s, e) => ShowMainHUD();
        }

        private void InitializeTweaker()
        {
            _debugEditor = new DebugTools(_ctx);

            // Logic preserved: Restore Tweaker position from Settings
            if (_ctx.Settings.TweakerX != -1 && _ctx.Settings.TweakerY != -1)
            {
                _debugEditor.StartPosition = FormStartPosition.Manual;
                _debugEditor.Location = new Point(_ctx.Settings.TweakerX, _ctx.Settings.TweakerY);
            }

            _debugEditor.FormClosing += (s, ev) =>
            {
                if (_debugEditor.WindowState == FormWindowState.Normal)
                {
                    _ctx.Settings.TweakerX = _debugEditor.Location.X;
                    _ctx.Settings.TweakerY = _debugEditor.Location.Y;
                    InternalFunctions.SaveSettings(_ctx.Settings);
                }
            };

            _debugEditor.Show();
        }

        public void ShowMainHUD()
        {
            if (_statsWindow == null || _statsWindow.IsDisposed)
            {
                _statsWindow = new StatsForm(_ctx);

                // Logic preserved: Position Restore from Settings
                if (_ctx.Settings.WindowX != -1 && _ctx.Settings.WindowY != -1)
                {
                    _statsWindow.StartPosition = FormStartPosition.Manual;
                    _statsWindow.Location = new Point(_ctx.Settings.WindowX, _ctx.Settings.WindowY);
                }

                if (StateEngine.IsDebug && _debugEditor != null)
                {
                    _debugEditor.LinkForm(_statsWindow);
                }

                _statsWindow.FormClosing += (s, ev) =>
                {
                    if (_statsWindow.WindowState == FormWindowState.Normal)
                    {
                        _ctx.Settings.WindowX = _statsWindow.Location.X;
                        _ctx.Settings.WindowY = _statsWindow.Location.Y;
                        InternalFunctions.SaveSettings(_ctx.Settings);
                    }
                    // Explicitly tell the Context to stop the UI CPU polling loop
                    _ctx.IsUiVisible = false;
                };
            }

            _statsWindow.TopMost = _ctx.Settings.TopMost;
            _statsWindow.Show();
            _statsWindow.WindowState = FormWindowState.Normal;
            _statsWindow.Activate();
        }

        public void RefreshUI()
        {
            if (_statsWindow != null && !_statsWindow.IsDisposed)
            {
                InternalFunctions.RefreshUI(_statsWindow);
            }
        }

        public void DisposeTray()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
        }
    }
}