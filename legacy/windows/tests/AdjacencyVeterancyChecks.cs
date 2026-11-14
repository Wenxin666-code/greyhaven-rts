using System;
using System.Drawing;
using System.Linq;
using Greyhaven;

class AdjacencyVeterancyChecks {
 static int Main(){try{Campaign.CheckAdjacencyAndVeterancy();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 static void VetAssert(bool value,string reason){if(!value)throw new Exception(reason);}
 static void VetNear(double actual,double expected,string reason,double tolerance=.0001){VetAssert(Math.Abs(actual-expected)<tolerance,reason+": "+actual+" != "+expected);}
 static Campaign VetWorld(){var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();return w;}
 internal static void CheckAdjacencyAndVeterancy(){
  var w=VetWorld();
  foreach(int side in new[]{0,1,2,3,4,5}){
   w.Units.Clear();var anchor=w.Spawn("power",side,600,900);var a=Footprint(anchor.T,anchor.P);
   foreach(string key in new[]{"power","refinery","barracks","factory","radar","satellite","post"}){
    var t=Info(key);int gap=key=="post"?4:2;
    foreach(int dx in new[]{-1,0,1})foreach(int dy in new[]{-1,0,1}){
     if(dx==0&&dy==0)continue;
     float x=dx<0?a.Left-(gap+t.FoundationWidth)*Tile:dx>0?a.Right+gap*Tile:a.Left;
     float y=dy<0?a.Top-(gap+t.FoundationHeight)*Tile:dy>0?a.Bottom+gap*Tile:a.Top;
     var r=new RectangleF(x,y,t.FoundationWidth*Tile,t.FoundationHeight*Tile);
     VetAssert(w.InBuildRange(t,r,side),"inclusive empty-cell boundary, diagonal included: "+key+" / "+side);
     if(dx!=0)r.X+=dx*Tile;else r.Y+=dy*Tile;
     VetAssert(!w.InBuildRange(t,r,side),"one extra empty cell must fail: "+key);
    }
    var touching=new RectangleF(a.Right,a.Top,t.FoundationWidth*Tile,t.FoundationHeight*Tile);
    VetAssert(w.InBuildRange(t,touching,side),"no mandatory empty gap "+key);
    VetAssert(!w.InBuildRange(t,touching,(side+1)%6),"enemy cannot anchor expansion");
   }
   anchor.Hp=0;VetAssert(!w.InBuildRange(Info("power"),a,side),"destroyed anchor");
  }
  foreach(string key in new[]{"post","residence","shop","municipal","relay","mcv"}){
   w=VetWorld();var anchor=w.Spawn(key,0,600,900);var r=Footprint(anchor.T,anchor.P);r.X+=r.Width;
   VetAssert(!w.InBuildRange(Info("power"),r,0),"ineligible expansion anchor "+key);
  }
  Console.WriteLine("PASS adjacency: all six sides, seven build types, eight directions, exact 2/4 empty-cell limits, touching, invalid/enemy/dead anchors.");

  w=VetWorld();var tank=w.Spawn("tank",0,600,900);tank.Hp=101;
  for(int i=1;i<=7;i++){
   var victim=w.Spawn("tank",1,1000,900);w.DamageTarget(tank,victim,1);VetNear(tank.Experience,Math.Min(i-1,6)/3.0,"nonlethal hits do not train");
   w.DamageTarget(tank,victim,10000);VetNear(tank.Experience,Math.Min(i,6)/3.0,"credited enemy cost");
   VetAssert(tank.Rank==(i<3?0:i<6?1:2),"3x / 6x own value thresholds");VetNear(tank.Hp,101,"promotion does not refill HP");
   double before=tank.Experience;w.DamageTarget(tank,victim,10000);VetNear(tank.Experience,before,"dead target cannot award twice");
  }
  tank.Experience=.999999;VetAssert(tank.Rank==0,"not promoted just below threshold");tank.Experience=1.999999;VetAssert(tank.Rank==1,"not elite just below threshold");
  tank.Experience=0;var own=w.Spawn("squad",0,1000,1100);w.DamageTarget(tank,own,10000);VetNear(tank.Experience,0,"friendly kills give no experience");
  var civil=w.Spawn("residence",-1,1000,1200);w.DamageTarget(tank,civil,10000);VetNear(tank.Experience,0,"unowned civilians give no experience");
  foreach(string key in new[]{"engineer","mcv","post","hq"}){
   var killer=w.Spawn(key,0,700,1200);var victim=w.Spawn("tank",1,1000,1200);w.DamageTarget(killer,victim,10000);VetNear(killer.Experience,0,"untrainable "+key);
  }
  var building=w.Spawn("refinery",1,1000,1400);w.DamageTarget(tank,building,10000);VetNear(tank.Experience,2000/2100.0,"enemy building value counts");
  w=VetWorld();tank=w.Spawn("tank",0,600,900);var foot=w.Spawn("squad",1,607,900);tank.ForceMoving=true;
  w.TrackStep(tank,new PointF(680,900),foot.P,.05f);VetAssert(!foot.Alive,"actual forced-move crush");VetNear(tank.Experience,200/2100.0,"crushing earns kill credit");
  Console.WriteLine("PASS experience: lethal credit, price-weighted 3x/6x thresholds, no promotion heal, cap, buildings, crushing, no friendly/civilian/untrainable credit.");

  w=VetWorld();var car=w.Spawn("ifv",0,600,900);foot=w.Spawn("squad",0,600,900);foot.Carrier=car;
  var target=w.Spawn("tank",1,1000,900);w.DamageTarget(car,target,10000);VetNear(car.Experience,700/1800.0,"loaded IFV gets credit");VetNear(foot.Experience,0,"passenger does not share credit");
  car.Experience=2;target=w.Spawn("tank",1,1000,900);w.DamageTarget(car,target,10000);VetNear(car.Experience,2,"elite IFV stays capped");VetNear(foot.Experience,0,"elite overflow is not donated");
  var attacker=w.Spawn("tank",1,1000,1200);w.DamageTarget(attacker,car,10000);VetNear(attacker.Experience,800/2100.0,"destroyed IFV and passenger both credited");VetAssert(!foot.Alive,"killed passenger");
  w=VetWorld();var house=w.Spawn("residence",0,600,900);foot=w.Spawn("squad",0,600,900);foot.Carrier=house;
  target=w.Spawn("tank",1,1000,900);w.DamageTarget(foot,target,10000);VetNear(foot.Experience,700/600.0,"garrison shooter earns experience");VetNear(house.Experience,0,"house does not get passenger XP");
  attacker=w.Spawn("tank",1,1000,1200);w.DamageTarget(attacker,house,10000);VetAssert(foot.Alive&&foot.Carrier==null,"civilian occupants evacuate");VetNear(attacker.Experience,Math.Min(2,house.T.Price/2100.0),"evacuees not counted as deaths");
  Console.WriteLine("PASS carrier experience: IFV owner, no passenger overflow, killed passenger value, individual garrison credit and surviving evacuees.");

  w=VetWorld();target=w.Spawn("tank",1,1200,900);
  foreach(string key in new[]{"squad","at","ifv","tank","arty"}){
   var unit=w.Spawn(key,0,600,900);var basic=ReferenceBalance.Weapon(unit);float damage=ReferenceBalance.Damage(unit,target,basic),speed=ReferenceBalance.Speed(unit);
   unit.Experience=1;VetNear(ReferenceBalance.Speed(unit),speed*1.2,"veteran speed "+key);VetNear(ReferenceBalance.Damage(unit,target,basic),damage*1.1,"veteran firepower "+key);
   VetNear(ReferenceBalance.Reload(unit,basic),basic.Reload*(unit.T.Foot?.6:1),"veteran rate by unit "+key);
   unit.Experience=2;VetNear(ReferenceBalance.Speed(unit),speed*1.2,"speed not stacked "+key);VetNear(ReferenceBalance.Damage(unit,target,basic),damage*1.1,"firepower not stacked "+key);
   var elite=ReferenceBalance.Weapon(unit);VetNear(ReferenceBalance.Reload(unit,elite),elite.Reload*.6,"elite rate "+key);
   target.Experience=1;VetNear(ReferenceBalance.Damage(unit,target,basic),damage*1.1/1.5,"veteran armor "+key);target.Experience=2;VetNear(ReferenceBalance.Damage(unit,target,basic),damage*1.1/1.5,"elite armor not stacked "+key);target.Experience=0;
   VetNear(Sight(unit),unit.T.SightCells*Tile,"sight unchanged "+key);
   if(key=="tank")VetAssert(elite.Burst==2&&elite.Frames==50&&elite.Damage==65,"elite tank weapon");
   if(key=="ifv")VetAssert(elite.Burst==4&&elite.Damage==80,"elite empty IFV weapon");
  }
  car=w.Spawn("ifv",0,600,1200);var engineer=w.Spawn("engineer",0,600,1200);engineer.Carrier=car;var damaged=w.Spawn("tank",0,630,1200);
  for(int rank=0;rank<=2;rank++){car.Experience=rank;car.Cool=0;damaged.Hp=100;w.RepairOrder(car,damaged);w.TickRepairVehicle(car,.05f);VetNear(damaged.Hp,rank==0?150:155,"repair firepower follows vehicle rank");VetNear(car.Cool,80/15.0*(rank==2?.6:1),"repair reload follows vehicle rank");VetNear(engineer.Experience,0,"repair does not train engineer");}
  Console.WriteLine("PASS abilities: per-type veteran ROF, inherited/non-stacking modifiers, elite weapons, unchanged sight and rank-aware repair IFV.");

  w=VetWorld();var eliteTank=w.Spawn("tank",0,600,900);eliteTank.Experience=2;eliteTank.Hp=100;
  var veteran=w.Spawn("squad",0,600,1000);veteran.Experience=1;veteran.Hp=50;
  var miner=w.Spawn("miner",0,700,1000);miner.Hp=100;miner.Mining=false;
  var eliteFoot=w.Spawn("squad",0,700,900);eliteFoot.Experience=2;eliteFoot.Hp=60;
  car=w.Spawn("ifv",0,800,900);var carried=w.Spawn("squad",0,800,900);carried.Experience=2;carried.Hp=60;carried.Carrier=car;
  int cash=w.Funds[0];Action<float> step=dt=>{w.AdvanceSelfHealClock(dt);foreach(var u in w.Units)w.TickSelfHealing(u);};
  for(int i=0;i<27;i++)step(1/30f);VetNear(eliteTank.Hp,100,"no early heal before 14 frames");
  eliteTank.HitAt=w.Time;step(1/30f);VetNear(eliteTank.Hp,101,"heal during combat");VetNear(eliteFoot.Hp,61,"elite foot heals");VetNear(miner.Hp,101,"natural miner healing");VetNear(veteran.Hp,50,"veteran has no self heal");VetNear(carried.Hp,60,"limbo passenger does not tick");
  carried.Carrier=null;for(int i=0;i<28;i++)step(1/30f);VetNear(carried.Hp,61,"unload gives no accumulated healing");
  eliteTank.Hp=eliteTank.T.Hp-.4f;eliteFoot.Hp=0;step(28/15f);VetNear(eliteTank.Hp,eliteTank.T.Hp,"healing capped at full HP");VetNear(eliteFoot.Hp,0,"never resurrect");VetAssert(w.Funds[0]==cash,"self heal is free");
  float[] health=new float[3];float[] deltas={1/30f,.1f,1f};int[] count={420,140,14};
  for(int k=0;k<3;k++){w=VetWorld();var p=w.Spawn("tank",0,600,900);p.Experience=2;p.Hp=100;for(int i=0;i<count[k];i++){w.AdvanceSelfHealClock(deltas[k]);w.TickSelfHealing(p);}health[k]=p.Hp;VetNear(p.Hp,115,"healing independent of update partition "+k);}
  w=VetWorld();tank=w.Spawn("tank",0,600,900);tank.Experience=2;tank.Hp=100;w.Spawn("mcv",1,2700,1700);
  for(int i=0;i<30;i++)w.Tick(1/30f);VetNear(tank.Hp,101,"self healing is wired into real world ticks");
  Console.WriteLine("PASS healing: 14-frame / 1 HP pulses, elite-only plus native miner, moving/combat eligibility, max/death/limbo limits, no money, partition consistency, actual game update.");
 }
}
}
