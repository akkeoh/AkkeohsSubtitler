using System;
using System.IO;
using System.Reflection;

namespace AkkeohsVegas.Installer
{
    /// <summary>
    /// Loads / extracts AkkeohsVegas.Core and Extension DLLs that are embedded in the setup EXE
    /// so distribution can be a single file.
    /// </summary>
    internal static class EmbeddedPayload
    {
        public const string CoreFileName = "AkkeohsVegas.Core.dll";
        public const string ExtensionFileName = "AkkeohsVegas.Extension.dll";
        public const string CoreResourceName = "AkkeohsVegas.Installer.Payload.AkkeohsVegas.Core.dll";
        public const string ExtensionResourceName = "AkkeohsVegas.Installer.Payload.AkkeohsVegas.Extension.dll";

        private static bool _registered;

        public static void RegisterAssemblyResolver()
        {
            if (_registered)
                return;
            _registered = true;
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        public static bool HasEmbeddedAssemblies()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            return HasResource(asm, CoreResourceName) && HasResource(asm, ExtensionResourceName);
        }

        /// <summary>Writes embedded plugin DLLs to a temp folder and returns that path.</summary>
        public static string ExtractToDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("directory");

            Directory.CreateDirectory(directory);
            WriteResourceToFile(CoreResourceName, Path.Combine(directory, CoreFileName));
            WriteResourceToFile(ExtensionResourceName, Path.Combine(directory, ExtensionFileName));
            return directory;
        }

        public static void WriteCoreTo(string destinationPath)
        {
            WriteResourceToFile(CoreResourceName, destinationPath);
        }

        public static void WriteExtensionTo(string destinationPath)
        {
            WriteResourceToFile(ExtensionResourceName, destinationPath);
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new AssemblyName(args.Name).Name;
                if (!string.Equals(name, "AkkeohsVegas.Core", StringComparison.OrdinalIgnoreCase))
                    return null;

                byte[] raw = ReadResourceBytes(CoreResourceName);
                if (raw == null || raw.Length == 0)
                    return null;
                return Assembly.Load(raw);
            }
            catch
            {
                return null;
            }
        }

        private static bool HasResource(Assembly asm, string resourceName)
        {
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (string.Equals(name, resourceName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void WriteResourceToFile(string resourceName, string destinationPath)
        {
            byte[] raw = ReadResourceBytes(resourceName);
            if (raw == null || raw.Length == 0)
                throw new FileNotFoundException("Embedded resource missing: " + resourceName);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
            File.WriteAllBytes(destinationPath, raw);
        }

        private static byte[] ReadResourceBytes(string resourceName)
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            using (Stream stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    return null;
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}
