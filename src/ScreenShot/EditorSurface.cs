using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenShot.Core;

namespace ScreenShot;

internal sealed class EditorSurface : FrameworkElement
{
    internal BitmapSource Screenshot { get; }
    internal Document Document { get; } = new();
    internal Box Area { get; set; }
    internal bool HasSelection { get; set; }
    internal Mark? Preview { get; set; }
    internal Guid? Selected { get; set; }
    internal EditorSurface(BitmapSource image) { Screenshot = image; Width = image.PixelWidth; Height = image.PixelHeight; Focusable = true; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawImage(Screenshot, new Rect(0, 0, Width, Height));
        var shade = new SolidColorBrush(Color.FromArgb(145, 5, 10, 20));
        if (!HasSelection) { dc.DrawRectangle(shade, null, new Rect(0, 0, Width, Height)); return; }
        dc.DrawRectangle(shade, null, new Rect(0, 0, Width, Area.Y));
        dc.DrawRectangle(shade, null, new Rect(0, Area.Bottom, Width, Math.Max(0, Height - Area.Bottom)));
        dc.DrawRectangle(shade, null, new Rect(0, Area.Y, Area.X, Area.Height));
        dc.DrawRectangle(shade, null, new Rect(Area.Right, Area.Y, Math.Max(0, Width - Area.Right), Area.Height));
        dc.PushClip(new RectangleGeometry(MarkRenderer.Rect(Area)));
        foreach (var mark in Document.Items) if (Preview?.Id != mark.Id) MarkRenderer.Draw(dc, mark);
        if (Preview != null) MarkRenderer.Draw(dc, Preview);
        foreach (var mark in Document.Items)
        {
            if (mark.Id != Selected) continue;
            var bounds = MarkRenderer.Bounds(Preview?.Id == mark.Id ? Preview : mark);
            dc.DrawRectangle(null, new Pen(Brushes.DeepSkyBlue, 1) { DashStyle = DashStyles.Dash }, bounds);
        }
        dc.Pop();
        dc.DrawRectangle(null, new Pen(Brushes.White, 1), MarkRenderer.Rect(Area));
        foreach (var p in new[] { new Point(Area.X,Area.Y), new Point(Area.Right,Area.Y),new Point(Area.X,Area.Bottom),new Point(Area.Right,Area.Bottom),new Point(Area.X+Area.Width/2,Area.Y),new Point(Area.X+Area.Width/2,Area.Bottom),new Point(Area.X,Area.Y+Area.Height/2),new Point(Area.Right,Area.Y+Area.Height/2) })
            dc.DrawRectangle(Brushes.White, new Pen(Brushes.DodgerBlue, 1), new Rect(p.X-4,p.Y-4,8,8));
    }
}
