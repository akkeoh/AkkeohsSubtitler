using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AkkeohsVegas.Core
{
    /// <summary>
    /// A detected (or custom) VEGAS Application Extensions install target.
    /// </summary>
    public sealed class VegasExtensionTarget
    {
        public string DisplayName { get; private set; }
        public string VersionFolder { get; private set; }
        public string ExtensionsDirectory { get; private set; }
        public bool IsPerUser { get; private set; }
        public bool IsCustom { get; private set; }
        public bool IsRecommended { get; private set; }

        public VegasExtensionTarget(
            string displayName,
            string versionFolder,
            string extensionsDirectory,
            bool isPerUser,
            bool isCustom,
            bool isRecommended)
        {
            DisplayName = displayName ?? "VEGAS";
            VersionFolder = versionFolder ?? string.Empty;
            ExtensionsDirectory = extensionsDirectory ?? string.Empty;
            IsPerUser = isPerUser;
            IsCustom = isCustom;
            IsRecommended = isRecommended;
        }

        public override string ToString()
        {
            return DisplayName + "  —  " + ExtensionsDirectory;
        }
    }

    /// <summary>
    /// Resolves install locations for binaries, models, and settings.
    /// </summary>
    public static class AppPaths
    {
        public const string ProductFolderName = "AkkeohsSubtitlesForVegas";
        public const string SettingsFileName = "settings.json";

        public static string ProgramDataRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    ProductFolderName);
            }
        }

        /// <summary>Per-user settings (writable without admin).</summary>
        public static string UserDataRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    ProductFolderName);
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(UserDataRoot, SettingsFileName); }
        }

        /// <summary>Legacy all-users settings written by older installs.</summary>
        public static string LegacySettingsPath
        {
            get { return Path.Combine(ProgramDataRoot, SettingsFileName); }
        }

        public static string InstallRoot
        {
            get
            {
                string pf = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    ProductFolderName);
                string pf86 = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    ProductFolderName);
                string legacyPf = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "WhisperSubtitlesForVegas");
                string legacyPf86 = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "WhisperSubtitlesForVegas");
                if (Directory.Exists(pf))
                    return pf;
                if (Directory.Exists(pf86))
                    return pf86;
                if (Directory.Exists(legacyPf))
                    return legacyPf;
                if (Directory.Exists(legacyPf86))
                    return legacyPf86;
                return pf;
            }
        }

        public static string DefaultAkkeohsCliPath
        {
            get { return Path.Combine(InstallRoot, "bin", "whisper-cli.exe"); }
        }

        public static string DefaultFfmpegPath
        {
            get { return Path.Combine(InstallRoot, "bin", "ffmpeg.exe"); }
        }

        public static string DefaultModelsDirectory
        {
            get { return Path.Combine(InstallRoot, "models"); }
        }

        public static string TempWorkDirectory
        {
            get
            {
                string path = Path.Combine(Path.GetTempPath(), ProductFolderName);
                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>VEGAS Pro 15 Application Extensions folder (all users).</summary>
        public static string Vegas15ApplicationExtensions
        {
            get { return GetVersionedApplicationExtensions("15.0"); }
        }

        /// <summary>Shared / legacy Application Extensions folder used by some installs.</summary>
        public static string VegasApplicationExtensionsFallback
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Vegas Pro",
                    "Application Extensions");
            }
        }

        public static string UserDocumentsApplicationExtensions
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Vegas Application Extensions");
            }
        }

        public static string GetVersionedApplicationExtensions(string versionFolder)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Vegas Pro",
                versionFolder,
                "Application Extensions");
        }

        public static string ProgramDataVegasProRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Vegas Pro");
            }
        }

        /// <summary>
        /// Discovers actually installed VEGAS Pro instances (ProgramData version folders and/or exe paths).
        /// </summary>
        public static IList<VegasExtensionTarget> DiscoverInstalledVegasInstances()
        {
            var results = new List<VegasExtensionTarget>();
            var seenVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string vegasRoot = ProgramDataVegasProRoot;
            if (Directory.Exists(vegasRoot))
            {
                foreach (string dir in Directory.GetDirectories(vegasRoot))
                {
                    string name = Path.GetFileName(dir);
                    if (string.IsNullOrEmpty(name))
                        continue;
                    if (!Regex.IsMatch(name, @"^\d+(\.\d+)?$"))
                        continue;

                    // Treat a version folder as installed if it exists (Application Extensions may be created on install).
                    string ext = Path.Combine(dir, "Application Extensions");
                    if (!seenVersions.Add(name))
                        continue;

                    results.Add(new VegasExtensionTarget(
                        "VEGAS Pro " + name,
                        name,
                        ext,
                        false,
                        false,
                        true));
                }
            }

            // Also map found executables to version folders when ProgramData was empty/incomplete.
            foreach (string exe in FindInstalledVegasExecutables())
            {
                string version = GuessVersionFolderFromPath(exe);
                if (string.IsNullOrEmpty(version) || !seenVersions.Add(version))
                    continue;

                results.Add(new VegasExtensionTarget(
                    "VEGAS Pro " + version,
                    version,
                    GetVersionedApplicationExtensions(version),
                    false,
                    false,
                    true));
            }

            results.Sort(CompareTargets);
            return results;
        }

        private static string GuessVersionFolderFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            Match m = Regex.Match(path, @"VEGAS\s*Pro\s*(\d+)(?:\.(\d+))?", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string major = m.Groups[1].Value;
                string minor = m.Groups[2].Success ? m.Groups[2].Value : "0";
                return major + "." + minor;
            }

            m = Regex.Match(Path.GetFileNameWithoutExtension(path) ?? string.Empty, @"(\d{2,3})", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string digits = m.Groups[1].Value;
                if (digits.Length == 2)
                    return digits + ".0";
                if (digits.Length == 3)
                    return digits.Substring(0, 2) + ".0";
            }

            return null;
        }

        /// <summary>
        /// Discovers VEGAS Application Extensions targets under ProgramData and Documents.
        /// Also offers known MAGIX-era version folders even if not yet created.
        /// </summary>
        public static IList<VegasExtensionTarget> DiscoverExtensionTargets()
        {
            var results = new List<VegasExtensionTarget>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (VegasExtensionTarget installed in DiscoverInstalledVegasInstances())
                AddTarget(results, seen, installed);

            // Known MAGIX-era ScriptPortal versions (14–18 highly compatible; 19+ experimental).
            string[] knownVersions =
            {
                "14.0", "15.0", "16.0", "17.0", "18.0",
                "19.0", "20.0", "21.0", "22.0", "23.0"
            };

            foreach (string version in knownVersions)
            {
                string ext = GetVersionedApplicationExtensions(version);
                AddTarget(results, seen, BuildVersionTarget(version, ext, Directory.Exists(Path.GetDirectoryName(ext))));
            }

            // Shared fallback folder.
            AddTarget(
                results,
                seen,
                new VegasExtensionTarget(
                    "VEGAS Pro (shared Application Extensions)",
                    string.Empty,
                    VegasApplicationExtensionsFallback,
                    false,
                    false,
                    Directory.Exists(VegasApplicationExtensionsFallback)));

            // Per-user Documents path (loaded by many Vegas versions).
            AddTarget(
                results,
                seen,
                new VegasExtensionTarget(
                    "Current user (Documents\\Vegas Application Extensions)",
                    string.Empty,
                    UserDocumentsApplicationExtensions,
                    true,
                    false,
                    true));

            results.Sort(CompareTargets);
            return results;
        }

        /// <summary>Targets that should be checked by default in the installer.</summary>
        public static IList<VegasExtensionTarget> GetDefaultSelectedTargets(IList<VegasExtensionTarget> all)
        {
            var selected = new List<VegasExtensionTarget>();
            if (all == null)
                return selected;

            foreach (VegasExtensionTarget t in all)
            {
                if (t == null)
                    continue;

                // Prefer every detected versioned ProgramData folder + Documents.
                if (t.IsPerUser)
                {
                    selected.Add(t);
                    continue;
                }

                if (!string.IsNullOrEmpty(t.VersionFolder))
                {
                    string parent = Path.GetDirectoryName(t.ExtensionsDirectory);
                    if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                        selected.Add(t);
                    else if (string.Equals(t.VersionFolder, "15.0", StringComparison.OrdinalIgnoreCase))
                        selected.Add(t); // always offer 15.0 as the primary build target
                }
            }

            if (selected.Count == 0)
            {
                foreach (VegasExtensionTarget t in all)
                {
                    if (t != null && string.Equals(t.VersionFolder, "15.0", StringComparison.OrdinalIgnoreCase))
                    {
                        selected.Add(t);
                        break;
                    }
                }
            }

            return selected;
        }

        public static string[] CommonVegasInstallRoots
        {
            get
            {
                var roots = new List<string>();
                string[] vendors = { "VEGAS", "Sony", "MAGIX" };
                string[] versions =
                {
                    "VEGAS Pro 14.0", "VEGAS Pro 15.0", "VEGAS Pro 16.0", "VEGAS Pro 17.0", "VEGAS Pro 18.0",
                    "VEGAS Pro 19.0", "VEGAS Pro 20.0", "VEGAS Pro 21.0", "VEGAS Pro 22.0",
                    "Vegas Pro 14.0", "Vegas Pro 15.0", "Vegas Pro 16.0", "Vegas Pro 17.0", "Vegas Pro 18.0"
                };

                string[] bases =
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                };

                foreach (string root in bases)
                {
                    foreach (string vendor in vendors)
                    {
                        foreach (string version in versions)
                        {
                            roots.Add(Path.Combine(root, vendor, version));
                        }
                    }
                }

                return roots.ToArray();
            }
        }

        /// <summary>Legacy name kept for callers; finds any nearby VEGAS ScriptPortal.dll.</summary>
        public static string FindVegas15ScriptPortalDll()
        {
            return FindAnyScriptPortalDll();
        }

        public static string FindAnyScriptPortalDll()
        {
            foreach (string root in CommonVegasInstallRoots)
            {
                string candidate = Path.Combine(root, "ScriptPortal.Vegas.dll");
                if (File.Exists(candidate))
                    return candidate;
            }
            return null;
        }

        /// <summary>Legacy name kept for callers; finds VEGAS Pro 15 exe if present.</summary>
        public static string FindVegas15Executable()
        {
            return FindVegasExecutable("15");
        }

        public static IList<string> FindInstalledVegasExecutables()
        {
            var found = new List<string>();
            string[] names =
            {
                "Vegas140.exe", "vegas140.exe",
                "Vegas150.exe", "vegas150.exe", "VEGAS150.exe",
                "Vegas160.exe", "vegas160.exe",
                "Vegas170.exe", "vegas170.exe",
                "Vegas180.exe", "vegas180.exe",
                "Vegas190.exe", "vegas190.exe",
                "Vegas200.exe", "vegas200.exe",
                "Vegas210.exe", "vegas210.exe",
                "Vegas220.exe", "vegas220.exe",
                "VEGASPro.exe", "VegasPro.exe"
            };

            foreach (string root in CommonVegasInstallRoots)
            {
                foreach (string name in names)
                {
                    string candidate = Path.Combine(root, name);
                    if (File.Exists(candidate) && !found.Exists(p => string.Equals(p, candidate, StringComparison.OrdinalIgnoreCase)))
                        found.Add(candidate);
                }
            }

            return found;
        }

        public static string FindVegasExecutable(string majorVersionHint)
        {
            foreach (string exe in FindInstalledVegasExecutables())
            {
                if (string.IsNullOrEmpty(majorVersionHint))
                    return exe;
                if (exe.IndexOf(majorVersionHint, StringComparison.OrdinalIgnoreCase) >= 0)
                    return exe;
            }
            IList<string> all = FindInstalledVegasExecutables();
            return all.Count > 0 ? all[0] : null;
        }

        private static VegasExtensionTarget BuildVersionTarget(string versionFolder, string extensionsDirectory, bool recommended)
        {
            int major = ParseMajor(versionFolder);
            string note = major >= 19 ? " (19+ experimental)" : string.Empty;
            return new VegasExtensionTarget(
                "VEGAS Pro " + versionFolder + note,
                versionFolder,
                extensionsDirectory,
                false,
                false,
                recommended);
        }

        private static int ParseMajor(string versionFolder)
        {
            if (string.IsNullOrEmpty(versionFolder))
                return 0;
            int dot = versionFolder.IndexOf('.');
            string majorText = dot > 0 ? versionFolder.Substring(0, dot) : versionFolder;
            int major;
            return int.TryParse(majorText, out major) ? major : 0;
        }

        private static void AddTarget(List<VegasExtensionTarget> list, HashSet<string> seen, VegasExtensionTarget target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.ExtensionsDirectory))
                return;
            string key = target.ExtensionsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!seen.Add(key))
                return;
            list.Add(target);
        }

        private static int CompareTargets(VegasExtensionTarget a, VegasExtensionTarget b)
        {
            if (a.IsPerUser != b.IsPerUser)
                return a.IsPerUser ? 1 : -1;
            if (a.IsCustom != b.IsCustom)
                return a.IsCustom ? 1 : -1;

            int am = ParseMajor(a.VersionFolder);
            int bm = ParseMajor(b.VersionFolder);
            if (am != bm)
                return am.CompareTo(bm);

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
