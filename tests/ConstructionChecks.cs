using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class ConstructionChecks {
 [STAThread]static int Main(string[] args){try{new Campaign(true).CheckConstruction();new Campaign(true).CheckEnemyConstruction();if(args.Length>0)using(var f=new CommandWindow())f.CheckConstructionInput(args[0]);return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void ExpectBuild(bool ok,string reason){if(!ok)throw new Exception(reason);}
 void AdvanceBuild(float seconds){for(int i=0;i<(int)Math.Ceiling(seconds*10);i++)Tick(.1f);}
 internal void CheckEnemyConstruction(){
  Units.Clear();Ore.Clear();var home=Home(1);Funds[1]=1800;Time=241;enemyTurns[1]=1;OreReceipts[1]=180;
  string[] keys={"hq","power","power","power","refinery","barracks","factory","radar","miner"};
  for(int i=0;i<keys.Length;i++)Spawn(keys[i],1,home.X+i%3*96-96,home.Y+i/3*96-96);
  for(int x=0;x<MapFogCols;x++)for(int y=0;y<MapFogRows;y++)discovered[1,x,y]=true;
  ExpectBuild(StartConstruction(Info("satellite"),1),"AI satellite order");TickConstruction(32);
  ExpectBuild(Construction(Info("satellite"),1).Ready&&Funds[1]<1100,"paid AI order falls below planning budget");EnemyEconomy(1);
  ExpectBuild(Units.Any(u=>u.Side==1&&u.T.Key=="satellite"),"AI places paid ready building without rechecking full starting budget");
  Console.WriteLine("AI uses the same paid construction and places ready satellite below its original planning budget.");
 }
 internal void CheckConstruction(){
  Units.Clear();Ore.Clear();Funds[0]=1000;var hq=Spawn("hq",0,600,600);Spawn("squad",1,2000,1400);
  for(int x=0;x<MapFogCols;x++)for(int y=0;y<MapFogRows;y++)discovered[0,x,y]=true;
  var power=Info("power");var site=new PointF(540,600);
  ExpectBuild(!Build(power,site),"placing unproduced building");ExpectBuild(StartConstruction(power),"start build");
  ExpectBuild(Funds[0]==1000&&!Units.Any(u=>u.T==power),"production starts without a map foundation or lump payment");
  AdvanceBuild(1);var job=Construction(power);int paid=job.Paid;float progress=job.Work;
  ExpectBuild(paid>0&&paid<power.Price&&Funds[0]==1000-paid,"progressive cost");HoldConstruction(power);AdvanceBuild(2);
  ExpectBuild(job.Work==progress&&job.Paid==paid,"manual pause");StartConstruction(power);Funds[0]=0;AdvanceBuild(2);
  ExpectBuild(job.Work==progress&&ConstructionState(job)=="等待资金","funds pause");Funds[0]=1000-paid;AdvanceBuild(10);
  ExpectBuild(job.Ready&&job.Paid==power.Price&&Funds[0]==830,"complete exact cost");
  ExpectBuild(!Build(power,hq.P)&&Construction(power)==job,"blocked placement retains ready item");
  ExpectBuild(!StartConstruction(Info("barracks")),"same column waits for ready placement");ExpectBuild(StartConstruction(Info("post")),"defense is independent column");
  ExpectBuild(Build(power,site)&&Funds[0]==830,"place already paid building");var plant=Units.Last(u=>u.T==power);ExpectBuild(!plant.Deployed,"brief assembly");AdvanceBuild(1.3f);ExpectBuild(plant.Deployed,"short assembly independent of production time");
  StartConstruction(Info("barracks"));AdvanceBuild(1);var barracksJob=Construction(Info("barracks"));int before=Funds[0],refund=barracksJob.Paid;HoldConstruction(barracksJob.Type);HoldConstruction(barracksJob.Type);ExpectBuild(Construction(barracksJob.Type)==null&&Funds[0]==before+refund,"cancel only spent credits");
  var factory=Spawn("factory",0,900,600);var adjacent=new PointF(factory.P.X,Footprint(factory.T,factory.P).Bottom+Tile);
  ExpectBuild(CanPlace(power,adjacent,null),"production exit is not reserved foundation");Spawn("power",0,adjacent.X,adjacent.Y);ExpectBuild(!ExitClear(factory,Info("tank"),Middle(ExitApron(factory.T,Footprint(factory.T,factory.P)))),"blocked exit still operationally blocked");
  hq.Hp=hq.T.Hp*.4f;StartConstruction(Info("barracks"));var pending=Construction(Info("barracks"));float work=pending.Work;
  ExpectBuild(CanRelocate(hq,new PointF(650,700)),"relocation route ignores own old footprint");ExpectBuild(Relocate(hq,new PointF(650,700)),"relocate");AdvanceBuild(1.3f);
  ExpectBuild(hq.T.Key=="mcv"&&Math.Abs(hq.Hp/hq.T.Hp-.4)<.001&&pending.Work==work,"health proportion and suspended construction survive packing");
  Halt(hq);ExpectBuild(Deploy(hq),"redeploy");AdvanceBuild(1.3f);ExpectBuild(hq.T.Key=="hq"&&Math.Abs(hq.Hp/hq.T.Hp-.4)<.001,"redeploy cannot heal");AdvanceBuild(1);ExpectBuild(pending.Work>work,"construction resumes after redeployment");
  int cash=Funds[0];Train(Info("ifv"),0,null);ExpectBuild(Funds[0]==cash,"unit queue not prepaid");UnitTick(factory,1,true);int spent=factory.WorkPaid;ExpectBuild(spent>0,"unit progressive payment");cash=Funds[0];CancelOrder(factory,0);ExpectBuild(Funds[0]==cash+spent,"unit paid portion refunded");
  plant.Hp=100;cash=Funds[0];ToggleRepair(plant);UnitTick(plant,1,true);ExpectBuild(plant.Hp>100&&Funds[0]<cash,"paid building repair");cash=Funds[0];ExpectBuild(Sell(plant)&&Funds[0]==cash+power.Price/2,"sell refund");UnitTick(plant,2,true);ExpectBuild(!plant.Alive,"sale removes building without wreck");
  Console.WriteLine("Construction: sidebar work/payment/pause/cancel, independent defense, placement, unreserved exits, MCV health/orders, unit refunds, repair/sell passed.");
 }
}
partial class CommandWindow {
 internal void CheckConstructionInput(string folder){
  StartMission(true,false);ClientSize=new Size(1920,1080);var mcv=world.Live(0).First(u=>u.T.Key=="mcv");selection.Clear();selection.Add(mcv);mouse=ScreenOf(mcv.P);
  if(DescribePointer(Keys.None).Kind!=PointerKind.Deploy)throw new Exception("MCV deploy cursor");ContextOrder(Keys.None);
  for(int i=0;i<15;i++)world.Tick(.1f);if(mcv.T.Key!="hq"||!mcv.Deployed)throw new Exception("repeat click deploy");
  ProductionTab(4);BuildCardClick(Campaign.Info("power"));if(place!=null)throw new Exception("unbuilt placement preview");
  for(int i=0;i<12;i++)world.Tick(.1f);BuildCardRight(Campaign.Info("power"));if(!world.Construction(Campaign.Info("power")).Paused)throw new Exception("right click pause");BuildCardClick(Campaign.Info("power"));
  for(int i=0;i<80;i++)world.Tick(.1f);ProductionTab(4);if(place!=Campaign.Info("power"))throw new Exception("Q ready placement");
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   mouse=ScreenOf(new PointF(260,550));hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"建筑就绪与逐格放置.png"),ImageFormat.Png);
   if(!world.Build(place,new PointF(260,550)))throw new Exception("ready UI placement");place=null;for(int i=0;i<14;i++)world.Tick(.1f);
   StructureTool(StructureMode.Repair);var power=world.Live(0).First(u=>u.T.Key=="power");power.Hp=100;mouse=ScreenOf(power.P);ContextOrder(Keys.None);if(!power.Repairing)throw new Exception("K repair click");StructureTool(StructureMode.Repair);
   mouse=ScreenOf(new PointF(320,470));selection.Clear();selection.Add(mcv);ContextOrder(Keys.None);if(mcv.Packing<=0)throw new Exception("base left click ground repack");
   hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"基地收起与搬迁.png"),ImageFormat.Png);
  }
  Console.WriteLine("Native inputs: deploy cursor/repeat click, production clicks, Q ready placement, repair tool and base relocation passed.");
 }
}
}
