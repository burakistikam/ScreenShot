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
        Title = "ScreenShot"; Width = 520; Height = 430; MinWidth = 440; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#0D1520"); FontFamily = new FontFamily("Segoe UI");
        var root = new StackPanel { Margin = new Thickness(24) };
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
        var version = Text("v0.3.0", 11, "#9AACBE"); version.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(version, Dock.Right); heading.Children.Add(version);
        heading.Children.Add(Text("ScreenShot", 24, "#F1F5F9", FontWeights.SemiBold)); root.Children.Add(heading);
        var subtitle = Text("Select an area, annotate, then copy or save.", 13, "#9FB0C4"); subtitle.Margin = new Thickness(0, 0, 0, 18); root.Children.Add(subtitle);

        var capture = new Button { Content = "Capture screenshot", Background = Brush("#72E0CD"), Foreground = Brush("#092B2B"), FontWeight = FontWeights.SemiBold, FontSize = 15, Padding = new Thickness(19, 13, 19, 13), HorizontalAlignment = HorizontalAlignment.Stretch };
        capture.Click += async (_, _) => { CancelRecording(); await app.CaptureAsync(); };
        AutomationProperties.SetHelpText(capture, "Select an area of your screen to capture and annotate."); root.Children.Add(capture);
        captureHint.FontSize = 11; captureHint.Foreground = Brush("#AFC0D0"); captureHint.Margin = new Thickness(0, 8, 0, 18); captureHint.TextWrapping = TextWrapping.Wrap; root.Children.Add(captureHint);

        var shortcutCard = new StackPanel { Margin = new Thickness(19, 16, 19, 16) };
        shortcutCard.Children.Add(Text("Shortcut", 15, "#EDF3F9", FontWeights.SemiBold));
        shortcutHint.Text = "Use it from any app. Click the keys below to change it."; shortcutHint.TextWrapping = TextWrapping.Wrap; shortcutHint.FontSize = 12; shortcutHint.Foreground = Brush("#9FB0C4"); shortcutHint.Margin = new Thickness(0, 6, 0, 12); shortcutCard.Children.Add(shortcutHint);
        var shortcutRow = new WrapPanel();
        shortcutButton.Content = app.Preferences.ShortcutLabel; shortcutButton.MinWidth = 156; shortcutButton.Margin = new Thickness(0); shortcutButton.Padding = new Thickness(16, 10, 16, 10); shortcutButton.Click += (_, _) => BeginRecording();
        AutomationProperties.SetName(shortcutButton, "Change capture shortcut");
        cancelButton.Click += (_, _) => CancelRecording(true); shortcutRow.Children.Add(shortcutButton); shortcutRow.Children.Add(cancelButton); shortcutCard.Children.Add(shortcutRow);
        root.Children.Add(new Border { Background = Brush("#111E2B"), BorderBrush = Brush("#263747"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = shortcutCard });
        status.Margin = new Thickness(0, 14, 0, 0); AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite); root.Children.Add(status);
        var footer = Text("Runs in the tray when closed. Right-click the tray icon to quit.", 11, "#9AACBE"); footer.Margin = new Thickness(0, 14, 0, 0); root.Children.Add(footer);
        Content = new ScrollViewer { Content = root, Background = Background, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        UpdateShortcutLabel();
        SetStatus(app.ShortcutStatus.Length == 0 ? "Ready to capture." : app.ShortcutStatus);
        Closing += (_, e) => { if (!app.Exiting) { e.Cancel = true; CancelRecording(); Hide(); } };
        Deactivated += (_, _) => CancelRecording(true);
        PreviewKeyDown += RecordShortcut;
    }

    private static TextBlock Text(string text, double size, string color, FontWeight? weight = null) => new() { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
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
