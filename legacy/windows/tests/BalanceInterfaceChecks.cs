using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Greyhaven;
class BalanceInterfaceChecks {
 [STAThread]static int Main(string[] args){try{using(var window=new CommandWindow())window.CheckBalanceInterface(args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {internal void BalancePreviewReveal(){for(int y=0;y<MapFogRows;y++)for(int x=0;x<MapFogCols;x++)Discover(0,x,y);}}
partial class CommandWindow {
 internal void CheckBalanceInterface(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);StartMission(true,false);pulse.Stop();world.Units.Clear();world.Ore.Clear();briefing=false;selection.Clear();
  world.Spawn("hq",0,480,600);world.Spawn("mcv",1,2800,1600);world.Spawn("power",0,420,480);
  world.BalancePreviewReveal();var at=new PointF(600,648);place=Campaign.Info("post");CenterAt(at);mouse=ScreenOf(at);hudDirty=true;
  CaptureBalance(folder,"01-炮台建造圆形射程.png");
  place=null;var tower=world.Spawn("post",0,at.X,at.Y);selection.Add(tower);hudDirty=true;
  CaptureBalance(folder,"02-选中炮台射程.png");
  briefing=true;rulesOpen=true;unitRoster=true;commandRules=false;selection.Clear();hudDirty=true;
  CaptureBalance(folder,"03-单位图鉴.png");
  Console.WriteLine("PASS: native construction/selection circle and current unit roster previews saved.");
 }
 void CaptureBalance(string folder,string name){using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,name),ImageFormat.Png);}}
}
}
