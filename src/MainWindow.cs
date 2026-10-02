using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Shell;
using IOPath = System.IO.Path;

namespace LinkDownloader {
    public sealed class FluentApp {
        public readonly Window Window;
        readonly TextBox url, log;
        readonly TextBlock folderText, title, detail, percent, downloadLabel;
        readonly Button download, cancel, paste, browse, open, update;
        readonly RadioButton audio, video;
        readonly ComboBox quality;
        readonly ComboBox fileFormat;
        string videoFormat="mp4", audioFormat="mp3";
        bool updatingFormat;
        int videoQuality, audioQuality;
        bool updatingQuality;
        readonly Border fill, marquee, badge, urlBorder, sheen;
        readonly Grid track, workspace;
        readonly RectangleGeometry progressClip, trackClip;
        readonly ProgressModel progress = new ProgressModel();
        readonly Geometry[] stepNumbers = new Geometry[3];
        readonly Border formCard, statusCard;
        readonly System.Windows.Shapes.Path statusGlyph;
        readonly Expander details;
        readonly Grid page;
        readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
        readonly DispatcherTimer timer;
        readonly bool preview;
        readonly string settingsDir = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LinkDownloader");
        readonly bool motion = SystemParameters.ClientAreaAnimation;
        CancellationTokenSource cancellation;
        bool busy, closing, dark, indeterminate, sheenRunning;
        bool? compact;
        DownloadStage lastStage = DownloadStage.Idle;
        string folder, lastFile;
        string statusHeading="Wszystko gotowe",statusMessage="Dodaj link, a resztą zajmiemy się tutaj.";
        bool statusError,statusSuccess,runningUpdate;
        object[] statusArguments=new object[0];
        double progressValue;
        public bool Busy { get { return busy; } }

