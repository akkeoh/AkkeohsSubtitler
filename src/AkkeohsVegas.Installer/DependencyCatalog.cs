using System;
using System.Collections.Generic;

namespace AkkeohsVegas.Installer
{
    public sealed class ModelDownloadOption
    {
        public string FileName { get; set; }
        public string DisplayName { get; set; }
        /// <summary>Standard whisper.cpp model id (e.g. tiny, base.en, large-v3).</summary>
        public string StandardName { get; set; }
        public string Url { get; set; }
        public bool Required { get; set; }
        public string ApproxSize { get; set; }
        public long ApproxSizeBytes { get; set; }

        public override string ToString()
        {
            return DisplayName ?? FileName ?? string.Empty;
        }
    }

    /// <summary>Pinned download URLs for third-party components.</summary>
    public static class DependencyCatalog
    {
        public const string WhisperCppZipUrl =
            "https://github.com/ggml-org/whisper.cpp/releases/download/v1.7.6/whisper-bin-x64.zip";

        public const string FfmpegZipUrl =
            "https://github.com/GyanD/codexffmpeg/releases/download/9.0.1/ffmpeg-9.0.1-essentials_build.zip";

        // Approximate installed footprint (not zip download size).
        public const long WhisperCppApproxBytes = 30L * 1024 * 1024;
        public const long FfmpegApproxBytes = 100L * 1024 * 1024;
        public const long ExtensionApproxBytes = 2L * 1024 * 1024;

        public const string WhisperCppLicenseName = "MIT License (whisper.cpp / ggml)";
        public const string FfmpegLicenseName = "LGPL/GPL (FFmpeg essentials build by Gyan / codexffmpeg)";
        public const string ModelLicenseName = "Model weights (ggerganov/whisper.cpp on Hugging Face; based on OpenAI Whisper)";
        public const string AppLicenseName = "Akkeoh's Subtitler installer & extension (your distribution terms)";

        public static readonly ModelDownloadOption[] Models =
        {
            new ModelDownloadOption
            {
                FileName = "ggml-tiny.bin",
                DisplayName = "Fast",
                StandardName = "tiny",
                Url = ModelUrl("ggml-tiny.bin"),
                Required = true,
                ApproxSize = "75 MB",
                ApproxSizeBytes = 75L * 1024 * 1024
            },
            new ModelDownloadOption
            {
                FileName = "ggml-tiny.en.bin",
                DisplayName = "Fast English",
                StandardName = "tiny.en",
                Url = ModelUrl("ggml-tiny.en.bin"),
                Required = true,
                ApproxSize = "75 MB",
                ApproxSizeBytes = 75L * 1024 * 1024
            },
            new ModelDownloadOption
            {
                FileName = "ggml-base.bin",
                DisplayName = "Basic",
                StandardName = "base",
                Url = ModelUrl("ggml-base.bin"),
                Required = false,
                ApproxSize = "142 MB",
                ApproxSizeBytes = 142L * 1024 * 1024
            },
            new ModelDownloadOption
            {
                FileName = "ggml-base.en.bin",
                DisplayName = "Basic English",
                StandardName = "base.en",
                Url = ModelUrl("ggml-base.en.bin"),
                Required = false,
                ApproxSize = "142 MB",
                ApproxSizeBytes = 142L * 1024 * 1024
            },
            new ModelDownloadOption
            {
                FileName = "ggml-small.bin",
                DisplayName = "Medium",
                StandardName = "small",
                Url = ModelUrl("ggml-small.bin"),
                Required = false,
                ApproxSize = "466 MB",
                ApproxSizeBytes = 466L * 1024 * 1024
            },
            new ModelDownloadOption
            {
                FileName = "ggml-small.en.bin",
                DisplayName = "Medium English",
                StandardName = "small.en",
                Url = ModelUrl("ggml-small.en.bin"),
                Required = false,
                ApproxSize = "466 MB",
                ApproxSizeBytes = 466L * 1024 * 1024
            },
            new ModelDownloadOption
            {
                FileName = "ggml-medium.bin",
                DisplayName = "High",
                StandardName = "medium",
                Url = ModelUrl("ggml-medium.bin"),
                Required = false,
                ApproxSize = "1.5 GB",
                ApproxSizeBytes = 1536L * 1024 * 1024
            },
            new ModelDownloadOption
            {
                FileName = "ggml-medium.en.bin",
                DisplayName = "High English",
                StandardName = "medium.en",
                Url = ModelUrl("ggml-medium.en.bin"),
                Required = false,
                ApproxSize = "1.5 GB",
                ApproxSizeBytes = 1536L * 1024 * 1024
            },
            new ModelDownloadOption
            {
                FileName = "ggml-large-v3.bin",
                DisplayName = "Ultra",
                StandardName = "large-v3",
                Url = ModelUrl("ggml-large-v3.bin"),
                Required = false,
                ApproxSize = "3.0 GB",
                ApproxSizeBytes = 3072L * 1024 * 1024
            }
        };

