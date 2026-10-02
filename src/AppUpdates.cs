using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace LinkDownloader {
    public sealed class AppRelease {
        public Version Version;
        public string Tag, Notes, Url;
    }
    public static class AppUpdates {
        public const string Repository="https://github.com/cavxvac/Vidnelo";
        public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public static AppRelease Parse(string json) {
            var data=new JavaScriptSerializer {MaxJsonLength=1048576}.Deserialize<Dictionary<string,object>>(json);
            if(data==null)throw new InvalidDataException();
            if((bool)data["draft"] || (bool)data["prerelease"])return null;
            string tag=(string)data["tag_name"];
            if(!Regex.IsMatch(tag??"",@"^v?\d+\.\d+(\.\d+)?(\.\d+)?$"))throw new InvalidDataException();
            Version parsed;
            if(!Version.TryParse(tag.TrimStart('v'),out parsed))throw new InvalidDataException();
            // Version(1,0) has -1 build/revision; normalize before comparing to assembly versions.
            parsed=new Version(parsed.Major,parsed.Minor,Math.Max(0,parsed.Build),Math.Max(0,parsed.Revision));
            string url=(string)data["html_url"];
            Uri uri;
            if(!Uri.TryCreate(url,UriKind.Absolute,out uri) || uri.Scheme!="https" || uri.Host!="github.com" || !uri.IsDefaultPort || uri.UserInfo!="" || !uri.AbsolutePath.StartsWith("/cavxvac/Vidnelo/releases/tag/",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();
            return new AppRelease {Version=parsed,Tag=tag,Url=url,Notes=data.ContainsKey("body") ? data["body"] as string ?? "" : ""};
        }
        public static Task<AppRelease> Check() {
            return Task.Run(()=> {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var request=(HttpWebRequest)WebRequest.Create("https://api.github.com/repos/cavxvac/Vidnelo/releases/latest");
                request.UserAgent="Vidnelo/"+Current.ToString(2);request.Accept="application/vnd.github+json";
                request.Timeout=10000;request.ReadWriteTimeout=10000;request.AllowAutoRedirect=false;
                try {
                    using(var response=(HttpWebResponse)request.GetResponse())
                    using(var reader=new StreamReader(response.GetResponseStream())) {
                        var buffer=new char[1048577];int count=0,read;
                        while(count<buffer.Length && (read=reader.Read(buffer,count,buffer.Length-count))>0)count+=read;
                        if(count>1048576)throw new InvalidDataException();
                        return Parse(new string(buffer,0,count));
                    }
                } catch(WebException e) {
                    using(var response=e.Response as HttpWebResponse) {if(response!=null && response.StatusCode==HttpStatusCode.NotFound)return null;}
                    throw;
                }
            });
        }
    }
    public sealed class UpdatesDialog {
        public readonly Window Window;
        public bool UpdateEngine;
        readonly TextBlock state, notes;
        readonly Button download, skip;
        readonly Action<string> saveSkip;
        AppRelease release;
        bool closed;
        public UpdatesDialog(Window owner,Action<string> saveSkipped) {
            saveSkip=saveSkipped;
            Window=new Window {Title=Localization.T("Aktualizacje"),Owner=owner,Icon=owner.Icon,Width=560,Height=Math.Min(650,SystemParameters.WorkArea.Height),MinWidth=400,ResizeMode=ResizeMode.CanResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false,FontFamily=owner.FontFamily};
            Window.Resources.MergedDictionaries.Add(owner.Resources);
            Window.SetResourceReference(Control.BackgroundProperty,"Canvas");Window.SetResourceReference(Control.ForegroundProperty,"Text");
            var panel=new StackPanel {Margin=new Thickness(24)};
            panel.Children.Add(Label("Aktualizacje",26));
            var app=new StackPanel();app.Children.Add(Label("Vidnelo · "+AppUpdates.Current.ToString(2),18));
            state=Label("Sprawdzanie aktualizacji…",13);app.Children.Add(state);
            notes=Label("",12);notes.Margin=new Thickness(0,4,0,12);
            app.Children.Add(new ScrollViewer {Content=notes,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
            download=Button("Otwórz pobieranie na GitHubie");download.IsEnabled=false;
            download.Click+=delegate {try {Process.Start(new ProcessStartInfo(release.Url){UseShellExecute=true});}catch(Exception){state.Text=Localization.T("Nie udało się otworzyć przeglądarki. Adres: {0}",release.Url);}};
            app.Children.Add(download);
            skip=Button("Pomiń tę wersję");skip.IsEnabled=false;skip.Click+=delegate {saveSkip(release.Tag);Window.Close();};app.Children.Add(skip);
            panel.Children.Add(Card(app));
            var engine=new StackPanel();engine.Children.Add(Label("yt-dlp",18));engine.Children.Add(Label("Silnik pobierania",13));
            var update=Button("Aktualizuj yt-dlp");update.Click+=delegate {UpdateEngine=true;Window.Close();};engine.Children.Add(update);panel.Children.Add(Card(engine));
            var later=Button("Później");later.IsCancel=true;later.HorizontalAlignment=HorizontalAlignment.Right;later.Click+=delegate {Window.Close();};panel.Children.Add(later);
            var scroll=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            scroll.SetResourceReference(Control.BackgroundProperty,"Canvas");Window.Content=scroll;
            Localization.SetDirection(Window);Window.Closed+=delegate {closed=true;};
        }
        static TextBlock Label(string text,double size) {return new TextBlock {Text=Localization.T(text),FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};}
        static Button Button(string text) {return new Button {Content=Localization.T(text),Margin=new Thickness(0,4,0,4)};}
        static Border Card(UIElement content) {
            var card=new Border {Child=content,CornerRadius=new CornerRadius(20),Padding=new Thickness(20),Margin=new Thickness(0,8,0,12),BorderThickness=new Thickness(1)};
            card.SetResourceReference(Border.BackgroundProperty,"Surface");card.SetResourceReference(Border.BorderBrushProperty,"Line");return card;
        }
        public void Display(AppRelease value) {
            release=value;
            bool newer=value!=null && value.Version>AppUpdates.Current;
            state.Text=Localization.T(value==null?"Brak opublikowanych wydań.":newer?"Dostępna wersja {0}":"Masz aktualną wersję.",value==null?"":value.Tag);
            notes.Text=newer?value.Notes:"";download.IsEnabled=skip.IsEnabled=newer;
        }
        public async Task Check() {
            try {var result=await AppUpdates.Check();if(!closed)Display(result);}
            catch(Exception) {if(!closed)state.Text=Localization.T("Nie udało się sprawdzić aktualizacji. Spróbuj później.");}
        }
    }
}
