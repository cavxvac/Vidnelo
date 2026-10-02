using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using LinkDownloader;

class FormatChecks {
    static string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-artifacts","formats-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static string Hash(string path){using(var file=File.OpenRead(path))using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(file));}
    static string Probe(string path) {
        var args=new[]{"-v","error","-show_entries","stream=codec_name,codec_type:format=format_name","-of","default=noprint_wrappers=1",path};
        using(var p=new Process {StartInfo=new ProcessStartInfo(Path.Combine(DownloadEngine.ToolsPath,"ffprobe.exe"),String.Join(" ",args.Select(DownloadEngine.Quote))) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}}) {
            p.Start();var error=p.StandardError.ReadToEndAsync();string result=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(error.Result);return result;
        }
    }
    static string Download(string format,bool audio) {
        string saved=null;var log=new StringBuilder();
        using(var token=new CancellationTokenSource(TimeSpan.FromSeconds(90))) {
            int exit=new DownloadEngine().RunAsync(DownloadEngine.Arguments("http://127.0.0.1:18769/sample.mp4",root,audio,0,format),delegate(string line){lock(log){log.AppendLine(line);if(line.StartsWith("ZAPISANO: "))saved=line.Substring(10);}},token.Token).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(root,format+".log"),log.ToString());
            Check(exit==0 && File.Exists(saved),"Download "+format);
        }
        Check(Path.GetExtension(saved)=="."+format && Path.GetFileNameWithoutExtension(saved).EndsWith(" - vidnelo"),"Correct extension and Vidnelo suffix: "+format);
        string probe=Probe(saved);File.WriteAllText(Path.Combine(root,format+"-probe.txt"),probe);
        Check(probe.Contains("codec_type=audio") && (audio?!probe.Contains("codec_type=video"):probe.Contains("codec_type=video")),"Correct media streams: "+format);
        string expected=format=="mp3"?"mp3":format=="m4a"?"aac":format=="opus"?"opus":format=="wav"?"pcm_s16le":format=="flac"?"flac":format=="webm"?"vp9":null;
        if(expected!=null)Check(probe.Contains("codec_name="+expected),"Real codec "+expected);
        return saved;
    }
    static int Main() {
        try {
            Directory.CreateDirectory(root);Console.WriteLine("Artifacts: "+root);
            string original=Download("mp4",false),hash=Hash(original);
            foreach(string format in new[]{"mkv","webm"})Download(format,false);
            foreach(string format in new[]{"mp3","m4a","opus","wav","flac"})Download(format,true);
            Check(Hash(original)==hash,"Other formats preserve original MP4 bytes");
            string m4a=Directory.GetFiles(root,"*.m4a").Single(),audioHash=Hash(m4a);DateTime stamp=File.GetLastWriteTimeUtc(m4a);
            Download("m4a",true);Check(Hash(m4a)==audioHash && File.GetLastWriteTimeUtc(m4a)==stamp,"Repeated M4A download preserves existing file");
            Check(!Directory.GetFiles(root,"*.part").Any() && !Directory.GetFiles(root,"*.tmp.*").Any(),"No incomplete files remain");
            Console.WriteLine("ALL FORMAT CHECKS PASSED");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}
    }
}
