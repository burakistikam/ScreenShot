using ScreenShot.Core;

int failed = 0;
void Check(string name, Action test) { try { test(); Console.WriteLine($"PASS {name}"); } catch(Exception ex) { failed++; Console.WriteLine($"FAIL {name}: {ex.Message}"); } }
void Equal<T>(T expected,T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}, got {actual}"); }
Check("reverse drag normalizes and clamps to captured desktop", () => Equal(new Box(0, 20, 80, 80), Box.FromDrag(new(80,100),new(-20,20),200,150)));
Check("move preserves size at right and top boundaries", () => Equal(new Box(70,0,30,40), new Box(10,20,30,40).Move(500,-100,100,100)));
Check("zero size selection cannot export", () => Equal(false, new Box(10,10,0,20).IsUsable));
Check("resize across opposite edge normalizes", () => Equal(new Box(40,20,40,40), new Box(10,20,30,40).Resize(new(80,20),true,false,false,false,100,100)));
Check("undo redo restores an edited annotation and branching removes redo", () => {
 var doc = new Document(); var a = new Mark(Guid.NewGuid(), Tool.Arrow, new[]{new Pt(10,20),new Pt(40,50)}, "#FF0000", 3, "");
 doc.Add(a); doc.Replace(a with { Text="changed" }); doc.Undo(); Equal("",doc.Items[0].Text); doc.Redo(); Equal("changed",doc.Items[0].Text);
 doc.Undo(); doc.Add(a with { Id=Guid.NewGuid() }); Equal(false,doc.CanRedo); Equal(2,doc.Items.Count); doc.Undo(); doc.Undo(); Equal(0,doc.Items.Count); doc.Undo(); Equal(0,doc.Items.Count);
});
Check("history owns stroke coordinates and cannot be changed by caller", () => { var points=new[]{new Pt(1,2),new Pt(3,4)}; var doc=new Document(); doc.Add(new(Guid.NewGuid(),Tool.Pen,points,"#FFFFFF",2,"")); points[0]=new Pt(99,99); Equal(new Pt(1,2),doc.Items[0].Points[0]); });
Check("move annotation translates every point",()=> { var mark=new Mark(Guid.NewGuid(),Tool.Line,new[]{new Pt(1,2),new Pt(3,4)},"#FFFFFF",2,""); Equal(new Pt(11,0),mark.Translate(10,-2).Points[0]); });
Check("changing width preserves selected annotation color and vice versa",()=> { var mark=new Mark(Guid.NewGuid(),Tool.Line,new[]{new Pt(1,2)},"#FF0000",3,""); Equal("#FF0000",mark.WithStyle(width:8).Color); Equal(3d,mark.WithStyle(color:"#0000FF").Width); });
Check("narrow and short selections retain a drawable center",()=> { Equal((false,false,false,false),new Box(0,0,100,12).HitEdges(new Pt(50,6))); Equal((false,false,false,false),new Box(0,0,12,100).HitEdges(new Pt(6,50))); Equal((true,false,false,false),new Box(0,0,100,12).HitEdges(new Pt(0,6))); });
return failed==0 ? 0 : 1;
