using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace AkkeohsVegas.Core
{

    public sealed class AudioExtractor
    {
        private static readonly Regex SilenceEndRegex = new Regex(
            @"silence_end:\s*([0-9]+(?:\.[0-9]+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SilenceStartRegex = new Regex(
            @"silence_start:\s*([0-9]+(?:\.[0-9]+)?)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string _ffmpegPath;

        public AudioExtractor(string ffmpegPath)
        {
            if (string.IsNullOrWhiteSpace(ffmpegPath))
                throw new ArgumentException("ffmpeg path is required.", "ffmpegPath");
            _ffmpegPath = ffmpegPath;
        }

        public void Extract(
            string mediaPath,
            double mediaOffsetMs,
            double durationMs,
            string outputWavPath,
            Action<string> progress = null)
        {
            if (!File.Exists(_ffmpegPath))
                throw new FileNotFoundException("ffmpeg.exe was not found. Re-run the installer or set the path in settings.", _ffmpegPath);
            if (!File.Exists(mediaPath))
                throw new FileNotFoundException("Source media was not found.", mediaPath);
            if (durationMs <= 0)
                throw new ArgumentOutOfRangeException("durationMs", "Duration must be positive.");

            string outDir = Path.GetDirectoryName(outputWavPath);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            string args = string.Format(
                CultureInfo.InvariantCulture,
                "-y -i \"{0}\" -ss {1} -t {2} -vn -ac 1 -ar 16000 -c:a pcm_s16le \"{3}\"",
                mediaPath,
                FormatSeconds(mediaOffsetMs),
                FormatSeconds(durationMs),
                outputWavPath);

            if (progress != null)
                progress("Extracting audio with ffmpeg...");

            RunProcess(_ffmpegPath, args, progress);
        }

        public double DetectLeadingSilenceMs(string wavPath, Action<string> progress = null)
        {
            if (string.IsNullOrEmpty(wavPath) || !File.Exists(wavPath))
                return 0;

            string args = string.Format(
                CultureInfo.InvariantCulture,
                "-i \"{0}\" -af silencedetect=noise=-35dB:d=0.25 -f null -",
                wavPath);

            string output = RunProcessCapture(_ffmpegPath, args);
            if (string.IsNullOrEmpty(output))
                return 0;

            MatchCollection starts = SilenceStartRegex.Matches(output);
            MatchCollection ends = SilenceEndRegex.Matches(output);
            if (starts.Count == 0 || ends.Count == 0)
                return 0;

            double firstStart = double.Parse(starts[0].Groups[1].Value, CultureInfo.InvariantCulture);

            if (firstStart > 0.15)
                return 0;

            double firstEnd = double.Parse(ends[0].Groups[1].Value, CultureInfo.InvariantCulture);
            if (firstEnd <= firstStart)
                return 0;

            double ms = firstEnd * 1000.0;

            if (ms < 80)
                return 0;
            if (ms > 8000)
                ms = 8000;

            if (progress != null)
                progress(string.Format(CultureInfo.InvariantCulture, "Leading silence detected: {0:0} ms", ms));

            return ms;
        }

        public double TrimLeadingSilence(string inputWav, string outputWav, Action<string> progress = null)
        {
            double leadingMs = DetectLeadingSilenceMs(inputWav, progress);
            if (leadingMs < 80)
            {
                File.Copy(inputWav, outputWav, true);
                return 0;
            }

            string args = string.Format(
                CultureInfo.InvariantCulture,
                "-y -i \"{0}\" -ss {1} -ac 1 -ar 16000 -c:a pcm_s16le \"{2}\"",
                inputWav,
                FormatSeconds(leadingMs),
                outputWav);

            if (progress != null)
                progress("Trimming leading silence for tighter speech sync...");

            RunProcess(_ffmpegPath, args, progress);
            return leadingMs;
        }

        private static string FormatSeconds(double milliseconds)
        {
            return (milliseconds / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void RunProcess(string exe, string args, Action<string> progress)
        {
            RunProcessCapture(exe, args, progress);
        }

        private static string RunProcessCapture(string exe, string args, Action<string> progress = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = psi })
            {
                var stderr = new StringBuilder();
                var stdout = new StringBuilder();
                process.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data))
                        return;
                    stdout.AppendLine(e.Data);
                    if (progress != null)
                        progress(e.Data);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data))
                        return;
                    stderr.AppendLine(e.Data);
                    bool silenceLog = e.Data.IndexOf("silence_", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool sizeLog = e.Data.IndexOf("size=", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (progress != null && !silenceLog && !sizeLog)
                        progress(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                string combined = stdout.ToString() + stderr.ToString();
                bool silenceDetect = args.IndexOf("silencedetect", StringComparison.OrdinalIgnoreCase) >= 0;

                if (process.ExitCode != 0 && !silenceDetect)
                {
                    throw new InvalidOperationException(
                        "ffmpeg failed (exit " + process.ExitCode + ")." + Environment.NewLine + stderr);
                }

                return combined;
            }
        }
    }
}
