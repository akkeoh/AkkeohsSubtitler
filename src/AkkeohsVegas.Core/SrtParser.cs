using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AkkeohsVegas.Core
{
    public static class SrtParser
    {
        private static readonly Regex TimestampLine = new Regex(
            @"^(\d{1,2}:\d{2}:\d{2}[,.]\d{1,3})\s*-->\s*(\d{1,2}:\d{2}:\d{2}[,.]\d{1,3})",
            RegexOptions.Compiled);

        public static IList<SubtitleSegment> ParseFile(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static IList<SubtitleSegment> Parse(string content)
        {
            var results = new List<SubtitleSegment>();
            if (string.IsNullOrWhiteSpace(content))
                return results;

            string normalized = content.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] blocks = Regex.Split(normalized, @"\n\s*\n");

            foreach (string block in blocks)
            {
                string[] lines = block.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2)
                    continue;

                int timestampIndex = -1;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (TimestampLine.IsMatch(lines[i].Trim()))
                    {
                        timestampIndex = i;
                        break;
                    }
                }

                if (timestampIndex < 0 || timestampIndex + 1 >= lines.Length)
                    continue;

                Match m = TimestampLine.Match(lines[timestampIndex].Trim());
                double start = AkkeohsJsonParser.ParseTimestampString(m.Groups[1].Value);
                double end = AkkeohsJsonParser.ParseTimestampString(m.Groups[2].Value);

                var textLines = new List<string>();
                for (int i = timestampIndex + 1; i < lines.Length; i++)
                    textLines.Add(lines[i].Trim());

                string text = string.Join(" ", textLines.ToArray()).Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (end <= start)
                    end = start + 500;

                results.Add(new SubtitleSegment(start, end, text));
            }

            return results;
        }
    }
}
