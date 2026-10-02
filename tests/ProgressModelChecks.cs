using System;
using LinkDownloader;

internal static class ProgressModelChecks {
    static int checks;
    static void Check(bool condition, string label) {
        if (!condition) throw new Exception(label);
        checks++;
        Console.WriteLine("PASS " + label);
    }
    static void Clear(ProgressModel progress, string label) {
        Check(progress.Speed == "" && progress.Eta == "" && progress.Size == "", label);
    }
    static int Main() {
        try {
            var p = new ProgressModel();
            Check(p.Stage == DownloadStage.Idle && !p.Percent.HasValue, "initial state is idle, with no invented percentage");
            p.Start(false);
            Check(p.Stage == DownloadStage.Connecting && p.TransferIndex == 0 && p.Filename == "", "new operation starts cleanly");
            Check(!p.Consume("[youtube] Downloading webpage") && !p.Consume("WARNING: retrying a request"), "metadata and warnings do not fabricate progress");
            Check(p.Consume(@"[download] Destination: C:\Downloads\Film [abc].f137.mp4"), "destination starts the first transfer");
            Check(p.Stage == DownloadStage.Downloading && p.TransferIndex == 1 && p.StageLabel == "Pobieranie filmu" && !p.Percent.HasValue, "video identified from destination; total remains unknown");
            p.Consume("[download]  42.7% of 87.15MiB at 8.64MiB/s ETA 00:06");
            Check(p.Percent == 42.7 && p.Speed == "8.64 MiB/s" && p.Eta == "00:06" && p.Size == "87.15 MiB", "real yt-dlp line exposes current percentage, size, rate and ETA");
            p.Consume("[download] 100% of 87.15MiB in 00:00:10 at 8.64MiB/s");
            Check(p.Stage == DownloadStage.Downloading && p.Percent == 100 && p.Speed == "" && p.Eta == "", "100 percent transfer does not claim operation success");
            p.Consume(@"[download] Destination: C:\Downloads\Film [abc].f140.m4a");
            Check(p.TransferIndex == 2 && p.StageLabel == "Pobieranie dźwięku" && !p.Percent.HasValue, "separate audio transfer resets its own progress");
            p.Consume("[download]  0.1% of 1.11MiB at 80.01KiB/s ETA 00:14");
            Check(p.Percent == 0.1 && p.Size == "1.11 MiB" && p.TransferIndex == 2, "audio percentage is not incorrectly treated as total-operation percentage");
            p.Consume("[download] 100% of 1.11MiB in 00:00:00 at 4.97MiB/s");
            p.Consume("[Merger] Merging formats into a file");
            Check(p.Stage == DownloadStage.Processing && !p.Percent.HasValue && p.StageLabel == "Łączenie obrazu i dźwięku", "merging switches to indeterminate processing");
            Clear(p, "processing clears stale transfer metrics");
            p.Consume(@"ZAPISANO: C:\Downloads\Film [abc].mp4");
            Check(p.Stage == DownloadStage.Processing && p.Filename.EndsWith("Film [abc].mp4"), "saved-path log still waits for process exit");
            p.Complete();
            Check(p.Stage == DownloadStage.Completed && p.Percent == 100, "only explicit successful process completion sets success");
            Clear(p, "success clears transient transfer metrics");
            Check(!p.Consume("[download] 1.0% of 1MiB at 1MiB/s ETA 00:01") && p.Stage == DownloadStage.Completed, "late queued progress cannot replace success");

            p.Start(true);
            p.Consume("[download] 25.0% of ~ 4.00MiB at Unknown B/s ETA Unknown");
            Check(p.Percent == 25 && p.Size == "≈ 4.00 MiB" && p.Speed == "" && p.Eta == "" && p.StageLabel == "Pobieranie dźwięku", "estimated total and unknown metrics remain explicit");
            p.Consume("[download] 2.00MiB at 1.00MiB/s (00:02)");
            Check(!p.Percent.HasValue && p.Size == "2.00 MiB pobrano" && p.Speed == "1.00 MiB/s" && p.Eta == "", "unknown-size stream has byte count but no fabricated percent or ETA");
            foreach (string raw in new[] { "NaN", "Infinity", "-1", "101" }) {
                p.Consume("[download] " + raw + "% of Unknown B at Unknown B/s ETA Unknown");
                Check(!p.Percent.HasValue && p.Speed == "" && p.Eta == "" && p.Size == "", "invalid percentage " + raw + " remains indeterminate");
            }
            p.Consume("[download] 100% of 4MiB in 00:01:00 at 1MiB/s");
            int previousTransfer = p.TransferIndex;
            p.Consume("[download] 0.0% of 20MiB at 1MiB/s ETA 00:20");
            Check(p.Percent == 0 && p.TransferIndex == previousTransfer + 1, "a percentage reset also identifies another transfer without a destination line");
            p.Consume("[ExtractAudio] Destination: audio.mp3");
            Check(p.Stage == DownloadStage.Processing && !p.Percent.HasValue && p.StageLabel == "Przygotowywanie dźwięku MP3", "MP3 extraction is processing");
            Clear(p, "MP3 extraction clears transfer metrics");
            p.Cancel();
            Check(p.Stage == DownloadStage.Cancelled && !p.Percent.HasValue, "cancellation has no success percentage");
            Check(!p.Consume("[Merger] late output"), "cancelled operation ignores late output");

            p.Start(false);
            p.Consume(@"[download] C:\Downloads\Existing.mp4 has already been downloaded");
            Check(p.Stage == DownloadStage.Processing && !p.Percent.HasValue && p.Filename.EndsWith("Existing.mp4"), "existing output waits for successful process exit");
            p.Fail();
            Check(p.Stage == DownloadStage.Failed && !p.Percent.HasValue, "failure has no success percentage");
            Clear(p, "failure clears metrics");
            p.Start(false);
            p.Consume("[download] 10.0% of 3.00GiB at 8.00MiB/s ETA 01:03:04 (frag 15/90)");
            Check(p.Eta == "01:03:04" && p.Size == "3.00 GiB", "long fragmented-stream ETA is parsed independently of fragment count");
            p.Consume("[VideoRemuxer] Remuxing video from webm to mp4");
            Check(p.Stage == DownloadStage.Processing && !p.Percent.HasValue, "video remux remains indeterminate");
            p.Complete(@"C:\Downloads\Final.mp4");
            Check(p.Filename == @"C:\Downloads\Final.mp4", "caller can supply confirmed final path");
            p.Start(false, true);
            Check(p.Stage == DownloadStage.Connecting && !p.Percent.HasValue && p.TransferIndex == 0, "updater has its own clean connecting state");
            p.Complete();
            Check(p.StageLabel == "Wszystko aktualne", "updater completion uses matching wording");
            Console.WriteLine("ALL " + checks + " PROGRESS CHECKS PASSED");
            return 0;
        } catch (Exception error) {
            Console.WriteLine("FAIL " + error);
            return 1;
        }
    }
}
