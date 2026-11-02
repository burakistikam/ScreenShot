using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ScreenShot;
internal static class EditorIcons
{
    internal static FrameworkElement Label(string name,string text)
    {
        string data=name switch {
            "Select"=>"M3,2 L3,18 L7,14 L11,21 L14,19 L10,12 L16,12 Z",
            "Arrow"=>"M3,20 L20,3 M10,3 L20,3 L20,13",
            "Pen"=>"M4,17 L3,21 L8,20 L21,7 L17,3 Z M14,6 L18,10",
            "Line"=>"M4,20 L20,4",
            "Rectangle"=>"M3,5 L21,5 L21,19 L3,19 Z",
            "Highlight"=>"M5,16 L14,3 L21,8 L12,21 Z M3,22 L18,22",
            "Text"=>"M4,4 L20,4 M12,4 L12,21 M8,21 L16,21",
            "Undo"=>"M8,4 L3,9 L8,14 M3,9 L15,9 C23,9 23,21 15,21",
            "Redo"=>"M16,4 L21,9 L16,14 M21,9 L9,9 C1,9 1,21 9,21",
            "Copy"=>"M8,8 L21,8 L21,22 L8,22 Z M16,5 L16,2 L2,2 L2,16 L5,16",
            "Save"=>"M3,3 L18,3 L22,7 L22,22 L3,22 Z M8,3 L8,10 L17,10 L17,3 M8,22 L8,15 L17,15 L17,22",
            "Crop"=>"M5,1 L5,19 L23,19 M1,5 L19,5 L19,23",
            "Close"=>"M5,5 L19,19 M19,5 L5,19",
            _=>"M12,3 L12,21 M3,12 L21,12"
        };
        var icon=new Path{Data=Geometry.Parse(data),Width=18,Height=18,Stretch=Stretch.Uniform,StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Margin=new Thickness(0,0,text.Length>0?7:0,0)};
        icon.SetBinding(Shape.StrokeProperty,new Binding("Foreground"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Button),1)});
        var panel=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};panel.Children.Add(icon);
        if(text.Length>0)panel.Children.Add(new TextBlock{Text=text,VerticalAlignment=VerticalAlignment.Center,Foreground=Brushes.White,FontSize=12});
        return panel;
    }
}
