using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using ScreenShot;
using ScreenShot.Core;

internal static class Program
{
    private static int failures;
    [STAThread] static int Main(string[] args)
    {
        var app=new App();app.InitializeComponent();
        var pixels = new byte[200*140*4];
        for(int i=0;i<pixels.Length;i+=4){pixels[i]=90;pixels[i+1]=70;pixels[i+2]=50;pixels[i+3]=255;}
        var source=BitmapSource.Create(200,140,96,96,PixelFormats.Bgra32,null,pixels,800);
        EditorBehaviorTests.Run(Check,source);
        Check("re-recording the registered global shortcut completes without capture or preference changes",()=>{
            var dashboard=new Dashboard(app);
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var field=typeof(App).GetField("dashboard",flags)!;
            field.SetValue(app,dashboard);
            var originalKey=app.Preferences.Key;var originalModifiers=app.Preferences.Modifiers;
            try {
                typeof(Dashboard).GetMethod("BeginRecording",flags)!.Invoke(dashboard,null);
                True(app.RecordingShortcut,"Recorder did not start");
                app.HandleShortcutPressed();
                True(!app.RecordingShortcut,"Current global key was ignored by recorder");
                True(typeof(App).GetField("overlay",flags)!.GetValue(app)==null,"Recording opened a capture overlay");
                Equal(originalKey,app.Preferences.Key);Equal(originalModifiers,app.Preferences.Modifiers);
                var status=(System.Windows.Controls.TextBlock)typeof(Dashboard).GetField("status",flags)!.GetValue(dashboard)!;
                True(status.Text.StartsWith("Shortcut unchanged."),"No unchanged confirmation");
                typeof(Dashboard).GetMethod("BeginRecording",flags)!.Invoke(dashboard,null);
                using var keyboardSource=new HwndSource(new HwndSourceParameters("Shortcut cancel test"){ParentWindow=new IntPtr(-3)});
                dashboard.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,keyboardSource,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                True(!app.RecordingShortcut,"Escape did not stop recording");
                Equal(originalKey,app.Preferences.Key);Equal(originalModifiers,app.Preferences.Modifiers);
            } finally { field.SetValue(app,null);dashboard.Close(); }
        });
        Check("export crops only selected pixels without overlay dimming or toolbar",()=> {
            var image=MarkRenderer.Export(source,new Box(30,20,100,60),Array.Empty<Mark>());
            Equal(100,image.PixelWidth);Equal(60,image.PixelHeight);
            var output=new byte[100*60*4];image.CopyPixels(output,400,0);
            for(int i=0;i<output.Length;i+=4){Equal((byte)90,output[i]);Equal((byte)50,output[i+2]);Equal((byte)255,output[i+3]);}
        });
        Check("annotations export in crop-local coordinates",()=> {
            var mark=new Mark(Guid.NewGuid(),Tool.Rectangle,new[]{new Pt(40,30),new Pt(100,60)},"#FF0000",4,"");
            var image=MarkRenderer.Export(source,new Box(30,20,100,60),new[]{mark});
            var sample=new byte[4];image.CopyPixels(new Int32Rect(30,10,1,1),sample,4,0);Equal((byte)255,sample[2]);Equal((byte)0,sample[0]);
        });
        Check("fractional and outside crop clamps to physical bitmap",()=> {
            var image=MarkRenderer.Export(source,new Box(-4.2,-2,15,12),Array.Empty<Mark>());
            Equal(11,image.PixelWidth);Equal(10,image.PixelHeight);
        });
        Check("wrapped text retains its layout after committing",()=> {
            var mark=new Mark(Guid.NewGuid(),Tool.Text,new[]{new Pt(0,0)},"#FFFFFF",3,"A long line of text that must wrap to several lines",80);
            var bounds=MarkRenderer.Bounds(mark); True(bounds.Width<=81,"Text exceeded assigned width: "+bounds); True(bounds.Height>60,"Text did not wrap");
        });
        Check("long URLs wrap without silently replacing content with ellipsis",()=> {
            var mark=new Mark(Guid.NewGuid(),Tool.Text,new[]{new Pt(0,0)},"#FFFFFF",3,"supercalifragilisticexpialidocious",80);
            True(MarkRenderer.Bounds(mark).Height>150,"Unbroken token was truncated instead of wrapping");
        });
        Check("PNG and JPEG encode decodable cropped images",()=> {
            var image=MarkRenderer.Export(source,new Box(10,10,80,50),Array.Empty<Mark>());
            foreach(var encoder in new BitmapEncoder[]{new PngBitmapEncoder(),new JpegBitmapEncoder()}){
                using var stream=new MemoryStream();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);stream.Position=0;
                var decoded=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);Equal(80,decoded.Frames[0].PixelWidth);Equal(50,decoded.Frames[0].PixelHeight);
            }
        });
        Check("losing mouse capture cancels pending gesture and restores crop",()=> {
            var window=new OverlayWindow(new CaptureFrame(source,0,0),new Settings());
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var surface=(EditorSurface)typeof(OverlayWindow).GetField("surface",flags)!.GetValue(window)!;
            surface.Area=new Box(0,0,50,50);surface.HasSelection=true;
            typeof(OverlayWindow).GetField("originalArea",flags)!.SetValue(window,new Box(10,10,80,60));
            typeof(OverlayWindow).GetField("gesture",flags)!.SetValue(window,"resize");
            surface.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,0){RoutedEvent=UIElement.LostMouseCaptureEvent});
            Equal("",(string)typeof(OverlayWindow).GetField("gesture",flags)!.GetValue(window)!);
            Equal(new Box(10,10,80,60),surface.Area);window.Close();
        });
        Check("all drawing tools render and history undo removes output",()=> {
            foreach(var tool in new[]{Tool.Pen,Tool.Arrow,Tool.Line,Tool.Rectangle,Tool.Highlight,Tool.Text}){
                var doc=new Document();doc.Add(new Mark(Guid.NewGuid(),tool,new[]{new Pt(10,10),new Pt(120,80)},"#FF0000",5,"Hello",100));
                var image=MarkRenderer.Export(source,new Box(0,0,200,140),doc.Items);var output=new byte[pixels.Length];image.CopyPixels(output,800,0);
                True(!output.AsSpan().SequenceEqual(pixels),"Tool produced no pixels: "+tool);
                doc.Undo();var undone=MarkRenderer.Export(source,new Box(0,0,200,140),doc.Items);undone.CopyPixels(output,800,0);True(output.AsSpan().SequenceEqual(pixels),"Undo left pixels behind");
            }
        });
        if(Array.IndexOf(args,"--window-smoke")>=0)
        Check("overlay HWND covers requested physical pixel bounds",()=> {
            var overlay=new OverlayWindow(new CaptureFrame(source,24,32),new Settings());
            try {
                overlay.Show();overlay.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                GetWindowRect(new WindowInteropHelper(overlay).Handle,out var rect);
                Equal(24,rect.Left);Equal(32,rect.Top);Equal(200,rect.Right-rect.Left);Equal(140,rect.Bottom-rect.Top);
            } finally { overlay.Close(); }
        });
        if(Array.IndexOf(args,"--previews")>=0)
        {
            Directory.CreateDirectory("artifacts/previews");
            var dashboard=new Dashboard(app);
            Render((FrameworkElement)dashboard.Content,464,321,"artifacts/previews/dashboard.png");
            Render((FrameworkElement)dashboard.Content,384,281,"artifacts/previews/dashboard-small.png");
            app.Preferences.ShortcutLabel="Alt + Control + Shift + Windows + NumPad9";
            typeof(Dashboard).GetMethod("UpdateShortcutLabel",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dashboard,null);
            Render((FrameworkElement)dashboard.Content,384,281,"artifacts/previews/dashboard-long-shortcut.png");
            app.Preferences.ShortcutLabel="Print Screen";
            typeof(Dashboard).GetMethod("UpdateShortcutLabel",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dashboard,null);
            typeof(Dashboard).GetMethod("BeginRecording",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(dashboard,null);
            Render((FrameworkElement)dashboard.Content,384,281,"artifacts/previews/dashboard-recording.png");
            dashboard.Close();
            var fixture=new DrawingVisual();using(var dc=fixture.RenderOpen()){
                dc.DrawRectangle(Dashboard.Brush("#EDF2F7"),null,new Rect(0,0,1280,800));
                dc.DrawRectangle(Dashboard.Brush("#FFFFFF"),null,new Rect(150,140,920,500));
                dc.DrawText(new FormattedText("Your idea, clearly captured.",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),34,Dashboard.Brush("#172233"),1),new Point(220,220));
                dc.DrawText(new FormattedText("A sample document for the ScreenShot editor preview.\nSelect a region, mark what matters, then copy or save.",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),21,Dashboard.Brush("#64748B"),1),new Point(220,300));
                dc.DrawRectangle(Dashboard.Brush("#DBEAFE"),null,new Rect(220,430,780,100));
            }
            var bg=new RenderTargetBitmap(1280,800,96,96,PixelFormats.Pbgra32);bg.Render(fixture);
            var overlay=new OverlayWindow(new CaptureFrame(bg,0,0),new Settings());
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var surface=(EditorSurface)typeof(OverlayWindow).GetField("surface",flags)!.GetValue(overlay)!;
            surface.Area=new Box(180,180,850,400);surface.HasSelection=true;
            surface.Document.Add(new(Guid.NewGuid(),Tool.Arrow,new[]{new Pt(700,420),new Pt(590,335)},"#FF4D67",4,""));
            surface.Document.Add(new(Guid.NewGuid(),Tool.Text,new[]{new Pt(720,440)},"#FF4D67",3,"Right here",260));
            typeof(OverlayWindow).GetMethod("SetTool",flags)!.Invoke(overlay,new object[]{Tool.Arrow});
            typeof(OverlayWindow).GetMethod("Refresh",flags)!.Invoke(overlay,new object[]{true});
            Render((FrameworkElement)overlay.Content,1280,800,"artifacts/previews/editor.png");overlay.Close();
        }
        return failures==0?0:1;
    }
    private static void Render(FrameworkElement root,int width,int height,string path){root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var output=File.Create(path);encoder.Save(output);}
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out WindowRect rect);
    [StructLayout(LayoutKind.Sequential)] private struct WindowRect {public int Left,Top,Right,Bottom;}
    private static void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception ex){failures++;Console.WriteLine("FAIL "+name+": "+ex.Message);}}
    private static void Equal<T>(T expected,T actual){if(!Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
    private static void True(bool condition,string message){if(!condition)throw new Exception(message);}
}
