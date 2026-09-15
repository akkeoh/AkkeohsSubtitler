using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace AkkeohsVegas.Core
{

    public static class AkkeohsJsonParser
    {
        public static IList<SubtitleSegment> ParseFile(string path)
        {
            string json = File.ReadAllText(path);
            return Parse(json);
        }

        public static IList<SubtitleSegment> Parse(string json)
        {
            var results = new List<SubtitleSegment>();
            if (string.IsNullOrWhiteSpace(json))
                return results;

            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;

            var root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null)
                return results;

            object transcriptionObj;
            if (!root.TryGetValue("transcription", out transcriptionObj))
                return results;

            var items = transcriptionObj as object[];
            if (items == null)
                return results;

            foreach (object itemObj in items)
            {
                var item = itemObj as Dictionary<string, object>;
                if (item == null)
                    continue;

                string text = GetString(item, "text");
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                text = text.Trim();

                double startMs = 0;
                double endMs = 0;
                object timestampsObj;
                if (item.TryGetValue("timestamps", out timestampsObj))
                {
                    var timestamps = timestampsObj as Dictionary<string, object>;
                    if (timestamps != null)
                    {
                        startMs = ParseTimestampValue(timestamps, "from");
                        endMs = ParseTimestampValue(timestamps, "to");
                    }
                }

                if (endMs <= startMs)
                    endMs = startMs + 500;

                results.Add(new SubtitleSegment(startMs, endMs, text));
            }

            return results;
        }

        private static string GetString(Dictionary<string, object> dict, string key)
        {
            object value;
            if (!dict.TryGetValue(key, out value) || value == null)
                return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static double ParseTimestampValue(Dictionary<string, object> timestamps, string key)
        {
            object value;
            if (!timestamps.TryGetValue(key, out value) || value == null)
                return 0;

            if (value is double)
                return (double)value * 1000.0;
            if (value is decimal)
                return (double)(decimal)value * 1000.0;
            if (value is int)
                return (int)value;
            if (value is long)
                return (long)value;

            string s = Convert.ToString(value, CultureInfo.InvariantCulture);
            return ParseTimestampString(s);
        }

        public static double ParseTimestampString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;

            value = value.Trim();

            double asNumber;
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out asNumber))
            {

                if (value.IndexOf('.') >= 0 || value.IndexOf(',') >= 0)
                    return asNumber * 1000.0;
                return asNumber;
            }

            var match = Regex.Match(
                value,
                @"^(?:(\d+):)?(\d{1,2}):(\d{1,2})[,.](\d{1,3})$");
            if (match.Success)
            {
                int hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
                int minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                int seconds = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                string msPart = match.Groups[4].Value.PadRight(3, '0').Substring(0, 3);
                int ms = int.Parse(msPart, CultureInfo.InvariantCulture);
                return (((hours * 60.0) + minutes) * 60.0 + seconds) * 1000.0 + ms;
            }

            return 0;
        }
    }
}
