using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AkkeohsVegas.Core
{

    public sealed class ClipAudioRequest
    {
        public string MediaPath { get; set; }
        public double MediaOffsetMs { get; set; }

        public double DurationMs { get; set; }

        public double TimelineDurationMs { get; set; }

        public double TimelineStartMs { get; set; }

        public double PlaybackRate { get; set; }
        public string DisplayName { get; set; }

        public ClipAudioRequest()
        {
            MediaPath = string.Empty;
            DisplayName = string.Empty;
            PlaybackRate = 1.0;
        }
    }

    public sealed class TimedSubtitle
    {
        public double TimelineStartMs { get; set; }
        public double TimelineEndMs { get; set; }
        public string Text { get; set; }

        public TimedSubtitle()
        {
            Text = string.Empty;
        }

        public TimedSubtitle(double startMs, double endMs, string text)
        {
            TimelineStartMs = startMs;
            TimelineEndMs = endMs;
            Text = text ?? string.Empty;
        }
    }

    public sealed class TranscriptionPipeline
    {
        private readonly PluginSettings _settings;

        public TranscriptionPipeline(PluginSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            _settings = settings;
        }

        public IList<TimedSubtitle> TranscribeClips(
            IList<ClipAudioRequest> clips,
            Action<string, int> progress = null)
        {
            if (clips == null || clips.Count == 0)
                throw new InvalidOperationException("No clips selected. Select one or more events on the timeline first.");

            var extractor = new AudioExtractor(_settings.FfmpegPath);
            var runner = new AkkeohsRunner(_settings);
            var all = new List<TimedSubtitle>();
            int clipCount = clips.Count;

            for (int i = 0; i < clipCount; i++)
            {
                ClipAudioRequest clip = clips[i];
                int span = Math.Max(1, 90 / clipCount);
                int basePct = (i * 90) / clipCount;

                Report(progress,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Clip {0}/{1}: {2}",
                        i + 1,
                        clipCount,
                        string.IsNullOrEmpty(clip.DisplayName) ? Path.GetFileName(clip.MediaPath) : clip.DisplayName),
                    basePct + 2);

                string stamp = string.Format(CultureInfo.InvariantCulture, "clip_{0:yyyyMMdd_HHmmss}_{1}", DateTime.Now, i);
                string wavPath = Path.Combine(AppPaths.TempWorkDirectory, stamp + ".wav");
                string trimmedPath = Path.Combine(AppPaths.TempWorkDirectory, stamp + "_trim.wav");

                try
                {
                    double rate = clip.PlaybackRate > 0.01 ? clip.PlaybackRate : 1.0;
                    double extractDuration = clip.DurationMs > 0 ? clip.DurationMs : clip.TimelineDurationMs * rate;
                    if (extractDuration <= 0)
                        extractDuration = clip.TimelineDurationMs;

                    int whisperPct = basePct + (span * 30) / 100;
                    int whisperPctMax = basePct + (span * 85) / 100;
                    int lineBump = 0;

                    Action<string> stageLog = msg =>
                    {
                        int pct = whisperPct + Math.Min(whisperPctMax - whisperPct, lineBump / 3);
                        if (msg != null && msg.IndexOf('[') >= 0)
                            lineBump++;
                        Report(progress, msg, pct);
                    };

                    Report(progress, "Extracting audio with ffmpeg...", basePct + (span * 10) / 100);
                    extractor.Extract(
                        clip.MediaPath,
                        clip.MediaOffsetMs,
                        extractDuration,
                        wavPath,
                        stageLog);

                    Report(progress, "Checking leading silence...", basePct + (span * 22) / 100);
                    double leadingSilenceMs = extractor.TrimLeadingSilence(wavPath, trimmedPath, stageLog);
                    string whisperInput = File.Exists(trimmedPath) ? trimmedPath : wavPath;

                    Report(progress, "Running whisper.cpp...", whisperPct);
                    IList<SubtitleSegment> segments = runner.Transcribe(whisperInput, stageLog);
                    double timingOffsetMs = _settings.TimingOffsetMs;

                    if (leadingSilenceMs > 0 || Math.Abs(timingOffsetMs) > 0.5)
                    {
                        Report(progress,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "Timing adjust: leading silence +{0:0} ms, user offset {1:+0;-0;+0} ms",
                                leadingSilenceMs,
                                timingOffsetMs),
                            basePct + (span * 88) / 100);
                    }

                    double timelineDuration = clip.TimelineDurationMs > 0
                        ? clip.TimelineDurationMs
                        : extractDuration / rate;

                    foreach (SubtitleSegment seg in segments)
                    {
                        double start = clip.TimelineStartMs
                            + ((leadingSilenceMs + seg.StartMs) / rate)
                            + timingOffsetMs;
                        double end = clip.TimelineStartMs
                            + ((leadingSilenceMs + seg.EndMs) / rate)
                            + timingOffsetMs;

                        double clipEnd = clip.TimelineStartMs + timelineDuration;
                        if (start < clip.TimelineStartMs)
                            start = clip.TimelineStartMs;
                        if (end > clipEnd)
                            end = clipEnd;
                        if (end <= start)
                            continue;

                        string text = (seg.Text ?? string.Empty).Trim();
                        if (text.Length == 0)
                            continue;

                        all.Add(new TimedSubtitle(start, end, text));
                    }

                    Report(progress,
                        string.Format(CultureInfo.InvariantCulture, "Clip {0}/{1} done.", i + 1, clipCount),
                        basePct + span);
                }
                finally
                {
                    TryDelete(wavPath);
                    TryDelete(trimmedPath);
                    TryDelete(Path.Combine(AppPaths.TempWorkDirectory, stamp + "_trim_whisper.json"));
                    TryDelete(Path.Combine(AppPaths.TempWorkDirectory, stamp + "_trim_whisper.srt"));
                    TryDelete(Path.Combine(AppPaths.TempWorkDirectory, stamp + "_whisper.json"));
                    TryDelete(Path.Combine(AppPaths.TempWorkDirectory, stamp + "_whisper.srt"));
                }
            }

            if (all.Count == 0)
                throw new InvalidOperationException("Transcription completed but produced no subtitle segments.");

            Report(progress, "Transcription complete.", 92);
            return all;
        }

        private static void Report(Action<string, int> progress, string message, int percent)
        {
            if (progress == null)
                return;
            if (percent < 0)
                percent = 0;
            if (percent > 100)
                percent = 100;
            progress(message, percent);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {

            }
        }
    }
}
