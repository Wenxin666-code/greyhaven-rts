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
   CheckRiverbendStory(capture);
   paused=true;capture("01-河湾任务简报");paused=false;diplomacyOpen=true;capture("02-河湾阵营");diplomacyOpen=false;
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

 void CheckRiverbendStory(Action<string> capture){
  StartRiverMission();
  var mission=world;var scene=world.RiverbendOperation;var baseCar=scene.CommandVehicle;var start=baseCar.P;
  var front=world.Units.Where(u=>u.Side>0&&!u.T.Building).ToArray();
  var sentries=front.Where(scene.IsImpoundGuard).ToArray();
  if(sentries.Length!=3||sentries.Any(u=>u.Side!=1||u.T.Key!="squad"||!u.GuardHome.HasValue))
   throw new Exception("red riflemen must guard the impound");
  var engineer=world.Live(0).Single(u=>u.T.Key=="engineer");
  var house=world.Units.First(u=>u.Side<0&&u.T.Civilian);
  var neutralTruck=world.Units.First(scene.IsSeizedTruck);
  if(world.CanEngineer(engineer,neutralTruck)||PreselectionLabel(neutralTruck).Contains("工兵可占领")||
     !world.CanEngineer(engineer,house)||!PreselectionLabel(house).Contains("工兵可占领"))
   throw new Exception("capture tooltip disagrees with legal engineer orders");
  for(int side=1;side<=2;side++)
   if(!new[]{"hq","power","refinery","barracks","factory","miner"}.All(key=>world.Live(side).Any(u=>u.T.Key==key)))
    throw new Exception("rival camp is missing its starting economy or production");
  TakeRiverbendCommand();
  if(!scene.Opening||scene.Ready)throw new Exception("river opening skipped");
  for(int i=0;i<35;i++)AdvanceRiverbendScene(.1,true);
  if(!front.Any(u=>u.Side==1&&u.FiredAt>=0)||!front.Any(u=>u.Side==2&&u.FiredAt>=0))
   throw new Exception("both sides should be fighting before the camera leaves the west bank");
  for(int i=35;i<100;i++)AdvanceRiverbendScene(.1,true);
  capture("河湾-东岸交火");
  if(!front.Any(u=>u.FiredAt>=0)||!front.Any(u=>u.Hp<u.T.Hp))throw new Exception("opening did not stage real fighting");
  for(int i=0;i<145&&!scene.Ready;i++)AdvanceRiverbendScene(.1,true);
  capture("河湾-接管");
  if(!sentries.Any(u=>u.Alive)||sentries.Any(u=>u.Alive&&(!u.GuardHome.HasValue||
       Campaign.Dist(u.GuardHome.Value,RiverbendMap.PortPoint(59,27))>4*Campaign.Tile)))
   throw new Exception("impound guards were reassigned or abandoned their post");
  if(!scene.Ready||world.Time<30||baseCar.P==start||!world.CanDeploy(baseCar)||
     !front.Any(u=>u.Alive&&u.Side==1)||!front.Any(u=>u.Alive&&u.Side==2))
   throw new Exception("river opening must deliver both rival detachments and a deployable base car: "+baseCar.P);
  for(int side=1;side<=2;side++)
   if(!world.Units.Any(u=>u.Side==side&&u.FiredAt>24))
    throw new Exception("rival fire stopped while the camera was returning west: "+side);
  var button=hits.Single(hit=>hit.Id=="接管先遣队");button.Run();
  if(world!=mission||scene.Guidance!=RiverbendOperation.HandoffRadio||!selection.Contains(baseCar)||
     world.Explored(0,world.Home(1))||world.Explored(0,world.Home(2))||!world.Explored(0,RiverbendMap.PortPoint(63,34)))
   throw new Exception("river handoff did not preserve restricted intelligence and actors");
  capture("河湾-开局视野");
  var bridge=scene.Bridge;
  var escort=world.Live(0).First(u=>u.T.Key=="ifv");
  var farBank=RiverbendMap.PortPoint(bridge.Right+2,34);
  if(!world.Move(escort,farBank,false))throw new Exception("formation could not use the bridge");
  // Cinematic driving uses normal swept movement. Let the actual battle hook observe the crossing.
  for(int i=0;i<1600&&!scene.Crossed;i++){
   world.MoveHarborVehicle(escort,(float)MatchTempo.Step);
   scene.UpdateBattleGuidance();
  }
  if(!scene.Crossed||scene.Guidance!=RiverbendOperation.CrossingRadio)throw new Exception("east-bank trigger or crossing copy");
  if(!world.Deploy(baseCar))throw new Exception("could not deploy the original base before victory");
  for(int i=0;i<140&&(!baseCar.Deployed||baseCar.T.Key!="hq");i++)world.Tick((float)MatchTempo.Step);
  if(!baseCar.Deployed||baseCar.T.Key!="hq")throw new Exception("base did not deploy");
  var infantry=world.Live(0).First(u=>u.T.Key=="squad");
  var basePosition=baseCar.P;
  float infantryTravel=infantry.Travel;
  foreach(var enemy in world.Units.Where(u=>u.Side>0))enemy.Hp=0;
  world.Tick((float)MatchTempo.Step);
  world.TickVisuals(Campaign.BuildingDestructionSeconds+.4f);
  BeginRiverbendDeparture();
  if(!scene.Departing)throw new Exception("river victory did not enter road reopening");
  var truckStart=scene.RoadTruck.P;
  for(int i=0;i<85;i++)AdvanceRiverbendScene(.1,true);
  capture("河湾-公路开放");
  for(int i=0;i<115&&!scene.Result;i++)AdvanceRiverbendScene(.1,true);
  capture("河湾-通车结算");
  if(!scene.Result||scene.RoadTruck.P.X<=truckStart.X||escort.Path.Count==0||
     baseCar.T.Key!="mcv"||baseCar.P.X<=basePosition.X+4*Campaign.Tile||infantry.Travel<=infantryTravel+4*Campaign.Tile||
     !hits.Any(hit=>hit.Id=="下一关 · 孤岛要塞"))throw new Exception("river result must show the actual base, infantry and escort on the road");
  var onResult=escort.P;
  var baseAtResult=baseCar.P;
  for(int i=0;i<12;i++)AdvanceRiverbendScene(.1,true);
  if(escort.P.X<=onResult.X||baseCar.P.X<=baseAtResult.X)throw new Exception("road column stopped under the result");
  hits.Single(hit=>hit.Id=="下一关 · 孤岛要塞").Run();
  if(!world.IslandMission)throw new Exception("river result must lead to the island mission");
  StartRiverMission();
  for(int i=0;i<200&&!world.RiverbendOperation.Ready;i++)AdvanceRiverbendScene(.1,true);
  TakeRiverbendCommand();
  Console.WriteLine("Riverbend chapter: live crossfire, handoff, fog, bridge radio, reopening, moving result and island transition passed.");
 }
}}