        private static string ModelUrl(string fileName)
        {
            return "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/" + fileName;
        }

        public static long EstimateInstallSizeBytes(IList<ModelDownloadOption> selectedModels)
        {
            long total = WhisperCppApproxBytes + FfmpegApproxBytes + ExtensionApproxBytes;
            if (selectedModels == null)
                return total;
            foreach (ModelDownloadOption model in selectedModels)
            {
                if (model != null)
                    total += model.ApproxSizeBytes;
            }
            return total;
        }

        public static string FormatSize(long bytes)
        {
            if (bytes < 1024L * 1024)
                return (bytes / 1024.0).ToString("0") + " KB";
            if (bytes < 1024L * 1024 * 1024)
                return (bytes / (1024.0 * 1024.0)).ToString("0") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.0") + " GB";
        }

        public static string BuildTermsText(IList<ModelDownloadOption> selectedModels)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("END-USER LICENSE / THIRD-PARTY NOTICES");
            sb.AppendLine();
            sb.AppendLine("By clicking Install you agree to download and install the following components ");
            sb.AppendLine("onto this computer for use with VEGAS Pro.");
            sb.AppendLine();
            sb.AppendLine("1) Akkeoh's Subtitler (extension + installer)");
            sb.AppendLine("   - Installed to your selected VEGAS Application Extensions folder");
            sb.AppendLine("   - Runtime binaries/models under Program Files\\AkkeohsSubtitlesForVegas");
            sb.AppendLine("   - Uninstaller: akkeohs_subtitler_uninstall.exe (+ Apps & Features)");
            sb.AppendLine("   - License: " + AppLicenseName);
            sb.AppendLine();
            sb.AppendLine("2) whisper.cpp Windows binaries (REQUIRED — always installed)");
            sb.AppendLine("   - Source: " + WhisperCppZipUrl);
            sb.AppendLine("   - License: " + WhisperCppLicenseName);
            sb.AppendLine("   - Upstream: https://github.com/ggml-org/whisper.cpp");
            sb.AppendLine();
            sb.AppendLine("3) FFmpeg (REQUIRED — always installed)");
            sb.AppendLine("   - Source: " + FfmpegZipUrl);
            sb.AppendLine("   - License: " + FfmpegLicenseName);
            sb.AppendLine("   - Upstream: https://ffmpeg.org/  |  Builds: https://www.gyan.dev/ffmpeg/builds/");
            sb.AppendLine();
            sb.AppendLine("4) Speech model weights (selected below)");
            sb.AppendLine("   - Source: https://huggingface.co/ggerganov/whisper.cpp");
            sb.AppendLine("   - License: " + ModelLicenseName);
            if (selectedModels != null)
            {
                foreach (ModelDownloadOption model in selectedModels)
                {
                    string req = model.Required ? " [required]" : "";
                    string standard = string.IsNullOrWhiteSpace(model.StandardName)
                        ? model.FileName
                        : model.StandardName;
                    sb.AppendLine(
                        "   - " + model.DisplayName + " (" + standard + ", " + model.FileName + ", ~" + model.ApproxSize + ")" + req);
                }
            }
            sb.AppendLine();
            sb.AppendLine("Estimated install size: ~" + FormatSize(EstimateInstallSizeBytes(selectedModels)));
            sb.AppendLine();
            sb.AppendLine("VEGAS Pro is a trademark of its respective owners. This installer does not grant ");
            sb.AppendLine("a VEGAS license. ScriptPortal.Vegas.dll is not redistributed; it must already be ");
            sb.AppendLine("installed with your copy of VEGAS.");
            sb.AppendLine();
            sb.Append("You can uninstall later via Windows Apps & Features or akkeohs_subtitler_uninstall.exe.");
            return sb.ToString();
        }
    }

    internal sealed class InstallManifest
    {
        public string InstallRoot { get; set; }
        public string ExtensionsDirectory { get; set; }
        public string VegasDisplayName { get; set; }
        public string UninstallerPath { get; set; }
        public List<string> ModelFiles { get; set; }

        public InstallManifest()
        {
            ModelFiles = new List<string>();
        }
    }
}
