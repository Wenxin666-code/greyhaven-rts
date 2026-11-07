using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class EconomyChecks {
 [STAThread]static int Main(string[] args){try{
  new Campaign(true).CheckEngineering();new Campaign(true).CheckFactories();new Campaign(true).CheckOreChoice();new Campaign(true).CheckSatellite();
  if(args.Length>0)using(var f=new CommandWindow())f.CaptureEconomy(args[0]);return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void AssertEconomy(bool ok,string message){if(!ok)throw new Exception(message);}
 void EmptyEconomy(){Units.Clear();Ore.Clear();for(int s=0;s<3;s++)Funds[s]=5000;for(int s=0;s<3;s++)for(int x=0;x<MapFogCols;x++)for(int y=0;y<MapFogRows;y++)discovered[s,x,y]=true;}
 internal void CheckEngineering(){
  EmptyEconomy();var house=Spawn("residence",-1,480,600);var door=Middle(ExitApron(house.T,Footprint(house.T,house.P)));var worker=Spawn("engineer",0,door.X,door.Y);
  AssertEconomy(EngineerOrder(worker,house),"capture order");UnitTick(worker,.1f,true);AssertEconomy(house.Side==0&&!worker.Alive&&HouseCount(house)==0,"capture consumes engineer without occupant");
  worker=Spawn("engineer",0,door.X,door.Y);AssertEconomy(!CanEngineer(worker,house),"full friendly building refuses waste");house.Hp=20;AssertEconomy(EngineerOrder(worker,house),"repair order");UnitTick(worker,.1f,true);AssertEconomy(!worker.Alive&&house.Hp==house.T.Hp,"engineer instant building repair");
  var foot=Spawn("squad",0,door.X,door.Y);EnterHouse(foot,house);UnitTick(foot,.1f,true);var invader=Spawn("engineer",1,door.X+20,door.Y);AssertEconomy(!CanEngineer(invader,house),"occupied enemy house refuses capture");
  var car=Spawn("ifv",0,900,600);worker=Spawn("engineer",0,930,600);AssertEconomy(Board(worker,car),"explicit boarding");UnitTick(worker,.1f,true);AssertEconomy(worker.Carrier==car&&RepairVehicle(car)&&!Armed(car),"IFV becomes repair vehicle");
  var tank=Spawn("tank",0,937,600);tank.Hp=40;AssertEconomy(RepairOrder(car,tank),"repair target");UnitTick(car,.1f,true);AssertEconomy(tank.Hp==65,"IFV repair works");var foe=Spawn("tank",1,900,660);Attack(car,foe);Suppress(car,foe.P);UnitTick(car,2,true);AssertEconomy(Shots.Count==0&&foe.Hp==foe.T.Hp,"repair IFV cannot shoot");
  Load(car);AssertEconomy(worker.Carrier==null&&worker.Alive&&Armed(car),"unload restores weapon and living engineer");
  Console.WriteLine("Engineer capture/repair, occupied-house rejection, explicit IFV boarding/repair/unload passed.");
 }
 internal void CheckFactories(){
  EmptyEconomy();Spawn("power",0,500,500);var a=Spawn("factory",0,400,600);var b=Spawn("factory",0,600,600);
  Train(Info("ifv"),0,b);AssertEconomy(a.Queue.Count==1&&b.Queue.Count==0,"selection is not primary factory");a.Work=3;SetPrimary(b);AssertEconomy(b.Work==3&&b.Queue.Count==1&&a.Queue.Count==0,"primary switch retains paid progress");AssertEconomy(Math.Abs(ProductionRate(b)-.5625f)<.001,"low power and factory bonus combine");Spawn("power",0,600,500);AssertEconomy(Math.Abs(ProductionRate(b)-1.25f)<.001,"second factory bonus");
  UnitTick(b,1,true);AssertEconomy(Math.Abs(b.Work-4.25f)<.001,"shared production rate");Kill(b);AssertEconomy(a.Queue.Count==1&&Math.Abs(a.Work-4.25f)<.001,"destroyed primary hands over orders");
  var enemy=Spawn("factory",1,1000,600);Train(Info("ifv"),1,null);UnitTick(enemy,1,true);int paid=enemy.WorkPaid;int money=Funds[1];var door=Middle(ExitApron(enemy.T,Footprint(enemy.T,enemy.P)));var eng=Spawn("engineer",0,door.X,door.Y);EngineerOrder(eng,enemy);UnitTick(eng,.1f,true);AssertEconomy(enemy.Side==0&&enemy.Queue.Count==0&&Funds[1]==money+paid,"captured factory never gifts old orders");
  Console.WriteLine("Shared queue, primary switch, production speed, destruction/capture handover passed.");
 }
 internal void CheckOreChoice(){
  EmptyEconomy();var near=new OrePatch(420,600,480);var far=new OrePatch(1100,600,480);Ore.Add(near);Ore.Add(far);var car=Spawn("miner",0,400,600);var other=Spawn("miner",0,900,900);other.Patch=near;car.Patch=far;
  RunMiner(car,.1f);AssertEconomy(car.Patch==near,"occupied near field beats free far field and stale assignment");
  Halt(other);other.P=new PointF(900,950);car.P=near.P;car.Path.Clear();car.Patch=near;car.WorkPoint=near.P;car.Cargo=0;car.Mining=true;float initial=near.Stock;
  for(int i=0;i<20;i++)RunMiner(car,.1f);AssertEconomy(Math.Abs(near.Stock+car.Cargo-initial)<.01&&near.Stock<initial,"local ore density and cargo conserved");
  near.Stock=1;car.Cargo=0;car.Returning=false;RunMiner(car,.1f);AssertEconomy(near.Stock==0&&car.Patch==null&&!car.Returning,"empty cell continues searching before partial return");
  var blocked=new OrePatch(500,600,480);Ore.Add(blocked);var obstacle=Spawn("tank",1,500,600);car.P=new PointF(470,600);car.Dispatch=0;car.Path.Clear();RunMiner(car,.1f);AssertEconomy(car.Patch!=blocked,"occupied cell not harvested from distant perimeter");
  Console.WriteLine("Near-mine selection, stale-target removal, ore conservation, depletion and occupied-cell rejection passed.");
 }
 internal void CheckSatellite(){
  Units.Clear();Ore.Clear();var point=new PointF(1500,1500);
  for(int side=0;side<3;side++){
   Spawn("power",side,400+side*200,500);var sat=Spawn("satellite",side,400+side*200,600);Reveal();AssertEconomy(SatelliteOnline(side)&&Explored(side,point),"satellite available to every faction");
   sat.Ready=.5f;Reveal();AssertEconomy(!SatelliteOnline(side)&&!Explored(side,point),"unfinished satellite does not reveal");sat.Ready=1;var power=Live(side).First(u=>u.T.Key=="power");power.Hp=0;Reveal();AssertEconomy(!SatelliteOnline(side)&&!Explored(side,point),"satellite loses remote vision without power");power.Hp=power.T.Hp;Reveal();Kill(sat);Reveal();AssertEconomy(!SatelliteOnline(side)&&!Explored(side,point),"destroyed satellite loses remote vision");
  }
  Console.WriteLine("Satellite completion, three factions, blackout and destruction vision tests passed.");
 }
}
partial class CommandWindow {
 internal void CaptureEconomy(string folder){
  ClientSize=new Size(1920,1080);OpenMissions();
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   riverMissionSelected=false;hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"选关预览-教学港区.png"),ImageFormat.Png);
   riverMissionSelected=true;hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"选关预览-河湾镇.png"),ImageFormat.Png);
   StartRiverMission();var home=world.Home(0);world.Spawn("power",0,home.X+130,home.Y);world.Spawn("power",0,home.X+130,home.Y+72);var satellite=world.Spawn("satellite",0,home.X+40,home.Y-110);
   var car=world.Spawn("ifv",0,home.X+46,home.Y+44);var eng=world.Spawn("engineer",0,car.P.X+27,car.P.Y);world.Load(car);var target=world.Spawn("tank",0,car.P.X+34,car.P.Y);target.Hp=50;world.RepairOrder(car,target);world.Tick(.1f);
   selection.Clear();selection.Add(car);CenterAt(home);hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"工兵维修车与间谍卫星.png"),ImageFormat.Png);
   var patch=world.Ore.OrderBy(p=>Campaign.Dist(p.P,home)).First();foreach(var p in world.Ore.Where(p=>Campaign.Dist(p.P,patch.P)<100))p.Stock*=Math.Max(0,Math.Min(1,(p.P.X-patch.P.X+36)/144));CenterAt(patch.P);selection.Clear();hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"连续矿田与逐格采空.png"),ImageFormat.Png);
  }
 }
}
}
