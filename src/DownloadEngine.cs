using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LinkDownloader {
    public sealed class DownloadEngine {
        public static string ToolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
        public static bool IsWebUrl(string value) {
            Uri uri;
            return !String.IsNullOrWhiteSpace(value) && value.IndexOfAny(new[] {'\r','\n','\0'}) < 0 &&
                Uri.TryCreate(value, UriKind.Absolute, out uri) && !String.IsNullOrEmpty(uri.Host) &&
                (uri.Scheme == "http" || uri.Scheme == "https");
        }
        // Windows command-line quoting; URLs are arguments, never executable shell commands.
        public static string Quote(string value) {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') result.Append('\\', slashes * 2 + 1).Append('"');
                else result.Append('\\', slashes).Append(c);
                slashes = 0;
            }
            return result.Append('\\', slashes * 2).Append('"').ToString();
        }
        public static string[] Arguments(string url, string folder, bool audio, int quality = 0, string format = null) {
            if (!IsWebUrl(url)) throw new ArgumentException("Wklej poprawny link zaczynający się od https:// lub http://.");
            format=format??(audio?"mp3":"mp4");
            if(Array.IndexOf(audio?new[]{"mp3","m4a","opus","wav","flac"}:new[]{"mp4","mkv","webm"},format)<0)throw new ArgumentException("Nieprawidłowy format pliku.");
            if(audio && format!="mp3" && quality!=0)throw new ArgumentException("Ten format używa automatycznej jakości dźwięku.");
            if(Array.IndexOf(audio?new[]{0,320,256,192,128}:new[]{0,2160,1440,1080,720,480},quality)<0)throw new ArgumentException("Nieprawidłowa jakość pobierania.");
            string qualitySuffix=quality==0?"":audio?"-"+quality+"k":" - max"+quality+"p";
            bool defaultFormat=format==(audio?"mp3":"mp4");
            string formatSuffix=defaultFormat?"":audio?"-"+format:" - "+format;
            string filename=(defaultFormat?"%(title).100s":"%(title).90s")+(audio?" - audio":"")+formatSuffix+qualitySuffix+" - vidnelo.%(ext)s";
            var args = new List<string> {
                "--ignore-config", "--no-playlist", "--newline", "--no-color", "--encoding", "utf-8",
                "--progress", "--no-simulate", "--windows-filenames", "--trim-filenames", "160",
                "--no-overwrites", "--ffmpeg-location", ToolsPath,
                "--js-runtimes", "deno:" + Path.Combine(ToolsPath, "deno.exe"),
                "--paths", folder, "--output", filename,
                "--print", "after_move:ZAPISANO: %(filepath)s"
            };
            if (audio) args.AddRange(new[] {"-f", "bestaudio/best", "--extract-audio", "--audio-format", format, "--audio-quality", quality==0?"0":quality.ToString(CultureInfo.InvariantCulture)+"K"});
            else {
                string limit=quality==0?"":"[height<=?"+quality.ToString(CultureInfo.InvariantCulture)+"]";
                string fallback="bv*"+limit+"+ba/b"+limit;
                string selector=format=="mp4"?"bv*[ext=mp4]"+limit+"+ba[ext=m4a]/b[ext=mp4]"+limit+"/"+fallback:format=="webm"?"bv*[ext=webm]"+limit+"+ba[ext=webm]/b[ext=webm]"+limit+"/"+fallback:fallback;
                args.AddRange(new[] {"-f",selector,"--merge-output-format",format=="webm"?"webm/mkv":format,format=="webm"?"--recode-video":"--remux-video",format});
                if(format=="webm")args.AddRange(new[]{"--postprocessor-args","VideoConvertor+ffmpeg_o:-c:v libvpx-vp9 -crf 30 -b:v 0 -c:a libopus"});
            }
            if(audio && quality!=0)args.AddRange(new[]{"--postprocessor-args","ExtractAudio+ffmpeg_o:-ar 44100"});
            args.Add("--"); args.Add(url);
            return args.ToArray();
        }
        public async Task<int> RunAsync(string[] arguments, Action<string> output, CancellationToken token) {
            int qualityIndex=Array.IndexOf(arguments,"--audio-quality"),bitrate=0;
            if(qualityIndex>=0 && qualityIndex+1<arguments.Length)Int32.TryParse(arguments[qualityIndex+1].TrimEnd('K','k'),out bitrate);
            string saved=null;
            int code=await RunToolAsync("yt-dlp.exe",arguments,delegate(string line) {
                if(bitrate>10 && line.StartsWith("ZAPISANO: ",StringComparison.Ordinal))saved=line.Substring(10).Trim();else output(line);
            },token);
            if(code!=0 || bitrate<=10 || String.IsNullOrEmpty(saved))return code;
            // yt-dlp may copy an existing MP3 without applying --audio-quality.
            int measured=0;
            int probe=await RunToolAsync("ffprobe.exe",new[]{"-v","error","-select_streams","a:0","-show_entries","stream=bit_rate","-of","default=noprint_wrappers=1:nokey=1",saved},delegate(string line){int value;if(Int32.TryParse(line,out value))measured=value;},token);
            if(probe!=0)throw new IOException("Nie można sprawdzić jakości zapisanego MP3.");
            if(measured!=bitrate*1000) {
                output("[ExtractAudio] Ustawiam jakość MP3: "+bitrate+" kb/s");
                string temporary=saved+"."+Guid.NewGuid().ToString("N")+".tmp.mp3";
                try {
                    int converted=await RunToolAsync("ffmpeg.exe",new[]{"-hide_banner","-loglevel","error","-nostdin","-n","-i",saved,"-map","0:a:0","-vn","-c:a","libmp3lame","-ar","44100","-b:a",bitrate+"k",temporary},output,token);
                    if(converted!=0)return converted;
                    token.ThrowIfCancellationRequested();File.Replace(temporary,saved,null);
                } finally {if(File.Exists(temporary))File.Delete(temporary);}
            }
            output("ZAPISANO: "+saved);return 0;
        }
        Task<int> RunToolAsync(string tool,string[] arguments, Action<string> output, CancellationToken token) {
            return Task.Run(delegate {
                token.ThrowIfCancellationRequested();
                string exe = Path.Combine(ToolsPath, tool);
                if (!File.Exists(exe)) throw new FileNotFoundException("Brakuje tools\\"+tool+". Przywróć plik do folderu programu.");
                var escaped = new List<string>();
                foreach (var arg in arguments) escaped.Add(Quote(arg));
                var info = new ProcessStartInfo(exe, String.Join(" ", escaped));
                info.UseShellExecute = false; info.CreateNoWindow = true;
                info.RedirectStandardOutput = true; info.RedirectStandardError = true;
                info.StandardOutputEncoding = Encoding.UTF8; info.StandardErrorEncoding = Encoding.UTF8;
                info.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                using (var process = new Process()) {
                    process.StartInfo = info;
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) output(e.Data); };
                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) output(e.Data); };
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine();
                    using (token.Register(delegate {
                        try {
                            if (process.HasExited) return;
                            var kill = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "taskkill.exe"),
                                "/PID " + process.Id.ToString(CultureInfo.InvariantCulture) + " /T /F");
                            kill.CreateNoWindow = true; kill.UseShellExecute = false;
                            using (var killer = Process.Start(kill)) { killer.WaitForExit(10000); }
                        } catch (InvalidOperationException) { }
                          catch (System.ComponentModel.Win32Exception) { }
                    })) {
                        process.WaitForExit();
                        token.ThrowIfCancellationRequested();
                        return process.ExitCode;
                    }
                }
            });
        }
    }

}
