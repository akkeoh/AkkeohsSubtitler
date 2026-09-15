using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AkkeohsVegas.Core;

namespace AkkeohsVegas.Installer
{
    internal static class Program
    {
        static Program()
        {

            EmbeddedPayload.RegisterAssemblyResolver();
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool quiet = HasArg(args, "/quiet") || HasArg(args, "/silent");
            bool uninstall =
                HasArg(args, "/uninstall")
                || HasArg(args, "-uninstall")
                || IsUninstallerExecutable();

            if (uninstall)
            {
                RunUninstall(quiet);
                return;
            }

            Application.Run(new MainForm());
        }

        private static bool IsUninstallerExecutable()
        {
            try
            {
                string name = Path.GetFileNameWithoutExtension(Application.ExecutablePath) ?? string.Empty;
                return name.IndexOf("uninstall", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasArg(string[] args, string flag)
        {
            if (args == null || args.Length == 0)
                return false;
            return args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
        }

        private static void RunUninstall(bool quiet)
        {
            if (!quiet)
            {
                DialogResult result = MessageBox.Show(
                    "Uninstall " + ProductInfo.DisplayName + "?\n\nThis removes the extension, downloaded binaries/models, and Apps & Features entry.",
                    ProductInfo.DisplayName + " — Uninstall",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (result != DialogResult.Yes)
                    return;
            }

            try
            {
                var logLines = new System.Collections.Generic.List<string>();
                var service = new InstallService(msg => logLines.Add(msg));
                service.UninstallFromManifest();

                if (!quiet)
                {
                    MessageBox.Show(
                        "Uninstall complete.\n\nRestart VEGAS if it is open.",
                        ProductInfo.DisplayName + " — Uninstall",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (!quiet)
                {
                    MessageBox.Show(
                        ex.Message,
                        ProductInfo.DisplayName + " — Uninstall",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                Environment.ExitCode = 1;
            }
        }
    }
}
