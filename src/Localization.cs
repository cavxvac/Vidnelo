using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Automation;

namespace LinkDownloader {
    // Catalog is embedded: switching languages never requires a network request.
    public static class Localization {
        public static readonly string[] Codes={"pl","en","es","fr","de","pt-BR","it","uk","zh-Hans","ja","ko","hi","ar","tr","id"};
        public static readonly string[] Names={"Polski","English","Español","Français","Deutsch","Português (Brasil)","Italiano","Українська","简体中文","日本語","한국어","हिन्दी","العربية","Türkçe","Bahasa Indonesia"};
        static readonly Dictionary<string,string[]> entries=new Dictionary<string,string[]>();
        static readonly Dictionary<string,string> sources=new Dictionary<string,string>();
        static int selected;
        public static string Code {get{return Codes[selected];}}
        static Localization() {
            using(var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Translations.txt"),System.Text.Encoding.UTF8)) {
                string line;int n=0;
                while((line=reader.ReadLine())!=null) {
                    n++;if(n==1 || String.IsNullOrWhiteSpace(line))continue;
                    string[] cells=line.Split('|');if(cells.Length!=16)throw new InvalidDataException("Translation row "+n);
                    var values=new string[15];Array.Copy(cells,1,values,0,15);
                    foreach(string value in values)if(String.IsNullOrWhiteSpace(value))throw new InvalidDataException("Empty translation: "+cells[0]);
                    entries.Add(cells[0],values);sources[values[0]]=cells[0];
                }
            }
        }
        public static string T(string source,params object[] args) {
            string key,result=source;
            if(source!=null && sources.TryGetValue(source,out key))result=entries[key][selected];
            return args.Length==0?result:String.Format(CultureInfo.InvariantCulture,result,args);
        }
        public static void SetLanguage(string code) {
            int index=Array.IndexOf(Codes,code);selected=index<0?0:index;
            foreach(var entry in entries)Application.Current.Resources["L10n."+entry.Key]=entry.Value[selected];
        }
        static void Bind(DependencyObject obj,DependencyProperty property) {
            string value=obj.GetValue(property) as string,key;
            if(value==null || !sources.TryGetValue(value,out key))return;
            var element=obj as FrameworkElement;
            if(element!=null)element.SetResourceReference(property,"L10n."+key);
            else {var content=obj as FrameworkContentElement;if(content!=null)content.SetResourceReference(property,"L10n."+key);}
        }
        static void Walk(DependencyObject obj,HashSet<DependencyObject> seen) {
            if(!seen.Add(obj))return;
            var text=obj as TextBlock;
            if(text!=null) {
                // Preserve hyperlinks and separately translated runs in rich paragraphs.
                if(text.Inlines.Count<=1 && !(text.Inlines.FirstInline is Span))Bind(text,TextBlock.TextProperty);
                var inlines=new List<Inline>(text.Inlines);foreach(Inline inline in inlines)Walk(inline,seen);
            }
            if(obj is Run)Bind(obj,Run.TextProperty);
            var span=obj as Span;if(span!=null) {var inlines=new List<Inline>(span.Inlines);foreach(Inline inline in inlines)Walk(inline,seen);}
            if(obj is ContentControl)Bind(obj,ContentControl.ContentProperty);
            if(obj is HeaderedContentControl)Bind(obj,HeaderedContentControl.HeaderProperty);
            if(obj is FrameworkElement)Bind(obj,FrameworkElement.ToolTipProperty);
            Bind(obj,AutomationProperties.NameProperty);
            if(obj is Window)Bind(obj,Window.TitleProperty);
            var children=new List<DependencyObject>();foreach(object child in LogicalTreeHelper.GetChildren(obj)) {var d=child as DependencyObject;if(d!=null)children.Add(d);}
            foreach(var child in children)Walk(child,seen);
        }
        public static void Apply(Window window) {
            Walk(window,new HashSet<DependencyObject>());
            SetDirection(window);
        }
        public static void SetDirection(Window window) {
            window.FlowDirection=Code=="ar"?FlowDirection.RightToLeft:FlowDirection.LeftToRight;
            window.Language=XmlLanguage.GetLanguage(Code);
            foreach(string name in new[]{"UrlInput","FolderText","LogBox","PercentText","SpeedText","EtaText","SizeText","BrandIcon","BrandWordmark","AboutIcon","AboutWordmark","SiteList","SearchInput"}) {
                var element=window.FindName(name) as FrameworkElement;if(element!=null)element.FlowDirection=FlowDirection.LeftToRight;
            }
        }
    }
}
