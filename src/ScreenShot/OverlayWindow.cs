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
using System.Windows.Automation;
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
    private readonly Dictionary<string,Button> swatches = new();
    private readonly TextBlock toolHint = new() { FontSize=11,Foreground=Dashboard.Brush("#9FB0C4"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6,8,6,2) };
    private bool syncingStyle;
    private Tool tool = Tool.Arrow;
    private string color = "#FF4D67";
    private double strokeWidth = 3;
    private string gesture = "";
    private Pt start;
    private Box originalArea;
    private Mark? originalMark;
    private bool edgeLeft, edgeTop, edgeRight, edgeBottom;
    private TextBox? textEditor;
    private Border? textPanel;
    private Mark? editingMark;
    private bool exporting;
    private HwndSource? hwndSource;
    internal event Action<string>? Completed;

    internal OverlayWindow(CaptureFrame frame, Settings settings)
    {
        this.frame = frame; this.settings = settings;
        Title = "ScreenShot — select an area"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true; Topmost = true; Background = Brushes.Black; WindowStartupLocation = WindowStartupLocation.Manual;
        surface = new EditorSurface(frame.Bitmap);
        canvas = new Canvas { Width = frame.Bitmap.PixelWidth, Height = frame.Bitmap.PixelHeight, ClipToBounds = true };
        canvas.Children.Add(surface);
        hint = new TextBlock { Text = "Drag to select an area  •  Esc to cancel", FontSize = 15, Foreground = Brushes.White, Background = Dashboard.Brush("#172233"), Padding = new Thickness(14, 9, 14, 9) };
        canvas.Children.Add(hint); Canvas.SetLeft(hint, 24); Canvas.SetTop(hint, 24);
        var rows = new StackPanel();
        var tools = new WrapPanel();
        foreach (var (value, label, key) in new[] { (Tool.Select,"Move","V"), (Tool.Arrow,"Arrow","A"), (Tool.Pen,"Pen","P"), (Tool.Line,"Line","L"), (Tool.Rectangle,"Box","R"), (Tool.Highlight,"Highlight","H"), (Tool.Text,"Text","T") })
        {
            var button = IconButton(value.ToString(),label,() => SetTool(value)); button.ToolTip = $"{label} ({key})";
            AutomationProperties.SetHelpText(button,value==Tool.Select?"Select an annotation to move it; drag empty space to move the crop":label);
            toolButtons[value] = button; tools.Children.Add(button);
        }
        rows.Children.Add(tools);
        rows.Children.Add(new Border{Height=1,Background=Dashboard.Brush("#2C3D4C"),Margin=new Thickness(6,8,6,8)});
        var styleRow=new WrapPanel();
        foreach(var shade in new[]{"#FF4D67","#FBBF24","#34D399","#38BDF8","#A78BFA","#FFFFFF","#172233"})
        {
            var swatch=MakeButton("",()=>SetColor(shade));swatch.Width=26;swatch.Height=26;swatch.MinHeight=26;swatch.Padding=new Thickness(0);swatch.Margin=new Thickness(4,5,4,5);swatch.Background=Dashboard.Brush(shade);swatch.ToolTip="Color "+shade;
            AutomationProperties.SetName(swatch,"Color "+shade);swatches[shade]=swatch;styleRow.Children.Add(swatch);
        }
        colorButton = MakeButton("More…", ChooseColor);colorButton.ToolTip="Choose a custom color";styleRow.Children.Add(colorButton);
        styleRow.Children.Add(new TextBlock{Text="Width",FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,4,0)});
        widthPicker = new ComboBox { Width = 62, Margin = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Line width / text size",MinHeight=30 };
        AutomationProperties.SetName(widthPicker,"Stroke width");
        var pickerTextStyle=new Style(typeof(TextBlock));pickerTextStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,Dashboard.Brush("#172233")));widthPicker.Resources.Add(typeof(TextBlock),pickerTextStyle);
        foreach (int width in new[] { 2, 3, 5, 8, 12 }) widthPicker.Items.Add(width);
        widthPicker.SelectedItem = 3; widthPicker.SelectionChanged += (_, _) => { if (!syncingStyle && widthPicker.SelectedItem is int n) { CommitText();strokeWidth = n; ApplyStyle(width:n); } };styleRow.Children.Add(widthPicker);rows.Children.Add(styleRow);
        var actions = new WrapPanel { Margin=new Thickness(0,7,0,0) };
        undoButton = IconButton("Undo","", () => Undo(false)); undoButton.ToolTip = "Undo (Ctrl+Z)"; actions.Children.Add(undoButton);
        redoButton = IconButton("Redo","", () => Undo(true)); redoButton.ToolTip = "Redo (Ctrl+Y)"; actions.Children.Add(redoButton);
        actions.Children.Add(IconButton("Crop","Reselect", Reselect));
        actions.Children.Add(IconButton("Copy","Copy", () => _ = CopyAsync()));
        var save = IconButton("Save","Save…", Save); save.Background = Dashboard.Brush("#0F766E");save.ToolTip="Save image (Ctrl+S)";actions.Children.Add(save);
        actions.Children.Add(IconButton("Close","", Close));
        rows.Children.Add(actions);
        rows.Children.Add(toolHint);
        toolbar = new Border { Child = rows, Background = Dashboard.Brush("#111E2B"),BorderBrush=Dashboard.Brush("#344554"),BorderThickness=new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(10), Visibility = Visibility.Collapsed };
        canvas.Children.Add(toolbar);
        Content = new Viewbox { Stretch = Stretch.Fill, Child = canvas };
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            hwndSource = HwndSource.FromHwnd(handle); hwndSource?.AddHook(WindowHook);
            if(!Native.SetWindowPos(handle, new IntPtr(-1), frame.Left, frame.Top, frame.Bitmap.PixelWidth, frame.Bitmap.PixelHeight, 0x0040))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Could not position the capture overlay.");
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
        var button = new Button { Content = label, Padding = new Thickness(9, 9, 9, 9), FontSize = 12, Focusable = true };
        AutomationProperties.SetName(button,label);
        button.Click += (_, _) => action(); return button;
    }
    private static Button IconButton(string icon,string label,Action action){var button=MakeButton(label,action);button.Content=EditorIcons.Label(icon,label);AutomationProperties.SetName(button,label.Length>0?label:icon);button.ToolTip=label.Length>0?label:icon;return button;}
    private Pt Position(MouseEventArgs e)
    {
        var p = e.GetPosition(surface); return new(Math.Clamp(p.X,0,surface.Width),Math.Clamp(p.Y,0,surface.Height));
    }
    private Pt InsideArea(Pt p) => new(Math.Clamp(p.X,surface.Area.X,surface.Area.Right),Math.Clamp(p.Y,surface.Area.Y,surface.Area.Bottom));
    private void SetTool(Tool value)
    {
        CommitText(); tool = value; surface.Selected = null;
        foreach (var pair in toolButtons){pair.Value.Background = Dashboard.Brush(pair.Key == value ? "#0F766E" : "#203140");pair.Value.BorderBrush=Dashboard.Brush(pair.Key==value?"#5EEAD4":"#203140");}
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
                originalMark = surface.Document.Items.Reverse().FirstOrDefault(m => MarkRenderer.HitTest(m,p));
                surface.Selected = originalMark?.Id;
                SyncStyle(originalMark);
                if (originalMark != null && e.ClickCount == 2 && originalMark.Tool == Tool.Text) { BeginText(originalMark.Points[0],originalMark); return; }
                gesture = originalMark == null ? "move-area" : "move-mark";
            }
            else if (tool == Tool.Text) { var existing=surface.Document.Items.Reverse().FirstOrDefault(m=>m.Tool==Tool.Text&&MarkRenderer.HitTest(m,p));BeginText(existing?.Points[0]??p,existing); return; }
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
        foreach(var entry in swatches){entry.Value.BorderThickness=new Thickness(entry.Key==color?2:1);entry.Value.BorderBrush=entry.Key==color?Brushes.White:Dashboard.Brush("#526272");}
        toolHint.Text=surface.Selected!=null?"Annotation selected · Drag to move · Delete to remove · F2 edits text":tool switch{Tool.Select=>"Click a mark to select it. Drag empty space to move the crop.",Tool.Text=>"Click to write. Click existing text to edit it.",Tool.Highlight=>"Drag over an area to highlight it.",_=>"Drag to draw · V selects marks · Ctrl+Z undoes the last change"};
        hint.Text = surface.HasSelection
            ? $"{Math.Ceiling(surface.Area.Width):0} × {Math.Ceiling(surface.Area.Height):0} px   •   Ctrl+C copy   •   Ctrl+S save   •   Esc cancel"
            : "Drag to select an area  •  Esc to cancel";
        if (!layout) return;
        toolbar.Visibility = surface.HasSelection && surface.Area.IsUsable ? Visibility.Visible : Visibility.Collapsed;
        if (toolbar.Visibility != Visibility.Visible) return;
        // Keep controls on the monitor containing the selection end, not in a virtual-desktop gap.
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(frame.Left+(int)surface.Area.Right-1,frame.Top+(int)surface.Area.Bottom-1));
        var bounds = screen.Bounds;
        double minX=Math.Max(0,bounds.Left-frame.Left), minY=Math.Max(0,bounds.Top-frame.Top);
        double maxX=Math.Min(surface.Width,bounds.Right-frame.Left), maxY=Math.Min(surface.Height,bounds.Bottom-frame.Top);
        toolbar.Width=Math.Max(240,Math.Min(600,maxX-minX-24));
        toolbar.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        double tw=toolbar.DesiredSize.Width, th=toolbar.DesiredSize.Height;
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
            MaxHeight=Math.Max(50,Math.Min(260,surface.Height-70)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            BorderBrush = Brushes.DeepSkyBlue, BorderThickness = new Thickness(2) };
        var textRows=new StackPanel();textRows.Children.Add(textEditor);
        var textActions=new StackPanel{Orientation=Orientation.Horizontal};textActions.Children.Add(MakeButton("Apply ↵",()=>CommitText()));textActions.Children.Add(MakeButton("Cancel",()=>CommitText(true)));
        textRows.Children.Add(textActions);textPanel=new Border{Child=textRows,Background=Dashboard.Brush("#111E2B"),CornerRadius=new CornerRadius(6)};
        canvas.Children.Add(textPanel);PositionTextPanel();textEditor.TextChanged+=(_,_)=>PositionTextPanel();
        textEditor.Focus(); textEditor.SelectAll(); hint.Text = "Enter to apply  •  Shift+Enter for a new line  •  Esc to discard";
    }
    private void PositionTextPanel()
    {
        if(textPanel==null||editingMark==null)return;
        textPanel.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));var p=editingMark.Points[0];
        Canvas.SetLeft(textPanel,Math.Clamp(p.X-5,0,Math.Max(0,surface.Width-textPanel.DesiredSize.Width)));
        Canvas.SetTop(textPanel,Math.Clamp(p.Y-5,0,Math.Max(0,surface.Height-textPanel.DesiredSize.Height)));
    }
    private void CommitText(bool cancel = false)
    {
        if (textEditor == null || editingMark == null) return;
        if (!cancel && !string.IsNullOrWhiteSpace(textEditor.Text))
        {
            var updated = editingMark with { Text = textEditor.Text };
            if (surface.Document.Items.Any(m=>m.Id==updated.Id)) surface.Document.Replace(updated); else surface.Document.Add(updated);
            surface.Selected=updated.Id;
        }
        else if(!cancel&&surface.Document.Items.Any(m=>m.Id==editingMark.Id)){surface.Document.Remove(editingMark.Id);surface.Selected=null;}
        if(textPanel!=null)canvas.Children.Remove(textPanel); textPanel=null;textEditor = null; editingMark = null; surface.Focus(); Refresh();
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
        SetColor($"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}");
    }
    private void SetColor(string value){CommitText();color=value;ApplyStyle(color:value);}
    private void SyncStyle(Mark? mark){if(mark==null)return;syncingStyle=true;try{color=mark.Color;strokeWidth=mark.Width;widthPicker.SelectedItem=(int)mark.Width;}finally{syncingStyle=false;}}
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
            if(e.Key==Key.Enter&&(Keyboard.FocusedElement is Button||e.OriginalSource is Button))return;
            if (e.Key == Key.Escape) { CommitText(true); e.Handled=true; }
            else if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { CommitText(); e.Handled=true; }
            else if(e.Key==Key.S&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control)){CommitText();Save();e.Handled=true;}
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
        else if(Keyboard.Modifiers==ModifierKeys.None&&Keyboard.FocusedElement is not ComboBox)
        {
            Tool? next=e.Key switch{Key.V=>Tool.Select,Key.A=>Tool.Arrow,Key.P=>Tool.Pen,Key.L=>Tool.Line,Key.R=>Tool.Rectangle,Key.H=>Tool.Highlight,Key.T=>Tool.Text,_=>null};
            if(next.HasValue){SetTool(next.Value);e.Handled=true;}
        }
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
