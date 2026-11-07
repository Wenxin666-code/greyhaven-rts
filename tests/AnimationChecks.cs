using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Greyhaven {
public partial class Campaign {
 internal void ReviewFire(Piece source,Piece target){source.Turn=Bearing(source.P,TargetPoint(source.P,target));source.Dir=(int)Math.Round(source.Turn)%16;Fire(source,target);}
 internal void ReviewKill(Piece target){Kill(target);}
}
partial class CommandWindow {
 internal void CheckBattleAnimations(){
  drawingClient=false;
  foreach(var key in buildingRoster){
   EnsureAnimation(key+"-build");EnsureAnimation(key+"-collapse");
   var build=painted[key+"-build"];var collapse=painted[key+"-collapse"];
   if(build.Size!=new Size(FrameSize*8,FrameSize*3)||collapse.Size!=new Size(FrameSize*8,FrameSize*2))throw new Exception("animation dimensions "+key);
   int changes=0,first=0,last=0;var end=AnimationCell(BuildFrames-1);var rubble=AnimationCell(CollapseFrames-1);
   for(int y=0;y<320;y+=4)for(int x=0;x<320;x+=4){
    var b=build.GetPixel(end.X+x,end.Y+y);var c=painted[key].GetPixel(x,y);
    if(b!=c)changes++;if(build.GetPixel(x,y).A>80)first++;if(b.A>80)last++;
    if(collapse.GetPixel(rubble.X+x,rubble.Y+y)!=painted[key].GetPixel(x,640+y))throw new Exception("collapse endpoint differs from rubble "+key);
   }
   if(changes>3||first>=last*.85)throw new Exception("construction endpoint/assembly "+key+" diff="+changes);
  }
  world=new Campaign(true);world.Units.Clear();world.Shots.Clear();
  var tank=world.Spawn("tank",0,600,600);var target=world.Spawn("factory",1,650,600);target.Hp=1;
  world.ReviewFire(tank,target);if(target.Alive||world.Shots.Count!=1||tank.FiredAt!=world.VisualTime)throw new Exception("shot event not synchronized with damage");
  world.Tick(.01f);if(!world.Won||world.OutcomeReady)throw new Exception("victory must allow the final explosion");
  float frozenTime=world.Time;world.TickVisuals(.6f);if(target.Dead<.6f||world.OutcomeReady||world.Time!=frozenTime)throw new Exception("outcome visual clock changed gameplay");
  world.TickVisuals(.8f);if(!world.OutcomeReady||world.Shots.Count!=0)throw new Exception("outcome completion / shot cleanup");
  target.Ready=.35f;target.Dead=.4f;Rectangle src;if(BuildingVisual(target,out src)!="factory-build")throw new Exception("unfinished wreck reveals a finished building");
  var hidden=new Shot{A=new PointF(3000,1800),B=new PointF(3000,1800),Weapon="tank",Born=world.VisualTime-.1f};
  using(var bitmap=new Bitmap(MW,MH))using(var g=Graphics.FromImage(bitmap)){
   CenterAt(hidden.B);zoom=1;ShotImpact(g,hidden);
   for(int y=0;y<MH;y+=3)for(int x=0;x<MW;x+=3)if(bitmap.GetPixel(x,y).A!=0)throw new Exception("impact leaked through shroud");
   world.Spawn("squad",0,hidden.B.X,hidden.B.Y);world.ReviewReveal();ShotImpact(g,hidden);
   bool visible=false;for(int y=0;y<MH;y+=3)for(int x=0;x<MW;x+=3)if(bitmap.GetPixel(x,y).A!=0)visible=true;
   if(!visible)throw new Exception("revealed impact missing / invalid test viewport");
  }
  foreach(var type in new[]{"blast","smoke","dust","spark","weld"})EnsureEffect(type);
  Console.WriteLine(buildingRoster.Length+" construction/collapse endpoints, partial building destruction, hit timing, final-explosion clock and hidden impacts passed.");
 }
 internal void CaptureAnimationReview(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1600,900);briefing=false;paused=false;modelReview=false;selection.Clear();showRanges=false;showGrid=false;mouse=new PointF(-100,-100);
  string output=Path.Combine(folder,"建造开火损毁-v06.mp4");
  var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("GREYHAVEN_FFMPEG")??"ffmpeg","-y -hide_banner -loglevel error -f rawvideo -pixel_format bgra -video_size 1600x900 -framerate 30 -i pipe:0 -an -c:v libx264 -preset fast -crf 20 -pix_fmt yuv420p -movflags +faststart \""+output+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true};
  using(var encoder=Process.Start(info))using(var frame=new Bitmap(1600,900,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame)){
   var bytes=new byte[1600*900*4];
   for(int scene=0;scene<3;scene++){
    world=new Campaign(true);world.Units.Clear();world.Shots.Clear();world.Landing=true;selection.Clear();
    world.Spawn("mcv",1,3000,1800);CenterAt(new PointF(720,590));zoom=1.95f;hudDirty=true;
    Piece[] builders=null,shooters=null,victims=null;
    if(scene==0){builders=new[]{world.Spawn("factory",0,610,510),world.Spawn("radar",0,765,510),world.Spawn("barracks",0,720,675)};foreach(var b in builders)b.Ready=.01f;}
    else if(scene==1){
     shooters=new[]{world.Spawn("squad",0,590,635),world.Spawn("at",0,645,635),world.Spawn("ifv",0,700,635),world.Spawn("tank",0,765,635),world.Spawn("arty",0,835,635)};
     victims=new[]{world.Spawn("squad",1,590,535),world.Spawn("tank",1,645,535),world.Spawn("ifv",1,700,535),world.Spawn("power",1,765,515),world.Spawn("factory",1,900,500)};
     for(int i=0;i<shooters.Length;i++){shooters[i].Dir=8;shooters[i].Turn=8;victims[i].Hp=10000;}
    }else{
     victims=new[]{world.Spawn("factory",1,610,510),world.Spawn("refinery",1,765,510),world.Spawn("tank",1,660,665),world.Spawn("barracks",1,820,685)};
     world.Spawn("engineer",0,570,620);foreach(var b in victims)b.Hp=b.T.Hp*.19f;
    }
    // Stage the examples; all frames use the actual runtime renderer and events.
    // Construction is deliberately accelerated for the review, not the game.
    world.ReviewReveal();
    int frames=scene==0?210:180;
    for(int i=0;i<frames;i++){
     world.TickVisuals(1f/30);world.Time=world.VisualTime;
     if(scene==0)foreach(var b in builders){b.Ready=Math.Min(1,.01f+i/170f);if(i==170)b.CompletedAt=world.VisualTime;}
     if(scene==1)for(int n=0;n<shooters.Length;n++)if(i%(int)(shooters[n].T.Reload*30)==0)world.ReviewFire(shooters[n],victims[n]);
     if(scene==2)for(int n=0;n<victims.Length;n++)if(i==24+n*18)world.ReviewKill(victims[n]);
     OnPaint(new PaintEventArgs(g,ClientRectangle));
     // Replace only the tutorial's text band in this staged recording.
     using(var panel=new SolidBrush(Color.FromArgb(24,36,39)))g.FillRectangle(panel,12,70,1235,84);
     using(var font=new Font("Microsoft YaHei UI",19,FontStyle.Bold))using(var brush=new SolidBrush(Color.FromArgb(236,181,78)))g.DrawString(scene==0?"01  建造装配 · 施工演示加速":scene==1?"02  实际武器射速 · 枪口、震动与弹着反馈":"03  受损冒烟 · 连锁爆炸、坍塌与残骸",font,brush,28,88);
     var bits=frame.LockBits(new Rectangle(0,0,1600,900),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
     try{Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);}finally{frame.UnlockBits(bits);}
     if(i==(scene==0?100:scene==1?60:66))frame.Save(Path.Combine(folder,new[]{"施工动画-v06.png","交火动画-v06.png","损毁动画-v06.png"}[scene]),ImageFormat.Png);
    }
   }
   encoder.StandardInput.Close();if(!encoder.WaitForExit(30000)||encoder.ExitCode!=0)throw new Exception("animation video export failed");
  }
  Console.WriteLine("Recorded 19-second native animation review: "+output);
 }
 internal void ProfileAnimationLoad(){
  ClientSize=new Size(1600,900);briefing=false;paused=false;selection.Clear();
  world=new Campaign(true);world.Units.Clear();world.Shots.Clear();world.Landing=true;
  var keys=new[]{"squad","at","ifv","tank","arty"};
  var shooters=new Piece[24];var targets=new Piece[24];
  for(int i=0;i<24;i++){
   float x=530+i%6*45,y=460+i/6*80;
   shooters[i]=world.Spawn(keys[i%keys.Length],0,x,y);targets[i]=world.Spawn(keys[i%keys.Length],1,x+17,y-32);targets[i].Hp=100000;
  }
  var factory=world.Spawn("factory",0,810,590);factory.Ready=.03f;
  world.ReviewReveal();CenterAt(new PointF(680,590));zoom=1.55f;hudDirty=true;
  var samples=new System.Collections.Generic.List<double>();
  using(var frame=new Bitmap(1600,900))using(var g=Graphics.FromImage(frame)){
   for(int i=0;i<270;i++){
    var watch=Stopwatch.StartNew();world.TickVisuals(1f/60);world.Time=world.VisualTime;
    factory.Ready=.03f+.95f*i/270;
    for(int n=0;n<24;n++)if((i+n)%30==0)world.ReviewFire(shooters[n],targets[n]);
    var save=g.Save();OnPaint(new PaintEventArgs(g,ClientRectangle));g.Restore(save);g.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
    if(i>89)samples.Add(watch.Elapsed.TotalMilliseconds);
    if(stampBytes>64L*1024*1024||world.Shots.Count>256)throw new Exception("effect cache/event cap exceeded");
   }
  }
  samples.Sort();Console.WriteLine("48-unit effects stress, 48 volleys/s + construction, 1600x900 offscreen: median {0:F2}ms p95 {1:F2}ms; sprite cache {2:F1} MiB (limit 64).",samples[samples.Count/2],samples[(int)(samples.Count*.95)],stampBytes/1048576.0);
 }
}
public partial class Campaign { internal void ReviewReveal(){foreach(var o in Units){var side=o.Side;float ready=o.Ready;o.Side=0;o.Ready=1;Reveal();o.Side=side;o.Ready=ready;}} }
}
