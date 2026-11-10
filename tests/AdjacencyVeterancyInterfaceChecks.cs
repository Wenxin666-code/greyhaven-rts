using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Greyhaven;

class AdjacencyVeterancyInterfaceChecks {
 [STAThread]static int Main(string[] args){try{using(var window=new CommandWindow())window.CheckVeterancyInterface(args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
partial class CommandWindow {
 internal void CheckVeterancyInterface(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);pulse.Stop();
  briefing=true;mapReview=false;rulesOpen=true;veterancyRules=true;commandRules=false;unitRoster=false;
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action<string> capture=name=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,name),ImageFormat.Png);};
   capture("01-晋升与扩建.png");
   FrontKey(Keys.Right);if(veterancyRules||commandRules||unitRoster)throw new Exception("rules forward navigation wraps to first page");
   FrontKey(Keys.Left);if(!veterancyRules)throw new Exception("rules backward navigation reaches new page");
   FrontKey(Keys.Left);if(veterancyRules||!commandRules)throw new Exception("leaving new page enables classic controls");
   StartMission(true,false);pulse.Stop();briefing=false;world.Units.Clear();world.Ore.Clear();selection.Clear();
   world.Spawn("hq",0,480,600);world.Spawn("mcv",1,2600,1700);
   world.Tick(.01f);
   var at=new PointF(576,600);CenterAt(at);mouse=ScreenOf(at);place=Campaign.Info("power");
   capture("02-普通建筑间距.png");place=Campaign.Info("post");capture("03-炮台间距.png");
  }
  Console.WriteLine("PASS native UI: new rules page and normal/defensive placement hints rendered at 1920x1080.");
 }
}
}
