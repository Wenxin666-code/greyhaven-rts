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
 public const int LoadValue=ReferenceBalance.OreValue*ReferenceBalance.MinerStorage;
 public readonly List<OrePatch> Ore=CreateFields();
 public bool FirstOreCollected {get;private set;}
 public readonly int[] OreReceipts=new int[MaxFactions];
 // The relay briefing supplies one local deposit to both starting factions.
 // This is map intelligence, not a larger unit sight radius or a global ore scan.
 void RevealStartingOre(){
  if(!RelayMission)return;
  for(int side=0;side<FactionCount;side++){
   var closest=Ore.OrderBy(p=>Dist(p.P,Home(side))).FirstOrDefault();if(closest==null)continue;
   foreach(var patch in Ore.Where(p=>Dist(p.P,closest.P)<=2*Tile)){
    int x=FogColumn(patch.P.X),y=FogRow(patch.P.Y);
    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)if(x+dx>=0&&y+dy>=0&&x+dx<MapFogCols&&y+dy<MapFogRows)Discover(side,x+dx,y+dy);
   }
  }
 }
 public PointF? LastEvent;public string EventLabel;public float EventTime;float lastStrike=-10;
 void SetEvent(PointF p,string label){EventTime=Time;LastEvent=p;EventLabel=label;}
 void ReportStrike(Piece unit){
  if(Time-lastStrike<2)return;
  SetEvent(unit.P,unit.T.Name+"遇袭");lastStrike=Time;
 }
 internal static List<OrePatch> CreateFields(){
  var ore=new List<OrePatch>();
  foreach(var center in new[]{new PointF(53,626),new PointF(EnemyHome.X+160,EnemyHome.Y+180),
   new PointF(449,221),new PointF(896,629),new PointF(1020,140),
   new PointF(550,1170),new PointF(1280,1310),new PointF(2060,470),
   new PointF(2710,520),new PointF(2690,1630),new PointF(1090,1750),new PointF(2010,2050)}){
   int cx=Math.Max(0,(int)(center.X/Tile)-2),cy=(int)(center.Y/Tile)-3;
   for(int y=0;y<10;y++)for(int x=0;x<7;x++){
    var p=new PointF((cx+x+.5f)*Tile,(cy+y+.5f)*Tile);
    if(center.X==53&&p.Y<684&&p.X>36)continue;
    if(!Roads.Any(r=>r.Contains(p))&&Dist(p,EnemyHome)>100&&Dist(p,new PointF(152,514))>82)ore.Add(new OrePatch(p.X,p.Y,1200));
   }
  }return ore;
 }
 public static PointF DockPoint(Piece refinery){
  return Middle(ExitApron(refinery.T,Footprint(refinery.T,refinery.P)));
 }
 void RunRefinery(Piece plant,float dt){
  if(!plant.StarterDelivered){
   PointF exit;if(Exit(plant,Info("miner"),out exit)){Spawn("miner",plant.Side,exit.X,exit.Y).DepartingDock=plant;plant.StarterDelivered=true;}
  }
  if(plant.Door>0)plant.MillPhase=(plant.MillPhase+dt*1.2f)%1;
 }
 public OrePatch OreAt(PointF point){return Ore.Where(p=>p.Stock>.01f&&Dist(p.P,point)<26).OrderBy(p=>Dist(p.P,point)).FirstOrDefault();}
 public void MineOrder(Piece truck,OrePatch patch,Piece plant){
  if(!truck.Deployed||truck.T.Key!="miner")return;
  Halt(truck);truck.Mining=true;truck.Patch=patch;truck.OrderedOre=patch;truck.Surveying=false;truck.Dispatch=0;truck.TrafficSampleTime=0;
  truck.Returning=truck.Cargo>=LoadValue-.01f||(plant!=null&&truck.Cargo>.01f);
  if(plant!=null&&truck.Returning)truck.Dock=plant;
 }
 public string MiningStatus(Piece truck){
  string mode=!truck.Mining?"待命":truck.Unload>0?"卸矿":truck.Returning?DockStatus(truck):truck.DepartingDock!=null?"驶离卸矿口":truck.Surveying?"探查新地形":truck.Path.Count>0?"前往矿区":truck.Patch==null?"寻找矿点":"采集";
  return mode+"  ·  载矿 "+(int)truck.Cargo+" / "+LoadValue;
 }
 bool WorkingPatch(Piece truck,OrePatch patch){
  return !Units.Any(o=>o!=truck&&o.Deployed&&o.Mining&&!o.Returning&&o.Patch==patch);
 }
 bool DockFree(Piece truck,Piece plant){
  return !Units.Any(o=>o!=truck&&o.Deployed&&o.Mining&&o.Returning&&o.Dock==plant&&
   (o.DockReserved||Dist(o.P,DockPoint(plant))<180&&EarlierDock(o,truck)));
 }
 // A stopped vehicle may occupy the vein center or one side of a loading gate.
 // Reserve the actual service position, not just a nominal map coordinate.
 bool RouteToWork(Piece truck,PointF center,bool unloading){
  PointF[] offsets=unloading?DockOffsets:MineOffsets;
  var blocks=MineTraffic(truck,center);
  foreach(var offset in offsets){
   var dest=new PointF(center.X+offset.X,center.Y+offset.Y);
   if(blocks!=null&&blocks.Any(r=>r.Contains(dest)))continue;
   if(Units.Any(u=>u!=truck&&u.Deployed&&!u.T.Building&&(Dist(u.P,dest)<truck.T.TrafficRadius+u.T.TrafficRadius+1||
      (u.Mining&&u.Path.Count>0&&!(unloading&&u.Returning&&u.Dock==truck.Dock&&!u.DockReserved)&&Dist(u.Path.Last(),dest)<truck.T.TrafficRadius+u.T.TrafficRadius+1))))continue;
   if(PlanRoute(truck,dest,blocks)){truck.WorkPoint=dest;return true;}
  }
  if(!unloading)foreach(var idle in Live(truck.Side).Where(u=>u!=truck&&!u.T.Building&&!u.Mining&&u.Path.Count==0&&u.Target==null&&Dist(u.P,center)<Tile).ToArray())NudgeIdleFriend(truck,idle);
  return false;
 }
 // Keep neighboring blockers together on every replan; going around only the last
 // vehicle can send a truck straight back into the previous one.
 RectangleF[] MineTraffic(Piece truck,PointF destination,Predicate<Piece> include=null){
  var blocks=new List<RectangleF>();
  foreach(var u in Units){
   if(u==truck||!u.Deployed||u.T.Building)continue;
   // Stationary blockers anywhere along the route must participate in the same
   // search. Moving traffic is only sampled nearby, avoiding a stale long wall.
   if(include!=null ? !include(u) : u.Path.Count>0&&Dist(truck.P,u.P)>140&&Dist(destination,u.P)>90)continue;
   float gap=Math.Max(Math.Abs(truck.P.X-u.P.X),Math.Abs(truck.P.Y-u.P.Y));
   float pad=Math.Min(gap-.25f,truck.T.TrafficRadius+u.T.TrafficRadius+1);
   if(pad>0)blocks.Add(new RectangleF(u.P.X-pad,u.P.Y-pad,2*pad,2*pad));
  }return blocks.Count==0?null:blocks.ToArray();
 }
 static readonly PointF[] DockOffsets={PointF.Empty};
 // Wider vehicle spacing can close the inner working ring. Use perimeter
 // positions before abandoning a known ore patch or driving off to explore.
 static readonly PointF[] MineOffsets={PointF.Empty,new PointF(6,0),new PointF(-6,0),new PointF(0,6),new PointF(0,-6)};
 bool TrafficStalled(Piece truck,float dt){
  if(truck.Path.Count==0){truck.TrafficSampleTime=0;return false;}
  if(truck.TrafficSampleTime<=0){truck.TravelSample=truck.P;truck.TrafficSampleTime=1.7f;}
  truck.TrafficSampleTime-=dt;
  return truck.TrafficSampleTime<=0&&truck.TrafficBlocked>.6f&&Dist(truck.P,truck.TravelSample)<3;
 }
 void RunMiner(Piece truck,float dt){
  truck.Dispatch-=dt;
  if(TrafficStalled(truck,dt)){
   var destination=truck.Path.Last();
   if(PlanRoute(truck,destination,MineTraffic(truck,destination))){truck.TrafficBlocked=0;truck.Dispatch=.5f;}
   else{truck.Path.Clear();truck.DockBlockedFor+=1.7f;if(truck.DockReserved)truck.DockRetryAfter=Time+1;truck.DockReserved=false;truck.Unload=0;truck.Dispatch=0;}
  }
  if(truck.Cargo>=LoadValue-.01f){truck.Cargo=LoadValue;truck.Returning=true;}
  if(truck.Returning){ReturnOre(truck,dt);return;}
  if(LeaveRefinery(truck))return;
  var patch=truck.Patch;
  if(patch!=null&&patch.Stock>.01f&&Dist(truck.P,truck.WorkPoint)<5&&Dist(truck.WorkPoint,patch.P)<=Tile*.5f){
   truck.Path.Clear();float take=patch.Extract(Math.Min(LoadValue-truck.Cargo,dt*ReferenceBalance.HarvestCreditsPerSecond));
   truck.Cargo+=take;if(truck.Side==0&&take>0)FirstOreCollected=true;
   if(patch.Stock<=.01f){truck.Patch=null;truck.OrderedOre=null;truck.Dispatch=0;}
   return;
  }
  if(truck.Dispatch>0||(truck.Path.Count>0&&!truck.Surveying))return;
  truck.Dispatch=1;truck.Patch=null;
  if(ChooseOre(truck))return;
  if(truck.OreSearching){truck.Dispatch=.12f;return;}
  if(truck.Cargo>.01f){truck.Returning=true;truck.Surveying=false;truck.Path.Clear();return;}
  // Busy cells are not an exhausted field. Wait nearby while another truck
  // clears its working cell instead of launching an unrelated exploration trip.
  if(Ore.Any(p=>p.Stock>.01f&&Explored(truck.Side,p.P)&&Units.Any(u=>u!=truck&&u.Deployed&&u.Mining&&u.Patch==p)&&CanMoveTo(truck,p.P))){truck.Path.Clear();truck.Surveying=false;truck.Dispatch=.7f;return;}
  if(truck.Path.Count==0)SurveyForOre(truck);
 }
 sealed class OreCandidate {internal OrePatch Patch;internal float Penalty,Lower;}
 sealed class OreSearch {
  internal PointF Origin;internal int Topology,Token,Next;internal OreCandidate[] Candidates;
  internal OrePatch Best;internal float BestCost=float.MaxValue;
 }
 readonly Dictionary<Piece,OreSearch> oreSearches=new Dictionary<Piece,OreSearch>();
 float oreBudgetAt=-1;int oreSearchRoutes;
 bool ChooseOre(Piece truck){
  var manual=truck.OrderedOre;truck.OrderedOre=null;
  if(manual!=null&&manual.Stock>.01f&&RouteToWork(truck,manual.P,false)){
   oreSearches.Remove(truck);truck.OreSearching=false;truck.Patch=manual;truck.Surveying=false;return true;
  }
  if(oreBudgetAt!=Time){oreBudgetAt=Time;oreSearchRoutes=0;}
  truck.OreSearching=true;
  if(oreSearchRoutes>=4)return false;
  OreSearch search;
  if(!oreSearches.TryGetValue(truck,out search)||search.Topology!=topology||search.Token!=truck.OreSearchToken||Dist(truck.P,search.Origin)>Tile){
   foreach(var dead in oreSearches.Keys.Where(p=>!p.Alive).ToArray())oreSearches.Remove(dead);
   search=new OreSearch{Origin=truck.P,Topology=topology,Token=truck.OreSearchToken,
    Candidates=Ore.Where(p=>p.Stock>.01f&&Explored(truck.Side,p.P)).Select(p=>new OreCandidate{Patch=p,Penalty=WorkingPatch(truck,p)?0:Tile})
     .Select(p=>{p.Lower=Math.Max(0,Dist(truck.P,p.Patch.P)-6)+p.Penalty;return p;}).OrderBy(p=>p.Lower).ToArray()};
   oreSearches[truck]=search;
  }
  // Keep the same least-route-cost choice, but spread difficult rejected veins
  // across updates. A pending search is neither exhausted ore nor an order to roam.
  var savedPath=truck.Path;var savedWork=truck.WorkPoint;
  try{
   while(search.Next<search.Candidates.Length&&oreSearchRoutes<4){
    var candidate=search.Candidates[search.Next];
    if(search.Best!=null&&candidate.Lower>=search.BestCost)break;
    search.Next++;if(candidate.Patch.Stock<=.01f)continue;oreSearchRoutes++;
    if(!RouteToWork(truck,candidate.Patch.P,false))continue;
    float cost=candidate.Penalty;var prev=truck.P;foreach(var p in truck.Path){cost+=Dist(prev,p);prev=p;}
    if(cost<search.BestCost){search.Best=candidate.Patch;search.BestCost=cost;}
   }
  }finally{truck.Path=savedPath;truck.WorkPoint=savedWork;}
  bool complete=search.Next>=search.Candidates.Length||search.Best!=null&&search.Candidates[search.Next].Lower>=search.BestCost;
  if(!complete)return false;
  oreSearches.Remove(truck);truck.OreSearching=false;
  if(search.Best==null)return false;
  if(search.Best.Stock>.01f&&RouteToWork(truck,search.Best.P,false)){
   truck.Patch=search.Best;truck.Surveying=false;return true;
  }
  // Traffic changed while comparing routes; retry known ore, not unexplored land.
  truck.OreSearching=true;return false;
 }
 void SurveyForOre(Piece truck){
  // Choose unexplored ground using the side's map, independently of ore locations.
  truck.Surveying=ExploreGround(truck);
  if(!truck.Surveying)truck.Dispatch=4;
 }
}
}
