using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;

namespace AkkeohsVegas.Installer
{
    internal sealed class DependencyDownloader
    {
        private readonly Action<string> _log;
        private readonly string _cacheDir;
        private readonly object _requestLock = new object();
        private HttpWebRequest _activeRequest;

        static DependencyDownloader()
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            }
            catch (NotSupportedException) { }
        }

        public DependencyDownloader(Action<string> log, string cacheDir)
        {
            _log = log ?? (s => { });
            _cacheDir = cacheDir;
            Directory.CreateDirectory(_cacheDir);
        }

        public void DownloadFile(string url, string destinationPath, string label, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? _cacheDir);
            _log("Downloading " + label + "…");
            _log("  " + url);

            string tempPath = destinationPath + ".partial";
            TryDeleteFile(tempPath);
            TryDeleteFile(destinationPath);

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.None;
            request.UserAgent = "AkkeohsSubtitlerInstaller/1.0";
            request.Accept = "*/*";
            request.Timeout = 30 * 60 * 1000;
            request.ReadWriteTimeout = 30 * 60 * 1000;

            lock (_requestLock)
                _activeRequest = request;

            using (cancel.Register(AbortActiveRequest))
            {
                try
                {
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (Stream remote = response.GetResponseStream())
                    using (var local = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        if (remote == null)
                            throw new IOException("No response body from " + url);

                        byte[] buffer = new byte[81920];
                        int read;
                        long total = 0;
                        long expected = response.ContentLength;
                        int lastPct = -1;
                        while ((read = remote.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            cancel.ThrowIfCancellationRequested();
                            local.Write(buffer, 0, read);
                            total += read;
                            if (expected > 0)
                            {
                                int pct = (int)((total * 100) / expected);
                                if (pct >= lastPct + 10)
                                {
                                    lastPct = pct;
                                    _log("  … " + pct + "% (" + FormatSize(total) + " / " + FormatSize(expected) + ")");
                                }
                            }
                        }

                        if (expected > 0 && total != expected)
                        {
                            TryDeleteFile(tempPath);
                            throw new IOException(
                                "Download incomplete for " + label + " (got " + total + " of " + expected + " bytes).");
                        }
                    }

                    cancel.ThrowIfCancellationRequested();
                    File.Move(tempPath, destinationPath);
                    var info = new FileInfo(destinationPath);
                    _log("  Saved " + info.Name + " (" + FormatSize(info.Length) + ")");
                }
                catch (WebException ex)
                {
                    TryDeleteFile(tempPath);
                    if (cancel.IsCancellationRequested)
                        throw new OperationCanceledException("Download cancelled.", ex);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    TryDeleteFile(tempPath);
                    throw;
                }
                finally
                {
                    lock (_requestLock)
                    {
                        if (ReferenceEquals(_activeRequest, request))
                            _activeRequest = null;
                    }
                }
            }
        }

        private void AbortActiveRequest()
        {
            HttpWebRequest request;
            lock (_requestLock)
                request = _activeRequest;
            try
            {
                if (request != null)
                    request.Abort();
            }
            catch { }
        }

        public void EnsureWhisperCppBinaries(string binDir, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            Directory.CreateDirectory(binDir);
            if (File.Exists(Path.Combine(binDir, "whisper-cli.exe"))
                && File.Exists(Path.Combine(binDir, "whisper.dll")))
            {
                _log("whisper.cpp binaries already present.");
                return;
            }

            string zipPath = Path.Combine(_cacheDir, "whisper-bin-x64.zip");
            EnsureValidZip(
                zipPath,
                DependencyCatalog.WhisperCppZipUrl,
                "whisper.cpp (Windows x64)",
                minBytes: 1 * 1024 * 1024,
                cancel);

            cancel.ThrowIfCancellationRequested();
            string extractDir = Path.Combine(_cacheDir, "whisper-extract");
            ExtractZip(zipPath, extractDir, "whisper.cpp");

            cancel.ThrowIfCancellationRequested();
            CopyMatching(extractDir, binDir, "whisper-cli.exe");
            CopyMatching(extractDir, binDir, "main.exe");
            if (!File.Exists(Path.Combine(binDir, "whisper-cli.exe")) && File.Exists(Path.Combine(binDir, "main.exe")))
                File.Copy(Path.Combine(binDir, "main.exe"), Path.Combine(binDir, "whisper-cli.exe"), true);

            CopyMatching(extractDir, binDir, "whisper.dll");
            CopyMatching(extractDir, binDir, "ggml.dll");
            CopyMatching(extractDir, binDir, "ggml-base.dll");
            foreach (string dll in Directory.GetFiles(extractDir, "ggml-cpu-*.dll", SearchOption.AllDirectories))
            {
                cancel.ThrowIfCancellationRequested();
                string dest = Path.Combine(binDir, Path.GetFileName(dll));
                File.Copy(dll, dest, true);
                _log("  Installed " + Path.GetFileName(dll));
            }

            if (!File.Exists(Path.Combine(binDir, "whisper-cli.exe")))
                throw new FileNotFoundException("whisper-cli.exe was not found inside the whisper.cpp zip.");
        }

        public void EnsureFfmpeg(string binDir, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            Directory.CreateDirectory(binDir);
            if (File.Exists(Path.Combine(binDir, "ffmpeg.exe")))
            {
                _log("FFmpeg already present.");
                return;
            }

            string zipPath = Path.Combine(_cacheDir, "ffmpeg-essentials.zip");
            TryDeleteFile(Path.Combine(_cacheDir, "ffmpeg-release-essentials.zip"));

            EnsureValidZip(
                zipPath,
                DependencyCatalog.FfmpegZipUrl,
                "FFmpeg (essentials)",
                minBytes: 40L * 1024 * 1024,
                cancel);

            cancel.ThrowIfCancellationRequested();
            string extractDir = Path.Combine(_cacheDir, "ffmpeg-extract");
            ExtractZip(zipPath, extractDir, "FFmpeg");

            cancel.ThrowIfCancellationRequested();
            CopyMatching(extractDir, binDir, "ffmpeg.exe");
            CopyMatching(extractDir, binDir, "ffprobe.exe");
            CopyMatching(extractDir, binDir, "ffplay.exe");

            if (!File.Exists(Path.Combine(binDir, "ffmpeg.exe")))
                throw new FileNotFoundException("ffmpeg.exe was not found inside the FFmpeg zip.");
        }

        public void CleanupCache()
        {
            if (string.IsNullOrWhiteSpace(_cacheDir) || !Directory.Exists(_cacheDir))
                return;

            _log("Cleaning download cache…");
            try
            {
                foreach (string file in Directory.GetFiles(_cacheDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                        _log("  Removed " + Path.GetFileName(file));
                    }
                    catch (Exception ex)
                    {
                        _log("  Could not remove " + Path.GetFileName(file) + ": " + ex.Message);
                    }
                }

                TryDeleteDir(_cacheDir);
                if (!Directory.Exists(_cacheDir))
                    _log("Removed cache folder " + _cacheDir);
            }
            catch (Exception ex)
            {
                _log("Cache cleanup warning: " + ex.Message);
            }
        }

        public void EnsureModel(ModelDownloadOption model, string modelsDir, CancellationToken cancel)
        {
            if (model == null)
                return;
            cancel.ThrowIfCancellationRequested();
            Directory.CreateDirectory(modelsDir);
            string dest = Path.Combine(modelsDir, model.FileName);
            if (File.Exists(dest) && new FileInfo(dest).Length > 1000)
            {
                _log("Model already present: " + model.FileName);
                return;
            }

            string cacheFile = Path.Combine(_cacheDir, model.FileName);
            if (!File.Exists(cacheFile) || new FileInfo(cacheFile).Length < 1000)
                DownloadFile(model.Url, cacheFile, model.DisplayName + " model", cancel);

            cancel.ThrowIfCancellationRequested();
            if (!File.Exists(cacheFile) || new FileInfo(cacheFile).Length < 1000)
                throw new IOException("Model download failed: " + model.FileName);

            File.Copy(cacheFile, dest, true);
            _log("Installed model " + model.FileName);
        }

        private void EnsureValidZip(
            string zipPath,
            string url,
            string label,
            long minBytes,
            CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            if (IsValidZip(zipPath, minBytes))
            {
                _log("Using cached " + Path.GetFileName(zipPath));
                return;
            }

            if (File.Exists(zipPath))
            {
                _log("Cached " + Path.GetFileName(zipPath) + " is incomplete/corrupt — re-downloading.");
                TryDeleteFile(zipPath);
            }

            DownloadFile(url, zipPath, label, cancel);

            cancel.ThrowIfCancellationRequested();
            if (!IsValidZip(zipPath, minBytes))
            {
                TryDeleteFile(zipPath);
                throw new InvalidDataException(
                    "Downloaded " + label + " is not a valid ZIP archive. Check your network and try again.");
            }
        }

        private void ExtractZip(string zipPath, string extractDir, string label)
        {
            TryDeleteDir(extractDir);
            Directory.CreateDirectory(extractDir);
            try
            {
                ZipFile.ExtractToDirectory(zipPath, extractDir);
            }
            catch (InvalidDataException ex)
            {
                TryDeleteFile(zipPath);
                throw new InvalidDataException(
                    "Could not extract " + label + " (corrupt ZIP). The cache was cleared — run Install again. "
                    + ex.Message,
                    ex);
            }
            _log("Extracted " + label + " archive");
        }

        private static bool IsValidZip(string path, long minBytes)
        {
            try
            {
                if (!File.Exists(path))
                    return false;
                var info = new FileInfo(path);
                if (info.Length < minBytes)
                    return false;

                using (var fs = File.OpenRead(path))
                {
                    if (fs.Length < 4)
                        return false;
                    int b0 = fs.ReadByte();
                    int b1 = fs.ReadByte();
                    int b2 = fs.ReadByte();
                    int b3 = fs.ReadByte();
                    if (b0 != 'P' || b1 != 'K' || b2 != 3 || b3 != 4)
                        return false;
                }

                using (ZipArchive archive = ZipFile.OpenRead(path))
                {
                    if (archive.Entries.Count == 0)
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void CopyMatching(string root, string destDir, string fileName)
        {
            string[] hits = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            if (hits.Length == 0)
                return;
            string best = hits
                .OrderByDescending(p => p.IndexOf("Release", StringComparison.OrdinalIgnoreCase) >= 0)
                .ThenByDescending(p => p.IndexOf("\\bin\\", StringComparison.OrdinalIgnoreCase) >= 0
                    || p.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0)
                .First();
            File.Copy(best, Path.Combine(destDir, fileName), true);
            _log("  Installed " + fileName);
        }

        private static void TryDeleteDir(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch { }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch { }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") + " GB";
        }
    }
}
