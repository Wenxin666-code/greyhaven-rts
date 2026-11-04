using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

namespace Greyhaven {
public class OrePatch {
 public PointF P;public float Stock;public readonly float Initial;
 public OrePatch(float x,float y,float stock){P=new PointF(x,y);Stock=Initial=stock;}
}
public partial class Campaign {
 public const int LoadValue=180;
 public readonly List<OrePatch> Ore=CreateFields();
 public bool FirstOreCollected {get;private set;}
 public readonly int[] OreReceipts=new int[2];
 public PointF? LastEvent;public string EventLabel;public float EventTime;float lastStrike=-10;
 void SetEvent(PointF p,string label){EventTime=Time;LastEvent=p;EventLabel=label;}
 void ReportStrike(Piece unit){
  if(Time-lastStrike<2)return;
  SetEvent(unit.P,unit.T.Name+"遇袭");lastStrike=Time;
 }
 static List<OrePatch> CreateFields(){
  var ore=new List<OrePatch>();
  foreach(var center in new[]{new PointF(53,626),new PointF(1590,867),new PointF(449,221),new PointF(896,629),new PointF(1020,140)}){
   foreach(var offset in new[]{new PointF(0,0),new PointF(29,14),new PointF(-24,19),new PointF(6,38)})
    ore.Add(new OrePatch(center.X+offset.X,center.Y+offset.Y,4200));
  }return ore;
 }
 public static PointF DockPoint(Piece refinery){
  var gate=Gate(Footprint(refinery.T,refinery.P));return new PointF(refinery.P.X,gate.Bottom-5);
 }
 void RunRefinery(Piece plant,float dt){
  if(!plant.StarterDelivered){
   PointF exit;if(Exit(plant,out exit)){Spawn("miner",plant.Side,exit.X,exit.Y);plant.StarterDelivered=true;}
  }
  if(plant.Door>0)plant.MillPhase=(plant.MillPhase+dt*1.2f)%1;
 }
 public OrePatch OreAt(PointF point){return Ore.Where(p=>p.Stock>.01f&&Dist(p.P,point)<26).OrderBy(p=>Dist(p.P,point)).FirstOrDefault();}
 public void MineOrder(Piece truck,OrePatch patch,Piece plant){
  if(!truck.Deployed||truck.T.Key!="miner")return;
  Halt(truck);truck.Mining=true;truck.Patch=patch;truck.Surveying=false;truck.Dispatch=0;truck.TrafficSampleTime=0;
  truck.Returning=truck.Cargo>=LoadValue-.01f||(plant!=null&&truck.Cargo>.01f);
  if(plant!=null&&truck.Returning&&DockFree(truck,plant))truck.Dock=plant;
 }
 public string MiningStatus(Piece truck){
  string mode=!truck.Mining?"待命":truck.Unload>0?"卸矿":truck.Returning?(truck.Dock==null?"等待泊位":"返厂"):truck.Surveying?"探查新地形":truck.Path.Count>0?"前往矿区":truck.Patch==null?"寻找矿点":"采集";
  return mode+"  ·  载矿 "+(int)truck.Cargo+" / "+LoadValue;
 }
 bool WorkingPatch(Piece truck,OrePatch patch){
  return !Units.Any(o=>o!=truck&&o.Deployed&&o.Mining&&!o.Returning&&o.Patch==patch);
 }
 bool DockFree(Piece truck,Piece plant){
  return !Units.Any(o=>o!=truck&&o.Deployed&&o.Mining&&o.Returning&&o.Dock==plant);
 }
 // A stopped vehicle may occupy the vein center or one side of a loading gate.
 // Reserve the actual service position, not just a nominal map coordinate.
 bool RouteToWork(Piece truck,PointF center,bool unloading,bool detour){
  PointF[] offsets=unloading?DockOffsets:MineOffsets;
  foreach(var offset in offsets){
   var dest=new PointF(center.X+offset.X,center.Y+offset.Y);
   if(Units.Any(u=>u!=truck&&u.Deployed&&!u.T.Building&&(Dist(u.P,dest)<19||
      (u.Mining&&u.Path.Count>0&&Dist(u.Path.Last(),dest)<18))))continue;
   RectangleF[] blocks=null;
   if(detour)blocks=Units.Where(u=>u!=truck&&u.Deployed&&!u.T.Building&&Dist(u.P,truck.P)<125).Select(u=>{
    float pad=Math.Min(Clearance(u)+Clearance(truck),Math.Max(Math.Abs(truck.P.X-u.P.X),Math.Abs(truck.P.Y-u.P.Y))-.3f);
    return pad>1?new RectangleF(u.P.X-pad,u.P.Y-pad,2*pad,2*pad):RectangleF.Empty;
   }).Where(r=>!r.IsEmpty).ToArray();
   if(PlanRoute(truck,dest,blocks)){truck.WorkPoint=dest;return true;}
  }return false;
 }
 static readonly PointF[] DockOffsets={PointF.Empty,new PointF(-23,0),new PointF(23,0)};
 static readonly PointF[] MineOffsets={PointF.Empty,new PointF(22,0),new PointF(-22,0),new PointF(0,22),new PointF(0,-22)};
 bool TrafficStalled(Piece truck,float dt){
  if(truck.Path.Count==0){truck.TrafficSampleTime=0;return false;}
  if(truck.TrafficSampleTime<=0){truck.TravelSample=truck.P;truck.TrafficSampleTime=1.7f;}
  truck.TrafficSampleTime-=dt;
  return truck.TrafficSampleTime<=0&&Dist(truck.P,truck.TravelSample)<3;
 }
 void RunMiner(Piece truck,float dt){
  truck.Dispatch-=dt;
  bool stalled=TrafficStalled(truck,dt);
  if(stalled){truck.Path.Clear();truck.Dock=null;truck.Dispatch=0;}
  if(truck.Cargo>=LoadValue-.01f){truck.Cargo=LoadValue;truck.Returning=true;}
  if(truck.Returning){
   var plant=truck.Dock;
   if(plant!=null&&(!plant.Deployed||plant.Side!=truck.Side)){truck.Dock=null;truck.Path.Clear();truck.Unload=0;plant=null;}
   if(plant!=null&&Dist(truck.P,truck.WorkPoint)<5&&Dist(truck.WorkPoint,DockPoint(plant))<=24){
    truck.Path.Clear();plant.Door=1;truck.Unload+=dt;
    if(truck.Unload>=1.6f){
     int receipt=(int)Math.Round(truck.Cargo);Funds[truck.Side]+=receipt;OreReceipts[truck.Side]+=receipt;plant.LastReceipt=receipt;plant.ReceiptAt=Time;
     truck.Cargo=0;truck.Unload=0;truck.Dock=null;truck.Returning=false;truck.Dispatch=0;
    }return;
   }
   if(truck.Path.Count>0||truck.Dispatch>0)return;
   truck.Dispatch=1;
   foreach(var dest in Live(truck.Side).Where(o=>o.T.Key=="refinery"&&DockFree(truck,o)).OrderBy(o=>o==plant?0:Dist(truck.P,o.P))){
    if(RouteToWork(truck,DockPoint(dest),true,stalled)){truck.Dock=dest;return;}
   }
   truck.Dock=null;return;
  }
  var patch=truck.Patch;
  if(patch!=null&&patch.Stock>.01f&&Dist(truck.P,truck.WorkPoint)<5&&Dist(truck.WorkPoint,patch.P)<=23){
   truck.Path.Clear();float take=Math.Min(Math.Min(patch.Stock,LoadValue-truck.Cargo),dt*36);
   truck.Cargo+=take;patch.Stock-=take;if(truck.Side==0&&take>0)FirstOreCollected=true;
   if(patch.Stock<=.01f){truck.Patch=null;truck.Returning=truck.Cargo>.01f;}
   return;
  }
  if(truck.Dispatch>0||(truck.Path.Count>0&&!truck.Surveying))return;
  truck.Dispatch=1;truck.Patch=null;
  foreach(var dest in Ore.Where(p=>p.Stock>.01f&&Explored(truck.Side,p.P)).OrderBy(p=>p==patch?0:WorkingPatch(truck,p)?1:2).ThenBy(p=>Dist(truck.P,p.P))){
   if(RouteToWork(truck,dest.P,false,stalled)){truck.Patch=dest;truck.Surveying=false;return;}
  }
  if(truck.Cargo>.01f){truck.Returning=true;truck.Surveying=false;truck.Path.Clear();return;}
  if(truck.Path.Count==0)SurveyForOre(truck);
 }
 void SurveyForOre(Piece truck){
  // Choose unexplored ground using the side's map, independently of ore locations.
  truck.Surveying=ExploreGround(truck);
  if(!truck.Surveying)truck.Dispatch=4;
 }
 bool ExploreGround(Piece unit){
  const int spacing=4*Tile;var candidates=new List<PointF>();
  for(int y=144;y<Height;y+=spacing)for(int x=48;x<Width;x+=spacing){
   var point=new PointF(x,y);if(!Explored(unit.Side,point))candidates.Add(point);
  }
  foreach(var point in candidates.OrderBy(p=>Dist(unit.P,p))){
   if(Route(unit,point))return true;
  }
  return false;
 }
}
}
