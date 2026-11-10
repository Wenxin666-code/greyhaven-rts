using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class CampaignStoryInterfaceChecks {
 [STAThread]static int Main(string[] args){try{using(var f=new CommandWindow())f.CheckStoryInterface(args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {partial class CommandWindow {
 internal void CheckStoryInterface(string folder){
  Directory.CreateDirectory(folder);pulse.Stop();ClientSize=new Size(1920,1080);
  using(var frame=new Bitmap(1920,1080))using(var graphics=Graphics.FromImage(frame)){
   Action<string> capture=name=>{hudDirty=true;OnPaint(new PaintEventArgs(graphics,ClientRectangle));frame.Save(Path.Combine(folder,name+".png"),ImageFormat.Png);};
   StartRiverMission();paused=true;capture("01-河湾任务简报");paused=false;diplomacyOpen=true;capture("02-河湾阵营");diplomacyOpen=false;
   StartIslandMission();diplomacyOpen=true;capture("03-五路武装");diplomacyOpen=false;
   var home=world.Home(0);world.Spawn("squad",3,home.X+2*Campaign.Tile,home.Y);world.Tick(3);world.Radio="矿石已入账。";
   if(String.IsNullOrEmpty(world.StoryNotice)||world.StoryNoticeUntil<=world.Time)throw new Exception("no retained faction contact notice");capture("04-侦察情报");
   paused=true;capture("05-孤岛任务简报");paused=false;world.Won=true;world.TickVisuals(3);capture("06-通关衔接");
   var fallback=new[]{"victory","time"};
   if(!StoryVictoryLines(fallback)[1].Contains("雾脊"))throw new Exception("third mission story transition");
   StartRiverMission();if(!StoryVictoryLines(fallback)[1].Contains("西北"))throw new Exception("second mission transition");
   LaunchMission();if(!StoryVictoryLines(fallback)[1].Contains("河湾"))throw new Exception("training story transition");
   StartRelayMission();if(StoryVictoryLines(fallback)!=fallback||!world.RelayMission||RadarOnline())throw new Exception("relay entry changed");
  }
  Console.WriteLine("Story UI: mission briefings, five faction identities, retained contact message, first-three victory links and relay entry passed.");
 }
}}
