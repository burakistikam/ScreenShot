using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ScreenShot;

public partial class App : Application
{
    private Mutex? instance;
    private EventWaitHandle? activation;
    private RegisteredWaitHandle? activationWait;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private GlobalShortcut? shortcut;
    private Dashboard? dashboard;
    private OverlayWindow? overlay;
    private bool capturing;
    internal Settings Preferences { get; private set; } = new();
    internal string ShortcutStatus { get; private set; } = "";
    internal bool Exiting { get; private set; }
    internal bool RecordingShortcut { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        activation=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\ScreenShot.Activate.v1");
        instance = new Mutex(true, "Local\\ScreenShot.Desktop.v1", out bool first);
        if (!first) { activation.Set(); Shutdown(); return; }
        activationWait=ThreadPool.RegisterWaitForSingleObject(activation,(_,_)=>Dispatcher.BeginInvoke(ShowDashboard),null,Timeout.Infinite,false);
        Preferences = Settings.Load();
        shortcut = new GlobalShortcut();
        shortcut.Pressed += HandleShortcutPressed;
        if (!shortcut.TrySet(Preferences.Modifiers, Preferences.Key))
        {
            if (shortcut.TrySet(6, 0x53))
            {
                Preferences.Modifiers = 6; Preferences.Key = 0x53; Preferences.ShortcutLabel = "Ctrl + Shift + S";
                ShortcutStatus = "Print Screen or your saved shortcut is busy. Using Ctrl + Shift + S for this session.";
            }
            else ShortcutStatus = "Shortcut is in use by another app. Choose a different shortcut below; Capture still works.";
        }
        using (var iconStream=GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream)
            trayIcon=new System.Drawing.Icon(iconStream);
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "ScreenShot — capture & annotate", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Capture area", null, (_, _) => Dispatcher.InvokeAsync(() => _ = CaptureAsync()));
        menu.Items.Add("Open ScreenShot", null, (_, _) => Dispatcher.InvokeAsync(ShowDashboard));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Dispatcher.InvokeAsync(Quit));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.InvokeAsync(ShowDashboard);
        ShowDashboard();
    }

    internal void HandleShortcutPressed()
    {
        // RegisterHotKey consumes the current binding before WPF sees KeyDown.
        // Re-recording that same binding must finish the recorder, not disappear.
        if (RecordingShortcut) dashboard?.ConfirmCurrentShortcut();
        else _ = CaptureAsync();
    }

    internal void ShowDashboard()
    {
        if(Exiting)return;
        if (overlay != null) { overlay.Activate(); return; }
        dashboard ??= new Dashboard(this);
        dashboard.Show(); dashboard.WindowState = WindowState.Normal; dashboard.Activate();
    }

    internal async Task CaptureAsync()
    {
        CaptureTrace.Write($"Capture requested; busy={capturing}; overlay={overlay!=null}");
        if(capturing)return;
        if(overlay!=null){overlay.Show();overlay.Activate();return;}
        capturing = true;
        bool restoreDashboard = dashboard?.IsVisible == true;
        try
        {
            dashboard?.Hide();
            await Task.Delay(180);
            CaptureTrace.Write("Starting desktop capture");
            var frame = Native.Capture();
            CaptureTrace.Write($"Captured {frame.Bitmap.PixelWidth}x{frame.Bitmap.PixelHeight} at {frame.Left},{frame.Top}");
            overlay = new OverlayWindow(frame, Preferences);
            overlay.Completed += message => { dashboard?.SetStatus(message); };
            overlay.Closed += (_, _) => { overlay = null; if (restoreDashboard) ShowDashboard(); };
            overlay.Show(); overlay.Activate();
            CaptureTrace.Write($"Overlay shown; visible={overlay.IsVisible}; active={overlay.IsActive}; size={overlay.ActualWidth}x{overlay.ActualHeight}");
        }
        catch (Exception ex)
        {
            CaptureTrace.Write("Capture failed: "+ex);
            var failedOverlay = overlay; overlay = null; failedOverlay?.Close(); ShowDashboard();
            MessageBox.Show("Could not capture the desktop.\n" + ex.Message, "ScreenShot", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { capturing = false; }
    }

    internal bool ChangeShortcut(uint modifiers, uint key, string label)
    {
        if (Preferences.Modifiers == modifiers && Preferences.Key == key && ShortcutStatus.Length == 0) return true;
        if (shortcut?.TrySet(modifiers, key) != true) return false;
        Preferences.Modifiers = modifiers; Preferences.Key = key; Preferences.ShortcutLabel = label;
        ShortcutStatus = "";
        try { Preferences.Save(); }
        catch (Exception ex) { MessageBox.Show("Shortcut changed for this session, but settings could not be saved.\n" + ex.Message, "ScreenShot"); }
        return true;
    }

    internal void Quit() { Exiting = true; overlay?.Close(); Shutdown(); }
    protected override void OnExit(ExitEventArgs e)
    {
        activationWait?.Unregister(null);activation?.Dispose();
        shortcut?.Dispose();
        if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        trayIcon?.Dispose();
        instance?.Dispose(); base.OnExit(e);
    }
}
