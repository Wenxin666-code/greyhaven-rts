using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Greyhaven;
class WindowChecks {
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(CommandWindow f,string n){return typeof(CommandWindow).GetField(n,flags).GetValue(f);}
 static void Frame(CommandWindow f,string output){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));if(output!=null)b.Save(output,System.Drawing.Imaging.ImageFormat.Png);}}
 static void Press(CommandWindow f,int x,int y,MouseButtons button=MouseButtons.Left){float size=(float)Field(f,"scale");int xx=(int)(x*size+(float)Field(f,"ox")),yy=(int)(y*size+(float)Field(f,"oy"));
  var e=new MouseEventArgs(button,1,xx,yy,0);foreach(var n in new[]{"OnMouseDown","OnMouseUp"})typeof(CommandWindow).GetMethod(n,flags).Invoke(f,new object[]{e});Frame(f,null);}
 [STAThread]static void Main(string[] args){
  using(var f=new CommandWindow()){Frame(f,null);Press(f,410,575);if((bool)Field(f,"briefing"))throw new Exception("briefing");
   var w=(Campaign)Field(f,"world");Press(f,1320,620);if(!w.Landing)throw new Exception("deploy button");
   for(int i=0;i<60;i++)w.Tick(.1f);
   Press(f,1265,292);Press(f,263,615);if(!w.Units.Any(o=>o.Side==0&&o.T.Key=="power"))throw new Exception("construction click");
   for(int i=0;i<60;i++)w.Tick(.1f);Press(f,1394,292);Press(f,123,677);for(int i=0;i<65;i++)w.Tick(.1f);
   Press(f,1260,350);Press(f,339,615);for(int i=0;i<110;i++)w.Tick(.1f);
   Press(f,1330,237);Press(f,1270,290);if(!w.Live(0).Any(o=>o.Queue.Count>0))throw new Exception("training click");
   Press(f,1270,290);Press(f,1412,237);
   if(!(bool)Field(f,"queueOpen"))throw new Exception("queue tab");
   var source=w.Live(0).Single(o=>o.Queue.Count>0);if(source.Queue.Count!=2)throw new Exception("second order");
   Frame(f,args.Length==0?null:args[0]);int funds=w.Funds[0];Press(f,1413,362);if(source.Queue.Count!=1||w.Funds[0]!=funds+85)throw new Exception("cancel waiting slot");
   if(w.Lesson!=3||!w.Tutorial)throw new Exception("default tutorial step");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   if(w.Lesson!=4)throw new Exception("delivery milestone");Press(f,752,104);Press(f,172,281,MouseButtons.Right);
   for(int i=0;i<400;i++)w.Tick(.1f);Frame(f,null);if(w.Lesson!=6)throw new Exception("capture plus prebuilt factory");
   Press(f,752,104);Press(f,1260,416);for(int i=0;i<95;i++)w.Tick(.1f);Frame(f,null);
   if(w.Lesson!=7)throw new Exception("ifv training");Press(f,752,104);
   typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.A)});Press(f,1055,353);
   for(int i=0;i<1100;i++)w.Tick(.1f);Frame(f,null);if(!w.Won)throw new Exception("tutorial victory through interface");
   Press(f,688,574);w=(Campaign)Field(f,"world");if(w.Tutorial||!w.Assisted)throw new Exception("handoff to assisted mission");Console.WriteLine("Native UI mission entry, deploy, placement, production and render passed.");}
 }
}
