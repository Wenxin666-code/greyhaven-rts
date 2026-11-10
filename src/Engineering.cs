using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 public bool RepairVehicle(Piece car){return car!=null&&car.Alive&&car.T.Key=="ifv"&&car.Passenger!=null&&car.Passenger.Alive&&car.Passenger.T.Key=="engineer";}
 public bool Armed(Piece u){return u!=null&&ReferenceBalance.Weapon(u)!=null;}
 public bool CanEngineer(Piece unit,Piece target){
  return unit!=null&&unit.Deployed&&unit.T.Key=="engineer"&&target!=null&&target.Alive&&target.T.Building&&target.Ready>=1&&target.Packing<=0&&
   (target.Side==unit.Side?target.Hp<target.T.Hp:!target.T.Defense&&(!target.T.Civilian||HouseCount(target)==0));
 }
 public bool EngineerOrder(Piece unit,Piece target){
  if(!CanEngineer(unit,target))return false;
  var door=Middle(ExitApron(target.T,Footprint(target.T,target.P)));
  if(!Move(unit,door,false))return false;
  unit.Entering=target;if(unit.Side==0)Radio=target.Side==unit.Side?"工兵进入后修复建筑；完成后消耗工兵。":"工兵进入后占领建筑；完成后消耗工兵。";return true;
 }
 bool TickEngineerEntry(Piece unit,float dt){
  var target=unit.Entering;if(!CanEngineer(unit,target)){Halt(unit);return false;}
  var door=Middle(ExitApron(target.T,Footprint(target.T,target.P)));
  if(Dist(unit.P,door)>14){if(unit.Path.Count==0&&unit.RouteRetryAt<=Time){unit.RouteRetryAt=Time+1;PlanRoute(unit,door,MineTraffic(unit,door));}return false;}
  if(target.Side==unit.Side){target.Hp=target.T.Hp;target.RepairAt=VisualTime;}
  else {
   TransferProduction(target);target.Side=unit.Side;target.Target=null;target.Contact=ContactOrder.None;target.FirePoint=null;target.Rally=null;target.Primary=false;
   // The free miner belongs to the refinery construction, not to each new owner.
   if(target.T.Key=="refinery")target.StarterDelivered=true;
   Changed();SetEvent(target.P,(unit.Side==0?"我方":"敌方 "+unit.Side)+"占领了"+target.T.Name);
  }
  Halt(unit);unit.Hp=0;unit.Dead=25;unit.Carrier=null;Reveal();
  if(unit.Side==0)Radio="工兵任务完成，已消耗。";return true;
 }
 public bool CanBoard(Piece foot,Piece car){return foot!=null&&foot.Deployed&&foot.T.Foot&&car!=null&&car.Deployed&&car.T.Key=="ifv"&&car.Side==foot.Side&&!Units.Any(u=>u.Alive&&u!=foot&&(u.Carrier==car||u.Entering==car));}
 public bool Board(Piece foot,Piece car){
  if(!CanBoard(foot,car))return false;
  PointF p;if(!Exit(car,foot.T,out p)||!Move(foot,p,false))return false;foot.Entering=car;return true;
 }
 bool TickBoard(Piece foot){
  var car=foot.Entering;if(!CanBoard(foot,car)){Halt(foot);return false;}
  if(Dist(foot.P,car.P)<34){Halt(foot);foot.Carrier=car;foot.P=car.P;if(RepairVehicle(car))Halt(car);if(car.Side==0)Radio="步兵战车："+ReferenceBalance.IFVRole(car)+"。";return true;}
  if(foot.RouteRetryAt<=Time){foot.RouteRetryAt=Time+1;PointF p;if(Exit(car,foot.T,out p))PlanRoute(foot,p,MineTraffic(foot,p));}return false;
 }
 public bool CanRepairVehicle(Piece car,Piece target){return RepairVehicle(car)&&target!=car&&target!=null&&target.Deployed&&target.Side==car.Side&&!target.T.Building&&!target.T.Foot&&target.Hp<target.T.Hp;}
 public bool RepairOrder(Piece car,Piece target){if(!CanRepairVehicle(car,target))return false;Halt(car);car.SupportTarget=target;return true;}
 float RepairSearchRange(Piece car){return car.GuardHome.HasValue?3.6f*Tile:1.8f*Tile;}
 bool RepairGuardAllows(Piece car,Piece target){return Dist(car.GuardHome??car.P,target.P)<=RepairSearchRange(car);}
 bool TickRepairVehicle(Piece car,float dt){
  if(!RepairVehicle(car)){car.SupportTarget=null;car.SupportAutomatic=false;return false;}
  car.Target=null;car.FirePoint=null;car.BurstRemaining=0;
  var target=car.SupportTarget;
  if(target!=null&&(!CanRepairVehicle(car,target)||car.SupportAutomatic&&!RepairGuardAllows(car,target))){
   bool back=car.SupportAutomatic&&car.GuardHome.HasValue;car.SupportTarget=null;car.SupportAutomatic=false;car.Path.Clear();target=null;
   if(back){car.GuardReturning=true;car.Replan=0;Route(car,car.GuardHome.Value);}
  }
  // Ordinary movement owns the vehicle. Explicit G / escort may seek nearby
  // damaged vehicles, and returns to its anchor after the repair completes.
  if(target==null&&!car.GuardTravelling&&!car.GuardReturning&&(car.Path.Count==0||car.Escort!=null)&&Time>=car.SupportScanAt){
   car.SupportScanAt=Time+.2f;
   target=Live(car.Side).Where(u=>CanRepairVehicle(car,u)&&RepairGuardAllows(car,u)&&Dist(car.P,u.P)<=RepairSearchRange(car)).OrderBy(u=>Dist(car.P,u.P)).FirstOrDefault();
   if(target!=null){car.SupportTarget=target;car.SupportAutomatic=true;}
  }
  if(target==null)return false;
  if(Dist(car.P,target.P)>1.8f*Tile){
   if(car.RouteRetryAt<=Time){car.RouteRetryAt=Time+1;var aim=target.P;
    for(int i=0;i<16;i++){double a=Math.Atan2(car.P.Y-aim.Y,car.P.X-aim.X)+i*Math.PI/8;var p=new PointF(aim.X+(float)Math.Cos(a)*36,aim.Y+(float)Math.Sin(a)*36);if(PlanRoute(car,p,MineTraffic(car,p)))break;}}
   return false;
  }
  car.Path.Clear();Face(car,target.P,dt);
  if(car.Cool<=0){target.Hp=Math.Min(target.T.Hp,target.Hp+ReferenceBalance.RepairAmount(car));car.Cool=ReferenceBalance.Reload(car,ReferenceBalance.WRepair);car.RepairAt=VisualTime;car.RepairPoint=target.P;}
  return true;
 }
}
}
