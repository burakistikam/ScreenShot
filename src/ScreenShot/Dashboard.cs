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
    private readonly TextBlock shortcutLabel = new();
    private bool recording;

    internal Dashboard(App app)
    {
        this.app = app;
        Title = "ScreenShot"; Width = 480; Height = 360; MinWidth = 400; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#0D1520"); FontFamily = new FontFamily("Segoe UI");
        var root = new StackPanel { Margin = new Thickness(22) };
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var version = Text("v0.3.5", 11, "#9AACBE"); version.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(version, Dock.Right); heading.Children.Add(version);
        heading.Children.Add(Text("ScreenShot", 24, "#F1F5F9", FontWeights.SemiBold)); root.Children.Add(heading);
        var subtitle = Text("Capture and annotate any part of your screen.", 13, "#9FB0C4"); subtitle.Margin = new Thickness(0, 0, 0, 14); root.Children.Add(subtitle);

        var capture = new Button { Content = "Capture screenshot", Background = Brush("#72E0CD"), Foreground = Brush("#092B2B"), FontWeight = FontWeights.SemiBold, FontSize = 15, Padding = new Thickness(19, 13, 19, 13), HorizontalAlignment = HorizontalAlignment.Stretch };
        capture.Click += async (_, _) => { CancelRecording(); await app.CaptureAsync(); };
        AutomationProperties.SetHelpText(capture, "Select an area of your screen to capture and annotate."); root.Children.Add(capture);

        var shortcutCard = new StackPanel { Margin = new Thickness(14, 11, 14, 11) };
        shortcutCard.Children.Add(Text("Shortcut", 13, "#9FB0C4", FontWeights.SemiBold));
        shortcutHint.Text = "Press a key or combination. Esc cancels."; shortcutHint.TextWrapping = TextWrapping.Wrap; shortcutHint.FontSize = 12; shortcutHint.Foreground = Brush("#9FB0C4"); shortcutHint.Margin = new Thickness(0, 6, 0, 0); shortcutHint.Visibility = Visibility.Collapsed;
        shortcutLabel.TextWrapping = TextWrapping.Wrap; shortcutLabel.FontSize = 14; shortcutLabel.FontWeight = FontWeights.SemiBold; shortcutLabel.Foreground = Brush("#EDF3F9"); shortcutLabel.VerticalAlignment = VerticalAlignment.Center; shortcutLabel.Margin = new Thickness(0, 0, 12, 0);
        var shortcutRow = new DockPanel();
        shortcutButton.Content = "Change"; shortcutButton.MinWidth = 78; shortcutButton.Margin = new Thickness(0); shortcutButton.Padding = new Thickness(13, 7, 13, 7); shortcutButton.Click += (_, _) => BeginRecording();
        AutomationProperties.SetName(shortcutButton, "Change capture shortcut");
        cancelButton.Click += (_, _) => CancelRecording(true); DockPanel.SetDock(shortcutButton, Dock.Right); DockPanel.SetDock(cancelButton, Dock.Right); shortcutRow.Children.Add(shortcutButton); shortcutRow.Children.Add(cancelButton); shortcutRow.Children.Add(shortcutLabel); shortcutCard.Children.Add(shortcutRow); shortcutCard.Children.Add(shortcutHint);
        root.Children.Add(new Border { Background = Brush("#111E2B"), BorderBrush = Brush("#263747"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Child = shortcutCard });
        status.Margin = new Thickness(0, 11, 0, 0); AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite); root.Children.Add(status);
        var footer = Text("Closing keeps ScreenShot in the tray.", 11, "#9AACBE"); footer.Margin = new Thickness(0, 10, 0, 0); root.Children.Add(footer);
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
        recording = true; app.RecordingShortcut = true; shortcutButton.Content = "Press a key…"; cancelButton.Visibility = Visibility.Visible; shortcutHint.Visibility = Visibility.Visible;
        SetStatus("Listening for your shortcut…"); shortcutButton.Focus();
    }
    private void UpdateShortcutLabel()
    {
        shortcutButton.Content = "Change";
        shortcutLabel.Text = app.Preferences.ShortcutLabel;
    }
    private void RecordShortcut(object sender, KeyEventArgs e)
    {
        if (!recording) return;
        if (e.IsRepeat) { e.Handled = true; return; }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0) return;
        e.Handled = true;
        if (key == Key.Escape) { CancelRecording(true); return; }
        // Keyboard activation must still let the explicit Cancel button work.
        if (cancelButton.IsKeyboardFocused && key is Key.Space or Key.Enter) { CancelRecording(true); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var modifiers = Keyboard.Modifiers;
        if (!IsSupportedShortcut(key, modifiers))
        { SetStatus("Press a key, optionally with Ctrl, Alt or Shift. Esc cancels; Tab moves between controls."); return; }
        uint flags = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) flags |= 1;
        if (modifiers.HasFlag(ModifierKeys.Control)) flags |= 2;
        if (modifiers.HasFlag(ModifierKeys.Shift)) flags |= 4;
        if (modifiers.HasFlag(ModifierKeys.Windows)) flags |= 8;
        string label = (flags == 0 ? "" : modifiers.ToString().Replace(", ", " + ") + " + ") + (key == Key.PrintScreen ? "Print Screen" : key == Key.Scroll ? "Scroll Lock" : key.ToString());
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        bool unchanged = app.Preferences.Modifiers == flags && app.Preferences.Key == virtualKey;
        if (!app.ChangeShortcut(flags, virtualKey, label)) { SetStatus("Those keys are in use. Try another shortcut, or cancel to keep your previous keys."); return; }
        CancelRecording();
        string typingWarning = flags == 0 && IsLetterOrDigit(key) ? " This key may trigger while typing." : "";
        SetStatus((unchanged ? "Shortcut unchanged. " : "Shortcut ready. ") + app.Preferences.ShortcutLabel + " captures from any app." + typingWarning);
    }
    internal static bool IsSupportedShortcut(Key key, ModifierKeys modifiers) =>
        key is not (Key.None or Key.Escape or Key.System or Key.ImeProcessed or Key.DeadCharProcessed
            or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) &&
        !(key == Key.Tab && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0) &&
        KeyInterop.VirtualKeyFromKey(key) != 0;

    private static bool IsLetterOrDigit(Key key)
    {
        int value = (int)key;
        return value >= (int)Key.A && value <= (int)Key.Z ||
               value >= (int)Key.D0 && value <= (int)Key.D9 ||
               value >= (int)Key.NumPad0 && value <= (int)Key.NumPad9;
    }

    internal void ConfirmCurrentShortcut()
    {
        if (!recording) return;
        CancelRecording();
        SetStatus("Shortcut unchanged. " + app.Preferences.ShortcutLabel + " captures from any app.");
    }

    private void CancelRecording(bool announce = false)
    {
        bool wasRecording = recording; recording = false; app.RecordingShortcut = false; cancelButton.Visibility = Visibility.Collapsed;
        shortcutHint.Visibility = Visibility.Collapsed; UpdateShortcutLabel();
        if (announce && wasRecording) SetStatus("Shortcut change canceled. " + app.Preferences.ShortcutLabel + " is still set.");
    }
    internal static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
