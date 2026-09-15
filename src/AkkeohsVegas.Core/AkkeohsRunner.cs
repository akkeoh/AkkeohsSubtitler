using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace AkkeohsVegas.Core
{

    public sealed class AkkeohsRunner
    {
        private readonly PluginSettings _settings;

        public AkkeohsRunner(PluginSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException("settings");
            _settings = settings;
        }

        public IList<SubtitleSegment> Transcribe(string wavPath, Action<string> progress = null)
        {
            if (!File.Exists(_settings.AkkeohsCliPath))
                throw new FileNotFoundException(
                    "whisper-cli.exe was not found. Re-run the installer or set the path in settings.",
                    _settings.AkkeohsCliPath);

            if (!File.Exists(_settings.ModelPath))
                throw new FileNotFoundException(
                    "Model was not found: " + _settings.ModelPath +
                    ". Download a ggml model into the models folder.",
                    _settings.ModelPath);

            if (!File.Exists(wavPath))
                throw new FileNotFoundException("WAV input was not found.", wavPath);

            string workDir = Path.GetDirectoryName(wavPath) ?? AppPaths.TempWorkDirectory;
            string baseName = Path.Combine(workDir, Path.GetFileNameWithoutExtension(wavPath) + "_whisper");

            var args = new StringBuilder();
            args.AppendFormat(CultureInfo.InvariantCulture, "-m \"{0}\" ", _settings.ModelPath);
            args.AppendFormat(CultureInfo.InvariantCulture, "-f \"{0}\" ", wavPath);
            args.AppendFormat(CultureInfo.InvariantCulture, "-of \"{0}\" ", baseName);
            args.Append("-oj -osrt ");
            args.AppendFormat(CultureInfo.InvariantCulture, "-t {0} ", Math.Max(1, _settings.Threads));
            args.AppendFormat(CultureInfo.InvariantCulture, "-l {0} ", string.IsNullOrWhiteSpace(_settings.Language) ? "auto" : _settings.Language);

            if (_settings.TranslateToEnglish)
                args.Append("-tr ");

            if (_settings.MaxSegmentChars > 0)
                args.AppendFormat(CultureInfo.InvariantCulture, "-ml {0} ", _settings.MaxSegmentChars);

            if (progress != null)
                progress("Running whisper.cpp...");

            RunProcess(_settings.AkkeohsCliPath, args.ToString().Trim(), progress);

            string jsonPath = baseName + ".json";
            string srtPath = baseName + ".srt";

            if (File.Exists(jsonPath))
            {
                var fromJson = AkkeohsJsonParser.ParseFile(jsonPath);
                if (fromJson.Count > 0)
                    return fromJson;
            }

            if (File.Exists(srtPath))
                return SrtParser.ParseFile(srtPath);

            throw new InvalidOperationException(
                "whisper-cli finished but no JSON/SRT output was found at " + baseName);
        }

        private static void RunProcess(string exe, string args, Action<string> progress)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory,
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
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data) && progress != null)
                        progress(e.Data);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        stderr.AppendLine(e.Data);
                        if (progress != null)
                            progress(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string hint = "";

                    if (unchecked((uint)process.ExitCode) == 0xC0000135u)
                    {
                        hint = Environment.NewLine +
                            "A required DLL is missing next to whisper-cli.exe " +
                            "(need whisper.dll, ggml.dll, ggml-base.dll, and ggml-cpu-*.dll). " +
                            "Download whisper-bin-x64.zip from https://github.com/ggml-org/whisper.cpp/releases " +
                            "and copy those DLLs into the bin folder, then reinstall.";
                    }

                    throw new InvalidOperationException(
                        "whisper-cli failed (exit " + process.ExitCode + ")." +
                        hint + Environment.NewLine + stderr);
                }
            }
        }
    }
}
