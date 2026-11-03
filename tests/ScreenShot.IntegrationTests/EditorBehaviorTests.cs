using System;
using System.Reflection;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Automation;
using System.Collections.Generic;
using ScreenShot;
using ScreenShot.Core;

internal static class EditorBehaviorTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    internal static void Run(Action<string,Action> check,BitmapSource source)
    {
        check("contextual style controls expose text pixels and preserve annotation color",()=>{
            var window=new OverlayWindow(new CaptureFrame(source,0,0),new Settings());
            try {
                var surface=Field<EditorSurface>(window,"surface");surface.Area=new Box(0,0,200,140);surface.HasSelection=true;
                Call(window,"SetTool",Tool.Select);
                Require(Field<WrapPanel>(window,"styleRow").Visibility==Visibility.Collapsed,"Move without selection shows irrelevant styles");
                Call(window,"SetTool",Tool.Highlight);
                Require(Field<ComboBox>(window,"widthPicker").Visibility==Visibility.Collapsed,"Highlight exposes unused width");
                Call(window,"SetTool",Tool.Text);
                var picker=Field<ComboBox>(window,"widthPicker");
                Require(Field<TextBlock>(window,"sizeLabel").Text=="Font size","Text shows stroke label");
                Require(((TextBlock)((ComboBoxItem)picker.SelectedItem).Content).Text=="27 px","Actual font size not shown");
                var mark=new Mark(Guid.NewGuid(),Tool.Text,new[]{new Pt(10,10)},"#38BDF8",3,"Text",100);surface.Document.Add(mark);surface.Selected=mark.Id;
                picker.SelectedValue=5.0;
                Require(surface.Document.Items[0].Width==5&&surface.Document.Items[0].Color=="#38BDF8","Size edit changed unrelated style");
                Require(((TextBlock)((ComboBoxItem)picker.SelectedItem).Content).Text=="33 px","Font size label does not match renderer");
                Call(window,"SetTool",Tool.Pen);
                Require(Field<TextBlock>(window,"sizeLabel").Text=="Thickness","Pen kept text label");
                var buttons=Field<Dictionary<Tool,SelectionButton>>(window,"toolButtons");
                var peer=UIElementAutomationPeer.CreatePeerForElement(buttons[Tool.Pen]);
                Require(((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).ToggleState==ToggleState.On,"Selected tool not exposed");
                Require(!buttons[Tool.Text].IsSelected,"Old tool remains selected");
                var existing=mark with { Width=8,Color="#A78BFA" };
                Call(window,"BeginText",existing.Points[0],existing);
                Require(((TextBlock)((ComboBoxItem)picker.SelectedItem).Content).Text=="42 px","Direct text edit shows previous tool size");
                Require(Field<string>(window,"color")==existing.Color,"Direct text edit shows previous tool color");
                Require(Field<Dictionary<string,SelectionButton>>(window,"swatches")[existing.Color].IsSelected,"Direct text edit does not update selected swatch");
                Call(window,"CommitText",true);
            } finally { window.Close(); }
        });
        check("single keys and combinations are accepted while recorder control keys stay reserved",()=>{
            Require(Dashboard.IsSupportedShortcut(Key.Scroll,ModifierKeys.None),"Scroll Lock rejected");
            Require(Dashboard.IsSupportedShortcut(Key.PrintScreen,ModifierKeys.None),"Print Screen rejected");
            Require(Dashboard.IsSupportedShortcut(Key.F12,ModifierKeys.None),"Function key rejected");
            Require(Dashboard.IsSupportedShortcut(Key.O,ModifierKeys.None),"Single key rejected");
            Require(Dashboard.IsSupportedShortcut(Key.O,ModifierKeys.Shift),"Shift key combination rejected");
            Require(Dashboard.IsSupportedShortcut(Key.NumPad1,ModifierKeys.None),"Numpad key rejected");
            Require(!Dashboard.IsSupportedShortcut(Key.Escape,ModifierKeys.None),"Cancel key accepted");
            Require(!Dashboard.IsSupportedShortcut(Key.Tab,ModifierKeys.Shift),"Navigation key accepted");
            Require(!Dashboard.IsSupportedShortcut(Key.LeftCtrl,ModifierKeys.Control),"Modifier-only accepted");
            Require(Dashboard.IsSupportedShortcut(Key.O,ModifierKeys.Control|ModifierKeys.Alt),"Modified key rejected");
        });
        check("move tool does not select empty interior of outlined shapes",()=>{
            var mark=new Mark(Guid.NewGuid(),Tool.Rectangle,new[]{new Pt(10,10),new Pt(100,100)},"#FFFFFF",3,"");
            Require(!MarkRenderer.HitTest(mark,new Pt(50,50)),"Empty rectangle center selected the outline");
            Require(MarkRenderer.HitTest(mark,new Pt(10,50)),"Rectangle edge missed");
            mark=mark with{Tool=Tool.Line};Require(!MarkRenderer.HitTest(mark,new Pt(20,80)),"Empty line bounds selected the line");
            Require(MarkRenderer.HitTest(mark,new Pt(50,50)),"Line missed");
        });
        check("clearing existing text removes it and undo restores it",()=>{
            var window=new OverlayWindow(new CaptureFrame(source,0,0),new Settings());
            try{
                var surface=Field<EditorSurface>(window,"surface");surface.Area=new Box(0,0,200,140);surface.HasSelection=true;
                var mark=new Mark(Guid.NewGuid(),Tool.Text,new[]{new Pt(20,20)},"#FFFFFF",3,"Original",100);surface.Document.Add(mark);
                Call(window,"BeginText",new Pt(20,20),mark);Field<TextBox>(window,"textEditor").Text="";Call(window,"CommitText",false);
                Require(surface.Document.Items.Count==0,"Empty edit kept old text");surface.Document.Undo();Require(surface.Document.Items[0].Text=="Original","Undo did not restore deleted text");
            }finally{window.Close();}
        });
        check("applying and cancelling text edits preserve expected history",()=>{
            var window=new OverlayWindow(new CaptureFrame(source,0,0),new Settings());
            try{
                var surface=Field<EditorSurface>(window,"surface");surface.Area=new Box(0,0,200,140);surface.HasSelection=true;
                Call(window,"BeginText",new Pt(20,20),null);Field<TextBox>(window,"textEditor").Text="New note";Call(window,"CommitText",false);
                Require(surface.Document.Items.Count==1&&surface.Document.Items[0].Text=="New note","Text not committed");
                var mark=surface.Document.Items[0];Call(window,"BeginText",new Pt(20,20),mark);Field<TextBox>(window,"textEditor").Text="Discard me";Call(window,"CommitText",true);
                Require(surface.Document.Items[0].Text=="New note","Cancel changed text");
                surface.Document.Undo();Require(surface.Document.Items.Count==0,"Cancel added an undo entry");
            }finally{window.Close();}
        });
        check("changing color while writing applies to the text being edited",()=>{
            var window=new OverlayWindow(new CaptureFrame(source,0,0),new Settings());
            try{var surface=Field<EditorSurface>(window,"surface");surface.Area=new Box(0,0,200,140);surface.HasSelection=true;
                Call(window,"BeginText",new Pt(20,20),null);Field<TextBox>(window,"textEditor").Text="Color me";
                Call(window,"SetColor","#38BDF8");Require(surface.Document.Items[0].Color=="#38BDF8","Text kept stale color");
            }finally{window.Close();}
        });
        check("Enter on the text Cancel control does not commit the edit",()=>{
            using var keyboardSource=new HwndSource(new HwndSourceParameters("ScreenShot keyboard test"){ParentWindow=new IntPtr(-3)});
            var window=new OverlayWindow(new CaptureFrame(source,0,0),new Settings());
            try{var surface=Field<EditorSurface>(window,"surface");surface.Area=new Box(0,0,200,140);surface.HasSelection=true;
                Call(window,"BeginText",new Pt(20,20),null);Field<TextBox>(window,"textEditor").Text="Discard me";
                var panel=Field<Border>(window,"textPanel");var rows=(StackPanel)panel.Child;var actions=(StackPanel)rows.Children[1];var cancel=(Button)actions.Children[1];
                cancel.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,keyboardSource,0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(surface.Document.Items.Count==0,"Enter committed text before Cancel could execute");
            }finally{window.Close();}
        });
    }
    private static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,Private)!.GetValue(instance)!;
    private static void Call(object instance,string name,params object?[] args)=>instance.GetType().GetMethod(name,Private)!.Invoke(instance,args);
    private static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
