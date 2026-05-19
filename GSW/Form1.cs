//<summary>[DO NOT REMOVE]Form1.cs</summary>

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace GSWEngine
{
    public partial class StatsForm : Form
    {
        public const string AppID = "GSWEngine.LauncherMonitor.UniqueV1";
        private readonly int _statusDisplaySeconds = 10;
        private readonly int[] _statusBeat = { 1, 1, 2, 1, 1, 2, 3 }; // 1=Funny, 2=Forensic, 3=Support
        private int _beatIndex = 0;
        private int _pulseTick = 0;
        private bool _isEngineInitialized = false;
        private int _engineTicks = 0;
        private int _currentPhase = 0; // 0 = Startup, 1 = Ludicrous, 2 = Forensic
        private int _forensicIndex = 0;
        private DateTime _appStartTime = DateTime.Now;
        private float _scanOffset = 0;
        private readonly Context _ctx;
        private readonly System.Windows.Forms.Timer _pulseTimer;
        private readonly string[] _themes = { "Cubic", "Alarm", "Cyber", "Deep", "Arctic" };
        private StatusData _statusData;
        internal ThemeColors _currentColors;
        private Dictionary<string, int> _themeUsage = new Dictionary<string, int>();
        private IntelForm _intelForm;
        private ToolTip _canvasToolTip = new ToolTip();
        private int _statusBeatCount = 0;
        private int _supporterIndex = 0;
        private int _lastTooltipRow = -1;
        private bool _isSortMode = false;
        private List<StoreTracker> _sortQueue = new List<StoreTracker>();
        private bool _isFinalizingSort = false;


        public StatsForm(Context context)
        {
            SetCurrentProcessExplicitAppUserModelID(AppID);
            _ctx = context;
            _statusData = InternalFunctions.LoadStatusData();

            try
            {
                string iconPath = InternalFunctions.AppIconPath;
                if (System.IO.File.Exists(iconPath)) this.Icon = new Icon(iconPath);
                else this.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            this.StartPosition = FormStartPosition.Manual;
            InitializeManualComponents();
            this.Text = $"{InternalFunctions.AppNameShort} {InternalFunctions.AppVersion}";

            canvas.MouseMove += Canvas_MouseMove;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseUp += Canvas_MouseUp;

            // Added for interactivity
            lblAward.Cursor = Cursors.Hand;
            lblAward.Click += (s, e) =>
            {
                // Only launch the link if Phase 3 (Support) is active
                if (_currentPhase == 3)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "https://buy.stripe.com/dRm9AS275dFo1tc8rP6sw00",
                            UseShellExecute = true
                        });
                    }
                    catch { } // Fail silently if the OS blocks the shell launch
                }
            };

            // CRITICAL: Interval set to 50ms (20 FPS) for smooth animation
            _pulseTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _pulseTimer.Tick += (s, e) => HandleHeartbeat();
            _pulseTimer.Start();

            ApplyTheme();
            this.DoubleBuffered = true;

            if (!_themeUsage.Any() && _ctx.CurrentTheme >= 0 && _ctx.CurrentTheme < _themes.Length)
            {
                string currentThemeName = _themes[_ctx.CurrentTheme];
                _themeUsage[currentThemeName] = 1;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.TopMost = _ctx.Settings.TopMost;
            UpdateOnTopVisuals();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // PERFECT ROUTING: Accessing data through the Settings bridge
            // Check for First Run (-1)
            if (_ctx.Settings.WindowX == -1 || _ctx.Settings.WindowY == -1)
            {
                // Action 1: First Run Side-by-Side Logic
                var screen = Screen.PrimaryScreen.WorkingArea;
                int hudWidth = this.Width;
                int intelWidth = 515; // IntelForm width
                int gap = 5;
                int totalWidth = hudWidth + gap + intelWidth;

                // Calculate centered start for HUD
                int startX = screen.Left + (screen.Width - totalWidth) / 2;
                int startY = screen.Top + (screen.Height - this.Height) / 2;

                this.StartPosition = FormStartPosition.Manual;
                this.Location = new Point(startX, startY);

                // Spawn Intel immediately
                if (!Application.OpenForms.OfType<IntelForm>().Any())
                {
                    // Pre-seed settings so IntelForm constructor picks up the location
                    _ctx.Settings.IntelX = this.Right + gap;
                    _ctx.Settings.IntelY = this.Top;

                    _intelForm = new IntelForm(_ctx, _currentColors, new Size(515, 716));
                    _intelForm.TopMost = this.TopMost;
                    _intelForm.FormClosed += (s, ev) => { _intelForm = null; };
                    _intelForm.Show(this);

                    // Action 3: Save Trigger
                    _ctx.Settings.WindowX = this.Location.X;
                    _ctx.Settings.WindowY = this.Location.Y;
                    _ctx.SaveSettings();
                }
            }
            else
            {
                Point target = new Point(_ctx.Settings.WindowX, _ctx.Settings.WindowY);
                bool isOffScreen = !Screen.AllScreens.Any(s => s.WorkingArea.Contains(target));

                if (isOffScreen)
                {
                    this.CenterToScreen();
                }
                else
                {
                    this.Location = target;
                }
            }

            this.TopMost = _ctx.Settings.TopMost;
            UpdateOnTopVisuals();
            UpdateVisibilityState();
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            if (this.WindowState == FormWindowState.Normal && this.Visible)
            {
                // PERFECT ROUTING: Saving data through the Settings bridge
                _ctx.Settings.WindowX = this.Location.X;
                _ctx.Settings.WindowY = this.Location.Y;
                _ctx.SaveSettings();
            }
        }

        private void chkAlwaysOnTop_Click(object sender, EventArgs e)
        {
            // Routed to _ctx.Settings
            _ctx.Settings.TopMost = !_ctx.Settings.TopMost;
            this.TopMost = _ctx.Settings.TopMost;
            UpdateOnTopVisuals();
            _ctx.SaveSettings();
        }

        public void UpdateUI()
        {
            if (canvas.InvokeRequired) canvas.Invoke(new Action(UpdateUI));
            else { canvas.Invalidate(); RefreshLog(); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (this.WindowState == FormWindowState.Minimized)
            {
                // Enter Low Power Mode: Kill the UI heartbeat completely.
                if (_pulseTimer != null) _pulseTimer.Stop();
            }
            else
            {
                // Restore Normal Operation: Resuscitate the UI heartbeat.
                if (_pulseTimer != null && !_pulseTimer.Enabled) _pulseTimer.Start();
            }

            UpdateVisibilityState();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateVisibilityState();
        }

        private void UpdateVisibilityState()
        {
            if (_ctx != null)
            {
                _ctx.IsUiVisible = this.Visible && this.WindowState != FormWindowState.Minimized;
            }
        }

        private void btnAddStore_Click(object sender, EventArgs e)
        {
            var allStores = InternalFunctions.GetAllPossibleStores();
            var availableStores = allStores.Where(m => !_ctx.trackers.Any(t => t.ProcessName.Equals(m.ProcessName, StringComparison.OrdinalIgnoreCase))).ToList();
            StoreSelectors.OpenStorePicker(this, _ctx, _currentColors, availableStores);
            this.ActiveControl = null;
        }

        private void btnThemes_Click(object sender, EventArgs e)
        {
            StoreSelectors.OpenThemePicker(this, _ctx, _currentColors, _themes, () =>
            {
                _currentColors = null;
                ApplyTheme();

                if (_intelForm != null && !_intelForm.IsDisposed)
                {
                    _intelForm.ApplyTheme();
                }

                // 2. NEW: Broadcast to the Pop-Out Terminal if open
                var openTerminal = Application.OpenForms.OfType<LogTerminalForm>().FirstOrDefault();
                if (openTerminal != null && !openTerminal.IsDisposed)
                {
                    openTerminal.ApplyTheme();
                }

                string selectedTheme = _themes[_ctx.CurrentTheme];

                // Routed to _ctx.Settings
                if (!_ctx.Settings.ThemeUsage.ContainsKey(selectedTheme)) _ctx.Settings.ThemeUsage[selectedTheme] = 0;
                _ctx.Settings.ThemeUsage[selectedTheme]++;
                _ctx.SaveSettings();
            });
        }

        private void btnInfo_Click(object sender, EventArgs e)
        {
            this.ActiveControl = null;
            if (Application.OpenForms.OfType<IntelForm>().Any()) return;
            _intelForm = new IntelForm(_ctx, _currentColors, new Size(515, 716));
            _intelForm.TopMost = this.TopMost;
            _intelForm.FormClosed += (s, ev) => { _intelForm = null; };
            _intelForm.Show(this);
        }

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);
    }
}