        public FluentApp(bool renderPreview) {
            preview = renderPreview;
            string savedLanguage="pl";
            if(!preview)try{savedLanguage=File.ReadAllText(IOPath.Combine(settingsDir,"language.txt")).Trim();}catch(IOException){}catch(UnauthorizedAccessException){}
            Localization.SetLanguage(savedLanguage);
            Application.Current.Resources["FastDuration"]=new Duration(TimeSpan.FromMilliseconds(motion?140:0));
            string assembly=Assembly.GetExecutingAssembly().GetName().Name;
            Window=(Window)Application.LoadComponent(new Uri("/"+assembly+";component/MainWindow.xaml",UriKind.Relative));
            // Center the visible digit outlines, without font baseline/line-box offsets.
            for(int i=0;i<3;i++) {
                var text=new FormattedText((i+1).ToString(CultureInfo.InvariantCulture),CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),10,Brushes.Black,1.0);
                stepNumbers[i]=text.BuildGeometry(new Point(0,0));stepNumbers[i].Freeze();
                var mark=Find<System.Windows.Shapes.Path>("Step"+(i+1)+"Mark");mark.Data=stepNumbers[i];mark.Width=9*stepNumbers[i].Bounds.Width/stepNumbers[i].Bounds.Height;
            }
            Localization.Apply(Window);
            url=Find<TextBox>("UrlInput");log=Find<TextBox>("LogBox");folderText=Find<TextBlock>("FolderText");
            title=Find<TextBlock>("StatusTitle");detail=Find<TextBlock>("StatusDetail");percent=Find<TextBlock>("PercentText");downloadLabel=Find<TextBlock>("DownloadLabel");
            download=Find<Button>("DownloadButton");cancel=Find<Button>("CancelButton");paste=Find<Button>("PasteButton");browse=Find<Button>("BrowseButton");open=Find<Button>("OpenButton");update=Find<Button>("UpdateButton");
            audio=Find<RadioButton>("AudioOption");video=Find<RadioButton>("VideoOption");details=Find<Expander>("DetailsExpander");
            quality=Find<ComboBox>("QualitySelect");
            fileFormat=Find<ComboBox>("FileFormatSelect");
            fill=Find<Border>("ProgressFill");track=Find<Grid>("ProgressTrack");marquee=Find<Border>("Marquee");badge=Find<Border>("StatusBadge");urlBorder=Find<Border>("UrlBorder");
            sheen=Find<Border>("ProgressSheen");track.Children.Remove(sheen);fill.Child=sheen;
            progressClip=new RectangleGeometry(new Rect(0,0,0,10),5,5);fill.Clip=progressClip;
            trackClip=new RectangleGeometry(new Rect(0,0,0,10),5,5);track.Clip=trackClip;
            workspace=Find<Grid>("Workspace");formCard=Find<Border>("FormCard");statusCard=Find<Border>("StatusCard");
            statusGlyph=Find<System.Windows.Shapes.Path>("StatusGlyph");page=Find<Grid>("Page");
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.png")) {
                var icon=new BitmapImage();icon.BeginInit();icon.CacheOption=BitmapCacheOption.OnLoad;icon.DecodePixelWidth=128;icon.StreamSource=stream;icon.EndInit();icon.Freeze();
                Find<Image>("BrandIcon").Source=icon;
            }
            // Render the original artwork's lettering; theme color follows its alpha mask.
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BrandLogo.png")) {
                var logo=new BitmapImage();logo.BeginInit();logo.CacheOption=BitmapCacheOption.OnLoad;logo.DecodePixelWidth=512;logo.StreamSource=stream;logo.EndInit();logo.Freeze();
                var lettering=new ImageBrush(logo) { ViewboxUnits=BrushMappingMode.RelativeToBoundingBox, Viewbox=new Rect(167.0/1254,934.0/1254,928.0/1254,204.0/1254), Stretch=Stretch.Fill };
                lettering.Freeze();Find<Rectangle>("BrandWordmark").OpacityMask=lettering;
            }
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon.ico")) Window.Icon=BitmapFrame.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
            Window.TaskbarItemInfo=new TaskbarItemInfo();
            folder=IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),"Pobrane z linku");
            if(!preview) {
                try {var saved=IOPath.Combine(settingsDir,"folder.txt");if(File.Exists(saved) && IOPath.IsPathRooted(File.ReadAllText(saved).Trim())) folder=File.ReadAllText(saved).Trim();} catch(IOException) {} catch(UnauthorizedAccessException) {}
                try {dark=File.Exists(IOPath.Combine(settingsDir,"theme.txt")) && File.ReadAllText(IOPath.Combine(settingsDir,"theme.txt"))=="dark";} catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            UpdateFolder();ApplyTheme();
            if(!preview) {videoQuality=ReadQuality("video-quality.txt",new[]{0,2160,1440,1080,720,480});audioQuality=ReadQuality("audio-quality.txt",new[]{0,320,256,192,128});}
            if(!preview) {videoFormat=ReadFormat("video-format.txt",new[]{"mp4","mkv","webm"});audioFormat=ReadFormat("audio-format.txt",new[]{"mp3","m4a","opus","wav","flac"});}
            UpdateFileFormats();
            fileFormat.SelectionChanged+=delegate {
                if(updatingFormat || fileFormat.SelectedItem==null)return;
                string chosen=(string)((ComboBoxItem)fileFormat.SelectedItem).Tag;
                if(audio.IsChecked==true) {audioFormat=chosen;Save("audio-format.txt",chosen);}else {videoFormat=chosen;Save("video-format.txt",chosen);}
                UpdateQuality();UpdateFormatLabels();
            };
            quality.SelectionChanged+=delegate {
                if(updatingQuality || quality.SelectedItem==null)return;
                int chosen=(int)((ComboBoxItem)quality.SelectedItem).Tag;
                if(audio.IsChecked==true) {audioQuality=chosen;Save("audio-quality.txt",chosen.ToString(CultureInfo.InvariantCulture));}
                else {videoQuality=chosen;Save("video-quality.txt",chosen.ToString(CultureInfo.InvariantCulture));}
            };
            paste.Click+=delegate {try {if(Clipboard.ContainsText()) {url.Text=Clipboard.GetText().Trim();url.Focus();url.CaretIndex=url.Text.Length;}} catch(ExternalException) {SetStatus("Schowek jest chwilowo zajęty","Spróbuj ponownie albo wklej link klawiszami Ctrl+V.",true,false);} };
            url.TextChanged+=delegate {Find<TextBlock>("UrlHint").Visibility=String.IsNullOrEmpty(url.Text)?Visibility.Visible:Visibility.Collapsed;};
            url.GotKeyboardFocus+=delegate {urlBorder.SetResourceReference(Border.BorderBrushProperty,"Accent");};
            url.LostKeyboardFocus+=delegate {urlBorder.SetResourceReference(Border.BorderBrushProperty,"Line");};
            download.Click+=async delegate {await StartDownload();};cancel.Click+=async delegate {await Cancel();};
            update.Click+=async delegate {
                if(busy)return;
                var dialog=new UpdatesDialog(Window,tag=>{Save("skipped-release.txt",tag);update.BorderThickness=new Thickness(0);update.ToolTip=Localization.T("Aktualizacje");});
                dialog.Window.Loaded+=async delegate {await dialog.Check();};
                dialog.Window.ShowDialog();
                if(dialog.UpdateEngine && !busy)await Run(new[]{"--ignore-config","-U"},true);
            };
            browse.Click+=delegate {using(var dialog=new System.Windows.Forms.FolderBrowserDialog()) {dialog.Description=Localization.T("Wybierz folder zapisu");dialog.SelectedPath=folder;if(dialog.ShowDialog(new DialogOwner(new WindowInteropHelper(Window).Handle))==System.Windows.Forms.DialogResult.OK) {folder=dialog.SelectedPath;UpdateFolder();Save("folder.txt",folder);}}};
            open.Click+=delegate {try {Directory.CreateDirectory(folder);Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});} catch(Exception e) {SetStatus("Nie można otworzyć folderu",e.Message,true,false);} };
            Find<Button>("LanguageButton").Click+=delegate {ShowLanguages();};
            Find<Button>("ThemeButton").Click+=delegate {dark=!dark;ApplyTheme(true);Save("theme.txt",dark?"dark":"light");};
            Find<Button>("SupportedSitesButton").Click+=delegate {new SupportedSitesDialog(Window).Window.ShowDialog();};
            Find<Button>("AboutButton").Click+=delegate {new AboutDialog(Window).Window.ShowDialog();};
            audio.Checked+=delegate {UpdateFileFormats();};
            video.Checked+=delegate {UpdateFileFormats();};
            track.SizeChanged+=delegate {trackClip.Rect=new Rect(0,0,Math.Max(0,track.ActualWidth),10);SetProgress(progressValue,false);if(indeterminate)StartMarquee();};
            Window.SizeChanged+=delegate {ResponsiveLayout();};
            Window.SourceInitialized+=delegate {ApplyNativeFrame();};
            Window.Loaded+=delegate {
                ResponsiveLayout();
                if(!preview && motion) {
                    int delay=0;foreach(var card in new[]{formCard,statusCard}) {
                        var duration=TimeSpan.FromMilliseconds(320);
                        ((TranslateTransform)card.RenderTransform).BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(10,0,duration){BeginTime=TimeSpan.FromMilliseconds(delay),EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
                        card.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,duration){BeginTime=TimeSpan.FromMilliseconds(delay),EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut}});delay+=45;
                    }
                }
                url.Focus();
            };
            Window.Closing+=OnClosing;
            if(!preview)Window.Loaded+=async delegate {
                try {
                    var release=await AppUpdates.Check();
                    string skipped="";try {skipped=File.ReadAllText(IOPath.Combine(settingsDir,"skipped-release.txt"));}catch(IOException){}catch(UnauthorizedAccessException){}
                    if(Window.IsVisible && release!=null && release.Version>AppUpdates.Current && skipped!=release.Tag) {
                        // A quiet visual hint; never interrupt a download or force an update.
                        update.SetResourceReference(Control.BorderBrushProperty,"Accent");update.BorderThickness=new Thickness(1);
                        update.ToolTip=Localization.T("Dostępna wersja {0}",release.Tag);
                    }
                }catch(Exception) {/* Offline startup is silent. Manual checks explain errors. */}
            };
            timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};timer.Tick+=delegate {Drain();};
            Window.Closed+=delegate {timer.Stop();};
            if(!preview) {Window.MaxHeight=SystemParameters.WorkArea.Height;Window.Height=Math.Min(Window.Height,Window.MaxHeight);Window.Width=Math.Min(Window.Width,SystemParameters.WorkArea.Width);}
        }
        public T Find<T>(string name) where T:class {return Window.FindName(name) as T;}
        void ChangeLanguage(string code) {
            string heading=statusHeading,message=statusMessage;bool error=statusError,success=statusSuccess;object[] arguments=statusArguments;
            Localization.SetLanguage(code);Localization.SetDirection(Window);
            UpdateFileFormats();ApplyTheme();RenderModel();
            SetStatus(heading,message,error,success,arguments);
            if(busy)downloadLabel.Text=Localization.T(runningUpdate?"Aktualizowanie…":"Trwa pobieranie…");
            Save("language.txt",Localization.Code);
        }
        void ShowLanguages() {
            var menu=new ContextMenu {PlacementTarget=Find<Button>("LanguageButton"),Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom,MaxHeight=520,MinWidth=230,FlowDirection=FlowDirection.LeftToRight};
            menu.Style=(Style)Window.FindResource("LanguageMenu");menu.Resources.MergedDictionaries.Add(Window.Resources);
            menu.SetResourceReference(Control.BackgroundProperty,"Surface");menu.SetResourceReference(Control.ForegroundProperty,"Text");
            for(int i=0;i<Localization.Codes.Length;i++) {
                string code=Localization.Codes[i];
                var item=new MenuItem {Header=Localization.Names[i],Tag=code,IsCheckable=true,IsChecked=Localization.Code==code,Padding=new Thickness(12,5,12,5),Language=XmlLanguage.GetLanguage(code)};
                item.Style=(Style)Window.FindResource("LanguageMenuItem");item.Click+=delegate {ChangeLanguage(code);};menu.Items.Add(item);
            }
            Find<Button>("LanguageButton").ContextMenu=menu;menu.IsOpen=true;
        }
        bool ConfirmClose() {
            var dialog=new Window {Title=Localization.T("Zamknąć Vidnelo?"),Owner=Window,Icon=Window.Icon,Width=450,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
            dialog.Resources.MergedDictionaries.Add(Window.Resources);
            dialog.SetResourceReference(Control.BackgroundProperty,"Canvas");dialog.SetResourceReference(Control.ForegroundProperty,"Text");
            var panel=new StackPanel {Margin=new Thickness(24)};
            panel.Children.Add(new TextBlock {Text=Localization.T("Pobieranie jeszcze trwa. Anulować je i zamknąć program?"),TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,0,0,24)});
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            var yes=new Button {Content=Localization.T("Tak"),MinWidth=95,Margin=new Thickness(0,0,10,0)};
            var no=new Button {Content=Localization.T("Nie"),MinWidth=95,IsDefault=true,IsCancel=true};
            yes.Click+=delegate {dialog.DialogResult=true;};no.Click+=delegate {dialog.DialogResult=false;};
            buttons.Children.Add(yes);buttons.Children.Add(no);panel.Children.Add(buttons);dialog.Content=panel;Localization.SetDirection(dialog);
            return dialog.ShowDialog()==true;
        }
        string ReadFormat(string file,string[] allowed) {
            try {string value=File.ReadAllText(IOPath.Combine(settingsDir,file)).Trim();if(Array.IndexOf(allowed,value)>=0)return value;}catch(IOException){}catch(UnauthorizedAccessException){}
            return allowed[0];
        }
        void UpdateFileFormats() {
            updatingFormat=true;fileFormat.Items.Clear();bool isAudio=audio.IsChecked==true;
            foreach(string format in isAudio?new[]{"mp3","m4a","opus","wav","flac"}:new[]{"mp4","mkv","webm"}) {
                var option=new ComboBoxItem {Tag=format,Content=format=="webm"?"WebM":format=="opus"?"Opus":format.ToUpperInvariant()};
                fileFormat.Items.Add(option);if(format==(isAudio?audioFormat:videoFormat))fileFormat.SelectedItem=option;
            }
            updatingFormat=false;UpdateQuality();UpdateFormatLabels();
        }
        void UpdateFormatLabels() {
            Find<TextBlock>("VideoFormatHint").Text=Localization.T("{0} · obraz i dźwięk",videoFormat.ToUpperInvariant());
            Find<TextBlock>("AudioFormatHint").Text=Localization.T("{0} · tylko audio",audioFormat.ToUpperInvariant());
            if(!busy)downloadLabel.Text=Localization.T("Pobierz {0}",(audio.IsChecked==true?audioFormat:videoFormat).ToUpperInvariant());
            fileFormat.ToolTip=audio.IsChecked==true?Localization.T("Format zapisanego dźwięku. WAV i FLAC nie dodają strat kompresji, ale nie poprawiają jakości źródła."):videoFormat=="webm"?Localization.T("WebM może wymagać konwersji, jeśli serwis nie udostępnia zgodnego pliku. Konwersja może potrwać dłużej."):Localization.T("Format zapisanego filmu.");
        }
        int ReadQuality(string file,int[] allowed) {
            try {int value;if(Int32.TryParse(File.ReadAllText(IOPath.Combine(settingsDir,file)),out value) && Array.IndexOf(allowed,value)>=0)return value;}catch(IOException){}catch(UnauthorizedAccessException){}
            return 0;
        }
        void UpdateQuality() {
            updatingQuality=true;quality.Items.Clear();bool isAudio=audio.IsChecked==true;
            if(isAudio && audioFormat!="mp3") {
                quality.Items.Add(new ComboBoxItem {Tag=0,Content=audioFormat=="wav" || audioFormat=="flac"?Localization.T("Jakość źródła"):Localization.T("Automatyczna")});quality.SelectedIndex=0;quality.IsEnabled=false;
                quality.ToolTip=audioFormat=="wav" || audioFormat=="flac"?Localization.T("Zapis bez dodatkowej stratnej kompresji. Jakość zależy od źródła."):Localization.T("Ten format korzysta z automatycznej jakości dźwięku.");
                updatingQuality=false;return;
            }
            quality.IsEnabled=!busy;
            int[] values=isAudio?new[]{0,320,256,192,128}:new[]{0,2160,1440,1080,720,480};
            foreach(int value in values) {
                var option=new ComboBoxItem {Tag=value,Content=value==0?Localization.T("Najlepsza dostępna"):isAudio?Localization.T("{0} kb/s",value):value==2160?"4K · "+Localization.T("Do {0}p",2160):Localization.T("Do {0}p",value)};
                quality.Items.Add(option);if(value==(isAudio?audioQuality:videoQuality))quality.SelectedItem=option;
            }
            quality.ToolTip=isAudio?Localization.T("Jakość zapisu MP3. Wyższy bitrate nie poprawi jakości źródła."):Localization.T("Wybiera najlepszy wariant do wskazanej wysokości. Przy braku danych o rozdzielczości serwis może zwrócić oryginalny plik.");
            updatingQuality=false;
        }
        void UpdateFolder() {folderText.Text=folder;folderText.ToolTip=folder;}
        void Save(string file,string value) {if(preview) return;try {Directory.CreateDirectory(settingsDir);File.WriteAllText(IOPath.Combine(settingsDir,file),value);} catch(IOException) {} catch(UnauthorizedAccessException) {}}
        void ApplyTheme(bool animate = false) {
            string[] names={"Canvas","Surface","Input","Text","Secondary","Line","Accent","AccentSoft","Green","GreenSoft","Red","RedSoft","Quiet","Track"};
            string[] light={"#F4F6FB","#FFFFFF","#F5F7FB","#19283F","#617087","#E5EAF2","#146CE5","#EEF5FF","#16805B","#E6F6EE","#BA3F4A","#FDECEF","#65748A","#DFE9F5"};
            string[] night={"#101621","#192330","#141E2B","#F1F5FC","#A9B7CB","#2A384A","#81BFFF","#22394F","#81D7AF","#203C33","#FFACA8","#482B34","#8F9DB2","#2B4058"};
            for(int i=0;i<names.Length;i++) {
                var previous=Window.Resources[names[i]] as SolidColorBrush;
                var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString((dark?night:light)[i]));
                if(animate && motion && previous!=null) AnimateThemeColor(brush,SolidColorBrush.ColorProperty,previous.Color,brush.Color);
                Window.Resources[names[i]]=brush;
            }
            ThemeGradient("StatusSurface",dark?"#1C334E":"#1A3554",dark?"#111D2C":"#122237",animate);
            ThemeGradient("AccentGradient","#438DFF","#56D5EE",animate);
            ThemeGradient("ButtonGradient",dark?"#2473E9":"#176DE8",dark?"#168AC3":"#088DC6",animate);
            download.Foreground=Brushes.White;
            Find<System.Windows.Shapes.Path>("ThemeGlyph").Data=(Geometry)Window.Resources[dark?"SunGeometry":"MoonGeometry"];
            Find<Button>("ThemeButton").ToolTip=dark?Localization.T("Przełącz na jasny motyw"):Localization.T("Przełącz na ciemny motyw");
            ApplyNativeFrame();
        }
        void AnimateThemeColor(Animatable target,DependencyProperty property,Color from,Color to) {
            target.BeginAnimation(property,new ColorAnimation(from,to,TimeSpan.FromMilliseconds(320)) {
                EasingFunction=new CubicEase { EasingMode=EasingMode.EaseInOut },FillBehavior=FillBehavior.Stop
            },HandoffBehavior.SnapshotAndReplace);
        }
        void ThemeGradient(string key,string first,string second,bool animate) {
            var previous=Window.Resources[key] as LinearGradientBrush;
            var brush=Gradient(first,second);if(key=="StatusSurface") {brush.StartPoint=new Point(0,0);brush.EndPoint=new Point(1,1);}
            if(animate && motion && previous!=null) for(int i=0;i<brush.GradientStops.Count;i++)
                AnimateThemeColor(brush.GradientStops[i],GradientStop.ColorProperty,previous.GradientStops[i].Color,brush.GradientStops[i].Color);
            Window.Resources[key]=brush;
        }
        static LinearGradientBrush Gradient(string first,string second) {return new LinearGradientBrush((Color)ColorConverter.ConvertFromString(first),(Color)ColorConverter.ConvertFromString(second),0);}
        void ResponsiveLayout() {
            bool stack=Window.ActualWidth<1000;if(compact==stack)return;compact=stack;
            workspace.ColumnDefinitions[0].Width=new GridLength(stack?1:1.45,GridUnitType.Star);
            workspace.ColumnDefinitions[1].Width=new GridLength(stack?0:24);
            workspace.ColumnDefinitions[2].Width=stack?new GridLength(0):new GridLength(1,GridUnitType.Star);
            Grid.SetColumn(statusCard,stack?0:2);Grid.SetRow(statusCard,stack?1:0);
            statusCard.Margin=stack?new Thickness(0,24,0,0):new Thickness(0);
            statusCard.MinHeight=stack?470:0;
        }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
        void ApplyNativeFrame() {
            IntPtr handle=new WindowInteropHelper(Window).Handle;if(handle==IntPtr.Zero)return;
            try {int rounded=2, isDark=dark?1:0;DwmSetWindowAttribute(handle,33,ref rounded,4);DwmSetWindowAttribute(handle,20,ref isDark,4);
                Color c=(Color)ColorConverter.ConvertFromString(dark?"#101621":"#F4F6FB");int color=c.R|(c.G<<8)|(c.B<<16);DwmSetWindowAttribute(handle,35,ref color,4);
            } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
        }
        void SetBusy(bool value) {
            quality.IsEnabled=!value && (audio.IsChecked!=true || audioFormat=="mp3");fileFormat.IsEnabled=!value;
            busy=value;download.IsEnabled=!value;paste.IsEnabled=!value;browse.IsEnabled=!value;update.IsEnabled=!value;audio.IsEnabled=!value;video.IsEnabled=!value;url.IsReadOnly=value;cancel.IsEnabled=value;
            downloadLabel.Text=value?Localization.T("Trwa pobieranie…"):Localization.T("Pobierz {0}",(audio.IsChecked==true?audioFormat:videoFormat).ToUpperInvariant());
            if(value)timer.Start();else {timer.Stop();AnimateSheen(false);}
            if(!value) Window.TaskbarItemInfo.ProgressState=TaskbarItemProgressState.None;
        }
        void SetStatus(string heading,string message,bool error,bool success,params object[] arguments) {
            statusHeading=heading;statusMessage=message;statusError=error;statusSuccess=success;statusArguments=arguments;
            title.Text=Localization.T(heading);detail.Text=Localization.T(message,arguments);
            if(!String.IsNullOrEmpty(progress.Filename) && message==IOPath.GetFileName(progress.Filename))detail.FlowDirection=FlowDirection.LeftToRight;
            else detail.ClearValue(FrameworkElement.FlowDirectionProperty);
            badge.SetResourceReference(Border.BackgroundProperty,error?"RedSoft":success?"GreenSoft":"StatusSurface");badge.CornerRadius=new CornerRadius(14);
            badge.Visibility=(error||success)?Visibility.Visible:Visibility.Collapsed;
            statusGlyph.Visibility=(error||success)?Visibility.Visible:Visibility.Collapsed;
            statusGlyph.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,error?"Red":success?"Green":"Accent");
            statusGlyph.Data=error?Geometry.Parse("M6,6 L18,18 M18,6 L6,18"):(Geometry)Window.Resources[success?"CheckGeometry":"DownloadGeometry"];
            if(success && motion && lastStage!=DownloadStage.Completed) {
                var scale=new ScaleTransform(0.78,0.78);badge.RenderTransform=scale;badge.RenderTransformOrigin=new Point(.5,.5);
                var pop=new DoubleAnimation(.78,1,TimeSpan.FromMilliseconds(320)){EasingFunction=new BackEase{Amplitude=.2,EasingMode=EasingMode.EaseOut}};
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,pop);scale.BeginAnimation(ScaleTransform.ScaleYProperty,pop);
            }
        }
        void Indeterminate(bool enabled) {
            if(indeterminate==enabled)return;indeterminate=enabled;
            marquee.Visibility=enabled?Visibility.Visible:Visibility.Collapsed;
            var transform=(TranslateTransform)marquee.RenderTransform;transform.BeginAnimation(TranslateTransform.XProperty,null);
            if(enabled) {
                progressClip.BeginAnimation(RectangleGeometry.RectProperty,null);progressClip.Rect=new Rect(0,0,0,10);AnimateSheen(false);StartMarquee();
                Window.TaskbarItemInfo.ProgressState=TaskbarItemProgressState.Indeterminate;
            }
        }
        void StartMarquee() {
            var transform=(TranslateTransform)marquee.RenderTransform;
            if(motion)transform.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(-90,Math.Max(100,track.ActualWidth),TimeSpan.FromMilliseconds(1200)){RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}});
            else transform.X=0;
        }
        void AnimateSheen(bool enabled) {
            enabled=enabled && motion;if(sheenRunning==enabled)return;sheenRunning=enabled;
            sheen.Visibility=enabled?Visibility.Visible:Visibility.Collapsed;
            var transform=(TranslateTransform)sheen.RenderTransform;transform.BeginAnimation(TranslateTransform.XProperty,null);
            if(enabled)transform.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(-64,Math.Max(200,track.ActualWidth),TimeSpan.FromMilliseconds(1800)){RepeatBehavior=RepeatBehavior.Forever});
        }
        void SetProgress(double value,bool animate) {
            progressValue=Math.Max(0,Math.Min(100,value));double width=Math.Max(0,track.ActualWidth*progressValue/100);
            Rect previous=progressClip.Rect,target=new Rect(0,0,width,10);
            progressClip.BeginAnimation(RectangleGeometry.RectProperty,null);progressClip.Rect=target;
            if(animate && motion && Math.Abs(previous.Width-width)>0.5)progressClip.BeginAnimation(RectangleGeometry.RectProperty,new RectAnimation(previous,target,TimeSpan.FromMilliseconds(240)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});
            Window.TaskbarItemInfo.ProgressValue=progressValue/100;
            if(busy && marquee.Visibility!=Visibility.Visible) Window.TaskbarItemInfo.ProgressState=TaskbarItemProgressState.Normal;
        }
        async Task StartDownload() {
            if(busy)return;
            string link=url.Text.Trim();
            if(!DownloadEngine.IsWebUrl(link)) {progress.Fail();RenderModel();SetStatus("Ten link wygląda na niepełny","Wklej adres zaczynający się od https:// lub http://.",true,false);url.Focus();return;}
            foreach(string tool in new[]{"yt-dlp.exe","ffmpeg.exe","ffprobe.exe","deno.exe"}) if(!File.Exists(IOPath.Combine(DownloadEngine.ToolsPath,tool))) {SetStatus("Brakuje narzędzia", "Przywróć plik tools\\{0} w folderze programu.",true,false,tool);return;}
            try {Directory.CreateDirectory(folder);Save("folder.txt",folder);await Run(DownloadEngine.Arguments(link,folder,audio.IsChecked==true,audio.IsChecked==true?(audioFormat=="mp3"?audioQuality:0):videoQuality,audio.IsChecked==true?audioFormat:videoFormat),false);} catch(Exception e) {SetStatus("Nie można rozpocząć pobierania",e.Message,true,false);}
        }
        async Task Run(string[] args,bool updating) {
            if(busy)return;
            runningUpdate=updating;log.Clear();lastFile=null;percent.Text="";SetProgress(0,false);progress.Start(audio.IsChecked==true,updating);SetBusy(true);RenderModel();
            downloadLabel.Text=updating?Localization.T("Aktualizowanie…"):Localization.T("Trwa pobieranie…");
            SetStatus(updating?"Sprawdzam aktualizację":"Przygotowuję pobieranie",updating?"Sprawdzam najnowszą wersję yt-dlp.":"Łączę się z serwisem i wyszukuję dostępny plik.",false,false);
            cancellation=new CancellationTokenSource();
            try {
                int code=await new DownloadEngine().RunAsync(args,delegate(string line){output.Enqueue(line);},cancellation.Token);
                Drain(true);
                if(code==0) {progress.Complete(lastFile);RenderModel();if(updating)SetStatus("Wszystko aktualne","Możesz wrócić do pobierania.",false,true);}
                else {progress.Fail();RenderModel();details.IsExpanded=true;}
            } catch(OperationCanceledException) {Drain(true);progress.Cancel();RenderModel();}
              catch(Exception e) {Drain(true);progress.Fail();RenderModel();log.AppendText(e.Message+Environment.NewLine);details.IsExpanded=true;SetStatus("Wystąpił błąd",e.Message,true,false);}
            finally {SetBusy(false);cancellation.Dispose();cancellation=null;if(closing) Window.Close();}
        }
        async Task Cancel() {
            if(cancellation==null || cancellation.IsCancellationRequested)return;
            cancel.IsEnabled=false;SetStatus("Anuluję pobieranie","Jeszcze chwila…",false,false);var current=cancellation;
            await Task.Run(delegate {try {current.Cancel();}catch(ObjectDisposedException){}});
        }
        void OnClosing(object sender,CancelEventArgs e) {
            if(!busy)return;e.Cancel=true;if(closing)return;
            if(ConfirmClose()) {closing=true;var ignored=Cancel();}
        }
        void Drain(bool all=false) {
            string line;var batch=new StringBuilder();bool changed=false;int count=0;
            while((all || count++<500) && output.TryDequeue(out line)) {batch.AppendLine(line);changed=progress.Consume(line)||changed;if(line.StartsWith("ZAPISANO: "))lastFile=line.Substring(10).Trim();}
            if(batch.Length>0) {if(log.Text.Length>120000)log.Text=log.Text.Substring(40000);log.AppendText(batch.ToString());if(details.IsExpanded)log.ScrollToEnd();}
            if(changed)RenderModel();
        }
        void ConsumeLine(string line) {
            if(log.Text.Length>120000)log.Text=log.Text.Substring(40000);
            log.AppendText(line+Environment.NewLine);if(details.IsExpanded)log.ScrollToEnd();
            if(line.StartsWith("ZAPISANO: "))lastFile=line.Substring(10).Trim();
            if(progress.Consume(line))RenderModel();
        }
        static string Metric(string text) {return String.IsNullOrEmpty(text)?"—":text.Replace(" pobrano"," "+Localization.T("pobrano"));}
        void RenderModel() {
            var stage=progress.Stage;bool done=stage==DownloadStage.Completed,failed=stage==DownloadStage.Failed;
            bool waiting=stage==DownloadStage.Connecting || stage==DownloadStage.Processing || (stage==DownloadStage.Downloading && !progress.Percent.HasValue);
            Indeterminate(waiting);
            if(!waiting)SetProgress(progress.Percent??0,true);
            AnimateSheen(stage==DownloadStage.Downloading && progress.Percent>0 && progress.Percent<100);
            percent.Text=progress.Percent.HasValue?progress.Percent.Value.ToString("0",CultureInfo.InvariantCulture)+"%":"—";
            string heading=progress.StageLabel, message="", caption="";
            switch(stage) {
                case DownloadStage.Connecting: message="Wyszukuję dostępny materiał.";caption=Localization.T("Przygotowanie");break;
                case DownloadStage.Downloading: message=String.IsNullOrEmpty(progress.Filename)?"Zapisuję materiał na komputerze.":IOPath.GetFileName(progress.Filename);caption=Localization.T("Bieżący plik")+(progress.TransferIndex>1?" · "+progress.TransferIndex:"");break;
                case DownloadStage.Processing: heading="Ostatni krok";message=progress.StageLabel;caption=Localization.T("Przygotowanie pliku");break;
                case DownloadStage.Completed: heading="Gotowe. Plik zapisany.";message=String.IsNullOrEmpty(progress.Filename)?"Znajdziesz go w wybranym folderze.":IOPath.GetFileName(progress.Filename);caption=Localization.T("Zakończono");break;
                case DownloadStage.Cancelled: message="Możesz wznowić pobieranie tym samym linkiem.";caption=Localization.T("Anulowano");break;
                case DownloadStage.Failed: message="Dokładny komunikat znajdziesz w szczegółach poniżej.";caption=Localization.T("Spróbuj ponownie");break;
                default: heading="Wszystko gotowe";message="Dodaj link, a resztą zajmiemy się tutaj.";caption=Localization.T("Czekam na link");break;
            }
            SetStatus(heading,message,failed,done);
            Find<TextBlock>("ProgressCaption").Text=caption;
            Find<TextBlock>("SpeedText").Text=Metric(progress.Speed);Find<TextBlock>("EtaText").Text=Metric(progress.Eta);Find<TextBlock>("SizeText").Text=Metric(progress.Size);
            Find<TextBlock>("StateText").Text=done?Localization.T("Zapisano"):failed?Localization.T("Błąd"):stage==DownloadStage.Cancelled?Localization.T("Przerwano"):busy?Localization.T("W toku"):Localization.T("Gotowy");
            Find<Border>("StatePill").SetResourceReference(Border.BackgroundProperty,done?"GreenSoft":failed?"RedSoft":"AccentSoft");
            Find<TextBlock>("StateText").SetResourceReference(TextBlock.ForegroundProperty,done?"Green":failed?"Red":"Accent");
            int active=stage==DownloadStage.Connecting?1:stage==DownloadStage.Downloading?2:stage==DownloadStage.Processing?3:done?4:0;
            for(int i=1;i<=3;i++) {
                bool complete=active>i, current=active==i;
                Find<Border>("Step"+i+"Badge").SetResourceReference(Border.BackgroundProperty,complete?"GreenSoft":current?"AccentSoft":"Track");
                var mark=Find<System.Windows.Shapes.Path>("Step"+i+"Mark");
                mark.Data=complete?(Geometry)Window.Resources["CheckGeometry"]:stepNumbers[i-1];
                mark.Width=complete?11:9*stepNumbers[i-1].Bounds.Width/stepNumbers[i-1].Bounds.Height;mark.Height=complete?8:9;
                if(complete) {mark.Fill=null;mark.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"Green");}
                else {mark.Stroke=null;mark.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,current?"Accent":"Secondary");}
                Find<TextBlock>("Step"+i+"Text").SetResourceReference(TextBlock.ForegroundProperty,current?"Text":"Secondary");
                Find<TextBlock>("Step"+i+"State").Text=current?Localization.T("Teraz"):complete?Localization.T("Gotowe"):"";
            }
            if(stage!=lastStage && motion) {
                title.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.55,1,TimeSpan.FromMilliseconds(180)));
                detail.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.55,1,TimeSpan.FromMilliseconds(220)));
            }
            lastStage=stage;
        }
        static void CaptureWindow(Window window,string path) {
            window.UpdateLayout();var visual=(FrameworkElement)window.Content;
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
            // Capturing a child visual omits the RTL mirror contributed by its Window.
            if(visual.FlowDirection==FlowDirection.RightToLeft) {
                var drawing=new DrawingVisual();using(var context=drawing.RenderOpen()) {context.PushTransform(new MatrixTransform(-1,0,0,1,bitmap.PixelWidth,0));context.DrawImage(bitmap,new Rect(0,0,bitmap.PixelWidth,bitmap.PixelHeight));}
                var corrected=new RenderTargetBitmap(bitmap.PixelWidth,bitmap.PixelHeight,96,96,PixelFormats.Pbgra32);corrected.Render(drawing);bitmap=corrected;
            }
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
        }
        public async Task LocalizationChecks(string directory) {
            Directory.CreateDirectory(directory);var report=new StringBuilder();
            url.Text="https://example.com/video?name=日本語";quality.SelectedIndex=4;
            string originalFolder=folder;string originalUrl=url.Text;
            foreach(string code in Localization.Codes) {
                ChangeLanguage(code);Window.UpdateLayout();await Task.Delay(350);
                if(url.Text!=originalUrl || folder!=originalFolder || videoQuality!=720 || videoFormat!="mp4")throw new Exception("Language lost input: "+code);
                if(downloadLabel.Text!=Localization.T("Pobierz {0}","MP4"))throw new Exception("Download caption: "+code);
                if(Window.FlowDirection!=(code=="ar"?FlowDirection.RightToLeft:FlowDirection.LeftToRight))throw new Exception("Direction: "+code);
                if(url.FlowDirection!=FlowDirection.LeftToRight)throw new Exception("URL direction: "+code);
                foreach(string name in new[]{"PasteButton","BrowseButton","OpenButton","DownloadButton","CancelButton","UpdateButton","ThemeButton","LanguageButton","AboutButton"}) {
                    var button=Find<Button>(name);var bounds=button.TransformToAncestor(page).TransformBounds(new Rect(0,0,button.ActualWidth,button.ActualHeight));
                    if(button.ActualHeight<38 || bounds.Left<-.5 || bounds.Right>page.ActualWidth+.5)throw new Exception("Button clipped: "+code+" "+name);
                }
                dark=false;ApplyTheme();CaptureWindow(Window,IOPath.Combine(directory,code+"-light.png"));
                dark=true;ApplyTheme();CaptureWindow(Window,IOPath.Combine(directory,code+"-dark.png"));
                progress.Start(false);SetBusy(true);ConsumeLine("[download] Destination: Original 日本語 - vidnelo.mp4");ConsumeLine("[download] 42.0% of 87.15MiB at 8.64MiB/s ETA 00:06");
                ChangeLanguage(code);if(!busy || progress.Percent!=42 || progress.Filename!="Original 日本語 - vidnelo.mp4")throw new Exception("Download state: "+code);
                progress.Fail();RenderModel();SetBusy(false);if(title.Text!=Localization.T("Nie udało się pobrać"))throw new Exception("Error localization: "+code);
                progress.Complete("Original 日本語 - vidnelo.mp4");RenderModel();
                var about=new AboutDialog(Window);about.Window.ShowInTaskbar=false;about.Window.Opacity=0;about.Window.Show();await Task.Delay(15);CaptureWindow(about.Window,IOPath.Combine(directory,code+"-about.png"));about.Window.Close();
                var sites=new SupportedSitesDialog(Window);sites.Window.ShowInTaskbar=false;sites.Window.Opacity=0;sites.Window.Show();if(sites.Loading!=null)await sites.Loading;
                ((TextBox)sites.Window.FindName("SearchInput")).Text="YouTube";
                if(((ListBox)sites.Window.FindName("SiteList")).Items.Count==0)throw new Exception("Site search: "+code);
                CaptureWindow(sites.Window,IOPath.Combine(directory,code+"-sites.png"));sites.Window.Close();
                report.AppendLine("PASS "+code+": main/light/dark, dialogs, search, RTL, input/quality preserved, active download and error");
            }
            ChangeLanguage("pl");ShowLanguages();var menu=Find<Button>("LanguageButton").ContextMenu;
            if(menu.Items.Count!=15)throw new Exception("Language count");
            await Task.Delay(60);menu.UpdateLayout();
            var menuBitmap=new RenderTargetBitmap((int)Math.Ceiling(menu.ActualWidth),(int)Math.Ceiling(menu.ActualHeight),96,96,PixelFormats.Pbgra32);menuBitmap.Render(menu);
            var menuEncoder=new PngBitmapEncoder();menuEncoder.Frames.Add(BitmapFrame.Create(menuBitmap));using(var file=File.Create(IOPath.Combine(directory,"language-menu.png")))menuEncoder.Save(file);
            ((MenuItem)menu.Items[7]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));menu.IsOpen=false;
            if(Localization.Code!="uk")throw new Exception("Language menu action");
            SetStatus("Ten link wygląda na niepełny","Wklej adres zaczynający się od https:// lub http://.",true,false);ChangeLanguage("en");
            if(title.Text!="This link looks incomplete")throw new Exception("Switching error status");
            SetStatus("Brakuje narzędzia","Przywróć plik tools\\{0} w folderze programu.",true,false,"ffmpeg.exe");ChangeLanguage("de");
            if(detail.Text!=Localization.T("Przywróć plik tools\\{0} w folderze programu.","ffmpeg.exe"))throw new Exception("Switching formatted status");
            Window.Width=730;Window.Height=660;
            foreach(string code in new[]{"de","ar"}) {ChangeLanguage(code);await Task.Delay(280);CaptureWindow(Window,IOPath.Combine(directory,code+"-compact.png"));}
            report.AppendLine("PASS menu selects Ukrainian; existing errors and formatted messages switch language; compact German/Arabic rendered");
            ChangeLanguage("invalid");if(Localization.Code!="pl")throw new Exception("Invalid saved language fallback");
            File.WriteAllText(IOPath.Combine(directory,"checks.txt"),report.ToString());
        }
        public async Task RenderChecks(string directory) {
            Directory.CreateDirectory(directory);var report=new StringBuilder();
            Window.UpdateLayout();
            Action<string> capture=delegate(string name) {
                Window.UpdateLayout();var visual=(FrameworkElement)Window.Content;
                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(IOPath.Combine(directory,name+".png")))encoder.Save(file);
            };
            Action check=delegate {foreach(string name in new[]{"PasteButton","BrowseButton","OpenButton","DownloadButton","CancelButton","UpdateButton","ThemeButton","LanguageButton","AboutButton"}) {
                var button=Find<Button>(name);var point=button.TranslatePoint(new Point(0,0),page);
                if(button.ActualHeight<38 || button.ActualWidth<38 || point.X<0 || point.X+button.ActualWidth>page.ActualWidth+1)throw new Exception("Button layout: "+name);
                report.AppendLine("PASS "+name+" "+button.ActualWidth+"x"+button.ActualHeight);
            }};
            check();await Task.Delay(250);capture("fluent-light");if(Find<ScrollViewer>("RootScroll").ScrollableHeight>0.5)throw new Exception("Default window unexpectedly scrolls: "+Find<ScrollViewer>("RootScroll").ScrollableHeight);report.AppendLine("PASS default window fits without scrolling");
            if(quality.Items.Count!=6 || videoQuality!=0)throw new Exception("Video quality defaults incorrect");
            quality.SelectedIndex=4;audio.IsChecked=true;quality.SelectedIndex=3;video.IsChecked=true;
            if(videoQuality!=720 || (int)((ComboBoxItem)quality.SelectedItem).Tag!=720)throw new Exception("Video quality selection was lost");
            audio.IsChecked=true;if(audioQuality!=192 || (int)((ComboBoxItem)quality.SelectedItem).Tag!=192)throw new Exception("Audio quality selection was lost");
            await Task.Delay(200);capture("quality-audio");video.IsChecked=true;quality.IsDropDownOpen=true;await Task.Delay(100);
            var qualityPopup=(System.Windows.Controls.Primitives.Popup)quality.Template.FindName("PART_Popup",quality);
            var qualityVisual=(FrameworkElement)qualityPopup.Child;qualityVisual.UpdateLayout();
            if(qualityVisual.ActualHeight<150)throw new Exception("Quality popup did not open");
            var qualityBitmap=new RenderTargetBitmap((int)Math.Ceiling(qualityVisual.ActualWidth),(int)Math.Ceiling(qualityVisual.ActualHeight),96,96,PixelFormats.Pbgra32);qualityBitmap.Render(qualityVisual);
            var qualityEncoder=new PngBitmapEncoder();qualityEncoder.Frames.Add(BitmapFrame.Create(qualityBitmap));using(var file=File.Create(IOPath.Combine(directory,"quality-options.png")))qualityEncoder.Save(file);
            quality.IsDropDownOpen=false;SetBusy(true);if(quality.IsEnabled)throw new Exception("Quality can change during download");SetBusy(false);
            report.AppendLine("PASS quality menu opens, remembers each format and locks during download");
            if(fileFormat.Items.Count!=3)throw new Exception("Missing video formats");fileFormat.SelectedIndex=2;
            audio.IsChecked=true;if(fileFormat.Items.Count!=5)throw new Exception("Missing audio formats");fileFormat.SelectedIndex=4;
            if(quality.IsEnabled || audioFormat!="flac" || downloadLabel.Text!="Pobierz FLAC")throw new Exception("Lossless format UI incorrect");
            await Task.Delay(200);capture("format-flac");video.IsChecked=true;
            if(videoFormat!="webm" || (string)((ComboBoxItem)fileFormat.SelectedItem).Tag!="webm")throw new Exception("Video format was not remembered");
            SetBusy(true);if(fileFormat.IsEnabled)throw new Exception("Format changes allowed during download");SetBusy(false);
            audio.IsChecked=true;fileFormat.SelectedIndex=0;if(!quality.IsEnabled || audioQuality!=192)throw new Exception("MP3 bitrate lost after format change");
            video.IsChecked=true;fileFormat.SelectedIndex=0;
            report.AppendLine("PASS all format choices, per-media selection, lossless quality state and download lock");
            var themeButton=Find<Button>("ThemeButton");themeButton.Focus();
            if(themeButton.Template.FindName("FocusRing",themeButton)!=null)throw new Exception("Persistent focus ring remains");
            themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(140);capture("theme-midpoint");
            var middle=((SolidColorBrush)Window.Resources["Canvas"]).Color;
            if(motion && (middle==Color.FromRgb(16,22,33) || middle==Color.FromRgb(244,246,251)))throw new Exception("Theme did not interpolate");
            themeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(400);
            if(((SolidColorBrush)Window.Resources["Canvas"]).Color!=Color.FromRgb(244,246,251))throw new Exception("Interrupted theme transition ended incorrectly");
            report.AppendLine("PASS smooth theme interpolation and rapid reversal; no persistent mouse-focus ring");
            // Exercise the same progress renderer used by the downloader, without network or user-setting changes.
            progress.Start(false);SetBusy(true);ConsumeLine("[download] Destination: Mój film.mp4");ConsumeLine("[download] 42.7% of 87.15MiB at 8.64MiB/s ETA 00:06");await Task.Delay(350);capture("fluent-progress");
            if(percent.Text!="43%" || progressClip.Rect.Width<100)throw new Exception("Progress rendering failed");report.AppendLine("PASS animated clipped progress and download state");
            ConsumeLine("[download] 100% of 87.15MiB in 00:10 at 8.64MiB/s");if(progress.Stage==DownloadStage.Completed)throw new Exception("Premature completion");
            ConsumeLine("[download] Destination: Mój film.m4a");ConsumeLine("[download] 5.0% of 1.11MiB at 0.50MiB/s ETA 00:02");if(percent.Text!="5%")throw new Exception("New stream did not reset");
            ConsumeLine("[Merger] Merging formats into Mój film.mp4");await Task.Delay(100);capture("fluent-processing");
            if(!indeterminate || percent.Text!="—" || Find<TextBlock>("SpeedText").Text!="—")throw new Exception("Processing metrics misleading");
            report.AppendLine("PASS separate audio stream and processing have honest progress");
            progress.Complete("Mój film.mp4");RenderModel();SetBusy(false);
            dark=true;ApplyTheme();await Task.Delay(400);capture("fluent-dark-success");
            Window.Width=760;Window.Height=780;dark=false;ApplyTheme();Window.UpdateLayout();check();capture("fluent-compact");
            url.Text="not a link";await StartDownload();if(title.Text!="Ten link wygląda na niepełny" || busy)throw new Exception("Input validation failed");report.AppendLine("PASS invalid URL produces inline error");
            audio.IsChecked=true;Window.UpdateLayout();if(video.IsChecked==true)throw new Exception("Format selection failed");report.AppendLine("PASS MP4 and MP3 tiles are mutually exclusive");
            details.IsExpanded=true;Window.UpdateLayout();if(log.ActualHeight<100)throw new Exception("Details hidden");report.AppendLine("PASS details panel expands");
            if(timer.IsEnabled || sheenRunning || indeterminate)throw new Exception("Idle animation/timer still running");report.AppendLine("PASS idle stops polling and infinite animation");
            foreach(bool night in new[]{false,true}) {
                dark=night;ApplyTheme();var sitesDialog=new SupportedSitesDialog(Window);sitesDialog.Window.Opacity=0;sitesDialog.Window.Show();await Task.Delay(50);await sitesDialog.Loading;
                var siteList=(ListBox)sitesDialog.Window.FindName("SiteList");var siteSearch=(TextBox)sitesDialog.Window.FindName("SearchInput");
                if(siteList.Items.Count<500)throw new Exception("Supported sites did not load");
                siteSearch.Text="yOuTuBe";if(siteList.Items.Count==0)throw new Exception("Site search is not case insensitive");
                siteSearch.Text="no-such-site-vidnelo-000";if(siteList.Items.Count!=0 || ((FrameworkElement)sitesDialog.Window.FindName("EmptyState")).Visibility!=Visibility.Visible)throw new Exception("Sites empty state failed");
                ((Button)((WrapPanel)sitesDialog.Window.FindName("PopularSites")).Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(siteList.Items.Count==0 || siteSearch.Text!="YouTube")throw new Exception("Popular site button failed");
                sitesDialog.Window.UpdateLayout();var sitesVisual=(FrameworkElement)sitesDialog.Window.Content;
                var sitesBitmap=new RenderTargetBitmap((int)sitesVisual.ActualWidth,(int)sitesVisual.ActualHeight,96,96,PixelFormats.Pbgra32);sitesBitmap.Render(sitesVisual);
                var sitesEncoder=new PngBitmapEncoder();sitesEncoder.Frames.Add(BitmapFrame.Create(sitesBitmap));using(var file=File.Create(IOPath.Combine(directory,night?"sites-dark.png":"sites-light.png")))sitesEncoder.Save(file);
                sitesDialog.Window.Close();
                var about=new AboutDialog(Window);about.Window.Opacity=0;about.Window.Show();about.Window.UpdateLayout();
                var aboutVisual=(FrameworkElement)about.Window.Content;
                var aboutBitmap=new RenderTargetBitmap((int)aboutVisual.ActualWidth,(int)aboutVisual.ActualHeight,96,96,PixelFormats.Pbgra32);aboutBitmap.Render(aboutVisual);
                var aboutEncoder=new PngBitmapEncoder();aboutEncoder.Frames.Add(BitmapFrame.Create(aboutBitmap));using(var file=File.Create(IOPath.Combine(directory,night?"about-dark.png":"about-light.png")))aboutEncoder.Save(file);
                ((Button)about.Window.FindName("CloseAboutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(about.Window.IsVisible || !Find<Button>("LanguageButton").IsEnabled)throw new Exception("About close or language picker failed");
                var updates=new UpdatesDialog(Window,tag=>{});updates.Window.Opacity=0;updates.Window.Show();
                updates.Display(new AppRelease {Version=new Version(1,1,0,0),Tag="v1.1",Url=AppUpdates.Repository+"/releases/tag/v1.1",Notes="Vidnelo 1.1\nPoprawki interfejsu i obsługi pobierania."});
                updates.Window.UpdateLayout();
                var updatesVisual=(FrameworkElement)updates.Window.Content;
                var updatesBitmap=new RenderTargetBitmap((int)updatesVisual.ActualWidth,(int)updatesVisual.ActualHeight,96,96,PixelFormats.Pbgra32);updatesBitmap.Render(updatesVisual);
                var updatesEncoder=new PngBitmapEncoder();updatesEncoder.Frames.Add(BitmapFrame.Create(updatesBitmap));using(var file=File.Create(IOPath.Combine(directory,night?"updates-dark.png":"updates-light.png")))updatesEncoder.Save(file);
                updates.Window.Close();
            }
            report.AppendLine("PASS updates dialog renders a simulated release in both themes without network access");
            report.AppendLine("PASS about dialog opens and closes in both themes; language picker enabled");
            report.AppendLine("PASS supported sites load from yt-dlp, cached reopen, search, empty state, popular shortcut and both themes");
            File.WriteAllText(IOPath.Combine(directory,"ui-checks.txt"),report.ToString());
        }
        public async Task FlowChecks(string directory) {
            Directory.CreateDirectory(directory);folder=IOPath.Combine(directory,"pobrane");UpdateFolder();
            url.Text="http://127.0.0.1:18769/sample.mp4";await StartDownload();
            if(!title.Text.StartsWith("Gotowe.") || Directory.GetFiles(folder,"*.mp4").Length!=1 || busy || !download.IsEnabled)throw new Exception("WPF MP4 flow failed: "+log.Text);
            audio.IsChecked=true;await StartDownload();
            if(!title.Text.StartsWith("Gotowe.") || Directory.GetFiles(folder,"*.mp3").Length!=1 || !File.Exists(Directory.GetFiles(folder,"*.mp4")[0]))throw new Exception("WPF MP3 flow failed: "+log.Text);
            foreach(var savedFile in Directory.GetFiles(folder)) if(!IOPath.GetFileNameWithoutExtension(savedFile).EndsWith(" - vidnelo",StringComparison.Ordinal))throw new Exception("Missing Vidnelo filename suffix: "+savedFile);
            url.Text="http://127.0.0.1:18769/missing.mp4";await StartDownload();
            if(!title.Text.StartsWith("Nie udało") || !details.IsExpanded || !download.IsEnabled)throw new Exception("WPF error state failed");
            File.WriteAllText(IOPath.Combine(directory,"flow-checks.txt"),"PASS MP4 through WPF click workflow\nPASS MP3 through WPF click workflow\nPASS original MP4 preserved\nPASS Vidnelo suffix on both saved formats\nPASS error details and controls recover\n");
        }
        public async Task MotionPreview(string directory) {
            Directory.CreateDirectory(directory);dark=true;ApplyTheme();progress.Start(false);SetBusy(true);RenderModel();
            for(int frame=0;frame<72;frame++) {
                if(frame==10)ConsumeLine("[download] Destination: Film z podróży.mp4");
                if(frame==12 || frame==19 || frame==26 || frame==33 || frame==40) {
                    int amount=frame==12?12:frame==19?31:frame==26?58:frame==33?82:100;
                    ConsumeLine("[download] "+amount+".0% of 87.15MiB at 8.64MiB/s ETA 00:06");
                }
                if(frame==43)ConsumeLine("[Merger] Merging formats into Film z podróży.mp4");
                if(frame==58) {progress.Complete("Film z podróży.mp4");RenderModel();SetBusy(false);}
                await Task.Delay(50);Window.UpdateLayout();
                var visual=(FrameworkElement)Window.Content;
                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
                Point origin=statusCard.TranslatePoint(new Point(0,0),visual);
                var crop=new CroppedBitmap(bitmap,new Int32Rect((int)Math.Round(origin.X),(int)Math.Round(origin.Y),(int)Math.Floor(statusCard.ActualWidth),(int)Math.Floor(statusCard.ActualHeight)));
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(crop));using(var file=File.Create(IOPath.Combine(directory,"frame"+frame.ToString("000")+".png")))encoder.Save(file);
            }
        }
        sealed class DialogOwner : System.Windows.Forms.IWin32Window {public IntPtr Handle{get;private set;}public DialogOwner(IntPtr handle){Handle=handle;}}
    }
    static class Program {
        [STAThread] static int Main(string[] args) {
            bool preview=args.Length==2 && (args[0]=="--render-preview" || args[0]=="--ui-test" || args[0]=="--motion-preview" || args[0]=="--localization-preview");
            try {
                var app=new Application();var ui=new FluentApp(preview);int exitCode=0;
                if(preview) {
                    ui.Window.ShowInTaskbar=false;ui.Window.Opacity=0;
                    ui.Window.ContentRendered+=async delegate {try {if(args[0]=="--localization-preview")await ui.LocalizationChecks(args[1]);else if(args[0]=="--ui-test")await ui.FlowChecks(args[1]);else if(args[0]=="--motion-preview")await ui.MotionPreview(args[1]);else await ui.RenderChecks(args[1]);}catch(Exception e){Directory.CreateDirectory(args[1]);File.WriteAllText(IOPath.Combine(args[1],"ui-error.txt"),e.ToString());exitCode=1;}finally {ui.Window.Close();}};
                }
                app.Run(ui.Window);return exitCode;
            } catch(Exception e) {
                if(preview) {Directory.CreateDirectory(args[1]);File.WriteAllText(IOPath.Combine(args[1],"ui-error.txt"),e.ToString());return 1;}
                MessageBox.Show(Localization.T("Nie udało się uruchomić programu.")+"\n\n"+e.Message,"Vidnelo",MessageBoxButton.OK,MessageBoxImage.Error);return 1;
            }
        }
    }
}
