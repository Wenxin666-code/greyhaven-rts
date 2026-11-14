using System;using System.Drawing;using System.Drawing.Imaging;using System.Linq;using System.Diagnostics;using System.IO;using System.Collections.Generic;using System.Windows.Forms;using Greyhaven;
class PerformanceNativeChecks {[STAThread]static int Main(string[] args){try{using(var w=new CommandWindow())w.MeasureNative(args[0],args.Length>1?int.Parse(args[1]):580);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}}
namespace Greyhaven {
public partial class Campaign {internal void PerfObserver(){ObserverDevelop();}}
partial class CommandWindow {
 internal void MeasureNative(string folder,int warmSeconds){
  Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);StartIslandMission();pulse.Stop();briefing=false;paused=false;ChangeMatchSpeed(4);
  var warm=Stopwatch.StartNew();float next=0;for(int i=0;i<warmSeconds*10&&!world.Won&&!world.Lost;i++){if(world.Time>=next){next=world.Time+.75f;world.PerfObserver();}world.Tick(.1f);if(i%1200==1199)Console.WriteLine("warm="+world.Time+" wall="+warm.Elapsed.TotalSeconds.ToString("F1"));}
  if(world.Won||world.Lost)throw new Exception("benchmark ended in warmup");
  world.PrepareBattleNavigation();CenterAt(world.Home(0));selection.Clear();selection.AddRange(world.Live(0).Where(u=>!u.T.Building).Take(16));
  var intervals=new List<double>();var draws=new List<double>();var updates=new List<double>();
  using(var canvas=new NativeTempoCanvas(1920,1080)){
   OnPaint(new PaintEventArgs(canvas.Surface,ClientRectangle));
      var real=Stopwatch.StartNew();double previous=0;float began=world.Time;int frames=0;
   while(real.Elapsed.TotalSeconds<10){double now=real.Elapsed.TotalSeconds;if(now-previous<1.0/60){System.Threading.Thread.Sleep(1);continue;}
    intervals.Add((now-previous)*1000);var timer=Stopwatch.StartNew();AdvanceMatch(now-previous);previous=now;updates.Add(timer.Elapsed.TotalMilliseconds);timer.Restart();
    Pan(frames%120<60?2:-2,frames%120<60?1:-1);OnPaint(new PaintEventArgs(canvas.Surface,ClientRectangle));draws.Add(timer.Elapsed.TotalMilliseconds);frames++;
   }
   intervals.Sort();draws.Sort();updates.Sort();var c=System.Globalization.CultureInfo.InvariantCulture;
   string result=String.Format(c,"{{\"game_seconds\":{0:F3},\"real_seconds\":{1:F3},\"actual_rate\":{2:F3},\"frames\":{3},\"frame_p95_ms\":{4:F3},\"simulation_p95_ms\":{5:F3},\"draw_p95_ms\":{6:F3},\"alive\":{7},\"warm_seconds\":{8:F2} }}",world.Time-began,previous,(world.Time-began)/Math.Max(.001,previous),frames,intervals[(int)(frames*.95)],updates[(int)(frames*.95)],draws[(int)(frames*.95)],world.Units.Count(u=>u.Alive),warm.Elapsed.TotalSeconds-real.Elapsed.TotalSeconds);
   File.WriteAllText(Path.Combine(folder,"native.json"),result);Console.WriteLine(result);canvas.Save(Path.Combine(folder,"native.png"));

  }
 }
 sealed class NativeTempoCanvas:IDisposable {
  IntPtr dc,dib,previous;internal Bitmap Image;internal Graphics Surface;
  [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern bool GdiFlush();
  internal NativeTempoCanvas(int width,int height){
   IntPtr pixels;var header=new DibHeader{Size=40,Width=width,Height=-height,Planes=1,BitCount=32};dc=CreateCompatibleDC(IntPtr.Zero);dib=CreateDIBSection(dc,ref header,0,out pixels,IntPtr.Zero,0);previous=SelectObject(dc,dib);
   if(dc==IntPtr.Zero||dib==IntPtr.Zero||previous==IntPtr.Zero||previous==new IntPtr(-1))throw new Exception("native benchmark surface");
   Image=new Bitmap(width,height,width*4,PixelFormat.Format32bppPArgb,pixels);Surface=Graphics.FromHdc(dc);
  }
  internal void Save(string path){Surface.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);GdiFlush();Image.Save(path,ImageFormat.Png);}
  public void Dispose(){if(Surface!=null)Surface.Dispose();if(Image!=null)Image.Dispose();if(previous!=IntPtr.Zero)SelectObject(dc,previous);if(dib!=IntPtr.Zero)DeleteObject(dib);if(dc!=IntPtr.Zero)DeleteDC(dc);}
 }
}}
