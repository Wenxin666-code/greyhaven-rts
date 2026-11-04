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
 static void MapClick(CommandWindow f,int x,int y,MouseButtons button=MouseButtons.Left){
  var p=(PointF)typeof(CommandWindow).GetMethod("ScreenOf",flags).Invoke(f,new object[]{new PointF(x,y)});Press(f,(int)p.X,(int)p.Y,button);
 }
 [STAThread]static int Main(string[] args){try{Run(args);return 0;}catch(Exception e){Console.Error.WriteLine(e.Message);Console.Error.WriteLine(e.StackTrace);return 1;}}
 static void Run(string[] args){
  using(var f=new CommandWindow()){f.CheckPlates(args.Length==0?null:Path.Combine(Path.GetDirectoryName(args[0]),"角度校准.png"));Frame(f,null);Press(f,410,575);if((bool)Field(f,"briefing"))throw new Exception("briefing");
   var screen=new PointF(647,405);var inv=typeof(CommandWindow).GetMethod("WorldOf",flags);
   var before=(PointF)inv.Invoke(f,new object[]{screen});float ui=(float)Field(f,"scale");
   typeof(CommandWindow).GetMethod("OnMouseWheel",flags).Invoke(f,new object[]{new MouseEventArgs(MouseButtons.None,0,(int)(screen.X*ui+(float)Field(f,"ox")),(int)(screen.Y*ui+(float)Field(f,"oy")),120)});
   var after=(PointF)inv.Invoke(f,new object[]{screen});if(Campaign.Dist(before,after)>2||(float)Field(f,"zoom")<=1.7f)throw new Exception("cursor anchored zoom");Frame(f,null);
   Press(f,1330,237);Press(f,1260,290);if(((Campaign)Field(f,"world")).LastOrder!=null)throw new Exception("locked card accepted");
   Press(f,1250,237);
   var w=(Campaign)Field(f,"world");Press(f,1320,620);if(!w.Landing)throw new Exception("deploy button");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   Press(f,1265,292);MapClick(f,263,548);if(!w.Units.Any(o=>o.Side==0&&o.T.Key=="power"))throw new Exception("construction click");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);Press(f,1394,292);MapClick(f,123,610);for(int i=0;i<65;i++)w.Tick(.1f);
   Press(f,1260,350);MapClick(f,339,548);for(int i=0;i<110;i++)w.Tick(.1f);
   Press(f,1330,237);Press(f,1270,290);if(!w.Live(0).Any(o=>o.Queue.Count>0))throw new Exception("training click");
   Press(f,1270,290);Press(f,1412,237);
   if(!(bool)Field(f,"queueOpen"))throw new Exception("queue tab");
   var source=w.Live(0).Single(o=>o.Queue.Count>0);if(source.Queue.Count!=2)throw new Exception("second order");
   Frame(f,args.Length==0?null:args[0]);int funds=w.Funds[0];Press(f,1413,362);if(source.Queue.Count!=1||w.Funds[0]!=funds+85)throw new Exception("cancel waiting slot");
   if(w.Lesson!=3||!w.Tutorial)throw new Exception("default tutorial step");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   if(w.Lesson!=4)throw new Exception("delivery milestone");Press(f,752,104);MapClick(f,172,214,MouseButtons.Right);
   for(int i=0;i<400;i++)w.Tick(.1f);Frame(f,null);if(w.Lesson!=6)throw new Exception("capture plus prebuilt factory");
   Press(f,752,104);Press(f,1394,350);for(int i=0;i<95;i++)w.Tick(.1f);Frame(f,null);
   if(args.Length>0){
    w.Upgrade();w.Train(Campaign.Info("tank"),0,null);for(int i=0;i<140;i++)w.Tick(.1f);
    Press(f,1330,237);typeof(CommandWindow).GetMethod("CenterAt",flags).Invoke(f,new object[]{new PointF(300,495)});
    typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.G)});
    Frame(f,Path.Combine(Path.GetDirectoryName(args[0]),"等距战场.png"));
   }
   if(w.Lesson!=7)throw new Exception("ifv training");Press(f,752,104);
   typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.A)});MapClick(f,1330,292);
   for(int i=0;i<1100;i++)w.Tick(.1f);Frame(f,null);if(!w.Won)throw new Exception("tutorial victory through interface");
   Press(f,688,574);w=(Campaign)Field(f,"world");if(w.Tutorial||!w.Assisted)throw new Exception("handoff to assisted mission");Console.WriteLine("Native UI mission entry, deploy, placement, production and render passed.");}
 }
}

