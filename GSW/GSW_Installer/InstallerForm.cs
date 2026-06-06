using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace GSWEngine.Installer
{
    public class InstallerForm : Form
    {
        private Panel _page1;
        private Panel _page2;
        private TextBox _txtPath;
        private RichTextBox _rtbMessage;
        private Button _btnInstallOnly;
        private Button _btnInstallRegister;
        private Button _btnBack;
        private CheckBox _chkLaunch;
        private CheckBox _chkStartWindows;
        private Label _lblStatus;

        // --- VERSION MANAGER ---
        public const string TargetAppVersion = "0.9.0";

        // Theming (Aligned with GSW Cubic/Dark aesthetics)
        private readonly Color _bg = Color.FromArgb(20, 20, 20);
        private readonly Color _fg = Color.FromArgb(255, 255, 255);
        private readonly Color _btnBg = Color.FromArgb(40, 40, 40);
        private readonly Color _btnBorder = Color.FromArgb(100, 100, 100);
        private readonly Color _accent = Color.FromArgb(50, 150, 219);

        public InstallerForm()
        {
            this.Text = "Game Store Wrangler - System Installer";
            this.Size = new Size(550, 250);
            this.BackColor = _bg;
            this.ForeColor = _fg;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;

            InitializePage1();
            InitializePage2();

            this.Controls.Add(_page2);
            this.Controls.Add(_page1);

            _page2.Visible = false;
        }

        private void InitializePage1()
        {
            _page1 = new Panel { Dock = DockStyle.Fill, BackColor = _bg };

            Label lblDest = new Label { Text = "Select Destination Folder:", Location = new Point(30, 35), AutoSize = true, ForeColor = _fg, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            _txtPath = new TextBox { Location = new Point(30, 65), Width = 380, Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GameStoreWrangler"), BackColor = _btnBg, ForeColor = _fg, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9) };

            Button btnBrowse = CreateButton("Browse...", new Rectangle(420, 65, 80, 23));
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    if (fbd.ShowDialog() == DialogResult.OK) _txtPath.Text = fbd.SelectedPath;
                }
            };

            Button btnNext = CreateButton("Next >", new Rectangle(157, 125, 220, 45));
            btnNext.Click += (s, e) => { _page1.Visible = false; _page2.Visible = true; };

            _page1.Controls.Add(lblDest);
            _page1.Controls.Add(_txtPath);
            _page1.Controls.Add(btnBrowse);
            _page1.Controls.Add(btnNext);
        }

        private void InitializePage2()
        {
            _page2 = new Panel { Dock = DockStyle.Fill, BackColor = _bg };

            _rtbMessage = new RichTextBox
            {
                Location = new Point(30, 15),
                Size = new Size(470, 100),
                ReadOnly = true,
                BackColor = _btnBg,
                ForeColor = _fg,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9),
                Text = "Welcome to Game Store Wrangler.\n\nPlease scroll to the bottom of this text to review all installation options.\n\nNote: This application requires the .NET 9 Desktop Runtime. If it is not detected on your system, this installer will automatically deploy it for you.\n\nTo help us improve the software and understand its global reach, we optionally collect anonymous installation telemetry.\n\nData Collected:\n- Application Version\n- Country Code (ISO standard)\n- Operating System Version\n\nNo personal information, usernames, IP addresses, or hardware signatures are transmitted.\n\n\n[End of Document]"
            };
            _rtbMessage.VScroll += RtbMessage_VScroll;

            _btnBack = CreateButton("Back", new Rectangle(30, 125, 80, 45));
            _btnBack.Click += (s, e) => { _page2.Visible = false; _page1.Visible = true; };

            _btnInstallRegister = CreateButton("Install - Register", new Rectangle(120, 125, 185, 45));
            _btnInstallRegister.BackColor = _accent;
            _btnInstallRegister.ForeColor = Color.White;
            _btnInstallRegister.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            _btnInstallRegister.Click += async (s, e) => await PerformInstall(true);

            _btnInstallOnly = CreateButton("Install - Do not Register", new Rectangle(315, 125, 185, 45));
            _btnInstallOnly.Visible = false;
            _btnInstallOnly.Font = new Font("Segoe UI", 10, FontStyle.Regular);
            _btnInstallOnly.Click += async (s, e) => await PerformInstall(false);

            _chkLaunch = new CheckBox
            {
                Text = "Launch Game Store Wrangler now",
                Location = new Point(60, 180),
                AutoSize = true,
                ForeColor = _fg,
                Checked = true
            };

            _chkStartWindows = new CheckBox
            {
                Text = "Start with Windows (Recommended)",
                Location = new Point(280, 180),
                AutoSize = true,
                ForeColor = _fg,
                Checked = true
            };

            _lblStatus = new Label
            {
                Location = new Point(30, 55),
                AutoSize = true,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = _accent,
                Visible = false
            };

            _page2.Controls.Add(_rtbMessage);
            _page2.Controls.Add(_btnBack);
            _page2.Controls.Add(_btnInstallRegister);
            _page2.Controls.Add(_btnInstallOnly);
            _page2.Controls.Add(_chkLaunch);
            _page2.Controls.Add(_chkStartWindows);
            _page2.Controls.Add(_lblStatus);
        }

        private void RtbMessage_VScroll(object sender, EventArgs e)
        {
            // Evaluates if the last character is within the visible drawing bounds of the box
            int scrollMax = _rtbMessage.GetPositionFromCharIndex(_rtbMessage.TextLength - 1).Y;
            if (scrollMax <= _rtbMessage.Height)
            {
                _btnInstallOnly.Visible = true;
            }
        }

        private Button CreateButton(string text, Rectangle bounds)
        {
            Button b = new Button
            {
                Text = text,
                Bounds = bounds,
                FlatStyle = FlatStyle.Flat,
                BackColor = _btnBg,
                ForeColor = _fg,
                Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = _btnBorder;
            return b;
        }

        [SupportedOSPlatform("windows")]
        private void CreateDesktopShortcut(string exePath, string targetDir)
        {
            try
            {
                string shortcutLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Game Store Wrangler.lnk");
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(t);
                var shortcut = shell.CreateShortcut(shortcutLocation);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = targetDir;
                shortcut.Save();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to create shortcut: " + ex.Message);
            }
        }

        private void ExtractResource(string resourceName, string outputPath)
        {
            using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new Exception($"Resource '{resourceName}' not found in the installer manifest.");
                using (Stream output = File.Create(outputPath))
                {
                    input.CopyTo(output);
                }
            }
        }

        private async Task PerformInstall(bool registerTelemetry)
        {
            _btnInstallRegister.Enabled = false;
            _btnInstallOnly.Enabled = false;

            _rtbMessage.Visible = false;
            _lblStatus.Text = "Extracting Game Store Wrangler (This may take a moment)...";
            _lblStatus.Visible = true;
            _lblStatus.Refresh();

            try
            {
                string targetDir = _txtPath.Text;
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                // Warden's Check: Verify .NET 9 Desktop Runtime
                bool netInstalled = false;
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App"))
                    {
                        if (key != null)
                        {
                            foreach (string valName in key.GetValueNames())
                            {
                                if (valName.StartsWith("9.0"))
                                {
                                    netInstalled = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"[GSW SILENT EXCEPTION] {ex.Message} | Source: {ex.StackTrace}");
                }

                if (!netInstalled)
                {
                    _lblStatus.Text = "Installing required .NET 9 Environment (Silent)...";
                    _lblStatus.Refresh();

                    string tempInstallerPath = Path.Combine(Path.GetTempPath(), "dotnet9_installer.exe");
                    ExtractResource("GSW_Installer.Resources.DotNetRuntime.exe", tempInstallerPath);

                    using (Process p = new Process())
                    {
                        p.StartInfo.FileName = tempInstallerPath;
                        p.StartInfo.Arguments = "/install /quiet /norestart";
                        p.StartInfo.UseShellExecute = true;
                        p.StartInfo.Verb = "runas"; // Request elevation for runtime install
                        p.Start();
                        p.WaitForExit();
                    }

                    if (File.Exists(tempInstallerPath)) File.Delete(tempInstallerPath);

                    _lblStatus.Text = "Extracting Game Store Wrangler...";
                    _lblStatus.Refresh();
                }

                string exePath = Path.Combine(targetDir, "GSW.exe");
                ExtractResource("GSW_Installer.Resources.GSW.exe", exePath);
                CreateDesktopShortcut(exePath, targetDir);

                // 1. Start with Windows Logic
                if (_chkStartWindows.Checked)
                {
                    using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        runKey?.SetValue("GameStoreWrangler", $"\"{exePath}\"");
                    }
                }
                else
                {
                    using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        runKey?.DeleteValue("GameStoreWrangler", false);
                    }
                }

                // 2. Generate Uninstaller Script
                string uninstallerPath = Path.Combine(targetDir, "Uninstall.bat");
                string batContent = $"@echo off\n" +
                                    $"echo Uninstalling Game Store Wrangler...\n" +
                                    $"taskkill /F /IM GSW.exe >nul 2>&1\n" +
                                    $"reg delete \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\GameStoreWrangler\" /f >nul 2>&1\n" +
                                    $"reg delete \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"GameStoreWrangler\" /f >nul 2>&1\n" +
                                    $"reg delete \"HKCU\\SOFTWARE\\GSW\" /f >nul 2>&1\n" +
                                    $"rmdir /S /Q \"{targetDir}\"\n" +
                                    $"exit";
                File.WriteAllText(uninstallerPath, batContent);

                // 3. Register in Windows "Installed Apps"
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GameStoreWrangler"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "Game Store Wrangler");
                        key.SetValue("DisplayIcon", $"\"{exePath}\"");
                        key.SetValue("UninstallString", $"\"{uninstallerPath}\"");
                        key.SetValue("Publisher", "GSW");
                        key.SetValue("DisplayVersion", TargetAppVersion); // Strict Version Control Mapping
                    }
                }

                if (registerTelemetry)
                {
                    try
                    {
                        // Warden's Memory: Prevent duplicate registrations
                        bool alreadyRegistered = false;
                        using (var key = Registry.CurrentUser.OpenSubKey(@"Software\GSW", false))
                        {
                            if (key?.GetValue("TelemetrySent")?.ToString() == "1") alreadyRegistered = true;
                        }

                        if (!alreadyRegistered)
                        {
                            await SupabaseManager.Initialize();
                            if (SupabaseManager.Client != null)
                            {
                                var installEvent = new InstallEvent
                                {
                                    Version = TargetAppVersion, // Synchronized with Supabase DB Schema
                                    Country = RegionInfo.CurrentRegion.TwoLetterISORegionName,
                                    OS = Environment.OSVersion.VersionString
                                };

                                var response = await SupabaseManager.Client.From<InstallEvent>().Insert(installEvent);

                                // ONLY if the server confirmed the write, we record it in the Registry
                                if (response.Models.Count > 0)
                                {
                                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\GSW"))
                                    {
                                        key.SetValue("TelemetrySent", "1");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Debug.WriteLine($"Telemetry Failsafe: {ex.Message}"); }
                }

                MessageBox.Show($"Game Store Wrangler Installation Complete.\n\nInstalled to: {targetDir}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (_chkLaunch.Checked)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = targetDir,
                        UseShellExecute = true
                    });
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _btnInstallRegister.Enabled = true;
                _btnInstallOnly.Enabled = true;
            }
        }
    }
}