using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LinkDownloader;
class ScreenshotCapture {
    [STAThread] static int Main(string[] args) {
        if(args.Length!=1)return 2;
        var app=new Application();var ui=new FluentApp(true);int exit=0;
        ui.Window.Opacity=0;ui.Window.ShowInTaskbar=false;
        ui.Window.ContentRendered+=async delegate {
            try {
                Directory.CreateDirectory(args[0]);
                typeof(FluentApp).GetMethod("ChangeLanguage",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{"en"});
                ui.Find<TextBlock>("FolderText").Text=@"C:\Users\You\Downloads";
                ui.Window.Width=1160;ui.Window.Height=860;
                await Task.Delay(400);Capture(ui.Window,Path.Combine(args[0],"vidnelo-light.png"));
                ui.Find<Button>("ThemeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(400);Capture(ui.Window,Path.Combine(args[0],"vidnelo-dark.png"));
            }catch(Exception e){Console.Error.WriteLine(e);exit=1;}finally{ui.Window.Close();}
        };
        app.Run(ui.Window);return exit;
    }
    static void Capture(Window window,string path) {
        window.UpdateLayout();var visual=(FrameworkElement)window.Content;
        var bitmap=new RenderTargetBitmap((int)visual.ActualWidth,(int)visual.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
    }
}
