//<summary>[DO NOT REMOVE]Program.cs</summary>

using System;
using System.Windows.Forms;
using System.Security.Principal;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace GSWEngine
{
    static class Program
    {
        private static System.Threading.Mutex _mutex = null;

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
            {
                string assemblyName = new System.Reflection.AssemblyName(resolveArgs.Name).Name;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DLLs", assemblyName + ".dll");
                return File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
            };

            bool createdNew;
            _mutex = new System.Threading.Mutex(true, InternalFunctions.AppNameFull.Replace(" ", ""), out createdNew);

            if (!createdNew)
            {
                // Another instance is already running
                MessageBox.Show($"{InternalFunctions.AppNameFull} is already running in the background. Check your system tray.", "Instance Detected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return; // Abort launch
            }

            bool noAdmin = args.Any(a => a.Equals("-noadmin", StringComparison.OrdinalIgnoreCase));
            if (!noAdmin && !IsAdministrator())
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.UseShellExecute = true;
                    startInfo.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    startInfo.FileName = Application.ExecutablePath;
                    startInfo.Verb = "runas";
                    startInfo.Arguments = string.Join(" ", args);
                    Process.Start(startInfo);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    if (ex.NativeErrorCode == 1223)
                    {
                        MessageBox.Show($"{InternalFunctions.AppNameShort} cannot proceed without full Admin privileges. Launch aborted by user.", "Launch Aborted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Critical Elevation Failure: {ex.NativeErrorCode}. Contact {InternalFunctions.AppAuthor} for support.", $"{InternalFunctions.AppNameShort} - System Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                // Launch the Context (The Concierge)
                Application.Run(new Context(args));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Critical Startup Failure: {ex.Message}\n\n{ex.StackTrace}",
                                $"{InternalFunctions.AppNameShort} - Error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}