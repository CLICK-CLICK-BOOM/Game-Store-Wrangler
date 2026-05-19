using System;
using System.Windows.Forms;
using GSWEngine.Installer;

namespace GSWEngine.Installer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Launch the Installer Interface
            Application.Run(new InstallerForm());
        }
    }
}