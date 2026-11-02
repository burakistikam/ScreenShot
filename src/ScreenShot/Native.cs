using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ScreenShot;

internal static class Native
{
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    internal static CaptureFrame Capture()
    {
        var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            DwmFlush();
            int x = GetSystemMetrics(76), y = GetSystemMetrics(77), w = GetSystemMetrics(78), h = GetSystemMetrics(79);
            if (w <= 0 || h <= 0) throw new InvalidOperationException("No desktop is available to capture.");
            using var bitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap)) g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h), CopyPixelOperation.SourceCopy);
            var handle = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return new(source, x, y);
            }
            finally { DeleteObject(handle); }
        }
        finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
    }
}

internal sealed record CaptureFrame(BitmapSource Bitmap, int Left, int Top);

internal sealed class GlobalShortcut : IDisposable
{
    private readonly HwndSource source;
    private int activeId;
    private uint activeModifiers,activeKey;
    internal event Action? Pressed;
    internal GlobalShortcut()
    {
        source = new HwndSource(new HwndSourceParameters("ScreenShot shortcut") { ParentWindow = new IntPtr(-3) });
        source.AddHook(Hook);
    }
    internal bool TrySet(uint modifiers, uint key)
    {
        if(activeId!=0&&activeModifiers==modifiers&&activeKey==key)return true;
        int nextId = activeId == 1 ? 2 : 1;
        // Register the replacement first, preserving the old binding on conflict.
        if (!Native.RegisterHotKey(source.Handle, nextId, modifiers | 0x4000, key)) return false;
        if (activeId != 0) Native.UnregisterHotKey(source.Handle, activeId);
        activeId = nextId;
        activeModifiers=modifiers;activeKey=key;
        return true;
    }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x0312 && w.ToInt32() == activeId) { handled = true; Pressed?.Invoke(); }
        return IntPtr.Zero;
    }
    public void Dispose()
    {
        if (activeId != 0) Native.UnregisterHotKey(source.Handle, activeId);
        source.RemoveHook(Hook); source.Dispose();
    }
}
