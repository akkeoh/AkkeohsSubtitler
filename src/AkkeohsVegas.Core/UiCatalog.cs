using System;
using System.Collections.Generic;
using System.IO;

namespace AkkeohsVegas.Core
{
    public sealed class NamedOption
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }

        public NamedOption(string id, string displayName)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? id ?? string.Empty;
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class ModelCatalog
    {
        public static readonly NamedOption[] All =
        {
            new NamedOption("ggml-tiny.bin", "Fast"),
            new NamedOption("ggml-tiny.en.bin", "Fast English"),
            new NamedOption("ggml-base.bin", "Basic"),
            new NamedOption("ggml-base.en.bin", "Basic English"),
            new NamedOption("ggml-small.bin", "Medium"),
            new NamedOption("ggml-small.en.bin", "Medium English"),
            new NamedOption("ggml-medium.bin", "High"),
            new NamedOption("ggml-medium.en.bin", "High English"),
            new NamedOption("ggml-large-v3.bin", "Ultra")
        };

        public static NamedOption[] GetInstalled(string modelsDirectory)
        {
            var list = new List<NamedOption>();
            if (string.IsNullOrWhiteSpace(modelsDirectory) || !Directory.Exists(modelsDirectory))
                return list.ToArray();

            foreach (NamedOption opt in All)
            {
                string path = Path.Combine(modelsDirectory, opt.Id);
                if (File.Exists(path) && new FileInfo(path).Length > 1000)
                    list.Add(opt);
            }

            try
            {
                foreach (string file in Directory.GetFiles(modelsDirectory, "*.bin"))
                {
                    string name = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(name) || new FileInfo(file).Length <= 1000)
                        continue;
                    bool known = false;
                    foreach (NamedOption opt in list)
                    {
                        if (string.Equals(opt.Id, name, StringComparison.OrdinalIgnoreCase))
                        {
                            known = true;
                            break;
                        }
                    }
                    if (!known)
                        list.Add(new NamedOption(name, name));
                }
            }
            catch { }

            return list.ToArray();
        }

        public static bool IsEnglishOnly(string modelFileName)
        {
            if (string.IsNullOrWhiteSpace(modelFileName))
                return false;
            string name = modelFileName.Trim().ToLowerInvariant();
            return name.Contains(".en.bin") || name.EndsWith(".en.bin");
        }

        public static NamedOption FindByFileName(string modelFileName)
        {
            if (string.IsNullOrWhiteSpace(modelFileName))
                return All[0];

            foreach (NamedOption opt in All)
            {
                if (string.Equals(opt.Id, modelFileName.Trim(), StringComparison.OrdinalIgnoreCase))
                    return opt;
            }

            return new NamedOption(modelFileName.Trim(), modelFileName.Trim());
        }

        public static NamedOption FindInstalledOrFirst(string modelFileName, NamedOption[] installed)
        {
            if (installed == null || installed.Length == 0)
                return FindByFileName(modelFileName);

            if (!string.IsNullOrWhiteSpace(modelFileName))
            {
                foreach (NamedOption opt in installed)
                {
                    if (string.Equals(opt.Id, modelFileName.Trim(), StringComparison.OrdinalIgnoreCase))
                        return opt;
                }
            }

            foreach (NamedOption opt in installed)
            {
                if (string.Equals(opt.Id, "ggml-tiny.bin", StringComparison.OrdinalIgnoreCase))
                    return opt;
            }
            foreach (NamedOption opt in installed)
            {
                if (string.Equals(opt.Id, "ggml-tiny.en.bin", StringComparison.OrdinalIgnoreCase))
                    return opt;
            }
            return installed[0];
        }
    }

    public static class LanguageCatalog
    {
        public static readonly NamedOption[] All =
        {
            new NamedOption("auto", "Auto"),
            new NamedOption("en", "English"),
            new NamedOption("es", "Spanish"),
            new NamedOption("fr", "French"),
            new NamedOption("de", "German"),
            new NamedOption("it", "Italian"),
            new NamedOption("pt", "Portuguese"),
            new NamedOption("nl", "Dutch"),
            new NamedOption("pl", "Polish"),
            new NamedOption("ru", "Russian"),
            new NamedOption("ja", "Japanese"),
            new NamedOption("ko", "Korean"),
            new NamedOption("zh", "Chinese"),
            new NamedOption("sv", "Swedish"),
            new NamedOption("da", "Danish"),
            new NamedOption("no", "Norwegian"),
            new NamedOption("fi", "Finnish")
        };

        public static NamedOption FindByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return All[0];

            string key = code.Trim().ToLowerInvariant();
            foreach (NamedOption opt in All)
            {
                if (string.Equals(opt.Id, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(opt.DisplayName, code.Trim(), StringComparison.OrdinalIgnoreCase))
                    return opt;
            }
            return new NamedOption(key, code.Trim());
        }

        public static string ToCode(object selected)
        {
            var opt = selected as NamedOption;
            if (opt != null)
                return opt.Id;
            return FindByCode(Convert.ToString(selected)).Id;
        }
    }
}
