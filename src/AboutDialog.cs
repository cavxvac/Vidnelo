using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LinkDownloader {
    public sealed class AboutDialog {
        public readonly Window Window;
        public AboutDialog(Window owner) {
            var assembly=Assembly.GetExecutingAssembly();
            Window=(Window)Application.LoadComponent(new Uri("/"+assembly.GetName().Name+";component/About.xaml",UriKind.Relative));
            Localization.Apply(Window);
            Window.Owner=owner;Window.Icon=owner.Icon;Window.Resources.MergedDictionaries.Add(owner.Resources);
            Window.MaxHeight=SystemParameters.WorkArea.Height;Window.Height=Math.Min(Window.Height,Window.MaxHeight);
            ((Image)Window.FindName("AboutIcon")).Source=((Image)owner.FindName("BrandIcon")).Source;
            ((Rectangle)Window.FindName("AboutWordmark")).OpacityMask=((Rectangle)owner.FindName("BrandWordmark")).OpacityMask;
            ((TextBlock)Window.FindName("VersionText")).Text=Localization.T("Wersja {0}",assembly.GetName().Version.ToString(2));
            ((Button)Window.FindName("CloseAboutButton")).Click+=delegate {Window.Close();};
            Window.AddHandler(System.Windows.Documents.Hyperlink.RequestNavigateEvent,new RequestNavigateEventHandler(delegate(object sender,RequestNavigateEventArgs e) {
                e.Handled=true;
                if(e.Uri.Scheme!=Uri.UriSchemeHttps)return;
                try {Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri){UseShellExecute=true});}
                catch(Exception) {var error=(TextBlock)Window.FindName("LinkError");error.Text=Localization.T("Nie udało się otworzyć przeglądarki. Adres: {0}",e.Uri.AbsoluteUri);error.Visibility=Visibility.Visible;}
            }));
            Window.SourceInitialized+=delegate {
                var color=((SolidColorBrush)owner.Resources["Canvas"]).Color;int dark=color.R<128?1:0,round=2,rgb=color.R|(color.G<<8)|(color.B<<16);
                var handle=new WindowInteropHelper(Window).Handle;
                try {DwmSetWindowAttribute(handle,20,ref dark,4);DwmSetWindowAttribute(handle,33,ref round,4);DwmSetWindowAttribute(handle,35,ref rgb,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
            };
        }
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
    }
}
