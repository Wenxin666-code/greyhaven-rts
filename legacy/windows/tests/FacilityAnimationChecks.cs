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
 internal void AnimationReveal(){foreach(var o in Units){int side=o.Side;float r=o.Ready;o.Side=0;o.Ready=1;Reveal();o.Side=side;o.Ready=r;}}
 internal void AnimationFire(Piece a,Piece b){a.Turn=Bearing(a.P,b.P);a.Dir=(int)Math.Round(a.Turn)%16;Fire(a,b);}
}
partial class CommandWindow {
 static int AnimationDifference(Bitmap a,Bitmap b){var x=BuildingPixels(a,new Rectangle(Point.Empty,a.Size));var y=BuildingPixels(b,new Rectangle(Point.Empty,b.Size));int count=0;for(int i=0;i<x.Length;i+=4)if(Math.Abs(x[i]-y[i])+Math.Abs(x[i+1]-y[i+1])+Math.Abs(x[i+2]-y[i+2])+Math.Abs(x[i+3]-y[i+3])>16)count++;return count;}
 internal void CheckFacilityMotion(){
  drawingClient=false;
  foreach(string key in buildingRoster){
   var t=Campaign.Info(key);using(var a=new Bitmap(360,380))using(var b=new Bitmap(360,380)){
    var rect=BuildingRect(Campaign.Footprint(t,PointF.Empty),new PointF(180,285),DefaultZoom);
    using(var g=Graphics.FromImage(a)){DrawB08Idle(g,key,rect,UnitColor(0),0,0);FacilityFx(g,key,rect,0,true,true);}
    using(var g=Graphics.FromImage(b)){DrawB08Idle(g,key,rect,UnitColor(0),1,0);FacilityFx(g,key,rect,1,true,true);}
    int difference=AnimationDifference(a,b);if(!t.Civilian&&difference<24)throw new Exception("idle not legible at actual game scale "+key+" "+difference);if(t.Civilian&&difference>50)throw new Exception("civilian idle too broad "+key);
    Console.WriteLine(key+" actual-scale idle changed pixels: "+difference);
   }
   for(int i=48;i<BuildingV08.IdleFrames;i++)B08Frame(key,"idle",i);
  }
  if(BuildingV08.Specs["power"].Sockets["smoke"].Length!=2)throw new Exception("chimney socket count");
  using(var off=new Bitmap(400,400))using(var g=Graphics.FromImage(off)){FacilityFx(g,"power",new Rectangle(0,40,320,320),1,false,true);if(BuildingPixels(off,new Rectangle(0,0,400,400)).Any(v=>v!=0))throw new Exception("ambient work persists when switched off");}
  for(int i=0;i<BuildingV08.DeployFrames;i++)B08Frame("hq","deploy",i);
  var final=painted[B08Frame("hq","deploy",95)];var end=BuildingPixels(final,B08Source());var complete=BuildingPixels(painted["hq"],B08Source());int error=0;for(int i=0;i<end.Length;i++)error=Math.Max(error,Math.Abs(end[i]-complete[i]));if(error>1)throw new Exception("deploy endpoint mismatch "+error);
  world=new Campaign(true);world.Units.Clear();var generator=world.Spawn("power",0,700,700);var sensor=world.Spawn("radar",0,900,700);sensor.CompletedAt=world.VisualTime;
  float initial=FacilityPhase(sensor);world.TickVisuals(.5f);float started=FacilityPhase(sensor);if(started<=initial)throw new Exception("machine did not start");
  generator.Hp=0;facilityAt=-1;world.TickVisuals(.6f);float stopped=FacilityPhase(sensor);if(stopped!=started)throw new Exception("power loss resets or advances machinery");
  generator.Hp=generator.T.Hp;facilityAt=-1;world.TickVisuals(.1f);float resumed=FacilityPhase(sensor);if(resumed<stopped||resumed-stopped>.02f)throw new Exception("power resume jumps instead of accelerating");
  world=new Campaign(true);world.Units.Clear();world.Spawn("mcv",1,3000,1800);var baseCar=world.Spawn("mcv",0,600,600);baseCar.Turn=6;baseCar.Dir=6;baseCar.Hp=baseCar.T.Hp*.4f;var original=baseCar.P;
  if(!world.Deploy(baseCar)||!baseCar.PendingDeploy||baseCar.T.Key!="mcv"||baseCar.DeployFacing!=6||baseCar.DeployOrigin!=original)throw new Exception("deploy lost chassis origin/facing");
  for(int i=0;i<15;i++)world.Tick(.1f);if(baseCar.Deployed||!baseCar.BaseTransition)throw new Exception("deploy skipped expansion stages");for(int i=0;i<20;i++)world.Tick(.1f);
  if(!baseCar.Deployed||baseCar.BaseTransition||Math.Abs(baseCar.Hp/baseCar.T.Hp-.4f)>.001f)throw new Exception("deploy completion/health");
  if(!world.Relocate(baseCar,new PointF(760,600)))throw new Exception("repack start");for(int i=0;i<34;i++)world.Tick(.1f);if(baseCar.T.Key!="mcv"||Math.Abs(baseCar.Hp/baseCar.T.Hp-.4f)>.001f)throw new Exception("repack final chassis/health");
  world.Units.Clear();var cancelled=world.Spawn("mcv",0,600,600);cancelled.Turn=6;cancelled.Dir=6;if(!world.Deploy(cancelled)||!cancelled.PendingDeploy)throw new Exception("pending deploy admission");world.Halt(cancelled);if(cancelled.PendingDeploy||cancelled.T.Key!="mcv")throw new Exception("stop did not cancel turn-to-deploy");if(!world.Deploy(cancelled)||!world.Move(cancelled,new PointF(612,600),false)||cancelled.PendingDeploy)throw new Exception("new move did not override deploy");
  foreach(string key in new[]{"squad","at","engineer"})for(int view=0;view<8;view++){
   var crop=GaitV09.Crop(key,view);byte[] first=null;int motion=0;for(int i=0;i<GaitV09.Frames;i++){
    string name=EnsureB08("gait/"+key+"/"+view+"/"+i);var image=painted[name];if(image.Size!=crop.Size||image.Size!=painted[name+"-paint"].Size)throw new Exception("gait dimensions");var raw=BuildingPixels(image,new Rectangle(Point.Empty,image.Size));if(first==null)first=raw;else if(i==6)for(int n=0;n<raw.Length;n++)if(raw[n]!=first[n])motion++;
    using(var tinted=BuildingTint(image,painted[name+"-paint"],new Rectangle(Point.Empty,image.Size),Color.FromArgb(17,239,111))){var colors=BuildingPixels(tinted,new Rectangle(Point.Empty,tinted.Size));for(int n=3;n<raw.Length;n+=4)if(colors[n]!=raw[n])throw new Exception("gait alpha changes with faction");}
   }if(motion<100)throw new Exception("gait is not articulated "+key+"/"+view);
  }
  Console.WriteLine("96-frame deploy/repack, preserved origin/facing/health, true 8-way gaits, faction alpha and 64 MiB frame limit passed.");
 }
 void ConfigureMotionScene(int scene){
  world=new Campaign(true);world.Units.Clear();world.Shots.Clear();world.Landing=true;world.Spawn("mcv",1,3000,1800);selection.Clear();briefing=false;paused=false;modelReview=false;mouse=new PointF(-200,-200);showGrid=false;showRanges=false;hudDirty=true;
  if(scene==0){string[] keys={"hq","power","radar","satellite","barracks","factory","refinery","power"};for(int i=0;i<keys.Length;i++){var o=world.Spawn(keys[i],0,550+i%4*125,480+i/4*145);if(o.T.Key=="factory"){var order=Campaign.Info("tank");o.Queue.Enqueue(order);o.Work=order.Seconds-3;o.WorkPaid=(int)(order.Price*o.Work/order.Seconds);world.Funds[0]-=o.WorkPaid;}if(o.T.Key=="refinery"){o.StarterDelivered=true;var lane=Campaign.ExitApron(o.T,Campaign.Footprint(o.T,o.P));var truck=world.Spawn("miner",0,lane.Left+12,lane.Bottom+30);truck.Cargo=180;world.MineOrder(truck,null,o);}}CenterAt(new PointF(740,555));zoom=1.80f;}
  else if(scene==1){var o=world.Spawn("mcv",0,600,600);o.Turn=6;o.Dir=6;world.Spawn("power",0,850,450);selection.Add(o);CenterAt(new PointF(620,590));zoom=2.25f;}
  else{for(int i=0;i<3;i++){var o=world.Spawn(new[]{"squad","at","engineer"}[i],0,520+i*30,580+i*30);o.Hp=100000;world.Move(o,new PointF(800+i*24,580+i*30),false);selection.Add(o);}world.Spawn("tank",1,900,660).Hp=100000;CenterAt(new PointF(680,610));zoom=2.25f;}
  world.AnimationReveal();
 }
 internal void CaptureFacilityMotion(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1600,900);string file=Path.Combine(folder,"待机展开与步态-v09.mp4");var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("GREYHAVEN_FFMPEG")??"ffmpeg","-y -hide_banner -loglevel error -f rawvideo -pixel_format bgra -video_size 1600x900 -framerate 30 -i pipe:0 -an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart \""+file+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true};
  using(var encoder=Process.Start(info))using(var frame=new Bitmap(1600,900,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame)){
   byte[] bytes=new byte[1600*900*4];for(int scene=0;scene<3;scene++){
    ConfigureMotionScene(scene);int count=scene==0?210:scene==1?285:180;
    for(int i=0;i<count;i++){
     if(scene==0){world.Tick(1f/30);}
     else{if(scene==1&&i==18)world.Deploy(selection[0]);if(scene==1&&i==164)world.Relocate(selection[0],new PointF(755,600));world.Tick(scene==1?1f/60:1f/30);}
     if(scene==2&&i==130)foreach(var o in selection.Where(o=>o.T.Hit>0)){var target=world.Units.Last(u=>u.Side==1&&u.T.Key=="tank");world.AnimationFire(o,target);}
     OnPaint(new PaintEventArgs(g,ClientRectangle));
     using(var brush=new SolidBrush(Color.FromArgb(255,25,37,39)))g.FillRectangle(brush,12,70,1235,80);
     using(var font=new Font("Microsoft YaHei UI",20,FontStyle.Bold))using(var brush=new SolidBrush(Color.FromArgb(236,181,78)))g.DrawString(scene==0?"01  工作待机 · 双烟囱、扫描、吊架、矿料与作业火花":scene==1?"02  工程车展开与收起 · 半速慢放":"03  实际行走步态 · 原有模型、势力涂装与单位尺寸",font,brush,28,88);
     if(scene==2){var state=g.Save(); // A labelled enlargement uses the same gait pixels, not a new in-game scale.
      using(var panelBrush=new SolidBrush(Color.FromArgb(230,25,37,39)))g.FillRectangle(panelBrush,28,650,490,210);
      using(var font=new Font("Microsoft YaHei UI",12))using(var textBrush=new SolidBrush(Color.FromArgb(218,227,219)))g.DrawString("步态局部放大 · 战场尺寸不变",font,textBrush,40,654);
      for(int n=0;n<selection.Count;n++){var o=selection[n];int view=((o.Dir+14)%16)/2;var crop=GaitV09.Crop(o.T.Key,view);string key=EnsureB08("gait/"+o.T.Key+"/"+view+"/"+((int)(o.Travel/24*12)%12));bool old=drawingClient;drawingClient=false;DrawStamp(g,key,new Rectangle(Point.Empty,crop.Size),new RectangleF(55+n*155+(120-170f*crop.Width/crop.Height)/2,680,170f*crop.Width/crop.Height,170),UnitColor(o.Side));drawingClient=old;}g.Restore(state);}
     if(i==(scene==0?80:scene==1?96:65))frame.Save(Path.Combine(folder,new[]{"军用建筑工作待机-v09.png","工程车展开过程-v09.png","士兵关节行走-v09.png"}[scene]),ImageFormat.Png);
     var bits=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);}finally{frame.UnlockBits(bits);}
    }
   }encoder.StandardInput.Close();if(!encoder.WaitForExit(30000)||encoder.ExitCode!=0)throw new Exception("V09 native movie failed");
  }
  CaptureBuildingV08(folder,false);
  modelReview=true;modelBuildings=true;modelUnit=1;modelPower=true;modelClock=2;modelSpin=false;briefing=true;hudDirty=true;
  using(var shot=new Bitmap(1600,900))using(var graphics=Graphics.FromImage(shot)){OnPaint(new PaintEventArgs(graphics,ClientRectangle));shot.Save(Path.Combine(folder,"电站排烟预览-v09.png"),ImageFormat.Png);}
  modelBuildings=false;modelUnit=Array.IndexOf(roster,"squad");modelWalking=true;modelAngle=56;modelClock=.18f;hudDirty=true;
  using(var shot=new Bitmap(1600,900))using(var graphics=Graphics.FromImage(shot)){OnPaint(new PaintEventArgs(graphics,ClientRectangle));shot.Save(Path.Combine(folder,"士兵行走模型预览-v09.png"),ImageFormat.Png);}
  Console.WriteLine("Saved native standby/deploy/gait recording: "+file);
 }
}}
class FacilityAnimationChecks {
 [STAThread]static int Main(string[] args){try{using(var f=new Greyhaven.CommandWindow()){f.CheckFacilityMotion();if(args.Length>0)f.ProfileBuildingFrames();if(args.Length>0)f.CaptureFacilityMotion(args[0]);}return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
