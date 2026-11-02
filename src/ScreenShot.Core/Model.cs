namespace ScreenShot.Core;
public readonly record struct Pt(double X, double Y);
public readonly record struct Box(double X,double Y,double Width,double Height) {
 public double Right => X + Width;
 public double Bottom => Y + Height;
 public bool IsUsable=>Width >= 2 && Height >= 2;
 public bool Contains(Pt p)=>p.X>=X && p.X<=Right && p.Y>=Y && p.Y<=Bottom;
 public static Box FromDrag(Pt a,Pt b,double w,double h) {
  var x1=Math.Clamp(a.X,0,w); var x2=Math.Clamp(b.X,0,w);
  var y1=Math.Clamp(a.Y,0,h); var y2=Math.Clamp(b.Y,0,h);
  return new(Math.Min(x1,x2),Math.Min(y1,y2),Math.Abs(x2-x1),Math.Abs(y2-y1));
 }
 public Box Move(double dx,double dy,double w,double h)=>this with { X=Math.Clamp(X+dx,0,Math.Max(0,w-Width)), Y=Math.Clamp(Y+dy,0,Math.Max(0,h-Height)) };
 public Box Resize(Pt p,bool left,bool top,bool right,bool bottom,double w,double h)=>FromDrag(new(left?p.X:X,top?p.Y:Y),new(right?p.X:Right,bottom?p.Y:Bottom),w,h);
 public (bool Left,bool Top,bool Right,bool Bottom) HitEdges(Pt p) {
  bool nearX=p.X>=X-8&&p.X<=Right+8,nearY=p.Y>=Y-8&&p.Y<=Bottom+8;
  double tx=Math.Min(8,Width/4),ty=Math.Min(8,Height/4);
  return (nearY&&Math.Abs(p.X-X)<tx,nearX&&Math.Abs(p.Y-Y)<ty,nearY&&Math.Abs(p.X-Right)<tx,nearX&&Math.Abs(p.Y-Bottom)<ty);
 }
}
public enum Tool { Select, Pen, Arrow, Line, Rectangle, Highlight, Text }
public record Mark(Guid Id,Tool Tool,IReadOnlyList<Pt> Points,string Color,double Width,string Text,double TextWidth=0) {
 public Mark Translate(double x,double y)=>this with { Points=Array.AsReadOnly(Points.Select(p=>new Pt(p.X+x,p.Y+y)).ToArray()) };
 public Mark Snapshot()=>this with { Points=Array.AsReadOnly(Points.ToArray()) };
 public Mark WithStyle(string? color=null,double? width=null)=>this with { Color=color??Color, Width=width??Width };
}
public sealed class Document {
 private readonly List<Mark[]> history=[[]]; private int index;
 public IReadOnlyList<Mark> Items=>Array.AsReadOnly(history[index]);
 public bool CanUndo=>index>0;
 public bool CanRedo=>index<history.Count-1;
 public void Add(Mark mark)=>Commit(Items.Append(mark));
 public void Replace(Mark mark)=>Commit(Items.Select(m=>m.Id==mark.Id?mark:m));
 public void Remove(Guid id)=>Commit(Items.Where(m=>m.Id!=id));
 private void Commit(IEnumerable<Mark> items) {
  var snapshot=items.Select(m=>m.Snapshot()).ToArray();
  history.RemoveRange(index+1,history.Count-index-1); history.Add(snapshot); index++;
  // Bound retained undo states for long sessions.
  if(history.Count>150){history.RemoveAt(0);index--;}
 }
 public void Undo(){if(CanUndo)index--;}
 public void Redo(){if(CanRedo)index++;}
}
