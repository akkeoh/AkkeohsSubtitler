using System;
using System.Collections;
using System.Collections.Generic;
using ScriptPortal.Vegas;
using AkkeohsVegas.Core;

namespace AkkeohsVegas.Extension
{

    public static class VegasClipCollector
    {
        public static List<ClipAudioRequest> CollectSelected(Project project)
        {
            if (project == null)
                throw new ArgumentNullException("project");

            var selected = new List<TrackEvent>();
            foreach (Track track in project.Tracks)
            {
                foreach (TrackEvent ev in track.Events)
                {
                    if (ev.Selected)
                        selected.Add(ev);
                }
            }

            if (selected.Count == 0)
                throw new InvalidOperationException(
                    "No timeline events are selected. Select one or more clips, then run " + ProductInfo.DisplayName + " again.");

            var preferred = PreferAudioEvents(selected);
            var clips = new List<ClipAudioRequest>();

            foreach (TrackEvent ev in preferred)
            {
                ClipAudioRequest clip = TryCreateRequest(ev);
                if (clip != null)
                    clips.Add(clip);
            }

            if (clips.Count == 0)
            {
                throw new InvalidOperationException(
                    "Selected events have no readable media file path. " +
                    "Generated media or missing takes cannot be transcribed.");
            }

            return clips;
        }

        private static List<TrackEvent> PreferAudioEvents(List<TrackEvent> selected)
        {
            var audio = new List<TrackEvent>();
            var video = new List<TrackEvent>();

            foreach (TrackEvent ev in selected)
            {
                if (ev is AudioEvent)
                    audio.Add(ev);
                else
                    video.Add(ev);
            }

            if (audio.Count == 0)
                return selected;

            var result = new List<TrackEvent>(audio);
            foreach (TrackEvent v in video)
            {
                bool paired = false;
                foreach (TrackEvent a in audio)
                {
                    if (NearlyEqual(a.Start.ToMilliseconds(), v.Start.ToMilliseconds(), 1) &&
                        NearlyEqual(a.Length.ToMilliseconds(), v.Length.ToMilliseconds(), 1))
                    {
                        paired = true;
                        break;
                    }
                }
                if (!paired)
                    result.Add(v);
            }
            return result;
        }

        private static bool NearlyEqual(double a, double b, double epsilonMs)
        {
            return Math.Abs(a - b) <= epsilonMs;
        }

        private static ClipAudioRequest TryCreateRequest(TrackEvent ev)
        {
            if (ev == null || ev.ActiveTake == null)
                return null;

            string path = null;
            if (ev.ActiveTake.Media != null && !string.IsNullOrEmpty(ev.ActiveTake.Media.FilePath))
                path = ev.ActiveTake.Media.FilePath;
            else if (!string.IsNullOrEmpty(ev.ActiveTake.MediaPath))
                path = ev.ActiveTake.MediaPath;

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return null;

            double offsetMs = 0;
            if (ev.ActiveTake.Offset != null)
                offsetMs = ev.ActiveTake.Offset.ToMilliseconds();

            double rate = 1.0;
            try
            {
                if (ev.PlaybackRate > 0.01)
                    rate = ev.PlaybackRate;
            }
            catch
            {
                rate = 1.0;
            }

            double timelineDurationMs = ev.Length.ToMilliseconds();

            return new ClipAudioRequest
            {
                MediaPath = path,
                MediaOffsetMs = offsetMs,
                DurationMs = timelineDurationMs * rate,
                TimelineDurationMs = timelineDurationMs,
                TimelineStartMs = ev.Start.ToMilliseconds(),
                PlaybackRate = rate,
                DisplayName = System.IO.Path.GetFileName(path)
            };
        }
    }
}
