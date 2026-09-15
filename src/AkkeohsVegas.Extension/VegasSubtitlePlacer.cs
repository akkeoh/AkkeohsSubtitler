using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ScriptPortal.Vegas;
using AkkeohsVegas.Core;

namespace AkkeohsVegas.Extension
{

    public static class VegasSubtitlePlacer
    {
        public static int Place(
            Vegas vegas,
            IList<TimedSubtitle> subtitles,
            PluginSettings settings)
        {
            if (vegas == null)
                throw new ArgumentNullException("vegas");
            if (subtitles == null || subtitles.Count == 0)
                throw new ArgumentException("No subtitles to place.", "subtitles");
            if (settings == null)
                throw new ArgumentNullException("settings");

            PlugInNode generator = FindTextGenerator(vegas, settings.TextGeneratorName);
            if (generator == null)
            {
                throw new InvalidOperationException(
                    "Could not find a Titles & Text generator. " +
                    "In VEGAS, check Media Generators for \"Sony Titles & Text\" or \"Titles & Text\".");
            }

            int placed = 0;

            using (new UndoBlock(ProductInfo.UndoBlockName))
            {
                VideoTrack track = vegas.Project.AddVideoTrack();
                try { track.Name = settings.TrackName; }
                catch {  }

                foreach (TimedSubtitle sub in subtitles)
                {
                    double duration = sub.TimelineEndMs - sub.TimelineStartMs;
                    if (duration <= 0 || string.IsNullOrWhiteSpace(sub.Text))
                        continue;

                    Timecode start = Timecode.FromMilliseconds(sub.TimelineStartMs);
                    Timecode length = Timecode.FromMilliseconds(duration);

                    VideoEvent videoEvent = AddTextEvent(track, generator, start, length);
                    SetTextOnEvent(videoEvent, sub.Text, settings);
                    ApplyTextAppearance(videoEvent, settings);
                    VegasEventPanCrop.Apply(vegas.Project, videoEvent, settings.PanCropPlacement);
                    placed++;
                }
            }

            return placed;
        }

