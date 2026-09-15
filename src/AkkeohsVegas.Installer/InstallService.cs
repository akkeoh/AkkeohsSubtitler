using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using AkkeohsVegas.Core;

namespace AkkeohsVegas.Installer
{
    public sealed class InstallService
    {
        public const string UninstallerFileName = "akkeohs_subtitler_uninstall.exe";
        public const string ManifestFileName = "install-manifest.json";
        private const string UninstallRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AkkeohsSubtitler";
        public const string SetupCacheFolderName = "AkkeohsSubtitlerSetupCache";

        private readonly Action<string> _log;

        public InstallService(Action<string> log)
        {
            _log = log ?? (s => { });
        }

        public static string SetupCacheDirectory
        {
            get { return Path.Combine(Path.GetTempPath(), SetupCacheFolderName); }
        }

        public void Install(
            string payloadRoot,
            VegasExtensionTarget vegasTarget,
            IList<ModelDownloadOption> selectedModels,
            CancellationToken cancel)
        {
            if (vegasTarget == null || string.IsNullOrWhiteSpace(vegasTarget.ExtensionsDirectory))
                throw new InvalidOperationException("Select a VEGAS Pro version.");
            if (selectedModels == null || selectedModels.Count == 0)
                throw new InvalidOperationException("Select at least the required models.");

            cancel.ThrowIfCancellationRequested();

            string stagedPayload = null;
            string extensionDll = ResolvePayloadFile(payloadRoot, EmbeddedPayload.ExtensionFileName);
            string coreDll = ResolvePayloadFile(payloadRoot, EmbeddedPayload.CoreFileName);

            if ((extensionDll == null || coreDll == null) && EmbeddedPayload.HasEmbeddedAssemblies())
            {
                stagedPayload = Path.Combine(Path.GetTempPath(), "AkkeohsSubtitlerEmbeddedPayload");
                _log("Using bundled plugin DLLs from installer.");
                EmbeddedPayload.ExtractToDirectory(stagedPayload);
                extensionDll = Path.Combine(stagedPayload, EmbeddedPayload.ExtensionFileName);
                coreDll = Path.Combine(stagedPayload, EmbeddedPayload.CoreFileName);
                if (string.IsNullOrWhiteSpace(payloadRoot))
                    payloadRoot = stagedPayload;
            }

            if (extensionDll == null || !File.Exists(extensionDll))
                throw new FileNotFoundException("AkkeohsVegas.Extension.dll is missing (not bundled and no payload folder).");
            if (coreDll == null || !File.Exists(coreDll))
                throw new FileNotFoundException("AkkeohsVegas.Core.dll is missing (not bundled and no payload folder).");

            string pf = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                AppPaths.ProductFolderName);
            string pf86 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                AppPaths.ProductFolderName);
            string installRoot = Directory.Exists(pf) ? pf : pf86;

            string binDir = Path.Combine(installRoot, "bin");
            string modelsDir = Path.Combine(installRoot, "models");
            string cacheDir = SetupCacheDirectory;
            string extDir = null;

            _log("Install root: " + installRoot);
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(modelsDir);
            Directory.CreateDirectory(AppPaths.ProgramDataRoot);

            var downloader = new DependencyDownloader(_log, cacheDir);

