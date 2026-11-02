using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenShot;

internal sealed class Dashboard : Window
{
    private readonly App app;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Brush("#C3D1DF"), LineHeight = 20 };
    private readonly Button shortcutButton = new();
    private readonly Button cancelButton = new() { Content = "Cancel", Visibility = Visibility.Collapsed, Margin = new Thickness(8, 0, 0, 0) };
    private readonly TextBlock shortcutHint = new();
    private readonly TextBlock captureHint = new();
    private bool recording;

    internal Dashboard(App app)
    {
        this.app = app;
        Title = "ScreenShot"; Width = 840; Height = 760; MinWidth = 640; MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#0D1520"); FontFamily = new FontFamily("Segoe UI");
        var root = new StackPanel { Margin = new Thickness(32, 24, 32, 20) };
        var brand = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        var local = new Border { Background = Brush("#142D2D"), CornerRadius = new CornerRadius(12), Padding = new Thickness(11, 5, 11, 5), HorizontalAlignment = HorizontalAlignment.Right, Child = Text("●  On your computer", 11, "#89DCD1") };
        DockPanel.SetDock(local, Dock.Right); brand.Children.Add(local);
        brand.Children.Add(Text("▣  ScreenShot", 19, "#F1F5F9", FontWeights.SemiBold)); root.Children.Add(brand);
        root.Children.Add(Text("A clear shot. A clearer point.", 30, "#F1F5F9", FontWeights.SemiBold));
        var subtitle = Text("Capture a detail, mark what matters, and keep things moving.", 14, "#9FB0C4"); subtitle.Margin = new Thickness(0, 8, 0, 22); root.Children.Add(subtitle);

        var hero = new Grid { Margin = new Thickness(24) };
        hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var artColumn = new ColumnDefinition { Width = new GridLength(260) }; hero.ColumnDefinitions.Add(artColumn);
        var action = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        action.Children.Add(Text("READY WHEN YOU ARE", 10, "#86CFC7", FontWeights.SemiBold));
        var title = Text("Start with an area", 23, "#F1F5F9", FontWeights.SemiBold); title.Margin = new Thickness(0, 9, 0, 7); action.Children.Add(title);
        action.Children.Add(Text("Drag across any part of your screen.\nYour tools appear right beside it.", 13, "#AFC0D0"));
        var capture = new Button { Content = "＋  Capture an area", Background = Brush("#72E0CD"), Foreground = Brush("#092B2B"), FontWeight = FontWeights.SemiBold, FontSize = 15, Padding = new Thickness(19, 13, 19, 13), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 20, 0, 0) };
        capture.Click += async (_, _) => { CancelRecording(); await app.CaptureAsync(); };
        AutomationProperties.SetHelpText(capture, "Select an area of your screen to capture and annotate."); action.Children.Add(capture);
        captureHint.FontSize = 11; captureHint.Foreground = Brush("#AFC0D0"); captureHint.Margin = new Thickness(0, 9, 0, 0); captureHint.TextWrapping = TextWrapping.Wrap; action.Children.Add(captureHint); hero.Children.Add(action);
        var art = new Image { Source = PreviewIllustration(), Width = 246, Height = 190, Margin = new Thickness(14, 0, 0, 0), IsHitTestVisible = false, Focusable = false };
        AutomationProperties.SetName(art, "Illustration of a selected screen area with an arrow annotation"); Grid.SetColumn(art, 1); hero.Children.Add(art);
        root.Children.Add(new Border { Background = Brush("#172433"), BorderBrush = Brush("#2C3C4D"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Child = hero });
        root.SizeChanged += (_, _) => { bool wide = root.ActualWidth >= 690; art.Visibility = wide ? Visibility.Visible : Visibility.Collapsed; artColumn.Width = new GridLength(wide ? 260 : 0); };

        var steps = new Grid { Margin = new Thickness(0, 18, 0, 18) };
        for (int i = 0; i < 3; i++) steps.ColumnDefinitions.Add(new ColumnDefinition());
        AddStep(steps, 0, "01", "Select", "Drag to frame your shot.");
        AddStep(steps, 1, "02", "Make it clear", "Add arrows, text or highlights.");
        AddStep(steps, 2, "03", "Copy or save", "Paste it, or keep a PNG."); root.Children.Add(steps);

        var shortcutCard = new StackPanel { Margin = new Thickness(19, 16, 19, 16) };
        shortcutCard.Children.Add(Text("Your capture shortcut", 15, "#EDF3F9", FontWeights.SemiBold));
        shortcutHint.Text = "Use it from any app. Click the keys below to change it."; shortcutHint.TextWrapping = TextWrapping.Wrap; shortcutHint.FontSize = 12; shortcutHint.Foreground = Brush("#9FB0C4"); shortcutHint.Margin = new Thickness(0, 6, 0, 12); shortcutCard.Children.Add(shortcutHint);
        var shortcutRow = new WrapPanel();
        shortcutButton.Content = app.Preferences.ShortcutLabel; shortcutButton.MinWidth = 156; shortcutButton.Margin = new Thickness(0); shortcutButton.Padding = new Thickness(16, 10, 16, 10); shortcutButton.Click += (_, _) => BeginRecording();
        AutomationProperties.SetName(shortcutButton, "Change capture shortcut");
        cancelButton.Click += (_, _) => CancelRecording(true); shortcutRow.Children.Add(shortcutButton); shortcutRow.Children.Add(cancelButton); shortcutCard.Children.Add(shortcutRow);
        root.Children.Add(new Border { Background = Brush("#111E2B"), BorderBrush = Brush("#263747"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = shortcutCard });
        status.Margin = new Thickness(0, 14, 0, 0); AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite); root.Children.Add(status);
        var footer = Text("No uploads. No account needed.\nClosing this window keeps ScreenShot in the tray. To quit, right-click the tray icon → Exit.", 11, "#9AACBE"); footer.Margin = new Thickness(0, 20, 0, 0); root.Children.Add(footer);
        Content = new ScrollViewer { Content = root, Background = Background, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        UpdateShortcutLabel();
        SetStatus(app.ShortcutStatus.Length == 0 ? "Ready to capture." : app.ShortcutStatus);
        Closing += (_, e) => { if (!app.Exiting) { e.Cancel = true; CancelRecording(); Hide(); } };
        Deactivated += (_, _) => CancelRecording(true);
        PreviewKeyDown += RecordShortcut;
    }

    private static TextBlock Text(string text, double size, string color, FontWeight? weight = null) => new() { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
    private static void AddStep(Grid grid, int column, string number, string title, string detail)
    {
        var item = new StackPanel { Margin = new Thickness(column == 0 ? 0 : 13, 0, column == 2 ? 0 : 10, 0) };
        item.Children.Add(Text(number, 11, "#72D9C8", FontWeights.SemiBold));
        var heading = Text(title, 14, "#E5EDF5", FontWeights.SemiBold); heading.Margin = new Thickness(0, 5, 0, 4); item.Children.Add(heading);
        item.Children.Add(Text(detail, 12, "#9FB0C4")); Grid.SetColumn(item, column); grid.Children.Add(item);
    }
    private static DrawingImage PreviewIllustration()
    {
        var group = new DrawingGroup();
        using (var dc = group.Open())
        {
            dc.DrawRoundedRectangle(Brush("#0D1723"), new Pen(Brush("#33495D"), 1), new Rect(5, 17, 230, 158), 11, 11);
            dc.DrawLine(new Pen(Brush("#2B4052"), 1), new Point(5, 43), new Point(235, 43));
            for (int i = 0; i < 3; i++) dc.DrawEllipse(Brush(i == 0 ? "#72D9C8" : "#42596E"), null, new Point(19 + i * 11, 30), 2.5, 2.5);
            dc.DrawRoundedRectangle(Brush("#24394B"), null, new Rect(23, 59, 48, 95), 4, 4);
            dc.DrawRoundedRectangle(Brush("#304B5F"), null, new Rect(87, 64, 119, 9), 3, 3);
            dc.DrawRoundedRectangle(Brush("#213849"), null, new Rect(87, 82, 86, 6), 3, 3);
            dc.DrawRoundedRectangle(Brush("#213849"), null, new Rect(87, 96, 108, 6), 3, 3);
            dc.DrawRoundedRectangle(Brush("#254D50"), null, new Rect(87, 118, 73, 23), 5, 5);
            var selection = new Pen(Brush("#72E0CD"), 1.5) { DashStyle = new DashStyle(new double[] { 4, 3 }, 0) };
            dc.DrawRectangle(null, selection, new Rect(77, 54, 139, 100));
            foreach (var point in new[] { new Point(77,54), new Point(216,54), new Point(77,154), new Point(216,154) }) dc.DrawRoundedRectangle(Brush("#C2FFF1"), null, new Rect(point.X - 3, point.Y - 3, 6, 6), 1, 1);
            var arrow = new Pen(Brush("#FFCB82"), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawLine(arrow, new Point(201, 115), new Point(162, 132));
            dc.DrawLine(arrow, new Point(162, 132), new Point(172, 113));
            dc.DrawLine(arrow, new Point(162, 132), new Point(185, 137));
            dc.DrawRoundedRectangle(Brush("#E7F9F4"), null, new Rect(105, 164, 107, 27), 7, 7);
            dc.DrawLine(new Pen(Brush("#1B5850"), 2), new Point(120, 177), new Point(132, 177));
            dc.DrawLine(new Pen(Brush("#1B5850"), 2), new Point(147, 171), new Point(147, 182));
            dc.DrawRectangle(null, new Pen(Brush("#1B5850"), 1.5), new Rect(162, 172, 11, 9));
            dc.DrawLine(new Pen(Brush("#1B5850"), 3), new Point(188, 180), new Point(198, 172));
        }
        group.Freeze(); return new DrawingImage(group);
    }

    internal void SetStatus(string text) => status.Text = text;
    private void BeginRecording()
    {
        recording = true; app.RecordingShortcut = true; shortcutButton.Content = "Press your keys…"; cancelButton.Visibility = Visibility.Visible;
        shortcutHint.Text = "Press a new shortcut. Esc or Cancel keeps your current keys.";
        SetStatus("Listening for your shortcut…"); shortcutButton.Focus();
    }
    private void UpdateShortcutLabel()
    {
        shortcutButton.Content = app.Preferences.ShortcutLabel;
        captureHint.Text = "or press " + app.Preferences.ShortcutLabel;
    }
    private void RecordShortcut(object sender, KeyEventArgs e)
    {
        if (!recording) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0) return;
        e.Handled = true;
        if (key == Key.Escape) { CancelRecording(true); return; }
        // Keyboard activation must still let the explicit Cancel button work.
        if (cancelButton.IsKeyboardFocused && key is Key.Space or Key.Enter) { CancelRecording(true); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var modifiers = Keyboard.Modifiers;
        if (key != Key.PrintScreen && key is not (>= Key.F1 and <= Key.F24) && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
        { SetStatus("Use Print Screen, a function key, or include Ctrl, Alt or Windows."); return; }
        uint flags = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) flags |= 1;
        if (modifiers.HasFlag(ModifierKeys.Control)) flags |= 2;
        if (modifiers.HasFlag(ModifierKeys.Shift)) flags |= 4;
        if (modifiers.HasFlag(ModifierKeys.Windows)) flags |= 8;
        string label = (flags == 0 ? "" : modifiers.ToString().Replace(", ", " + ") + " + ") + (key == Key.PrintScreen ? "Print Screen" : key.ToString());
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        bool unchanged = app.Preferences.Modifiers == flags && app.Preferences.Key == virtualKey;
        if (!app.ChangeShortcut(flags, virtualKey, label)) { SetStatus("Those keys are in use. Try another shortcut, or cancel to keep your previous keys."); return; }
        CancelRecording();
        SetStatus((unchanged ? "Shortcut unchanged. " : "Shortcut ready. ") + app.Preferences.ShortcutLabel + " captures from any app.");
    }
    private void CancelRecording(bool announce = false)
    {
        bool wasRecording = recording; recording = false; app.RecordingShortcut = false; cancelButton.Visibility = Visibility.Collapsed;
        shortcutHint.Text = "Use it from any app. Click the keys below to change it."; UpdateShortcutLabel();
        if (announce && wasRecording) SetStatus("Shortcut change canceled. " + app.Preferences.ShortcutLabel + " is still set.");
    }
    internal static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