namespace Greyhaven {
partial class CommandWindow {
 internal void CheckPlates(string path){
  var keys=new[]{"hq","power","barracks","factory","radar","post","depot","freight","harbormaster"};
  using(var b=new Bitmap(1160,835))using(var g=Graphics.FromImage(b)){
   g.Clear(C("#253431"));g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   Txt(g,"灰港登陆  /  地面投影验收",28,18,28,pale,true);
   Txt(g,"青色角标为实际占地的四角；门前台阶与出厂坡道可延伸至预留区域。",29,60,16,dim);
   for(int i=0;i<keys.Length;i++){
    var k=keys[i];var t=Campaign.Info(k);string name=t==null?world.Sites[i-6].Name:t.Name;
    var lot=t==null?new RectangleF(0,0,48,48):Campaign.Footprint(t,PointF.Empty);
    var label=name+"   "+lot.Width/Campaign.Tile+" × "+lot.Height/Campaign.Tile;
    var p=new PointF(128+i%5*226,269+i/5*229);
    float z=180/((lot.Width+lot.Height)*Axis);var target=BuildingRect(lot,p,z);BuildingArt(g,k,0,target,1);
    foreach(var corner in new[]{new PointF(lot.Left,lot.Top),new PointF(lot.Right,lot.Top),new PointF(lot.Right,lot.Bottom),new PointF(lot.Left,lot.Bottom)}){
     float x=corner.X-lot.Left-lot.Width/2,y=corner.Y-lot.Top-lot.Height/2;
     var screen=new PointF(p.X+z*Axis*(x-y),p.Y+z*Rise*(x+y));
     int sx=(int)Math.Round((screen.X-target.X)*FrameSize/target.Width),sy=(int)Math.Round((screen.Y-target.Y)*FrameSize/target.Height);
     if(!Enumerable.Range(-2,5).Any(dx=>Enumerable.Range(-2,5).Any(dy=>painted[k].GetPixel(sx+dx,sy+dy).A>100)))throw new Exception(k+" foundation contact");
     foreach(float sign in new[]{-1f,1f}){var tx=screen.X+(x<0?1:-1)*sign*8;var ty=screen.Y+(sign>0?(x<0?1:-1):(y<0?1:-1))*4;
      if(sign<0)tx=screen.X-(y<0?1:-1)*8;Line(g,screen.X,screen.Y,tx,ty,teal,2);}
    }
    Txt(g,label,p.X-84,p.Y+50,16,pale,true);
   }
   Line(g,24,584,1130,584,dim);Txt(g,"车辆接地方向  /  斜向与正侧面",28,596,20,gold,true);
   int j=0;foreach(var k in new[]{"mcv","ifv","tank","arty"}){
    var t=Campaign.Info(k);var sheet=painted[k];if(sheet.Width!=FrameSize*8||sheet.Height!=FrameSize*6)throw new Exception(k+" directional sheet");
    var baseX=40+j++*285;Txt(g,t.Name,baseX,641,16,pale,true);
    for(int n=0;n<2;n++){int dir=n==0?14:4;double yaw=dir*Math.PI/8;
     var pt=new PointF(baseX+50+n*120,741);float dx=(float)Math.Sin(yaw)*60,dy=(float)Math.Cos(yaw)*30;
     Line(g,pt.X-dx,pt.Y-dy,pt.X+dx,pt.Y+dy,teal,1);DrawLive(g,t,0,dir,pt,132,1);}
   }
   Txt(g,"图取自发布程序内嵌资源。车辆行进、施工预览和侧栏图标使用同套图集。",28,796,15,dim);
   if(path!=null)b.Save(path,System.Drawing.Imaging.ImageFormat.Png);
  }
 }
}
}
