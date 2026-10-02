using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LinkDownloader {
    public enum DownloadStage { Idle, Connecting, Downloading, Processing, Completed, Cancelled, Failed }

    // Each percentage belongs to the current yt-dlp transfer. Separate video/audio
    // streams intentionally start separate transfers; only the process exit confirms success.
    public sealed class ProgressModel {
        public DownloadStage Stage { get; private set; }
        public double? Percent { get; private set; }
        public string Speed { get; private set; }
        public string Eta { get; private set; }
        public string Size { get; private set; }
        public string StageLabel { get; private set; }
        public string Filename { get; private set; }
        public int TransferIndex { get; private set; }
        bool audio, updating;
        string transferLabel;

        public ProgressModel() {
            Stage = DownloadStage.Idle;
            StageLabel = "Gotowe do pobierania";
            Filename = String.Empty;
            ClearMetrics();
        }

        public void Start(bool audio, bool update = false) {
            this.audio = audio;
            updating = update;
            Stage = DownloadStage.Connecting;
            StageLabel = update ? "Sprawdzanie aktualizacji" : "Łączenie z serwisem";
            transferLabel = audio ? "Pobieranie dźwięku" : "Pobieranie pliku";
            Filename = String.Empty;
            TransferIndex = 0;
            ClearMetrics();
        }

        // Call on the UI thread after draining stdout/stderr. Empty metrics mean unknown.
        public bool Consume(string line) {
            if (String.IsNullOrWhiteSpace(line) || Stage == DownloadStage.Idle || IsTerminal || updating) return false;
            line = line.Trim();
            if (line.StartsWith("ZAPISANO: ", StringComparison.Ordinal)) {
                Filename = CleanFilename(line.Substring(10));
                Processing("Finalizowanie pliku");
                return true;
            }
            if (line.StartsWith("[Merger]", StringComparison.Ordinal)) {
                Processing("Łączenie obrazu i dźwięku");
                return true;
            }
            if (line.StartsWith("[ExtractAudio]", StringComparison.Ordinal)) {
                Processing("Przygotowywanie pliku dźwiękowego");
                return true;
            }
            if (line.StartsWith("[VideoRemuxer]", StringComparison.Ordinal) || line.StartsWith("[VideoConvertor]", StringComparison.Ordinal)
                || line.StartsWith("[Fixup", StringComparison.Ordinal) || line.StartsWith("[ffmpeg]", StringComparison.Ordinal)) {
                Processing("Przygotowywanie gotowego pliku");
                return true;
            }
            if (!line.StartsWith("[download]", StringComparison.Ordinal)) return false;
            string content = line.Substring(10).Trim();
            if (content.StartsWith("Destination: ", StringComparison.Ordinal)) {
                string destination = CleanFilename(content.Substring(13));
                if (Stage != DownloadStage.Downloading || destination != Filename) TransferIndex++;
                Filename = destination;
                transferLabel = TransferLabel(destination);
                Stage = DownloadStage.Downloading;
                StageLabel = transferLabel;
                ClearMetrics();
                return true;
            }
            int existing = content.IndexOf(" has already been downloaded", StringComparison.Ordinal);
            if (existing >= 0) {
                Filename = CleanFilename(content.Substring(0, existing));
                Processing("Sprawdzanie gotowego pliku");
                return true;
            }

            Match percentage = Parser.Percent.Match(content);
            Match total = Parser.Total.Match(content);
            Match downloaded = Parser.Downloaded.Match(content);
            // Retry notices, resume messages and warnings are log entries, not progress.
            if (!percentage.Success && !total.Success && !downloaded.Success) return false;
            double value;
            double? next = percentage.Success && Double.TryParse(percentage.Groups[1].Value.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !Double.IsNaN(value) &&
                !Double.IsInfinity(value) && value >= 0 && value <= 100 ? (double?)value : null;
            if (TransferIndex == 0 || (Percent == 100 && next.HasValue && next.Value < 100)) TransferIndex++;
            Stage = DownloadStage.Downloading;
            StageLabel = next == 100 ? "Kończenie transferu" : transferLabel;
            Percent = next;
            Size = total.Success ? FormatSize(total) : downloaded.Success ? FormatSize(downloaded) + " pobrano" : String.Empty;
            Match speed = Parser.Speed.Match(content);
            Match eta = Parser.Eta.Match(content);
            Speed = next == 100 || !speed.Success ? String.Empty : FormatSize(speed) + "/s";
            Eta = next == 100 || !eta.Success ? String.Empty : eta.Groups[1].Value;
            return true;
        }

        public void Complete(string finalFilename = null) {
            Stage = DownloadStage.Completed;
            StageLabel = updating ? "Wszystko aktualne" : "Plik gotowy";
            if (!String.IsNullOrWhiteSpace(finalFilename)) Filename = CleanFilename(finalFilename);
            ClearMetrics();
            // This is the only whole-operation completion percentage.
            Percent = 100;
        }

        public void Cancel() {
            Stage = DownloadStage.Cancelled;
            StageLabel = updating ? "Aktualizacja anulowana" : "Pobieranie anulowane";
            ClearMetrics();
        }

        public void Fail() {
            Stage = DownloadStage.Failed;
            StageLabel = updating ? "Nie udało się zaktualizować" : "Nie udało się pobrać";
            ClearMetrics();
        }

        bool IsTerminal {
            get { return Stage == DownloadStage.Completed || Stage == DownloadStage.Cancelled || Stage == DownloadStage.Failed; }
        }

        void ClearMetrics() { Percent = null; Speed = Eta = Size = String.Empty; }
        void Processing(string label) { Stage = DownloadStage.Processing; StageLabel = label; ClearMetrics(); }
        static string CleanFilename(string value) { return value.Trim().Trim('"'); }
        string TransferLabel(string path) {
            if (audio) return "Pobieranie dźwięku";
            string lower = path.ToLowerInvariant();
            foreach (string extension in new[] { ".m4a", ".mp3", ".aac", ".opus", ".ogg", ".wav", ".flac", ".weba" })
                if (lower.EndsWith(extension, StringComparison.Ordinal)) return "Pobieranie dźwięku";
            foreach (string extension in new[] { ".mp4", ".webm", ".mkv", ".mov", ".avi", ".flv", ".ts" })
                if (lower.EndsWith(extension, StringComparison.Ordinal)) return "Pobieranie filmu";
            return "Pobieranie pliku";
        }
        static string FormatSize(Match match) {
            return (match.Groups["approx"].Success ? "≈ " : String.Empty) + match.Groups["number"].Value + " " + match.Groups["unit"].Value;
        }

        // The parser is initialized only when the first transfer progress arrives.
        // No compiled regex or downloader process is needed during app startup.
        static class Parser {
            const string Amount = @"(?<number>\d+(?:[.,]\d+)?)\s*(?<unit>[KMGTPEZY]?i?B)";
            internal static readonly Regex Percent = new Regex(@"^([+-]?(?:\d+(?:[.,]\d+)?|NaN|Infinity))%", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            internal static readonly Regex Total = new Regex(@"\bof\s+(?<approx>~\s*)?" + Amount, RegexOptions.CultureInvariant);
            internal static readonly Regex Downloaded = new Regex(@"^" + Amount + @"(?:\s|$)", RegexOptions.CultureInvariant);
            internal static readonly Regex Speed = new Regex(@"\bat\s+" + Amount + @"/s\b", RegexOptions.CultureInvariant);
            internal static readonly Regex Eta = new Regex(@"\bETA\s+(\d+(?::\d{2}){1,2})\b", RegexOptions.CultureInvariant);
        }
    }
}
