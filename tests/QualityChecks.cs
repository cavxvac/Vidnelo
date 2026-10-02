using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using LinkDownloader;

class QualityChecks {
    static string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-artifacts","quality-engine");
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static string Execute(string tool,string[] args) {
        var p=new Process {StartInfo=new ProcessStartInfo(Path.Combine(DownloadEngine.ToolsPath,tool),String.Join(" ",args.Select(DownloadEngine.Quote))) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        p.Start();var error=p.StandardError.ReadToEndAsync();string result=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(error.Result);return result.Trim();
    }
    static int Main() {
        try {
            Directory.CreateDirectory(root);
            foreach(int limit in new[]{0,2160,1440,1080,720,480}) {
                var args=DownloadEngine.Arguments("http://127.0.0.1:18769/sample.mp4",root,false,limit);string selector=args[Array.IndexOf(args,"-f")+1];
                string selected=Execute("yt-dlp.exe",new[]{"--ignore-config","--simulate","--no-check-formats","--load-info-json",Path.Combine(root,"formats.json"),"-f",selector,"--print","%(format_id)s"});
                string expected=limit==0 || limit>=2160?"2160+a":limit>=1080?"1080+a":"480+a";
                Check(selected==expected,"Actual yt-dlp format selection for limit "+limit+": "+selected);
            }
            foreach(int quality in new[]{128,192,256,320}) DownloadAndProbe("sample.mp4",quality);
            DownloadAndProbe("source-128.mp3",320);
            Console.WriteLine("ALL QUALITY CHECKS PASSED");return 0;
        } catch(Exception e){Console.WriteLine(e);return 1;}
    }
    static void DownloadAndProbe(string source,int quality) {
        string saved=null;var log=new StringBuilder();
        int exit=new DownloadEngine().RunAsync(DownloadEngine.Arguments("http://127.0.0.1:18769/"+source,root,true,quality),delegate(string line){lock(log){log.AppendLine(line);if(line.StartsWith("ZAPISANO: "))saved=line.Substring(10);}},CancellationToken.None).GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(root,source+quality+".log"),log.ToString());
        Check(exit==0 && File.Exists(saved),"Download and conversion "+source+" at "+quality);
        string measured=Execute("ffprobe.exe",new[]{"-v","error","-select_streams","a:0","-show_entries","stream=codec_name,bit_rate","-of","default=noprint_wrappers=1",saved});
        Check(measured.Contains("codec_name=mp3") && measured.Contains("bit_rate="+(quality*1000)),"Real MP3 codec and bitrate "+quality+" kb/s from "+source);
        Check(Path.GetFileName(saved).EndsWith("- vidnelo.mp3"),"Vidnelo remains at filename end");
    }
}