        public static bool TryReadVegasTextDefaults(
            Vegas vegas,
            string preferredGenerator,
            out double outlineWidth,
            out Color outlineColor)
        {
            outlineWidth = 2.0;
            outlineColor = Color.Black;

            try
            {
                PlugInNode generator = FindTextGenerator(vegas, preferredGenerator);
                if (generator == null)
                    return false;

                Media media = new Media(generator);
                try
                {
                    if (media.Generator != null)
                        media.Generator.Preset = "(Default)";
                }
                catch { }

                Effect fx = media.Generator;
                if (fx == null || fx.OFXEffect == null)
                    return false;

                OFXEffect ofx = fx.OFXEffect;
                OFXDoubleParameter widthParam = ofx.FindParameterByName("OutlineWidth") as OFXDoubleParameter;
                if (widthParam != null)
                {
                    double d = widthParam.Default;
                    if (d < 0) d = 0;
                    if (d > 10) d = 10;
                    outlineWidth = d;
                }

                OFXRGBAParameter colorParam = ofx.FindParameterByName("OutlineColor") as OFXRGBAParameter;
                if (colorParam != null)
                    outlineColor = FromOfxColor(colorParam.Default);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static VideoEvent AddTextEvent(
            VideoTrack track,
            PlugInNode generator,
            Timecode start,
            Timecode length)
        {
            Media media = new Media(generator);
            try
            {
                if (media.Generator != null)
                    media.Generator.Preset = "(Default)";
            }
            catch
            {

            }

            MediaStream stream = media.GetVideoStreamByIndex(0);
            if (stream == null)
                throw new InvalidOperationException("Titles & Text media has no video stream.");

            VideoEvent videoEvent = track.AddVideoEvent(start, length);
            videoEvent.AddTake(stream);
            return videoEvent;
        }

        private static void SetTextOnEvent(VideoEvent ev, string text, PluginSettings settings)
        {
            if (ev == null || ev.ActiveTake == null || ev.ActiveTake.Media == null)
                return;

            Effect fx = ev.ActiveTake.Media.Generator;
            if (fx == null)
                return;

            OFXEffect ofx = fx.OFXEffect;
            if (ofx == null)
                return;

            OFXStringParameter textParam = ofx.FindParameterByName("Text") as OFXStringParameter;
            if (textParam == null)
                return;

            using (var rtb = new RichTextBox())
            {
                try
                {
                    if (!string.IsNullOrEmpty(textParam.Value))
                        rtb.Rtf = textParam.Value;
                }
                catch
                {

                }

                Font savedFont = null;
                HorizontalAlignment savedAlign = HorizontalAlignment.Center;
                try
                {
                    rtb.SelectAll();
                    savedFont = rtb.SelectionFont;
                    savedAlign = rtb.SelectionAlignment;
                }
                catch { }

                rtb.Rtf = string.Empty;
                string displayText = text ?? string.Empty;
                if (!string.IsNullOrEmpty(displayText)
                    && string.Equals(settings.TextStyle, "Capitalized", StringComparison.OrdinalIgnoreCase))
                {
                    displayText = displayText.ToUpperInvariant();
                }
                rtb.AppendText(displayText);
                rtb.SelectAll();

                try
                {
                    string family = VegasFontCatalog.Resolve(settings.FontFamily);
                    Font font = null;
                    try
                    {
                        font = new Font(family, settings.FontSize, FontStyle.Bold, GraphicsUnit.Point);
                    }
                    catch
                    {
                        try
                        {
                            font = new Font(family, settings.FontSize, FontStyle.Regular, GraphicsUnit.Point);
                        }
                        catch
                        {
                            font = savedFont;
                        }
                    }

                    if (font != null)
                        rtb.SelectionFont = font;

                    rtb.SelectionAlignment = HorizontalAlignment.Center;

                    Color fill = NormalizeOpaque(Color.FromArgb(settings.TextColorArgb));
                    rtb.SelectionColor = fill;
                }
                catch
                {
                    if (savedFont != null)
                    {
                        try
                        {
                            rtb.SelectionFont = savedFont;
                            rtb.SelectionAlignment = savedAlign;
                        }
                        catch { }
                    }
                }

                textParam.Value = rtb.Rtf;
            }
        }

        private static void ApplyTextAppearance(VideoEvent ev, PluginSettings settings)
        {
            if (ev == null || ev.ActiveTake == null || ev.ActiveTake.Media == null)
                return;

            Effect fx = ev.ActiveTake.Media.Generator;
            if (fx == null || fx.OFXEffect == null)
                return;

            OFXEffect ofx = fx.OFXEffect;

            try
            {
                OFXRGBAParameter textColorParam = ofx.FindParameterByName("TextColor") as OFXRGBAParameter;
                if (textColorParam != null)
                    textColorParam.Value = ToOfxColor(NormalizeOpaque(Color.FromArgb(settings.TextColorArgb)));
            }
            catch { }

            try
            {
                OFXDoubleParameter widthParam = ofx.FindParameterByName("OutlineWidth") as OFXDoubleParameter;
                if (widthParam != null)
                {
                    double width = settings.OutlineWidth;
                    if (width < 0) width = 0;
                    if (width > 10) width = 10;
                    if (width < widthParam.Min) width = widthParam.Min;
                    if (width > widthParam.Max) width = widthParam.Max;
                    widthParam.Value = width;
                }
            }
            catch { }

            try
            {
                OFXRGBAParameter outlineColorParam = ofx.FindParameterByName("OutlineColor") as OFXRGBAParameter;
                if (outlineColorParam != null)
                    outlineColorParam.Value = ToOfxColor(NormalizeOpaque(Color.FromArgb(settings.OutlineColorArgb)));
            }
            catch { }
        }

        private static Color NormalizeOpaque(Color color)
        {
            if (color.A == 0)
                return Color.FromArgb(255, color.R, color.G, color.B);
            return Color.FromArgb(255, color.R, color.G, color.B);
        }

        private static OFXColor ToOfxColor(Color color)
        {
            return new OFXColor(
                color.R / 255.0,
                color.G / 255.0,
                color.B / 255.0,
                1.0);
        }

        private static Color FromOfxColor(OFXColor color)
        {
            int r = ClampByte(color.R * 255.0);
            int g = ClampByte(color.G * 255.0);
            int b = ClampByte(color.B * 255.0);
            int a = ClampByte(color.A * 255.0);
            if (a <= 0)
                a = 255;
            return Color.FromArgb(a, r, g, b);
        }

        private static int ClampByte(double v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (int)Math.Round(v);
        }

        private static PlugInNode FindTextGenerator(Vegas vegas, string preferredName)
        {
            string[] candidates =
            {
                preferredName,
                "Sony Titles & Text",
                "Titles & Text",
                "VEGAS Titles & Text",
                "VEGAS Pro Titles & Text"
            };

            foreach (string name in candidates)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                PlugInNode node = vegas.Generators.GetChildByName(name);
                if (node != null)
                    return node;

                try
                {
                    node = vegas.Generators.FindChildByName(name);
                    if (node != null)
                        return node;
                }
                catch { }
            }

            foreach (PlugInNode child in vegas.Generators)
            {
                if (child == null || string.IsNullOrEmpty(child.Name))
                    continue;
                string n = child.Name.ToLowerInvariant();
                if (n.Contains("title") && n.Contains("text"))
                    return child;
            }

            return null;
        }
    }
}
