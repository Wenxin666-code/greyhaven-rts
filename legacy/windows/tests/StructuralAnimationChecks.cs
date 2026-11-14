using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Greyhaven {
public partial class Campaign {
 internal void StructureKill(Piece o){Kill(o);}
 internal void StructureReveal(){foreach(var o in Units){int side=o.Side;float ready=o.Ready;o.Side=0;o.Ready=1;Reveal();o.Side=side;o.Ready=ready;}}
}
partial class CommandWindow {
 internal void CheckStructuralAnimation(){
  drawingClient=false;
  foreach(var key in buildingRoster){
   string previous=null;int differences=0;
   foreach(int frame in new[]{0,13,31,50,71}){
    string name=B08Frame(key,"build",frame);using(var colored=BuildingTint(painted[name],painted[name+"-paint"],B08Source(),Color.FromArgb(131,227,19))){var raw=BuildingPixels(colored,B08Source());string hash=Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(raw));if(previous!=null&&hash!=previous)differences++;previous=hash;}
   }if(differences!=4)throw new Exception("construction phases not distinct "+key);
   for(int level=1;level<=3;level++){
    string name=EnsureB08("b08/"+key+"/damage-"+level);var body=BuildingPixels(painted[name],B08Source());
    foreach(var color in new[]{Color.FromArgb(17,239,111),Color.FromArgb(213,31,196)})using(var image=BuildingTint(painted[name],painted[name+"-paint"],B08Source(),color)){
     var tinted=BuildingPixels(image,B08Source());for(int i=3;i<body.Length;i+=4)if(tinted[i]!=body[i])throw new Exception("damage recolor changes alpha "+key);
    }
   }
   Console.WriteLine(key+": rigid build stages, three upright damaged skins, arbitrary faction alpha and no retained ruins passed.");
  }
  world=new Campaign(true);world.Units.Clear();var partial=world.Spawn("factory",0,600,600);partial.Ready=.35f;world.StructureKill(partial);partial.Dead=.4f;Rectangle source;
  var selected=BuildingVisual(partial,out source);if(!selected.StartsWith("b08/factory/build/",StringComparison.Ordinal))throw new Exception("unfinished dying structure changes to a completed building");
  partial.Dead=DestructionSeconds;var end=BuildingVisual(partial,out source);partial.Dead+=2;var held=BuildingVisual(partial,out source);if(end!=held||end!="removed-building")throw new Exception("partial wreck remains after the explosion");
  partial.Ready=1;partial.Dead=.10f;var first=BuildingVisual(partial,out source);partial.Dead=TerminalBlastAt-.01f;if(BuildingVisual(partial,out source)!=first||first!="b08/factory/damage-3")throw new Exception("dying structure moves or shrinks before detonation");
  partial.Dead=TerminalWreckAt+.01f;if(BuildingVisual(partial,out source)!="removed-building")throw new Exception("a building ruin remains after the explosion");
  partial.Hp=partial.T.Hp*.4f;partial.Dead=0;if(BuildingDamageLevel(partial)!=2)throw new Exception("damage health threshold");partial.Hp=partial.T.Hp;if(BuildingDamageLevel(partial)!=0||BuildingVisual(partial,out source)!="factory")throw new Exception("repair does not restore intact appearance");
  using(var image=new Bitmap(420,420))using(var g=Graphics.FromImage(image)){
   StructuralEffects(g,"factory",new Rectangle(50,50,320,320),.42f,false);var pixels=BuildingPixels(image,new Rectangle(0,0,420,420));if(!pixels.Any(v=>v!=0))throw new Exception("construction weld sites are not visible");
   g.Clear(Color.Transparent);StructuralEffects(g,"factory",new Rectangle(50,50,320,320),1,false);pixels=BuildingPixels(image,new Rectangle(0,0,420,420));if(pixels.Any(v=>v!=0))throw new Exception("completed building still emits construction effects");
   g.Clear(Color.Transparent);FatalBuildingFx(g,"factory",new Rectangle(50,50,320,320),.4f);pixels=BuildingPixels(image,new Rectangle(0,0,420,420));int earlyWhite=0;for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]>60&&pixels[i+1]*255>239*pixels[i+3]&&pixels[i]*255>179*pixels[i+3])earlyWhite++;
   g.Clear(Color.Transparent);FatalBuildingFx(g,"factory",new Rectangle(50,50,320,320),TerminalBlastAt+.02f);pixels=BuildingPixels(image,new Rectangle(0,0,420,420));int lateWhite=0;for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]>60&&pixels[i+1]*255>239*pixels[i+3]&&pixels[i]*255>179*pixels[i+3])lateWhite++;
   if(earlyWhite!=0||lateWhite<1)throw new Exception("terminal flash timing: early="+earlyWhite+", late="+lateWhite);
  }
  world=new Campaign(true);world.Units.Clear();var gone=world.Spawn("factory",0,600,600);var witness=world.Spawn("squad",0,625,630);world.StructureKill(gone);gone.Dead=2;world.StructureReveal();CenterAt(gone.P);zoom=1.5f;
  using(var clear=new Bitmap(420,420))using(var graphics=Graphics.FromImage(clear)){
   DrawBuildingState(graphics,gone,new PointF(210,210),gone.T.Size*zoom);DrawGroundShadow(graphics,gone,new PointF(210,210),gone.T.Size*zoom);
   if(BuildingPixels(clear,new Rectangle(0,0,420,420)).Any(v=>v!=0))throw new Exception("wreck or building shadow persists");
   var p=ScreenOf(gone.P);graphics.TranslateTransform(210-p.X,210-p.Y);int unitsBefore=world.Units.Count;DrawBuildingScars(graphics);
   if(world.Units.Count!=unitsBefore||buildingCraters.Count!=1||!BuildingPixels(clear,new Rectangle(0,0,420,420)).Any(v=>v!=0))throw new Exception("crater missing or added as a gameplay object");
   world.Units.Remove(gone);graphics.Clear(Color.Transparent);DrawBuildingScars(graphics);if(buildingCraters.Count!=1||!BuildingPixels(clear,new Rectangle(0,0,420,420)).Any(v=>v!=0))throw new Exception("decorative crater disappears with corpse cleanup");
  }
  Console.WriteLine("Standing damage, repair recovery, burn-before-blast timing, cleared wrecks and decorative craters passed.");
 }
 Piece[] ConfigureStructureScene(int scene){
  world=new Campaign(true);world.Units.Clear();world.Shots.Clear();world.Landing=true;world.Spawn("mcv",1,3000,1800);world.Spawn("hq",0,1800,1800);selection.Clear();briefing=false;paused=false;modelReview=false;mouse=new PointF(-200,-200);showGrid=false;showRanges=false;hudDirty=true;
  var pieces=new[]{world.Spawn("power",0,605,745),world.Spawn("factory",0,700,650),world.Spawn("radar",0,795,555)};
  if(scene==0)foreach(var p in pieces){p.Ready=.001f;p.Assembly=true;}
  else if(scene==2){float[] readiness={.2f,.53f,.82f};for(int i=0;i<pieces.Length;i++)pieces[i].Ready=readiness[i];}
  world.StructureReveal();CenterAt(new PointF(640,590));zoom=2.4f;return pieces;
 }
 internal void CaptureStructuralReview(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1600,900);string output=Path.Combine(folder,"结构搭建与受损燃爆-v10.mp4");
  var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("GREYHAVEN_FFMPEG")??"ffmpeg","-y -hide_banner -loglevel error -f rawvideo -pixel_format bgra -video_size 1600x900 -framerate 30 -i pipe:0 -an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart \""+output+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true};
  using(var encoder=Process.Start(info))using(var frame=new Bitmap(1600,900,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame)){
   var bytes=new byte[1600*900*4];
   for(int scene=0;scene<4;scene++){
    var pieces=ConfigureStructureScene(scene);bool killed=false;
    for(int i=0;i<210;i++){
     if(scene==0)world.Tick(1f/120);
     else if(scene==1){if(i==30)foreach(var p in pieces)p.Hp=p.T.Hp*.65f;if(i==65)foreach(var p in pieces)p.Hp=p.T.Hp*.40f;if(i==100)foreach(var p in pieces)p.Hp=p.T.Hp*.15f;if(i>=125&&!killed){foreach(var p in pieces)world.StructureKill(p);killed=true;}world.TickVisuals(1f/30);}
     else if(scene==2){if(i>=20&&!killed){foreach(var p in pieces)world.StructureKill(p);killed=true;}world.TickVisuals(1f/60);}
     world.StructureReveal();
     if(scene<3)OnPaint(new PaintEventArgs(g,ClientRectangle));else{
      g.Clear(C("#202c32"));float clock=i/30f;string[] keys={"factory","residence"};for(int n=0;n<2;n++){var rect=new RectangleF(65+n*760,125,650,650);if(clock<2.4f){int level=Math.Min(3,(int)(clock/.6f));DrawB08Idle(g,keys[n],rect,UnitColor(n),clock,0,level);BuildingFireFx(g,keys[n],rect,clock,level);}else{DrawFatalBuilding(g,keys[n],rect,UnitColor(n),clock-2.4f);FatalBuildingFx(g,keys[n],rect,clock-2.4f);}Txt(g,Campaign.Info(keys[n]).Name,260+n*760,780,23,pale,true);}
     }
     using(var brush=new SolidBrush(Color.FromArgb(255,25,37,39)))g.FillRectangle(brush,12,70,1315,80);
     string title=scene==0?"01  分阶段施工 · 1/4 速展示，游戏生产用时不变":scene==1?"02  外壳破损、冒烟着火与最终燃爆 · 正常速度":scene==2?"03  施工中受毁 · 半速展示":"04  破损细节 · 破口、火星、燃爆与地面弹坑";
     using(var font=new Font("Microsoft YaHei UI",20,FontStyle.Bold))using(var brush=new SolidBrush(Color.FromArgb(236,181,78)))g.DrawString(title,font,brush,28,88);
     if(i==(scene==0?58:scene==1?112:scene==2?80:65))frame.Save(Path.Combine(folder,new[]{"承重骨架搭建-v10.png","建筑受损着火-v10.png","未完工建筑受毁-v10.png","破损细节与火焰-v10.png"}[scene]),ImageFormat.Png);
     var bits=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);}finally{frame.UnlockBits(bits);}
    }
   }
   encoder.StandardInput.Close();if(!encoder.WaitForExit(30000)||encoder.ExitCode!=0)throw new Exception("structural review encoding failed");
  }
  using(var board=new Bitmap(1600,1160))using(var g=Graphics.FromImage(board)){BuildingBoard(g,.90f,0);board.Save(Path.Combine(folder,"十一类建筑施工骨架-v10.png"),ImageFormat.Png);BuildingBoard(g,.92f,2);board.Save(Path.Combine(folder,"十一类建筑受损燃烧-v10.png"),ImageFormat.Png);}
  foreach(string kind in new[]{"flame","soot","blast"}){EnsureHazard(kind);painted["hazard/"+kind].Save(Path.Combine(folder,"hazard-"+kind+"-v10.png"),ImageFormat.Png);}
  Console.WriteLine("Saved native construction / damage / burn-up / explosion video: "+output);
 }
}}
class StructuralAnimationChecks {
 [STAThread]static int Main(string[] args){try{using(var window=new Greyhaven.CommandWindow()){window.CheckStructuralAnimation();if(args.Length>0)window.CaptureStructuralReview(args[0]);}return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
