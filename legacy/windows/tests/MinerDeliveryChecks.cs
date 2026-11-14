using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Greyhaven;

class MinerDeliveryChecks {
 static int Main(){try{Campaign.CheckMinerDelivery();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void DeliveryAssert(bool value,string message){if(!value)throw new Exception(message);}
 static Campaign DeliveryWorld(){
  var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",1,2900,1800);
  for(int x=0;x<w.MapFogCols;x++)for(int y=0;y<w.MapFogRows;y++)w.discovered[0,x,y]=true;return w;
 }
 static Piece DeliveryPlant(Campaign w,float x,float y){var plant=w.Spawn("refinery",0,x,y);plant.StarterDelivered=true;return plant;}
 internal static void CheckMinerDelivery(){
  CheckRealDockReservation();CheckStarterClearance();CheckStableRefinery();CheckDeliveryBatches();CheckContinuousIslandMining();
 }
 static void CheckRealDockReservation(){
  var w=DeliveryWorld();var plant=DeliveryPlant(w,600,650);var gate=DockPoint(plant);
  var head=w.Spawn("miner",0,gate.X,gate.Y+45);head.Cargo=LoadValue;head.Returning=true;head.Dock=plant;head.DockTicket=1;
  var incoming=w.Spawn("miner",0,gate.X+120,gate.Y+380);incoming.Cargo=LoadValue;incoming.Returning=true;incoming.Dock=plant;incoming.DockTicket=2;w.Route(incoming,gate);
  w.RunMiner(head,.1f);DeliveryAssert(head.DockReserved&&head.Path.Count>0&&Dist(head.Path.Last(),gate)<.01f,"A distant planned destination must not occupy the real unloading bay");
  w.RunMiner(incoming,.1f);DeliveryAssert(!incoming.DockReserved,"Only one truck may reserve a bay");
  for(int i=0;i<12*30;i++)w.Tick(1f/30);
  DeliveryAssert(head.Cargo==0&&Dist(head.P,gate)>Tile*2,"The first empty truck must vacate the bay even with no known ore");
  Console.WriteLine("Dock reservation: distant arrivals do not block the head; empty truck leaves an exhausted-field entrance.");
 }
 static void CheckStarterClearance(){
  var w=DeliveryWorld();var plant=w.Spawn("refinery",0,600,650);var gate=DockPoint(plant);
  var loaded=w.Spawn("miner",0,gate.X,gate.Y+150);loaded.Cargo=LoadValue;loaded.Returning=true;
  for(int i=0;i<20*30;i++)w.Tick(1f/30);
  var starter=w.Live(0).Single(t=>t.T.Key=="miner"&&t!=loaded);
  DeliveryAssert(plant.StarterDelivered&&Dist(starter.P,gate)>Tile*2&&loaded.Cargo==0&&w.OreReceipts[0]==LoadValue,"A new refinery's starter truck must leave its gate even when no ore remains");
  Console.WriteLine("Starter delivery: the included empty miner clears the entrance before looking for ore.");
 }
 static void CheckStableRefinery(){
  var w=DeliveryWorld();var near=DeliveryPlant(w,600,650);var other=DeliveryPlant(w,1050,650);var gate=DockPoint(near);
  var truck=w.Spawn("miner",0,gate.X,gate.Y+150);truck.Cargo=LoadValue;truck.Returning=true;
  w.RunMiner(truck,.1f);DeliveryAssert(truck.Dock==near,"Select shortest reachable refinery");
  truck.DockBlockedFor=10;truck.Dispatch=0;w.RunMiner(truck,.1f);
  DeliveryAssert(truck.Dock==near,"Temporary congestion must not send a truck to a different refinery");
  near.Side=1;truck.Dispatch=0;w.RunMiner(truck,.1f);
  DeliveryAssert(truck.Dock==other&&truck.Cargo==LoadValue,"Captured refinery reselects without losing cargo");
  near.Side=0;truck.Dock=near;
  near.Hp=0;w.Kill(near);truck.Dispatch=0;w.RunMiner(truck,.1f);
  DeliveryAssert(truck.Dock==other&&truck.Cargo==LoadValue,"Destroyed refinery reselects without losing cargo");
  w=DeliveryWorld();near=DeliveryPlant(w,600,650);other=DeliveryPlant(w,1050,900);gate=DockPoint(near);
  truck=w.Spawn("miner",0,gate.X,gate.Y+190);truck.Cargo=LoadValue;truck.Returning=true;truck.Dock=near;truck.DockBlockedFor=3;
  for(int x=-48;x<=48;x+=24)for(int y=-48;y<=48;y+=24)if(Math.Abs(x)==48||Math.Abs(y)==48)w.Spawn("post",-1,gate.X+x,gate.Y+y);
  w.RunMiner(truck,.1f);DeliveryAssert(truck.Dock==other,"Actually sealed entrance must choose another reachable refinery");
  w=DeliveryWorld();near=DeliveryPlant(w,600,650);other=DeliveryPlant(w,744,650);gate=DockPoint(near);
  var queued=w.Spawn("miner",0,gate.X,gate.Y+60);queued.Cargo=LoadValue;queued.Returning=true;queued.Dock=near;queued.DockTicket=1;
  truck=w.Spawn("miner",0,gate.X+66,gate.Y+200);truck.Cargo=LoadValue;truck.Returning=true;truck.DockTicket=2;
  DeliveryAssert(w.ChooseRefinery(truck,null)&&truck.Dock==near,"A short normal queue does not outweigh the shortest reachable route");
  truck.DepartingDock=near;w.Halt(truck);DeliveryAssert(truck.DepartingDock==null&&!truck.Mining&&truck.Cargo==LoadValue,"Stop cancels dock-exit automation without discarding cargo");
  Console.WriteLine("Refinery target: stable through temporary queues, reselected after destruction or a permanently sealed entrance.");
 }
 static void CheckDeliveryBatches(){
  foreach(int plants in new[]{1,2})foreach(int count in new[]{4,8}){
   var w=DeliveryWorld();var near=DeliveryPlant(w,600,650);if(plants==2)DeliveryPlant(w,1050,650);
   var gate=DockPoint(near);var trucks=Enumerable.Range(0,count).Select(i=>w.Spawn("miner",0,gate.X+(i%4-1)*30,gate.Y+260+i/4*30)).ToArray();
   foreach(var truck in trucks){truck.Cargo=LoadValue;truck.Returning=true;}
   int funds=w.Funds[0],switches=0;var targets=new Piece[count];
   // Budget scales with the new reference speed; collision and exact-once checks are unchanged.
   float budget=90*48/Info("miner").Speed;
   for(int i=0;i<budget*30&&trucks.Any(t=>t.Cargo>0);i++){
    w.Tick(1f/30);
    for(int n=0;n<count;n++){
     var truck=trucks[n];if(truck.Dock!=null){if(targets[n]!=null&&targets[n]!=truck.Dock)switches++;targets[n]=truck.Dock;}
     foreach(var other in trucks.Where(t=>t.Id>truck.Id))DeliveryAssert(!TrafficContact(truck.T,truck.P,other.T,other.P),"Returning miners overlap");
    }
    foreach(var plant in w.Live(0).Where(t=>t.T.Key=="refinery"))DeliveryAssert(trucks.Count(t=>t.Dock==plant&&t.DockReserved)<=1,"Bay has multiple owners");
   }
   DeliveryAssert(trucks.All(t=>t.Cargo==0)&&w.OreReceipts[0]==count*LoadValue&&w.Funds[0]-funds==count*LoadValue,"All loaded trucks must deliver exactly once");
   DeliveryAssert(switches==0,"No refinery ping-pong during a normal multi-truck queue");
   Console.WriteLine("Loaded batch: "+plants+" refineries / "+count+" trucks delivered "+w.OreReceipts[0]+" in "+w.Time.ToString("F1")+" game seconds, zero target switches/overlaps.");
  }
 }
 static void CheckContinuousIslandMining(){
  var w=new Campaign(true,false,true,true);w.Units.Clear();w.Spawn("mcv",1,w.Home(1).X,w.Home(1).Y);var home=w.Home(0);
  w.Spawn("hq",0,home.X,home.Y);for(int x=0;x<w.MapFogCols;x++)for(int y=0;y<w.MapFogRows;y++)w.discovered[0,x,y]=true;
  for(int i=0;i<2;i++){
   PointF site;DeliveryAssert(w.AiBuildSite(0,Info("refinery"),new PointF(home.X+(i==0?-100:100),home.Y+110),out site),"Third mission refinery placement");DeliveryPlant(w,site.X,site.Y);
  }
  var trucks=new List<Piece>();
  for(int i=0;i<8;i++){
   Piece truck=null;for(int slot=0;slot<160;slot++){
    var point=new PointF(home.X-150+slot%10*30,home.Y+160+slot/10*30);
    if(w.Blocked(point,Info("miner").Clearance,null)||w.Units.Any(u=>u.Deployed&&!u.T.Building&&TrafficContact(Info("miner"),point,u.T,u.P)))continue;
    truck=w.Spawn("miner",0,point.X,point.Y);break;
   }DeliveryAssert(truck!=null,"Third mission truck starting point");trucks.Add(truck);
  }
  var delivered=new int[trucks.Count];double initial=w.Ore.Sum(p=>(double)p.Stock);int cash=w.Funds[0];
  for(int i=0;i<240*30;i++){
   var cargo=trucks.Select(t=>t.Cargo).ToArray();w.Tick(1f/30);
   for(int n=0;n<trucks.Count;n++)if(cargo[n]>.01&&trucks[n].Cargo<.01)delivered[n]++;
  }
  double mined=initial-w.Ore.Sum(p=>(double)p.Stock),accounted=w.OreReceipts[0]+trucks.Sum(p=>(double)p.Cargo);
  Console.WriteLine("Third mission 240s: receipts="+w.OreReceipts[0]+" cycles="+String.Join("/",delivered)+" conservation error="+Math.Abs(mined-accounted).ToString("F3"));
  DeliveryAssert(delivered.All(n=>n>=2),"Every miner must complete repeated mine-return-unload cycles");
  DeliveryAssert(Math.Abs(mined-accounted)<2&&w.Funds[0]-cash==w.OreReceipts[0],"Ore, cargo and actual cash remain conserved");
 }
}
}
