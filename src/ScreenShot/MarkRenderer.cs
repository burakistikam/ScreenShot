using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenShot.Core;

namespace ScreenShot;

internal static class MarkRenderer
{
    internal static bool HitTest(Mark mark, Pt point)
    {
        if(mark.Points.Count==0)return false;
        if(mark.Tool==Tool.Text)return Bounds(mark).Contains(Point(point));
        var a=Point(mark.Points[0]);var b=Point(mark.Points[^1]);var p=Point(point);
        double tolerance=Math.Max(6,mark.Width/2+3);
        if(mark.Tool==Tool.Highlight)return new Rect(a,b).Contains(p);
        if(mark.Tool==Tool.Rectangle){var r=new Rect(a,b);return Near(p,r.TopLeft,r.TopRight,tolerance)||Near(p,r.TopRight,r.BottomRight,tolerance)||Near(p,r.BottomRight,r.BottomLeft,tolerance)||Near(p,r.BottomLeft,r.TopLeft,tolerance);}
        for(int i=1;i<mark.Points.Count;i++)if(Near(p,Point(mark.Points[i-1]),Point(mark.Points[i]),tolerance))return true;
        if(mark.Points.Count==1)return (p-a).Length<=tolerance;
        if(mark.Tool==Tool.Arrow){var v=a-b;if(v.Length>0.01){v.Normalize();var side=new Vector(-v.Y,v.X);double length=Math.Max(12,mark.Width*4);return Near(p,b,b+v*length+side*length*0.5,tolerance)||Near(p,b,b+v*length-side*length*0.5,tolerance);}}
        return false;
    }
    private static bool Near(Point p,Point a,Point b,double tolerance){var v=b-a;double t=v.LengthSquared<0.001?0:Math.Clamp(Vector.Multiply(p-a,v)/v.LengthSquared,0,1);return (p-(a+v*t)).Length<=tolerance;}
    internal static Point Point(Pt p) => new(p.X, p.Y);
    internal static Rect Rect(Box b) => new(b.X, b.Y, b.Width, b.Height);
    internal static FormattedText Text(Mark m)
    {
        var text = new FormattedText(m.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 18 + m.Width * 3, Dashboard.Brush(m.Color), 1);
        text.Trimming = TextTrimming.None;
        if (m.TextWidth > 0) text.MaxTextWidth = m.TextWidth;
        return text;
    }
    internal static Rect Bounds(Mark m)
    {
        if (m.Points.Count == 0) return System.Windows.Rect.Empty;
        if (m.Tool == Tool.Text) { var t = Text(m); double width=m.TextWidth>0?Math.Min(m.TextWidth,t.WidthIncludingTrailingWhitespace):t.WidthIncludingTrailingWhitespace; return new Rect(m.Points[0].X, m.Points[0].Y, Math.Max(8,width), Math.Max(8,t.Height)); }
        var rect = new Rect(new Point(m.Points.Min(p => p.X), m.Points.Min(p => p.Y)), new Point(m.Points.Max(p => p.X), m.Points.Max(p => p.Y)));
        rect.Inflate(Math.Max(6, m.Width), Math.Max(6, m.Width)); return rect;
    }
    internal static void Draw(DrawingContext dc, Mark m)
    {
        if (m.Points.Count == 0) return;
        var brush = Dashboard.Brush(m.Color);
        var pen = new Pen(brush, m.Width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        var a = Point(m.Points[0]); var b = Point(m.Points[^1]);
        switch (m.Tool)
        {
            case Tool.Text: dc.DrawText(Text(m), a); break;
            case Tool.Rectangle: dc.DrawRectangle(null, pen, new Rect(a, b)); break;
            case Tool.Highlight:
                dc.PushOpacity(0.32); dc.DrawRectangle(brush, null, new Rect(a, b)); dc.Pop(); break;
            case Tool.Line: dc.DrawLine(pen, a, b); break;
            case Tool.Arrow:
                dc.DrawLine(pen, a, b);
                var vector = a - b;
                if (vector.Length > 0.01)
                {
                    vector.Normalize(); var side = new Vector(-vector.Y, vector.X); double length = Math.Max(12, m.Width * 4);
                    dc.DrawLine(pen, b, b + vector * length + side * length * 0.5);
                    dc.DrawLine(pen, b, b + vector * length - side * length * 0.5);
                }
                break;
            case Tool.Pen:
                if (m.Points.Count == 1) { dc.DrawEllipse(brush, null, a, m.Width / 2, m.Width / 2); break; }
                var path = new StreamGeometry();
                using (var ctx = path.Open()) { ctx.BeginFigure(a, false, false); ctx.PolyLineTo(m.Points.Skip(1).Select(Point).ToArray(), true, true); }
                dc.DrawGeometry(null, pen, path); break;
        }
    }
    internal static BitmapSource Export(BitmapSource background, Box area, IEnumerable<Mark> marks)
    {
        int left = Math.Clamp((int)Math.Floor(area.X), 0, background.PixelWidth - 1);
        int top = Math.Clamp((int)Math.Floor(area.Y), 0, background.PixelHeight - 1);
        int right = Math.Clamp((int)Math.Ceiling(area.Right), left + 1, background.PixelWidth);
        int bottom = Math.Clamp((int)Math.Ceiling(area.Bottom), top + 1, background.PixelHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(-left, -top));
            dc.DrawImage(background, new Rect(0, 0, background.PixelWidth, background.PixelHeight));
            dc.PushClip(new RectangleGeometry(Rect(area)));
            foreach (var mark in marks) Draw(dc, mark);
            dc.Pop(); dc.Pop();
        }
        var image = new RenderTargetBitmap(right-left, bottom-top, 96, 96, PixelFormats.Pbgra32);
        image.Render(visual); image.Freeze(); return image;
    }
}
