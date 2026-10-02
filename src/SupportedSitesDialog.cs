using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace LinkDownloader {
    public sealed class SupportedSitesDialog {
        public readonly Window Window;
        public Task Loading { get; private set; }
        readonly TextBox search;
        readonly ListBox list;
        readonly TextBlock count, emptyText;
        readonly FrameworkElement empty;
        readonly Button retry;
        readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        string[] sites=new string[0];
        static string[] cached;
        static DateTime cachedVersion;
        bool closed;
        public SupportedSitesDialog(Window owner) {
            string assembly=Assembly.GetExecutingAssembly().GetName().Name;
            Window=(Window)Application.LoadComponent(new Uri("/"+assembly+";component/SupportedSites.xaml",UriKind.Relative));
            Localization.Apply(Window);
            Window.Owner=owner;Window.Icon=owner.Icon;Window.Resources.MergedDictionaries.Add(owner.Resources);
            Window.MaxHeight=SystemParameters.WorkArea.Height;Window.Height=Math.Min(Window.Height,Window.MaxHeight);
            search=(TextBox)Window.FindName("SearchInput");list=(ListBox)Window.FindName("SiteList");count=(TextBlock)Window.FindName("ResultCount");
            empty=(FrameworkElement)Window.FindName("EmptyState");emptyText=(TextBlock)Window.FindName("EmptyText");retry=(Button)Window.FindName("RetryButton");
            foreach(string site in new[]{"YouTube","Vimeo","TikTok","Instagram","Twitch","SoundCloud"}) {
                var button=new Button {Content=site,FontSize=11,MinHeight=30,Padding=new Thickness(10,5,10,5),Margin=new Thickness(0,0,6,6)};
                button.Click+=delegate {search.Text=site;search.Focus();search.CaretIndex=search.Text.Length;};
                ((WrapPanel)Window.FindName("PopularSites")).Children.Add(button);
            }
            search.TextChanged+=delegate {((TextBlock)Window.FindName("SearchHint")).Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Filter();};
            retry.Click+=delegate {Loading=Load();};
            ((Button)Window.FindName("CloseButton")).Click+=delegate {Window.Close();};
            Window.Loaded+=delegate {search.Focus();Loading=Load();};
            Window.Closed+=delegate {closed=true;lifetime.Cancel();};
            Window.SourceInitialized+=delegate {
                var color=((SolidColorBrush)owner.Resources["Canvas"]).Color;int dark=color.R<128?1:0,round=2,rgb=color.R|(color.G<<8)|(color.B<<16);
                var handle=new WindowInteropHelper(Window).Handle;
                try {DwmSetWindowAttribute(handle,20,ref dark,4);DwmSetWindowAttribute(handle,33,ref round,4);DwmSetWindowAttribute(handle,35,ref rgb,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
            };
        }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
        async Task Load() {
            retry.Visibility=Visibility.Collapsed;empty.Visibility=Visibility.Visible;emptyText.Text=Localization.T("Wczytywanie listy z yt-dlp…");count.Text=Localization.T("Wczytywanie listy…");
            try {
                var version=File.GetLastWriteTimeUtc(Path.Combine(DownloadEngine.ToolsPath,"yt-dlp.exe"));
                if(cached!=null && version==cachedVersion) sites=cached;
                else {
                    var lines=new List<string>();
                    using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)) {
                        timeout.CancelAfter(TimeSpan.FromSeconds(20));
                        int exit=await new DownloadEngine().RunAsync(new[]{"--ignore-config","--list-extractors"},delegate(string line){lock(lines)lines.Add(line);},timeout.Token);
                        if(exit!=0)throw new IOException("Nie udało się odczytać listy z yt-dlp.");
                    }
                    sites=lines.Where(s=>!String.IsNullOrWhiteSpace(s) && !s.StartsWith("WARNING:",StringComparison.OrdinalIgnoreCase)).Select(s=>s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s=>s,StringComparer.OrdinalIgnoreCase).ToArray();
                    if(sites.Length==0)throw new IOException("yt-dlp zwróciło pustą listę.");
                    cached=sites;cachedVersion=version;
                }
                if(!closed)Filter();
            } catch(Exception e) {
                if(closed)return;
                count.Text=Localization.T("Lista niedostępna");emptyText.Text=e is OperationCanceledException?Localization.T("Wczytywanie trwało zbyt długo. Spróbuj ponownie."):Localization.T("Nie udało się wczytać listy. Sprawdź, czy plik yt-dlp jest w folderze tools.");
                retry.Visibility=Visibility.Visible;
            }
        }
        void Filter() {
            if(sites.Length==0)return;
            string query=search.Text.Trim();var matches=sites.Where(s=>s.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).ToArray();list.ItemsSource=matches.Select(s=>s.Replace("(CURRENTLY BROKEN)",Localization.T("(chwilowo niedostępny)"))).ToArray();
            count.Text=Localization.T("Wyniki: {0} z {1} wpisów · serwisy i ich warianty",matches.Length,sites.Length);
            empty.Visibility=matches.Length==0?Visibility.Visible:Visibility.Collapsed;
            emptyText.Text=Localization.T("Brak pasujących wpisów. Spróbuj krótszej nazwy serwisu.");
        }
    }
}
