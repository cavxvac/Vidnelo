using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LinkDownloader;

// Run against test-server.py on localhost. Outputs stay under test-artifacts.
// No GUI, saved settings, user Downloads, or AppData are touched.
internal static class EngineChecks {
    private static string runFolder;
    private static string outputFolder;
    private const string SampleUrl = "http://127.0.0.1:18769/sample.mp4?a=1&b=2";

    private static void Check(bool condition, string description) {
        if (!condition) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }

    private static string Hash(string path) {
        using (var sha = SHA256.Create())
        using (var file = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(file));
    }

    private static string Probe(string path, string selector) {
        var info = new ProcessStartInfo(Path.Combine(DownloadEngine.ToolsPath, "ffprobe.exe"),
            "-v error -select_streams " + selector + " -show_entries stream=codec_name -of default=noprint_wrappers=1:nokey=1 " + DownloadEngine.Quote(path));
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using (var process = Process.Start(info)) {
            var output = process.StandardOutput.ReadToEnd();
            var errors = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new Exception("ffprobe failed: " + errors);
            return output.Trim();
        }
    }

    private static async Task<int> Run(string name, string url, bool audio) {
        using (var limit = new CancellationTokenSource(TimeSpan.FromSeconds(40)))
        using (var writer = new StreamWriter(Path.Combine(runFolder, name + ".log"), false, Encoding.UTF8)) {
            int exit = await new DownloadEngine().RunAsync(DownloadEngine.Arguments(url, outputFolder, audio), delegate(string line) {
                lock (writer) writer.WriteLine(line);
            }, limit.Token);
            return exit;
        }
    }

    private static async Task Tests() {
        var invalidUrls = new[] { "", "not a link", "file:///C:/test", "ftp://example.org/sample.mp4", "https://example.org/\n--exec anything", "https://example.org/\rfoo", "https://example.org/\0foo" };
        Check(invalidUrls.All(value => !DownloadEngine.IsWebUrl(value)), "Invalid URL schemes, newlines, and nulls rejected");
        Check(DownloadEngine.IsWebUrl(SampleUrl), "HTTP URL with query parameters accepted");
        bool rejected = false;
        try { DownloadEngine.Arguments("file:///C:/test", outputFolder, false); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Arguments rejects invalid URL before launching a process");

        var values = new[] { "", "https://example.org/?a=1&b=$(test)|hello", "some \"quoted\" text", @"C:\folder with spaces\", "żółć", "\\\"", "\\\\" };
        var echo = new ProcessStartInfo(typeof(EngineChecks).Assembly.Location, "--echo " + String.Join(" ", values.Select(DownloadEngine.Quote)));
        echo.UseShellExecute = false;
        echo.CreateNoWindow = true;
        echo.RedirectStandardOutput = true;
        using (var process = Process.Start(echo)) {
            var actual = process.StandardOutput.ReadToEnd().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            process.WaitForExit();
            Check(process.ExitCode == 0 && actual.SequenceEqual(values.Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)))), "Windows argument quoting round-trips empty, Unicode, quoted, and shell-like values");
        }
        var args = DownloadEngine.Arguments(SampleUrl, outputFolder, false);
        Check(args[args.Length - 2] == "--" && args[args.Length - 1] == SampleUrl && args.Contains("--ignore-config") && args.Contains("--no-overwrites") && args.Contains("--no-playlist"), "URL remains one argument; user config, playlists, and overwrites remain disabled");

        Check(await Run("01-video", SampleUrl, false) == 0, "MP4 download returns success");
        string video = Directory.GetFiles(outputFolder, "*.mp4").Single();
        string originalHash = Hash(video);
        DateTime originalTime = File.GetLastWriteTimeUtc(video);
        Check(Path.GetFileName(video) == "sample - vidnelo.mp4", "Video filename contains title and Vidnelo suffix without source ID");
        Check(originalHash == Hash(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-artifacts", "sample.mp4")), "Downloaded video bytes match the source in the chosen Unicode folder");
        Check(Probe(video, "v:0").Length > 0 && Probe(video, "a:0").Length > 0, "Downloaded MP4 has video and audio streams");
        Check(await Run("02-repeat-video", SampleUrl, false) == 0 && Hash(video) == originalHash && File.GetLastWriteTimeUtc(video) == originalTime, "Repeat MP4 download preserves existing file content and timestamp");
        Check(await Run("03-audio", SampleUrl, true) == 0, "MP3 conversion returns success");
        string audioFile = Directory.GetFiles(outputFolder, "*.mp3").Single();
        Check(Path.GetFileName(audioFile) == "sample - audio - vidnelo.mp3", "Audio filename omits source ID and preserves Vidnelo suffix");
        Check(Probe(audioFile, "a:0") == "mp3" && Probe(audioFile, "v:0").Length == 0, "FFmpeg output contains a real MP3 audio stream without video");
        Check(File.Exists(video) && Hash(video) == originalHash && File.GetLastWriteTimeUtc(video) == originalTime, "MP3 conversion preserves earlier MP4 content and timestamp");

        using (var cancel = new CancellationTokenSource())
        using (var writer = new StreamWriter(Path.Combine(runFolder, "04-cancel.log"), false, Encoding.UTF8)) {
            var slow = new DownloadEngine().RunAsync(DownloadEngine.Arguments("http://127.0.0.1:18769/slow.mp4", outputFolder, false), delegate(string line) {
                lock (writer) writer.WriteLine(line);
            }, cancel.Token);
            var waiting = Stopwatch.StartNew();
            while (!Directory.GetFiles(outputFolder, "*.part").Any() && !slow.IsCompleted && waiting.ElapsedMilliseconds < 15000) await Task.Delay(100);
            bool started = Directory.GetFiles(outputFolder, "*.part").Any();
            var elapsed = Stopwatch.StartNew();
            cancel.Cancel();
            if (await Task.WhenAny(slow, Task.Delay(15000)) != slow) throw new TimeoutException("Cancellation did not end yt-dlp within 15 seconds; inspect taskkill permissions in this environment.");
            bool cancelled = false;
            try { await slow; } catch (OperationCanceledException) { cancelled = true; }
            Check(started && cancelled && elapsed.ElapsedMilliseconds < 15000, "Cancellation terminates an active slow download promptly");
            var partials = Directory.GetFiles(outputFolder, "*.part");
            var lengths = partials.Select(path => new FileInfo(path).Length).ToArray();
            await Task.Delay(700);
            Check(partials.Select(path => new FileInfo(path).Length).SequenceEqual(lengths), "Cancelled partial file stops growing");
        }
        Check(await Run("05-missing", "http://127.0.0.1:18769/missing.mp4", false) != 0, "HTTP 404 returns a nonzero error result after cancellation");
        Check(await Run("06-recovery", SampleUrl, false) == 0, "A later valid download succeeds after cancellation and HTTP failure");
        Check(Hash(video) == originalHash && File.GetLastWriteTimeUtc(video) == originalTime, "Completed MP4 remains intact after all regression checks");
    }

    private static int Main(string[] args) {
        if (args.Length > 0 && args[0] == "--echo") {
            foreach (var value in args.Skip(1)) Console.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
            return 0;
        }
        runFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-artifacts", "engine-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        outputFolder = Path.Combine(runFolder, "Wideo z testu ąę");
        Directory.CreateDirectory(outputFolder);
        Console.WriteLine("Artifacts: " + runFolder);
        try {
            Tests().GetAwaiter().GetResult();
            Console.WriteLine("ALL ENGINE CHECKS PASSED");
            return 0;
        } catch (Exception error) {
            Console.WriteLine("FAIL: " + error);
            return 1;
        }
    }
}
