using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Forms;
namespace Greyhaven {partial class CommandWindow {
 internal void CheckBlastV11(){
  drawingClient=false;EnsureBlastArt();
  if(painted["cloud-v11"].Size!=new Size(1536,2304)||painted["debris-v11"].Size!=new Size(2048,2048))throw new Exception("blast atlas dimensions");
  float maximumLife=0;int profiles=0;
  foreach(string key in buildingRoster)for(int seed=0;seed<12;seed++)foreach(float ready in new[]{.15f,.5f,1f}){
   var parts=BlastProfile(key,seed,ready);if(parts.Length<8||parts.Select(p=>p.Kind).Distinct().Count()!=4)throw new Exception("missing rigid debris types");
   foreach(var p in parts){maximumLife=Math.Max(maximumLife,p.Release+p.Life);float lastX=0;for(int step=0;step<80;step++){float age=p.Release+p.Life*step/79;var v=p.Sample(age);if(v.Y< -.0001f)throw new Exception("debris penetrates ground");var repeat=p.Sample(age);if(repeat.X!=v.X||repeat.Y!=v.Y||repeat.Z!=v.Z)throw new Exception("frame-rate-dependent trajectory");lastX=v.X;}
    var hit=p.Sample(p.Release+p.Impact);if(Math.Abs(hit.Y)>.0001f||p.Opacity(p.Release+p.Life+.001f)!=0)throw new Exception("impact / cleanup continuity");
   }profiles++;
  }
  if(maximumLife+TerminalBlastAt>DestructionSeconds+.20f)throw new Exception("outcome cuts off shrapnel");if(blastProfiles.Count>128)throw new Exception("debris profile budget");
  foreach(int kind in new[]{0,1,2,3})for(int yaw=0;yaw<8;yaw++){
   byte[] first=null;int different=0;
   for(int pose=0;pose<32;pose++){
    int frame=(kind*8+yaw)*32+pose;var crop=new Rectangle(frame%32*64,frame/32*64,64,64);var a=BuildingPixels(painted["debris-v11"],crop);int ink=0;for(int i=3;i<a.Length;i+=4)if(a[i]>80)ink++;if(ink<8)throw new Exception("missing / clipped rigid fragment");if(first==null)first=a;else if(!first.SequenceEqual(a))different++;
    if(pose%8==0)using(var tinted=BuildingTint(painted["debris-v11"],painted["debris-v11-paint"],crop,Color.FromArgb(173,31,222))){var b=BuildingPixels(tinted,new Rectangle(0,0,64,64));for(int i=3;i<a.Length;i+=4)if(a[i]!=b[i])throw new Exception("debris faction tint alpha");}
   }if(different<24)throw new Exception("rigid pieces do not tumble");
  }
  world=new Campaign(true);world.Units.Clear();world.Spawn("mcv",0,600,600);var beforeFunds=(int[])world.Funds.Clone();int beforeCount=world.Units.Count;
  using(var image=new Bitmap(640,460))using(var g=Graphics.FromImage(image)){
   float z=DefaultZoom;var box=BuildingRect(Campaign.Footprint(Campaign.Info("factory"),PointF.Empty),new PointF(320,290),z);
   foreach(var p in BlastProfile("factory",91,1))if(p.Opacity(.40f)>0)DrawShard(g,p,ProjectShard(new PointF(320,290),p.Sample(.40f),z),z,.40f,UnitColor(0));
   var pixels=BuildingPixels(image,new Rectangle(0,0,640,460));int solid=0;for(int i=3;i<pixels.Length;i+=4)if(pixels[i]>120)solid++;if(solid<160)throw new Exception("rigid fragments not legible at default zoom: "+solid);
   g.Clear(Color.Transparent);DrawBlastDebris(g,"factory",box,1.5f,91,1,UnitColor(0),false);DrawBlastDebris(g,"factory",box,1.5f,91,1,UnitColor(0),true);pixels=BuildingPixels(image,new Rectangle(0,0,640,460));if(pixels.Any(v=>v!=0))throw new Exception("debris left a persistent wreck");
   if(world.Units.Count!=beforeCount||!beforeFunds.SequenceEqual(world.Funds))throw new Exception("visual debris changed gameplay");
   Console.WriteLine("Default zoom rigid debris area: "+solid+" opaque pixels; no persistent mesh / collision object.");
  }
  Console.WriteLine(profiles+" deterministic trajectories, 1024 rigid poses, arbitrary faction tint, impact/bounce/cleanup passed; longest debris "+maximumLife.ToString("F3")+"s.");
  drawingClient=true;scale=1;ox=oy=0;zoom=DefaultZoom;ClearStampCache();stampScale=scale;stampZoom=zoom;
  var samples=new List<double>();using(var image=new Bitmap(1600,900))using(var g=Graphics.FromImage(image))for(int i=0;i<150;i++){
   float age=.08f+(i%75)/75f*1.20f;var watch=Stopwatch.StartNew();g.Clear(C("#394442"));for(int n=0;n<3;n++){
    var t=Campaign.Info(new[]{"factory","power","radar"}[n]);var box=BuildingRect(Campaign.Footprint(t,PointF.Empty),new PointF(290+n*490,670),DefaultZoom);
    DrawBlastDebris(g,t.Key,box,age,n+17,1,UnitColor(n),true);DrawBlastCloud(g,SocketPoint(box,new PointF(160,215)),110*DefaultZoom,age/.98f);DrawBlastDebris(g,t.Key,box,age,n+17,1,UnitColor(n),false);DrawBlastEmbers(g,t.Key,box,age,n+17);
   }g.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);if(i>=75)samples.Add(watch.Elapsed.TotalMilliseconds);if(debrisColorFrames.Count>1280)throw new Exception("debris image budget");
  }samples.Sort();Console.WriteLine("Three explosion effect layers only, warm median "+samples[samples.Count/2].ToString("F2")+"ms; p95 "+samples[(int)(samples.Count*.95)].ToString("F2")+"ms; color frames "+debrisColorFrames.Count+"/1280.");drawingClient=false;
 }
 internal void CaptureBlastV11(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1600,900);string file=Path.Combine(folder,"弹片与爆炸实机-v11.mp4");var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("GREYHAVEN_FFMPEG")??"ffmpeg","-y -hide_banner -loglevel error -f rawvideo -pixel_format bgra -video_size 1600x900 -framerate 30 -i pipe:0 -an -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p -movflags +faststart \""+file+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true};
  using(var encoder=Process.Start(info))using(var frame=new Bitmap(1600,900,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame)){
   var bytes=new byte[1600*900*4];for(int scene=0;scene<3;scene++){
    var parts=ConfigureStructureScene(1);foreach(var p in parts)p.Hp=p.T.Hp*.18f;
    if(scene==1){world.Units.Remove(parts[0]);world.Units.Remove(parts[2]);world.StructureKill(parts[1]);parts[1].Dead=1.10f;}
    for(int i=0;i<180;i++){
     g.ResetTransform();g.ResetClip();
     if(scene==0){for(int n=0;n<parts.Length;n++)if(i==30+n*6)world.StructureKill(parts[n]);world.TickVisuals(1f/30);}
     else world.TickVisuals(1f/90);
     world.StructureReveal();
     if(scene<2)OnPaint(new PaintEventArgs(g,ClientRectangle));else{
      drawingClient=false;
      g.Clear(C("#202c32"));var rect=new RectangleF(80,290,500,500);float age=1.10f+i/90f;DrawFatalBuilding(g,"factory",rect,UnitColor(0),age);FatalBuildingFx(g,"factory",rect,age,1,49,UnitColor(0));
      string[] names={"金属板 · 有厚度和折边","混凝土 · 不规则断面","弯曲构件 · 长条轮廓","设备碎块 · 格栅与外壳"};
      for(int n=0;n<4;n++){int pose=((i+n*7)/2)%32,index=(n*8+2)*32+pose;var sprite=DebrisColorFrame(index,n==3?UnitColor(2):UnitColor(0));g.DrawImage(sprite,new Rectangle(920+n%2*270,255+n/2*240,160,160));Txt(g,names[n],875+n%2*270,420+n/2*240,16,pale,true);}
      Txt(g,"局部放大 · 与实机相同的碎片和轨迹",65,810,20,dim);
     }
     g.ResetTransform();g.ResetClip();using(var brush=new SolidBrush(Color.FromArgb(255,25,37,39)))g.FillRectangle(brush,12,70,1370,78);using(var font=new Font("Microsoft YaHei UI",20,FontStyle.Bold))using(var brush=new SolidBrush(Color.FromArgb(236,181,78)))g.DrawString(scene==0?"01  独立弹片、翻卷烟团与落地反应 · 正常速度":scene==1?"02  弹片飞行、翻转和烟尾 · 1/3 速查看":"03  弹片模型与运动细节 · 局部放大",font,brush,28,88);
     if(i==(scene==0?83:scene==1?55:55))frame.Save(Path.Combine(folder,new[]{"弹片正常镜头-v11.png","弹片翻转与烟尾-v11.png","弹片模型与燃爆-v11.png"}[scene]),ImageFormat.Png);
     if(scene==0&&i==179)frame.Save(Path.Combine(folder,"弹片清除与弹坑-v11.png"),ImageFormat.Png);
     var data=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(data.Scan0,bytes,0,bytes.Length);encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);}finally{frame.UnlockBits(data);}
    }
   }encoder.StandardInput.Close();if(!encoder.WaitForExit(30000)||encoder.ExitCode!=0)throw new Exception("V11 recording failed");
  }
  EnsureBlastArt();painted["cloud-v11"].Save(Path.Combine(folder,"blast-cloud-v11.png"),ImageFormat.Png);painted["debris-v11"].Save(Path.Combine(folder,"debris-base-v11.png"),ImageFormat.Png);painted["debris-v11-paint"].Save(Path.Combine(folder,"debris-paint-v11.png"),ImageFormat.Png);
  Console.WriteLine("Saved native V11 blast/debris review: "+file);
 }
}}
class BlastEffectChecks {
 [STAThread]static int Main(string[] args){try{using(var window=new Greyhaven.CommandWindow()){window.CheckBlastV11();window.CheckStructuralAnimation();if(args.Length>0)window.CaptureBlastV11(args[0]);}return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
