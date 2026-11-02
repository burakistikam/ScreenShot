using System;
using System.IO;

namespace ScreenShot;
internal static class CaptureTrace
{
    internal static void Write(string message)
    {
        try {
            var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScreenShot");
            Directory.CreateDirectory(folder);
            var file=Path.Combine(folder,"capture.log");
            if(File.Exists(file)&&new FileInfo(file).Length>128*1024)File.WriteAllText(file,"");
            File.AppendAllText(file,$"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
}
