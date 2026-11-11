using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenShot;

internal static class ImageFile
{
    internal const string Filter = "PNG image|*.png|JPEG image|*.jpg;*.jpeg|PDF document|*.pdf";

    internal static void Save(BitmapSource image, string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".pdf"))
            throw new NotSupportedException("Choose a PNG, JPEG or PDF file extension.");
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write);
        if (extension == ".pdf") { WritePdf(image, output); return; }
        BitmapEncoder encoder = extension is ".jpg" or ".jpeg"
            ? new JpegBitmapEncoder { QualityLevel = 95 } : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(output);
    }

    // A single lossless RGB image on a borderless page. No printer or runtime package required.
    private static void WritePdf(BitmapSource image, Stream output)
    {
        var rgb = new FormatConvertedBitmap(image, PixelFormats.Rgb24, null, 0);
        int stride = checked(rgb.PixelWidth * 3);
        var row = new byte[stride];
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            for (int y = 0; y < rgb.PixelHeight; y++)
            {
                rgb.CopyPixels(new System.Windows.Int32Rect(0, y, rgb.PixelWidth, 1), row, stride, 0);
                zlib.Write(row);
            }

        // Screenshot exports use 96 DPI. Keep the page within PDF's 14,400-point limit
        // even for exceptionally large virtual desktops, without resampling the pixels.
        double scale = Math.Min(72.0 / 96, 14400.0 / Math.Max(rgb.PixelWidth, rgb.PixelHeight));
        string width = (rgb.PixelWidth * scale).ToString("0.######", CultureInfo.InvariantCulture);
        string height = (rgb.PixelHeight * scale).ToString("0.######", CultureInfo.InvariantCulture);
        var offsets = new long[6];
        void Text(string value) => output.Write(Encoding.ASCII.GetBytes(value));
        void Object(int id, string body)
        {
            offsets[id] = output.Position;
            Text($"{id} 0 obj\n{body}\nendobj\n");
        }
        Text("%PDF-1.4\n");
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>");
        offsets[4] = output.Position;
        Text($"4 0 obj\n<< /Type /XObject /Subtype /Image /Width {rgb.PixelWidth} /Height {rgb.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /Length {compressed.Length} >>\nstream\n");
        compressed.Position = 0;
        compressed.CopyTo(output);
        Text("\nendstream\nendobj\n");
        string commands = $"q\n{width} 0 0 {height} 0 0 cm\n/Im0 Do\nQ\n";
        Object(5, $"<< /Length {Encoding.ASCII.GetByteCount(commands)} >>\nstream\n{commands}endstream");
        long xref = output.Position;
        Text("xref\n0 6\n0000000000 65535 f \n");
        for (int i = 1; i < offsets.Length; i++)
            Text(offsets[i].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        Text($"trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
    }
}
