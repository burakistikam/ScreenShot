using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ScreenShot.Core;

namespace ScreenShot;

internal sealed class OverlayWindow : Window
{
    private readonly CaptureFrame frame;
    private readonly Settings settings;
    private readonly Canvas canvas;
    private readonly EditorSurface surface;
    private readonly Border toolbar;
    private readonly TextBlock hint;
    private readonly Dictionary<Tool, Button> toolButtons = new();
    private readonly Button undoButton, redoButton;
    private readonly ComboBox widthPicker;
    private readonly Button colorButton;
    private Tool tool = Tool.Arrow;
    private string color = "#FF4D67";
    private double strokeWidth = 3;
    private string gesture = "";
    private Pt start;
    private Box originalArea;
    private Mark? originalMark;
    private bool edgeLeft, edgeTop, edgeRight, edgeBottom;
    private TextBox? textEditor;
    private Mark? editingMark;
    private bool exporting;
    private HwndSource? hwndSource;
    internal event Action<string>? Completed;

    internal OverlayWindow(CaptureFrame frame, Settings settings)
    {
        this.frame = frame; this.settings = settings;
        Title = "ScreenShot — select an area"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; Background = Brushes.Black; WindowStartupLocation = WindowStartupLocation.Manual;
        surface = new EditorSurface(frame.Bitmap);
        canvas = new Canvas { Width = frame.Bitmap.PixelWidth, Height = frame.Bitmap.PixelHeight, ClipToBounds = true };
        canvas.Children.Add(surface);
        hint = new TextBlock { Text = "Drag to select an area  •  Esc to cancel", FontSize = 15, Foreground = Brushes.White, Background = Dashboard.Brush("#172233"), Padding = new Thickness(14, 9, 14, 9) };
        canvas.Children.Add(hint); Canvas.SetLeft(hint, 24); Canvas.SetTop(hint, 24);
        var rows = new StackPanel();
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (value, label) in new[] { (Tool.Select,"↖ Move"), (Tool.Arrow,"↗ Arrow"), (Tool.Pen,"✎ Pen"), (Tool.Line,"╱ Line"), (Tool.Rectangle,"□ Box"), (Tool.Highlight,"▰ Highlight"), (Tool.Text,"T Text") })
        {
            var button = MakeButton(label, () => SetTool(value)); button.ToolTip = value == Tool.Select ? "Select/move annotations; drag empty selection to move crop" : label;
            toolButtons[value] = button; tools.Children.Add(button);
        }
        rows.Children.Add(tools);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        colorButton = MakeButton("Color", ChooseColor); colorButton.Background = Dashboard.Brush(color); colorButton.ToolTip = "Annotation color"; actions.Children.Add(colorButton);
        widthPicker = new ComboBox { Width = 86, Margin = new Thickness(5), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Line width / text size" };
        var pickerTextStyle=new Style(typeof(TextBlock));pickerTextStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,Dashboard.Brush("#172233")));widthPicker.Resources.Add(typeof(TextBlock),pickerTextStyle);
        foreach (int width in new[] { 2, 3, 5, 8, 12 }) widthPicker.Items.Add(width);
        widthPicker.SelectedItem = 3; widthPicker.SelectionChanged += (_, _) => { if (widthPicker.SelectedItem is int n) { strokeWidth = n; ApplyStyle(width:n); } }; actions.Children.Add(widthPicker);
        undoButton = MakeButton("↶", () => Undo(false)); undoButton.ToolTip = "Undo (Ctrl+Z)"; actions.Children.Add(undoButton);
        redoButton = MakeButton("↷", () => Undo(true)); redoButton.ToolTip = "Redo (Ctrl+Y)"; actions.Children.Add(redoButton);
        actions.Children.Add(MakeButton("Reselect", Reselect));
        actions.Children.Add(MakeButton("Copy", () => _ = CopyAsync()));
        var save = MakeButton("Save…", Save); save.Background = Dashboard.Brush("#0284C7"); actions.Children.Add(save);
        actions.Children.Add(MakeButton("✕", Close));
        rows.Children.Add(actions);
        toolbar = new Border { Child = rows, Background = Dashboard.Brush("#101B2B"), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Visibility = Visibility.Collapsed };
        canvas.Children.Add(toolbar);
        Content = new Viewbox { Stretch = Stretch.Fill, Child = canvas };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            hwndSource = HwndSource.FromHwnd(handle); hwndSource?.AddHook(WindowHook);
            Native.SetWindowPos(handle, new IntPtr(-1), frame.Left, frame.Top, frame.Bitmap.PixelWidth, frame.Bitmap.PixelHeight, 0x0040);
        };
        Loaded += (_, _) => { surface.Focus(); SetTool(Tool.Arrow); };
        Closed += (_, _) => { hwndSource?.RemoveHook(WindowHook); };
        surface.MouseLeftButtonDown += PointerDown;
        surface.MouseMove += PointerMove;
        surface.MouseLeftButtonUp += PointerUp;
        surface.LostMouseCapture += (_, _) => CancelGesture();
        Deactivated += (_, _) => CancelGesture();
        PreviewKeyDown += OnKey;
    }
    private IntPtr WindowHook(IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        // One fixed virtual-desktop HWND: ignore suggested per-monitor repositioning.
        if (msg == 0x02E0) handled = true;
        if (msg == 0x007E) { Dispatcher.BeginInvoke(Close); handled = true; }
        return IntPtr.Zero;
    }
    private static Button MakeButton(string label, Action action)
    {
        var button = new Button { Content = label, Padding = new Thickness(11, 9, 11, 9), FontSize = 13, Focusable = false };
        button.Click += (_, _) => action(); return button;
    }
    private Pt Position(MouseEventArgs e)
    {
        var p = e.GetPosition(surface); return new(Math.Clamp(p.X,0,surface.Width),Math.Clamp(p.Y,0,surface.Height));
    }
    private Pt InsideArea(Pt p) => new(Math.Clamp(p.X,surface.Area.X,surface.Area.Right),Math.Clamp(p.Y,surface.Area.Y,surface.Area.Bottom));
    private void SetTool(Tool value)
    {
        CommitText(); tool = value; surface.Selected = null;
        foreach (var pair in toolButtons) pair.Value.Background = Dashboard.Brush(pair.Key == value ? "#0369A1" : "#253349");
        surface.Cursor = value == Tool.Select ? Cursors.Arrow : value == Tool.Text ? Cursors.IBeam : Cursors.Cross;
        Refresh();
    }
    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (exporting) return;
        CommitText(); surface.Focus(); var p = Position(e); start = p;
        originalArea = surface.Area;
        edgeLeft = edgeRight = edgeTop = edgeBottom = false;
        if (!surface.HasSelection)
        { gesture = "select"; surface.HasSelection = true; surface.Area = Box.FromDrag(p,p,surface.Width,surface.Height); }
        else
        {
            var b = surface.Area;
            (edgeLeft,edgeTop,edgeRight,edgeBottom)=b.HitEdges(p);
            if (edgeLeft || edgeRight || edgeTop || edgeBottom) gesture = "resize";
            else if (!b.Contains(p)) { surface.Selected = null; Refresh(); return; }
            else if (tool == Tool.Select)
            {
                originalMark = surface.Document.Items.Reverse().FirstOrDefault(m => MarkRenderer.Bounds(m).Contains(MarkRenderer.Point(p)));
                surface.Selected = originalMark?.Id;
                if (originalMark != null && e.ClickCount == 2 && originalMark.Tool == Tool.Text) { BeginText(originalMark.Points[0],originalMark); return; }
                gesture = originalMark == null ? "move-area" : "move-mark";
            }
            else if (tool == Tool.Text) { BeginText(p,null); return; }
            else
            {
                gesture = "draw";
                surface.Preview = new Mark(Guid.NewGuid(), tool, new[] { p }, color, strokeWidth, "");
                surface.Selected = null;
            }
        }
        if (!surface.CaptureMouse()) { CancelGesture(); return; }
        toolbar.Visibility = Visibility.Collapsed; Refresh(false); e.Handled = true;
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        var p = Position(e);
        if (gesture.Length == 0) return;
        switch (gesture)
        {
            case "select": surface.Area = Box.FromDrag(start,p,surface.Width,surface.Height); break;
            case "resize": surface.Area = originalArea.Resize(p,edgeLeft,edgeTop,edgeRight,edgeBottom,surface.Width,surface.Height); break;
            case "move-area": surface.Area = originalArea.Move(p.X-start.X,p.Y-start.Y,surface.Width,surface.Height); break;
            case "move-mark": if (originalMark != null) surface.Preview = originalMark.Translate(p.X-start.X,p.Y-start.Y); break;
            case "draw":
                if (surface.Preview != null)
                {
                    p = InsideArea(p);
                    var points = surface.Preview.Tool == Tool.Pen ? surface.Preview.Points.Append(p).ToArray() : new[] { start,p };
                    surface.Preview = surface.Preview with { Points = points };
                }
                break;
        }
        Refresh(false);
    }
    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (gesture.Length == 0) return;
        PointerMove(sender,e);
        if (surface.Preview != null)
        {
            if (gesture == "draw") surface.Document.Add(surface.Preview);
            else if (gesture == "move-mark") surface.Document.Replace(surface.Preview);
        }
        surface.Preview = null; originalMark = null; gesture = ""; surface.ReleaseMouseCapture();
        if (!surface.Area.IsUsable) surface.HasSelection = false;
        Refresh();
    }
    private void CancelGesture()
    {
        if(gesture.Length==0)return;
        if(gesture=="select")surface.HasSelection=false;
        else if(gesture is "resize" or "move-area")surface.Area=originalArea;
        gesture="";surface.Preview=null;originalMark=null;
        if(surface.IsMouseCaptured)surface.ReleaseMouseCapture();
        Refresh();
    }
    private void Refresh(bool layout = true)
    {
        surface.InvalidateVisual(); undoButton.IsEnabled = surface.Document.CanUndo; redoButton.IsEnabled = surface.Document.CanRedo;
        hint.Text = surface.HasSelection
            ? $"{Math.Ceiling(surface.Area.Width):0} × {Math.Ceiling(surface.Area.Height):0} px   •   Ctrl+C copy   •   Ctrl+S save   •   Esc cancel"
            : "Drag to select an area  •  Esc to cancel";
        if (!layout) return;
        toolbar.Visibility = surface.HasSelection && surface.Area.IsUsable ? Visibility.Visible : Visibility.Collapsed;
        if (toolbar.Visibility != Visibility.Visible) return;
        toolbar.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        double tw=toolbar.DesiredSize.Width, th=toolbar.DesiredSize.Height;
        // Keep controls on the monitor containing the selection end, not in a virtual-desktop gap.
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(frame.Left+(int)surface.Area.Right-1,frame.Top+(int)surface.Area.Bottom-1));
        var bounds = screen.Bounds;
        double minX=Math.Max(0,bounds.Left-frame.Left), minY=Math.Max(0,bounds.Top-frame.Top);
        double maxX=Math.Min(surface.Width,bounds.Right-frame.Left), maxY=Math.Min(surface.Height,bounds.Bottom-frame.Top);
        double x=Math.Clamp(surface.Area.Right-tw,minX,Math.Max(minX,maxX-tw));
        double y=surface.Area.Bottom+12;
        if(y+th>maxY) y=surface.Area.Y-th-12;
        y=Math.Clamp(y,minY+8,Math.Max(minY+8,maxY-th-8));
        Canvas.SetLeft(toolbar,x); Canvas.SetTop(toolbar,y);
        Canvas.SetLeft(hint,Math.Clamp(surface.Area.X,minX,Math.Max(minX,maxX-580)));
        Canvas.SetTop(hint,Math.Max(minY+8,surface.Area.Y-42));
    }
    private void BeginText(Pt point, Mark? existing)
    {
        editingMark = existing ?? new Mark(Guid.NewGuid(),Tool.Text,new[] {point},color,strokeWidth,"",Math.Max(40,Math.Min(480,surface.Area.Right-point.X)));
        textEditor = new TextBox { Text = existing?.Text ?? "", FontFamily = new FontFamily("Segoe UI"), FontSize = 18+editingMark.Width*3,
            Foreground = Dashboard.Brush(editingMark.Color), Background = Dashboard.Brush("#F8FAFC"), Padding = new Thickness(3),
            Width = (editingMark.TextWidth>0?editingMark.TextWidth:480)+10, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            BorderBrush = Brushes.DeepSkyBlue, BorderThickness = new Thickness(2) };
        canvas.Children.Add(textEditor); Canvas.SetLeft(textEditor,point.X-5); Canvas.SetTop(textEditor,point.Y-5);
        textEditor.Focus(); textEditor.SelectAll(); hint.Text = "Type your text  •  Ctrl+Enter to apply  •  Esc to discard";
    }
    private void CommitText(bool cancel = false)
    {
        if (textEditor == null || editingMark == null) return;
        if (!cancel && !string.IsNullOrWhiteSpace(textEditor.Text))
        {
            var updated = editingMark with { Text = textEditor.Text };
            if (surface.Document.Items.Any(m=>m.Id==updated.Id)) surface.Document.Replace(updated); else surface.Document.Add(updated);
        }
        canvas.Children.Remove(textEditor); textEditor = null; editingMark = null; surface.Focus(); Refresh();
    }
    private void Undo(bool redo)
    {
        CommitText(); if (redo) surface.Document.Redo(); else surface.Document.Undo(); surface.Selected = null; Refresh();
    }
    private void Reselect()
    {
        CommitText(); surface.HasSelection = false; surface.Selected = null; Refresh();
        // Keep annotation history; selecting a new crop never destroys edits.
    }
    private void ChooseColor()
    {
        CommitText();
        using var picker = new System.Windows.Forms.ColorDialog { FullOpen = true, Color = System.Drawing.ColorTranslator.FromHtml(color) };
        if (picker.ShowDialog(new WindowHandle(new WindowInteropHelper(this).Handle)) != System.Windows.Forms.DialogResult.OK) return;
        color = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}"; colorButton.Background = Dashboard.Brush(color); ApplyStyle(color:color);
    }
    private void ApplyStyle(string? color=null,double? width=null)
    {
        if (surface.Selected is Guid id)
        { var mark=surface.Document.Items.FirstOrDefault(m=>m.Id==id); if(mark!=null)surface.Document.Replace(mark.WithStyle(color,width)); }
        Refresh();
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (textEditor != null)
        {
            if (e.Key == Key.Escape) { CommitText(true); e.Handled=true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { CommitText(); e.Handled=true; }
            return;
        }
        if (gesture.Length>0 && e.Key!=Key.Escape) return;
        if (e.Key == Key.Escape) { Close(); e.Handled=true; }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            switch(e.Key)
            {
                case Key.C: _=CopyAsync(); e.Handled=true; break;
                case Key.S: Save(); e.Handled=true; break;
                case Key.Z: Undo(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); e.Handled=true; break;
                case Key.Y: Undo(true); e.Handled=true; break;
                case Key.A: surface.Area=new(0,0,surface.Width,surface.Height); surface.HasSelection=true; Refresh(); e.Handled=true; break;
            }
        }
        else if (e.Key == Key.Delete && surface.Selected is Guid id) { surface.Document.Remove(id); surface.Selected=null; Refresh(); e.Handled=true; }
        else if (e.Key == Key.F2 && surface.Selected is Guid selected)
        { var mark=surface.Document.Items.FirstOrDefault(m=>m.Id==selected); if(mark?.Tool==Tool.Text)BeginText(mark.Points[0],mark); e.Handled=true; }
    }
    private async Task CopyAsync()
    {
        if (exporting || !surface.HasSelection || !surface.Area.IsUsable) return;
        CommitText(); exporting=true;
        try
        {
            var bitmap=MarkRenderer.Export(frame.Bitmap,surface.Area,surface.Document.Items);
            for (int attempt=0;;attempt++)
            {
                try { Clipboard.SetImage(bitmap); break; }
                catch (COMException) when(attempt<4) { await Task.Delay(70); }
            }
            Completed?.Invoke($"Copied {bitmap.PixelWidth} × {bitmap.PixelHeight} image to clipboard."); Close();
        }
        catch(Exception ex) { MessageBox.Show(this,"Could not copy the image. Try saving it instead.\n"+ex.Message,"ScreenShot"); }
        finally { exporting=false; }
    }
    private void Save()
    {
        if(exporting || !surface.HasSelection || !surface.Area.IsUsable)return;
        CommitText(); exporting=true;
        try
        {
            var dialog=new SaveFileDialog { Title="Save screenshot", Filter="PNG image|*.png|JPEG image|*.jpg", DefaultExt=".png", AddExtension=true,
                FileName=$"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss}", InitialDirectory=Directory.Exists(settings.SaveFolder)?settings.SaveFolder:Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) };
            if(dialog.ShowDialog(this)!=true)return;
            var bitmap=MarkRenderer.Export(frame.Bitmap,surface.Area,surface.Document.Items);
            BitmapEncoder encoder=Path.GetExtension(dialog.FileName).Equals(".jpg",StringComparison.OrdinalIgnoreCase) || Path.GetExtension(dialog.FileName).Equals(".jpeg",StringComparison.OrdinalIgnoreCase)
                ? new JpegBitmapEncoder { QualityLevel=95 } : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=new FileStream(dialog.FileName,FileMode.Create,FileAccess.Write))encoder.Save(stream);
            settings.SaveFolder=Path.GetDirectoryName(dialog.FileName)!;
            try { settings.Save(); } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { /* Image was saved; folder preference is best effort. */ }
            Completed?.Invoke("Saved to " + dialog.FileName); Close();
        }
        catch(Exception ex) { MessageBox.Show(this,"Could not save the image.\n"+ex.Message,"ScreenShot"); }
        finally { exporting=false; }
    }
    private sealed class WindowHandle(IntPtr handle) : System.Windows.Forms.IWin32Window { public IntPtr Handle => handle; }
}
