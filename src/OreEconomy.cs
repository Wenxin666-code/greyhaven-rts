using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

namespace Greyhaven {
public class OrePatch {
 public PointF P;double stock;public readonly float Initial;
 public float Stock {get{return (float)stock;}set{stock=value;}}
 public OrePatch(float x,float y,float stock){P=new PointF(x,y);Stock=Initial=stock;}
 public float Extract(float request){float take=(float)Math.Min(stock,request);stock-=take;return take;}
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
  foreach(var center in new[]{new PointF(53,626),new PointF(EnemyHome.X+42,EnemyHome.Y+367),
   new PointF(449,221),new PointF(896,629),new PointF(1020,140),
   new PointF(550,1170),new PointF(1280,1310),new PointF(2060,470)}){
   foreach(var offset in new[]{new PointF(0,0),new PointF(29,14),new PointF(-24,19),new PointF(6,38)})
    ore.Add(new OrePatch(center.X+offset.X,center.Y+offset.Y,4200));
  }return ore;
 }
 public static PointF DockPoint(Piece refinery){
  var gate=Gate(Footprint(refinery.T,refinery.P));return new PointF(refinery.P.X,gate.Bottom-5);
 }
 void RunRefinery(Piece plant,float dt){
  if(!plant.StarterDelivered){
   PointF exit;if(Exit(plant,Info("miner"),out exit)){Spawn("miner",plant.Side,exit.X,exit.Y);plant.StarterDelivered=true;}
  }
  if(plant.Door>0)plant.MillPhase=(plant.MillPhase+dt*1.2f)%1;
 }
 public OrePatch OreAt(PointF point){return Ore.Where(p=>p.Stock>.01f&&Dist(p.P,point)<26).OrderBy(p=>Dist(p.P,point)).FirstOrDefault();}
 public void MineOrder(Piece truck,OrePatch patch,Piece plant){
  if(!truck.Deployed||truck.T.Key!="miner")return;
  Halt(truck);truck.Mining=true;truck.Patch=patch;truck.Surveying=false;truck.Dispatch=0;truck.TrafficSampleTime=0;
  truck.Returning=truck.Cargo>=LoadValue-.01f||(plant!=null&&truck.Cargo>.01f);
  if(plant!=null&&truck.Returning)truck.Dock=plant;
 }
 public string MiningStatus(Piece truck){
  string mode=!truck.Mining?"待命":truck.Unload>0?"卸矿":truck.Returning?DockStatus(truck):truck.Surveying?"探查新地形":truck.Path.Count>0?"前往矿区":truck.Patch==null?"寻找矿点":"采集";
  return mode+"  ·  载矿 "+(int)truck.Cargo+" / "+LoadValue;
 }
 bool WorkingPatch(Piece truck,OrePatch patch){
  return !Units.Any(o=>o!=truck&&o.Deployed&&o.Mining&&!o.Returning&&o.Patch==patch);
 }
 bool DockFree(Piece truck,Piece plant){
  return !Units.Any(o=>o!=truck&&o.Deployed&&o.Mining&&o.Returning&&o.Dock==plant&&
   (o.DockReserved||o.DockRetryAfter<=Time&&Dist(o.P,DockPoint(plant))<140&&EarlierDock(o,truck)));
 }
 // A stopped vehicle may occupy the vein center or one side of a loading gate.
 // Reserve the actual service position, not just a nominal map coordinate.
 bool RouteToWork(Piece truck,PointF center,bool unloading){
  PointF[] offsets=unloading?DockOffsets:MineOffsets;
  var blocks=MineTraffic(truck,center);
  foreach(var offset in offsets){
   var dest=new PointF(center.X+offset.X,center.Y+offset.Y);
   if(blocks!=null&&blocks.Any(r=>r.Contains(dest)))continue;
   if(Units.Any(u=>u!=truck&&u.Deployed&&!u.T.Building&&(Dist(u.P,dest)<truck.T.Clearance+u.T.Clearance+1||
      (u.Mining&&u.Path.Count>0&&Dist(u.Path.Last(),dest)<truck.T.Clearance+u.T.Clearance+1))))continue;
   if(PlanRoute(truck,dest,blocks)){truck.WorkPoint=dest;return true;}
  }return false;
 }
 // Keep neighboring blockers together on every replan; going around only the last
 // vehicle can send a truck straight back into the previous one.
 RectangleF[] MineTraffic(Piece truck,PointF destination){
  var blocks=new List<RectangleF>();
  foreach(var u in Units){
   if(u==truck||!u.Deployed||u.T.Building)continue;
   if(Dist(truck.P,u.P)>140&&Dist(destination,u.P)>90)continue;
   float gap=Math.Max(Math.Abs(truck.P.X-u.P.X),Math.Abs(truck.P.Y-u.P.Y));
   float pad=Math.Min(gap-.25f,(truck.T.Clearance+u.T.Clearance)*.78f+1);
   if(pad>0)blocks.Add(new RectangleF(u.P.X-pad,u.P.Y-pad,2*pad,2*pad));
  }return blocks.Count==0?null:blocks.ToArray();
 }
 static readonly PointF[] DockOffsets={PointF.Empty,new PointF(-23,0),new PointF(23,0)};
 static readonly PointF[] MineOffsets={PointF.Empty,new PointF(22,0),new PointF(-22,0),new PointF(0,22),new PointF(0,-22),new PointF(-21,-21),new PointF(21,-21),new PointF(-21,21),new PointF(21,21)};
 bool TrafficStalled(Piece truck,float dt){
  if(truck.Path.Count==0){truck.TrafficSampleTime=0;return false;}
  if(truck.TrafficSampleTime<=0){truck.TravelSample=truck.P;truck.TrafficSampleTime=1.7f;}
  truck.TrafficSampleTime-=dt;
  return truck.TrafficSampleTime<=0&&Dist(truck.P,truck.TravelSample)<3;
 }
 void RunMiner(Piece truck,float dt){
  truck.Dispatch-=dt;
  if(TrafficStalled(truck,dt)){truck.Path.Clear();if(truck.DockReserved)truck.DockRetryAfter=Time+2;truck.DockReserved=false;truck.Unload=0;truck.Dispatch=0;}
  if(truck.Cargo>=LoadValue-.01f){truck.Cargo=LoadValue;truck.Returning=true;}
  if(truck.Returning){ReturnOre(truck,dt);return;}
  var patch=truck.Patch;
  if(patch!=null&&patch.Stock>.01f&&Dist(truck.P,truck.WorkPoint)<5&&Dist(truck.WorkPoint,patch.P)<=30){
   truck.Path.Clear();float take=patch.Extract(Math.Min(LoadValue-truck.Cargo,dt*36));
   truck.Cargo+=take;if(truck.Side==0&&take>0)FirstOreCollected=true;
   if(patch.Stock<=.01f){truck.Patch=null;truck.Returning=truck.Cargo>.01f;}
   return;
  }
  if(truck.Dispatch>0||(truck.Path.Count>0&&!truck.Surveying))return;
  truck.Dispatch=1;truck.Patch=null;
  foreach(var dest in Ore.Where(p=>p.Stock>.01f&&Explored(truck.Side,p.P)).OrderBy(p=>p==patch?0:WorkingPatch(truck,p)?1:2).ThenBy(p=>Dist(truck.P,p.P))){
   if(RouteToWork(truck,dest.P,false)){truck.Patch=dest;truck.Surveying=false;return;}
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
