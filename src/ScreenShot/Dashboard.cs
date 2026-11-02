using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScreenShot;

internal sealed class Dashboard : Window
{
    private readonly App app;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = Brush("#94A3B8") };
    private readonly Button shortcutButton = new();
    private bool recording;
    internal Dashboard(App app)
    {
        this.app = app;
        Title = "ScreenShot"; Width = 760; Height = 620; MinWidth = 620; MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brush("#0B1220"); FontFamily = new FontFamily("Segoe UI");
        var root = new StackPanel { Margin = new Thickness(36, 28, 36, 24) };
        root.Children.Add(new TextBlock { Text = "SCREENSHOT  /  WINDOWS", Foreground = Brush("#38BDF8"), FontSize = 12, FontWeight = FontWeights.SemiBold });
        root.Children.Add(new TextBlock { Text = "Capture. Make your point.", FontSize = 32, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 8) });
        root.Children.Add(new TextBlock { Text = "Select any area, add a little clarity, and share it your way.", FontSize = 15, Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 0, 0, 26) });
        var card = new StackPanel { Margin = new Thickness(24) };
        card.Children.Add(new TextBlock { Text = "Your next screenshot", FontSize = 20, FontWeight = FontWeights.SemiBold });
        card.Children.Add(new TextBlock { Text = "Drag to select • Annotate in place • Copy or save", Foreground = Brush("#A9B9CD"), Margin = new Thickness(0, 8, 0, 18) });
        var capture = new Button { Content = "＋   Capture an area", Background = Brush("#0284C7"), FontSize = 17, Padding = new Thickness(22, 16, 22, 16), HorizontalAlignment = HorizontalAlignment.Left };
        capture.Click += async (_, _) => await app.CaptureAsync(); card.Children.Add(capture);
        root.Children.Add(new Border { Background = Brush("#172233"), CornerRadius = new CornerRadius(14), Child = card });
        var shortcuts = new Grid { Margin = new Thickness(0, 24, 0, 18) };
        shortcuts.ColumnDefinitions.Add(new ColumnDefinition()); shortcuts.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new StackPanel(); label.Children.Add(new TextBlock { Text = "Capture shortcut", FontSize = 15, FontWeight = FontWeights.SemiBold });
        label.Children.Add(new TextBlock { Text = "Click the shortcut, then press your preferred keys.", FontSize = 12, Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 6, 0, 0) });
        shortcuts.Children.Add(label); shortcutButton.Content = app.Preferences.ShortcutLabel;
        shortcutButton.MinWidth = 170; shortcutButton.Click += (_, _) => { recording = true; app.RecordingShortcut = true; shortcutButton.Content = "Press keys… (Esc cancels)"; shortcutButton.Focus(); };
        Grid.SetColumn(shortcutButton, 1); shortcuts.Children.Add(shortcutButton); root.Children.Add(shortcuts);
        root.Children.Add(new TextBlock { Text = "Arrow  ·  Pen  ·  Text  ·  Rectangle  ·  Highlight", Foreground = Brush("#CBD5E1"), Margin = new Thickness(0, 0, 0, 18) });
        root.Children.Add(status);
        root.Children.Add(new TextBlock { Text = "LOCAL BY DESIGN    No uploads. No accounts.\nClosing this window keeps ScreenShot in the system tray. Use tray → Exit to quit.", FontSize = 11, Foreground = Brush("#64748B"), Margin = new Thickness(0, 24, 0, 0), TextWrapping = TextWrapping.Wrap });
        Content = new ScrollViewer { Content = root, Background=Background, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        SetStatus(app.ShortcutStatus.Length == 0 ? "Ready when you are. Your captures stay on this computer." : app.ShortcutStatus);
        Closing += (_, e) => { if (!app.Exiting) { e.Cancel = true; CancelRecording(); Hide(); } };
        Deactivated += (_, _) => CancelRecording();
        PreviewKeyDown += RecordShortcut;
    }
    internal void SetStatus(string text) => status.Text = text;
    private void RecordShortcut(object sender, KeyEventArgs e)
    {
        if (!recording) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { CancelRecording(); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var modifiers = Keyboard.Modifiers;
        if (key != Key.PrintScreen && key is not (>= Key.F1 and <= Key.F24) && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
        { SetStatus("Use Print Screen, a function key, or a shortcut containing Ctrl, Alt or Windows."); return; }
        uint flags = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt)) flags |= 1;
        if (modifiers.HasFlag(ModifierKeys.Control)) flags |= 2;
        if (modifiers.HasFlag(ModifierKeys.Shift)) flags |= 4;
        if (modifiers.HasFlag(ModifierKeys.Windows)) flags |= 8;
        string label = (flags == 0 ? "" : modifiers.ToString().Replace(", ", " + ") + " + ") + (key == Key.PrintScreen ? "Print Screen" : key.ToString());
        if (!app.ChangeShortcut(flags, (uint)KeyInterop.VirtualKeyFromKey(key), label)) { SetStatus("That shortcut is already used. Try another combination. Your previous shortcut is unchanged."); return; }
        CancelRecording(); shortcutButton.Content = label; SetStatus("Shortcut saved. " + label + " captures from any app.");
    }
    private void CancelRecording() { recording=false; app.RecordingShortcut=false; shortcutButton.Content=app.Preferences.ShortcutLabel; }
    internal static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
