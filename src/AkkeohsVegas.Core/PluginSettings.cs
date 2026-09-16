using System;
using System.IO;
using System.Web.Script.Serialization;

namespace AkkeohsVegas.Core
{
    public sealed class PluginSettings
    {
        public const int DefaultTextColorArgb = -1; // White opaque (0xFFFFFFFF)
        public const int DefaultOutlineColorArgb = -16777216; // Black opaque (0xFF000000)

        public string AkkeohsCliPath { get; set; }
        public string FfmpegPath { get; set; }
        public string ModelsDirectory { get; set; }
        public string ModelFileName { get; set; }
        public string Language { get; set; }
        public int Threads { get; set; }
        public bool TranslateToEnglish { get; set; }
        public int MaxSegmentChars { get; set; }
        public string TrackName { get; set; }
        public string TextGeneratorName { get; set; }
        public string FontFamily { get; set; }
        public float FontSize { get; set; }
        /// <summary>"Capitalized" (ALL CAPS) or "Normal".</summary>
        public string TextStyle { get; set; }
        /// <summary>Fill color as ARGB (System.Drawing.Color.ToArgb).</summary>
        public int TextColorArgb { get; set; }
        /// <summary>Titles &amp; Text OutlineWidth (0–10).</summary>
        public double OutlineWidth { get; set; }
        /// <summary>Outline color as ARGB.</summary>
        public int OutlineColorArgb { get; set; }
        /// <summary>Event Pan/Crop preset name (see <see cref="PanCropPlacements"/>).</summary>
        public string PanCropPlacement { get; set; }
        /// <summary>
        /// Extra delay applied to all subtitle times (ms). Positive = later / after speech.
        /// </summary>
        public double TimingOffsetMs { get; set; }

        public PluginSettings()
        {
            AkkeohsCliPath = AppPaths.DefaultAkkeohsCliPath;
            FfmpegPath = AppPaths.DefaultFfmpegPath;
            ModelsDirectory = AppPaths.DefaultModelsDirectory;
            ModelFileName = "ggml-tiny.bin";
            Language = "auto";
            Threads = Math.Max(2, Environment.ProcessorCount / 2);
            TranslateToEnglish = false;
            MaxSegmentChars = 42;
            TrackName = ProductInfo.DefaultTrackName;
            TextGeneratorName = "Sony Titles & Text";
            FontFamily = "Arial";
            // Keep default small so Titles & Text stays inside the frame after Pan/Crop.
            FontSize = 14f;
            TextStyle = "Capitalized";
            TextColorArgb = DefaultTextColorArgb;
            OutlineWidth = 2.0;
            OutlineColorArgb = DefaultOutlineColorArgb;
            PanCropPlacement = PanCropPlacements.LowerThird;
            TimingOffsetMs = 0;
        }

        public string ModelPath
        {
            get { return Path.Combine(ModelsDirectory ?? string.Empty, ModelFileName ?? string.Empty); }
        }

        public static PluginSettings Load()
        {
            try
            {
                string[] candidates = { AppPaths.SettingsPath, AppPaths.LegacySettingsPath };
                foreach (string path in candidates)
                {
                    if (!File.Exists(path))
                        continue;
                    string json = File.ReadAllText(path);
                    var serializer = new JavaScriptSerializer();
                    var loaded = serializer.Deserialize<PluginSettings>(json);
                    if (loaded != null)
                        return Normalize(loaded);
                }
            }
            catch
            {
                // Fall through to defaults.
            }
            return new PluginSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.UserDataRoot);
                var serializer = new JavaScriptSerializer();
                File.WriteAllText(AppPaths.SettingsPath, serializer.Serialize(this));
            }
            catch (UnauthorizedAccessException)
            {
                // Never fail the host workflow over settings I/O.
            }
            catch (IOException)
            {
            }
        }

        private static PluginSettings Normalize(PluginSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.AkkeohsCliPath))
                s.AkkeohsCliPath = AppPaths.DefaultAkkeohsCliPath;
            if (string.IsNullOrWhiteSpace(s.FfmpegPath))
                s.FfmpegPath = AppPaths.DefaultFfmpegPath;
            if (string.IsNullOrWhiteSpace(s.ModelsDirectory))
                s.ModelsDirectory = AppPaths.DefaultModelsDirectory;
            if (string.IsNullOrWhiteSpace(s.ModelFileName))
                s.ModelFileName = "ggml-tiny.bin";
            if (string.IsNullOrWhiteSpace(s.Language))
                s.Language = "auto";
            if (s.Threads <= 0)
                s.Threads = Math.Max(2, Environment.ProcessorCount / 2);
            if (s.MaxSegmentChars <= 0)
                s.MaxSegmentChars = 42;
            if (string.IsNullOrWhiteSpace(s.TrackName))
                s.TrackName = ProductInfo.DefaultTrackName;
            if (string.IsNullOrWhiteSpace(s.TextGeneratorName))
                s.TextGeneratorName = "Sony Titles & Text";
            if (string.IsNullOrWhiteSpace(s.FontFamily))
                s.FontFamily = "Arial";
            if (s.FontSize <= 0)
                s.FontSize = 14f;
            if (s.FontSize < 8f)
                s.FontSize = 8f;
            if (s.FontSize > 72f)
                s.FontSize = 72f;
            // Legacy JSON without color fields deserializes as 0 — treat as defaults.
            if (s.TextColorArgb == 0)
                s.TextColorArgb = DefaultTextColorArgb;
            if (string.Equals(s.TextStyle, "Normal", StringComparison.OrdinalIgnoreCase))
                s.TextStyle = "Normal";
            else
                s.TextStyle = "Capitalized";
            if (s.OutlineColorArgb == 0)
                s.OutlineColorArgb = DefaultOutlineColorArgb;
            if (double.IsNaN(s.OutlineWidth) || double.IsInfinity(s.OutlineWidth))
                s.OutlineWidth = 2.0;
            if (s.OutlineWidth < 0)
                s.OutlineWidth = 0;
            if (s.OutlineWidth > 10)
                s.OutlineWidth = 10;
            s.PanCropPlacement = PanCropPlacements.Normalize(s.PanCropPlacement);
            if (double.IsNaN(s.TimingOffsetMs) || double.IsInfinity(s.TimingOffsetMs))
                s.TimingOffsetMs = 0;
            if (s.TimingOffsetMs < -10000)
                s.TimingOffsetMs = -10000;
            if (s.TimingOffsetMs > 10000)
                s.TimingOffsetMs = 10000;
            return s;
        }
    }
}
