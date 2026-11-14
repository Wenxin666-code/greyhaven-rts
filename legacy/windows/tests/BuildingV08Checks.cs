using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 static byte[] BuildingPixels(Bitmap image,Rectangle rect){
  var data=image.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);var result=new byte[rect.Width*rect.Height*4];
  try{for(int y=0;y<rect.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),result,y*rect.Width*4,rect.Width*4);}finally{image.UnlockBits(data);}return result;
 }
 internal void CheckBuildingAssets(){
  drawingClient=false;int checkedFrames=0;
  foreach(var key in buildingRoster){
   var t=Campaign.Info(key);var spec=BuildingV08.Specs[key];if(t.FoundationWidth!=spec.Width||t.FoundationHeight!=spec.Depth)throw new Exception("foundation mismatch "+key);
   var body=BuildingPixels(painted[key],B08Source());var mask=BuildingPixels(painted[key+"-paint"],B08Source());int opaque=0,area=0;
   for(int p=0;p<body.Length;p+=4){if(body[p+3]>80)opaque++;if(mask[p+3]>80)area++;}
   if(area<opaque*.20)throw new Exception("insufficient faction paint area "+key+" "+area+"/"+opaque);
   foreach(var color in paintSwatches.Concat(new[]{Color.Black,Color.White,Color.FromArgb(17,239,111)})){
    using(var tinted=BuildingTint(painted[key],painted[key+"-paint"],B08Source(),color)){
     var output=BuildingPixels(tinted,B08Source());for(int p=0;p<body.Length;p+=4){
      if(output[p+3]!=body[p+3])throw new Exception("recolor changes silhouette "+key);
      for(int channel=0;channel<3;channel++)if((mask[p+channel]==0||color==Color.White)&&output[p+channel]!=body[p+channel])throw new Exception("recolor changes neutral material "+key);
     }
    }
   }
   int first=0,last=0;byte[] idleFirst=null,idleOther=null;
   foreach(string action in new[]{"build","idle"})for(int i=0;i<(action=="idle"?BuildingV08.IdleFrames:BuildFrames);i++){
    var name=B08Frame(key,action,i);var bitmap=painted[name];checkedFrames++;
    var crop=action=="idle"?spec.Idle:B08Source();if(bitmap.Size!=crop.Size||bitmap.Size!=painted[name+"-paint"].Size)throw new Exception("invalid frame size "+name);
    if(action=="idle"&&i==0)idleFirst=BuildingPixels(bitmap,new Rectangle(Point.Empty,bitmap.Size));
    if(action=="idle"&&i==11)idleOther=BuildingPixels(bitmap,new Rectangle(Point.Empty,bitmap.Size));
    if(action=="build"&&(i==0||i==BuildFrames-1)){
     var pixels=BuildingPixels(bitmap,B08Source());int count=0;for(int p=3;p<pixels.Length;p+=4)if(pixels[p]>80)count++;if(i==0)first=count;else{last=count;if(!pixels.SequenceEqual(body))throw new Exception("build endpoint "+key);if(!BuildingPixels(painted[name+"-paint"],B08Source()).SequenceEqual(mask))throw new Exception("build paint endpoint "+key);}
    }

    if(buildingFrameBytes>BuildingFrameBudget)throw new Exception("decoded frame budget exceeded");
   }
   if(first>=last*.85||first<40)throw new Exception("construction staging "+key);
   int motion=0;for(int p=0;p<idleFirst.Length;p+=4)if(Math.Abs(idleFirst[p]-idleOther[p])+Math.Abs(idleFirst[p+1]-idleOther[p+1])+Math.Abs(idleFirst[p+2]-idleOther[p+2])+Math.Abs(idleFirst[p+3]-idleOther[p+3])>4)motion++;
   if(!t.Civilian&&motion<1)throw new Exception("idle animation does not move "+key);
   // Fixed structures and foreground edges must still occlude animated parts.
   using(var combined=new Bitmap(320,320,PixelFormat.Format32bppPArgb))using(var graphics=Graphics.FromImage(combined)){
    DrawB08Idle(graphics,key,new Rectangle(0,0,320,320),Color.White,0,0);
    var pixels=BuildingPixels(combined,B08Source());int mismatch=0;
    for(int p=0;p<pixels.Length;p+=4)if(Math.Abs(pixels[p+3]-body[p+3])>32||Math.Abs(pixels[p]-body[p])+Math.Abs(pixels[p+1]-body[p+1])+Math.Abs(pixels[p+2]-body[p+2])>96)mismatch++;
    if(mismatch>1600)throw new Exception("idle decomposition/occlusion "+key+" mismatched="+mismatch);
    Console.WriteLine("{0}: paint {1:F1}%, 11 colors invariant, staged assembly + wreck endpoints, idle motion {2}px, composite mismatch {3}px.",key,100f*area/opaque,motion,mismatch);
   }
  }
  Console.WriteLine("{0} layered frames verified; decoded cache {1:F1}/64 MiB, {2} pairs retained.",checkedFrames,buildingFrameBytes/1048576.0,buildingFrames.Count);
 }
 internal void ProfileBuildingFrames(){
  drawingClient=true;scale=1;ox=oy=0;zoom=DefaultZoom;ClearStampCache();
  var samples=new System.Collections.Generic.List<double>();var cold=new System.Collections.Generic.List<double>();
  using(var frame=new Bitmap(1600,1000))using(var g=Graphics.FromImage(frame))for(int n=0;n<600;n++){
   var watch=Stopwatch.StartNew();g.Clear(C("#202c32"));
   for(int i=0;i<buildingRoster.Length;i++){
    var key=buildingRoster[i];var t=Campaign.Info(key);var box=BuildingRect(Campaign.Footprint(t,PointF.Empty),new PointF(220+i%4*330,270+i/4*300),DefaultZoom);
    DrawB08Idle(g,key,box,t.Civilian?UnitColor(-1):UnitColor(i%3),n/60f+i*.371f,key=="factory"?(float)(.5-.5*Math.Cos(n/60f*Math.PI)):0);
   }
   g.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);(n<300?cold:samples).Add(watch.Elapsed.TotalMilliseconds);
   if(buildingFrameBytes>BuildingFrameBudget||stampBytes>64L*1024*1024)throw new Exception("animation cache budget exceeded");
  }
  samples.Sort();cold.Sort();Console.WriteLine("11 active buildings at game scale, 600 frames: warm median {0:F2}ms p95 {1:F2}ms; first-cycle p95 {2:F2}ms. Frames {3:F1}/64 MiB; stamps {4:F1}/64 MiB.",samples[samples.Count/2],samples[(int)(samples.Count*.95)],cold[(int)(cold.Count*.95)],buildingFrameBytes/1048576.0,stampBytes/1048576.0);
  drawingClient=false;
 }
 void BuildingBoard(Graphics g,float time,int mode){
  g.Clear(C("#202c32"));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
  Txt(g,"建筑模型与动画 · V10",30,18,30,pale,true);
  Txt(g,mode==0?"搭建：基础 → 承重骨架 → 墙板 → 屋顶与设备":mode==1?"待机：风扇、阵面、天线、输送线、吊架与厂门":"摧毁：外壳破损 → 冒烟着火 → 最终燃爆 → 残骸",32,65,18,gold);
  for(int i=0;i<buildingRoster.Length;i++){
   string key=buildingRoster[i];var t=Campaign.Info(key);int x=i%4*400,y=i/4*332+92;var box=new RectangleF(x+40,y-20,320,320);Color color=t.Civilian?UnitColor(-1):paintSwatches[i%paintSwatches.Length];
   if(mode==1)DrawB08Idle(g,key,box,color,time,key=="factory"?(float)(.5-.5*Math.Cos(time*Math.PI)):0);
   else if(mode==0){float progress=Math.Min(1,time/2.8f);var name=B08Frame(key,"build",Math.Min(BuildFrames-1,(int)(progress*(BuildFrames-1))));DrawStamp(g,name,B08Source(),box,color);StructuralEffects(g,key,box,progress,false);}
   else if(time<1.1f){int level=Math.Min(3,(int)(time/.28f));DrawB08Idle(g,key,box,color,time,0,level);BuildingFireFx(g,key,box,time,level);}
   else{DrawFatalBuilding(g,key,box,color,time-1.1f);FatalBuildingFx(g,key,box,time-1.1f);}
   Txt(g,t.Name,x+26,y+283,20,pale,true);Txt(g,t.FoundationWidth+" × "+t.FoundationHeight+" 格",x+300,y+286,13,dim);
  }
  Txt(g,"实际运行素材 · 独立涂装层 · 原有建筑占地 · 旧版素材已保留",32,1110,17,dim);
 }
 internal void CaptureBuildingV08(string folder,bool video){
  Directory.CreateDirectory(folder);drawingClient=false;
  using(var board=new Bitmap(1600,1160))using(var graphics=Graphics.FromImage(board)){BuildingBoard(graphics,1,1);board.Save(Path.Combine(folder,"建筑建模-v10.png"),ImageFormat.Png);}
  using(var board=new Bitmap(1600,650))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,"同一建筑 · 任意势力涂装",28,18,29,pale,true);Txt(g,"玻璃、混凝土、金属、磨损边缘和阴影保持原色；透明轮廓不随颜色变化。",30,63,17,dim);
   for(int i=0;i<8;i++){float x=i%4*400,y=100+i/4*260;DrawStamp(g,"hq",B08Source(),new RectangleF(x+38,y-60,320,320),paintSwatches[i]);Rect(g,x+32,y+211,26,12,paintSwatches[i]);}
   board.Save(Path.Combine(folder,"建筑势力换色-v10.png"),ImageFormat.Png);
  }
  ClientSize=new Size(1600,900);modelReview=true;modelBuildings=true;modelUnit=0;modelClock=1;modelSpin=false;modelBuildingMode=0;briefing=true;hudDirty=true;
  using(var frame=new Bitmap(1600,900))using(var g=Graphics.FromImage(frame)){OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"建筑动画预览界面-v10.png"),ImageFormat.Png);}
  if(!video)return;
  drawingClient=true;scale=1;ox=oy=0;zoom=1;stampScale=scale;stampZoom=zoom;ClearStampCache();
  string output=Path.Combine(folder,"建筑动画-v10.mp4");
  var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("GREYHAVEN_FFMPEG")??"ffmpeg","-y -hide_banner -loglevel error -f rawvideo -pixel_format bgra -video_size 1600x1160 -framerate 30 -i pipe:0 -an -c:v libx264 -preset fast -crf 19 -pix_fmt yuv420p -movflags +faststart \""+output+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true};
  using(var encoder=Process.Start(info))using(var frame=new Bitmap(1600,1160,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(frame)){
   var bytes=new byte[1600*1160*4];for(int scene=0;scene<3;scene++)for(int i=0;i<120;i++){
    BuildingBoard(g,i/30f,scene);var bits=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
    try{Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length);}finally{frame.UnlockBits(bits);}
   }
   encoder.StandardInput.Close();if(!encoder.WaitForExit(30000)||encoder.ExitCode!=0)throw new Exception("building animation recording failed");
  }
  drawingClient=false;Console.WriteLine("Native 11-building animation review: "+output);
 }
}
}
class BuildingV08Checks {
 [STAThread]static int Main(string[] args){try{using(var window=new Greyhaven.CommandWindow()){window.CheckBuildingAssets();window.ProfileBuildingFrames();if(args.Length>0)window.CaptureBuildingV08(args[0],args.Length>1&&args[1]=="--video");}return 0;}catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