            try
            {
                downloader.EnsureWhisperCppBinaries(binDir, cancel);
                downloader.EnsureFfmpeg(binDir, cancel);

                CopyDirectoryIfExists(Path.Combine(payloadRoot ?? string.Empty, "bin"), binDir, cancel);
                string payloadModels = Path.Combine(payloadRoot ?? string.Empty, "models");
                foreach (ModelDownloadOption model in selectedModels)
                {
                    cancel.ThrowIfCancellationRequested();
                    if (model == null)
                        continue;
                    string localModel = Path.Combine(payloadModels, model.FileName);
                    if (File.Exists(localModel))
                    {
                        string destModel = Path.Combine(modelsDir, model.FileName);
                        File.Copy(localModel, destModel, true);
                        _log("Copied local model " + model.FileName);
                    }
                    downloader.EnsureModel(model, modelsDir, cancel);
                }

                cancel.ThrowIfCancellationRequested();
                extDir = vegasTarget.ExtensionsDirectory.Trim();
                Directory.CreateDirectory(extDir);
                _log("Installing extension to " + vegasTarget.DisplayName);
                _log("  → " + extDir);
                File.Copy(extensionDll, Path.Combine(extDir, EmbeddedPayload.ExtensionFileName), true);
                File.Copy(coreDll, Path.Combine(extDir, EmbeddedPayload.CoreFileName), true);

                File.Copy(coreDll, Path.Combine(installRoot, EmbeddedPayload.CoreFileName), true);
                File.Copy(extensionDll, Path.Combine(installRoot, EmbeddedPayload.ExtensionFileName), true);

                cancel.ThrowIfCancellationRequested();
                string uninstallerPath = Path.Combine(installRoot, UninstallerFileName);
                string currentExe = Application.ExecutablePath;
                File.Copy(currentExe, uninstallerPath, true);

                var manifest = new InstallManifest
                {
                    InstallRoot = installRoot,
                    ExtensionsDirectory = extDir,
                    VegasDisplayName = vegasTarget.DisplayName,
                    UninstallerPath = uninstallerPath
                };
                foreach (ModelDownloadOption model in selectedModels)
                    manifest.ModelFiles.Add(model.FileName);

                string manifestPath = Path.Combine(installRoot, ManifestFileName);
                File.WriteAllText(manifestPath, new JavaScriptSerializer().Serialize(manifest));
                _log("Wrote " + manifestPath);

                var settings = new PluginSettings
                {
                    AkkeohsCliPath = Path.Combine(binDir, "whisper-cli.exe"),
                    FfmpegPath = Path.Combine(binDir, "ffmpeg.exe"),
                    ModelsDirectory = modelsDir,
                    ModelFileName = "ggml-tiny.bin"
                };
                if (!File.Exists(settings.ModelPath) && File.Exists(Path.Combine(modelsDir, "ggml-tiny.en.bin")))
                    settings.ModelFileName = "ggml-tiny.en.bin";
                settings.Save();
                _log("Wrote settings: " + AppPaths.SettingsPath);

                WriteUninstallRegistry(uninstallerPath, installRoot, vegasTarget.DisplayName);
                _log("Registered uninstaller: " + uninstallerPath);

                cancel.ThrowIfCancellationRequested();
                _log("Install finished.");
                _log("Restart VEGAS, then open View → Extensions → " + ProductInfo.DisplayName);
            }
            catch (OperationCanceledException)
            {
                _log("Installation cancelled — removing partial files…");
                RollbackPartialInstall(installRoot, extDir);
                throw;
            }
            catch (Exception)
            {
                _log("Installation failed — removing partial files…");
                RollbackPartialInstall(installRoot, extDir);
                throw;
            }
            finally
            {
                downloader.CleanupCache();
                if (!string.IsNullOrEmpty(stagedPayload))
                    TryDeleteDir(stagedPayload);
            }
        }

        public void CleanupAfterFailedInstall()
        {
            TryDeleteDir(SetupCacheDirectory);

            string pf = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                AppPaths.ProductFolderName);
            string pf86 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                AppPaths.ProductFolderName);

            foreach (string root in new[] { pf, pf86 })
            {
                if (!Directory.Exists(root))
                    continue;

                string manifest = Path.Combine(root, ManifestFileName);
                string uninstaller = Path.Combine(root, UninstallerFileName);
                if (File.Exists(manifest) && File.Exists(uninstaller))
                    continue;
                TryDeleteDirectoryContents(root, skipFileName: null);
                TryDeleteDir(root);
            }
        }

        private void RollbackPartialInstall(string installRoot, string extensionsDirectory)
        {
            if (!string.IsNullOrWhiteSpace(extensionsDirectory))
            {
                TryDeleteFile(Path.Combine(extensionsDirectory, "AkkeohsVegas.Extension.dll"));
                TryDeleteFile(Path.Combine(extensionsDirectory, "AkkeohsVegas.Core.dll"));
            }

            TryDeleteFile(AppPaths.SettingsPath);
            TryDeleteFile(AppPaths.LegacySettingsPath);
            RemoveUninstallRegistry();

            if (!string.IsNullOrWhiteSpace(installRoot) && Directory.Exists(installRoot))
            {
                TryDeleteDirectoryContents(installRoot, skipFileName: null);
                TryDeleteDir(installRoot);
            }

            TryDeleteDir(SetupCacheDirectory);
            _log("Partial install cleaned up.");
        }

        private void CopyDirectoryIfExists(string source, string dest, CancellationToken cancel)
        {
            if (!Directory.Exists(source))
                return;

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                cancel.ThrowIfCancellationRequested();
                string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(dest, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
                _log("Copied local " + relative);
            }
        }

        public void UninstallFromManifest()
        {
            string manifestPath = Path.Combine(AppPaths.InstallRoot, ManifestFileName);
            InstallManifest manifest = null;
            if (File.Exists(manifestPath))
            {
                try
                {
                    manifest = new JavaScriptSerializer().Deserialize<InstallManifest>(File.ReadAllText(manifestPath));
                }
                catch (Exception ex)
                {
                    _log("Could not read manifest: " + ex.Message);
                }
            }

            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.ExtensionsDirectory))
                dirs.Add(manifest.ExtensionsDirectory);

            dirs.Add(AppPaths.Vegas15ApplicationExtensions);
            dirs.Add(AppPaths.VegasApplicationExtensionsFallback);
            dirs.Add(AppPaths.UserDocumentsApplicationExtensions);
            foreach (VegasExtensionTarget detected in AppPaths.DiscoverInstalledVegasInstances())
            {
                if (detected != null)
                    dirs.Add(detected.ExtensionsDirectory);
            }

            foreach (string dir in dirs)
            {
                TryDeleteFile(Path.Combine(dir, "AkkeohsVegas.Extension.dll"));
                TryDeleteFile(Path.Combine(dir, "AkkeohsVegas.Core.dll"));
                TryDeleteFile(Path.Combine(dir, "WhisperVegas.Extension.dll"));
                TryDeleteFile(Path.Combine(dir, "WhisperVegas.Core.dll"));
            }

            string installRoot = (manifest != null && !string.IsNullOrWhiteSpace(manifest.InstallRoot))
                ? manifest.InstallRoot
                : AppPaths.InstallRoot;

            TryDeleteDirectoryContents(installRoot, skipFileName: null);

            TryDeleteFile(Path.Combine(installRoot, "AkkeohsVegas.Core.dll"));
            TryDeleteFile(Path.Combine(installRoot, "AkkeohsVegas.Extension.dll"));
            TryDeleteFile(Path.Combine(installRoot, ManifestFileName));
            TryDeleteFile(Path.Combine(installRoot, UninstallerFileName));

            TryDeleteFile(AppPaths.SettingsPath);
            TryDeleteFile(AppPaths.LegacySettingsPath);
            TryDeleteDir(AppPaths.UserDataRoot);
            TryDeleteDir(AppPaths.ProgramDataRoot);
            TryDeleteDir(SetupCacheDirectory);
            TryDeleteDir(AppPaths.TempWorkDirectory);

            TryDeleteDir(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "WhisperSubtitlesForVegas"));
            TryDeleteDir(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "WhisperSubtitlesForVegas"));

            RemoveUninstallRegistry();

            ScheduleRemoveInstallRoot(installRoot);

            _log("Uninstall finished. Restart VEGAS if it is open.");
        }

        private void ScheduleRemoveInstallRoot(string installRoot)
        {
            if (string.IsNullOrWhiteSpace(installRoot))
                return;

            try
            {
                string batPath = Path.Combine(Path.GetTempPath(), "akkeohs_subtitler_remove_" + Guid.NewGuid().ToString("N") + ".cmd");
                var sb = new StringBuilder();
                sb.AppendLine("@echo off");
                sb.AppendLine("ping 127.0.0.1 -n 3 >nul");
                sb.AppendLine("rmdir /s /q \"" + installRoot + "\"");

                sb.AppendLine("del /f /q \"" + Path.Combine(installRoot, UninstallerFileName) + "\" >nul 2>&1");
                sb.AppendLine("del /f /q \"" + Path.Combine(installRoot, "AkkeohsVegas.Core.dll") + "\" >nul 2>&1");
                sb.AppendLine("del /f /q \"" + Path.Combine(installRoot, "AkkeohsVegas.Extension.dll") + "\" >nul 2>&1");
                sb.AppendLine("rmdir /s /q \"" + installRoot + "\" >nul 2>&1");
                sb.AppendLine("del \"%~f0\"");
                File.WriteAllText(batPath, sb.ToString());

                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                _log("Scheduled removal of " + installRoot);
            }
            catch (Exception ex)
            {
                _log("Could not schedule folder removal: " + ex.Message);
                _log("Delete manually if needed: " + installRoot);
            }
        }

        private void WriteUninstallRegistry(string uninstallerPath, string installRoot, string vegasName)
        {
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(UninstallRegistryKey))
            {
                if (key == null)
                    return;
                key.SetValue("DisplayName", ProductInfo.DisplayName);
                key.SetValue("Publisher", "Akkeoh");
                key.SetValue("InstallLocation", installRoot);
                key.SetValue("DisplayVersion", Assembly.GetExecutingAssembly().GetName().Version.ToString());
                key.SetValue("UninstallString", "\"" + uninstallerPath + "\" /uninstall");
                key.SetValue("QuietUninstallString", "\"" + uninstallerPath + "\" /uninstall /quiet");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("Comments", "Installed for " + vegasName);
            }
        }

        private void RemoveUninstallRegistry()
        {
            try
            {
                Registry.LocalMachine.DeleteSubKeyTree(UninstallRegistryKey, false);
                _log("Removed Apps & Features entry.");
            }
            catch (Exception ex)
            {
                _log("Could not remove registry uninstall key: " + ex.Message);
            }
        }

        private static string ResolvePayloadFile(string payloadRoot, string fileName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] roots =
            {
                payloadRoot,
                Path.Combine(baseDir, "payload"),
                baseDir,
                Path.GetFullPath(Path.Combine(baseDir, "..", "payload")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "dist", "payload"))
            };

            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                    continue;
                string direct = Path.Combine(root, fileName);
                if (File.Exists(direct))
                    return direct;
                string[] hits = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                if (hits.Length > 0)
                    return hits[0];
            }
            return null;
        }

        private void TryDeleteDirectoryContents(string path, string skipFileName)
        {
            if (!Directory.Exists(path))
                return;

            foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                if (!string.IsNullOrEmpty(skipFileName)
                    && string.Equals(Path.GetFileName(file), skipFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                TryDeleteFile(file);
            }

            string[] dirs;
            try
            {
                dirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);
            }
            catch
            {
                return;
            }

            Array.Sort(dirs, (a, b) => b.Length.CompareTo(a.Length));
            foreach (string dir in dirs)
                TryDeleteDir(dir);
        }

        private void TryDeleteDir(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    _log("Removed " + path);
                }
            }
            catch (Exception ex)
            {
                _log("Could not remove folder " + path + ": " + ex.Message);
            }
        }

        private void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    _log("Removed " + path);
                }
            }
            catch (Exception ex)
            {
                _log("Could not remove " + path + ": " + ex.Message);
            }
        }
    }
}
