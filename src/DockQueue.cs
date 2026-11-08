using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 bool ChooseRefinery(Piece truck,Piece excluded){
  var saved=truck.Path;int savedVersion=truck.RouteVersion;Piece best=null;PointF[] bestRoute=null;float bestCost=float.MaxValue;
  foreach(var candidate in Live(truck.Side).Where(o=>o.T.Key=="refinery"&&o!=excluded).OrderBy(o=>Dist(truck.P,DockPoint(o)))){
   var gate=DockPoint(candidate);
   // Evaluate static access, not momentary occupancy by a truck unloading.
   // This also avoids committing to the closest building on the wrong bank.
   if(!PlanRoute(truck,gate,null))continue;
   float distance=0;var previous=truck.P;foreach(var p in truck.Path){distance+=Dist(previous,p);previous=p;}
   int ahead=Units.Count(u=>u!=truck&&u.Deployed&&u.Side==truck.Side&&u.Returning&&u.Dock==candidate&&
    (u.DockReserved||EarlierDock(u,truck)&&Dist(u.P,gate)<180));
   float cost=distance/Math.Max(1,truck.T.Speed)+ahead*1.6f;
   if(cost<bestCost){best=candidate;bestCost=cost;bestRoute=truck.Path.ToArray();}
  }
  truck.Path=saved;truck.RouteVersion=savedVersion;if(best==null)return false;
  truck.Dock=best;truck.DockReserved=false;truck.DockRetryAfter=0;truck.DockBlockedFor=0;truck.Unload=0;
  truck.Path=new System.Collections.Generic.Queue<PointF>(bestRoute);truck.WorkPoint=DockPoint(best);truck.RouteVersion=topology;return true;
 }
 static bool EarlierDock(Piece a,Piece b){return a.DockTicket<b.DockTicket||a.DockTicket==b.DockTicket&&a.Id<b.Id;}
 int DockPosition(Piece truck){return 1+Units.Count(o=>o!=truck&&o.Deployed&&o.Mining&&o.Returning&&o.Dock==truck.Dock&&EarlierDock(o,truck));}
 string DockStatus(Piece truck){
  if(!Live(truck.Side).Any(o=>o.T.Key=="refinery"))return "缺少精炼厂";
  if(truck.Dock==null)return "安排返厂";
  if(truck.DockBlockedFor>=2)return "通路受阻 · 重寻路线/精炼厂";
  if(truck.DockReserved)return "驶入卸矿位";
  if(truck.DockRetryAfter>Time)return "卸矿通道受阻";
  return Dist(truck.P,DockPoint(truck.Dock))>140?"返厂排队":"卸矿排队第 "+DockPosition(truck)+" 位";
 }
 bool DockQueueRoute(Piece truck,Piece plant){
  var center=DockPoint(plant);int index=DockPosition(truck)-1;
  float[] lanes={-84,-42,42,84};
  for(int attempt=0;attempt<12;attempt++){
   int slot=index+attempt;var dest=new PointF(center.X+lanes[slot%4],center.Y+72+slot/4*36);
   if(Units.Any(u=>u!=truck&&u.Deployed&&!u.T.Building&&(Dist(u.P,dest)<truck.T.TrafficRadius+u.T.TrafficRadius+1||u.Path.Count>0&&Dist(u.Path.Last(),dest)<truck.T.TrafficRadius+u.T.TrafficRadius+1)))continue;
   if(Dist(truck.P,dest)<5){truck.Path.Clear();return true;}
   if(PlanRoute(truck,dest,MineTraffic(truck,dest))){truck.WorkPoint=dest;return true;}
  }
  return false;
 }
 void ReturnOre(Piece truck,float dt){
  if(truck.DockTicket<=0)truck.DockTicket=Math.Max(.0001f,Time);
  var plant=truck.Dock;
  if(plant!=null&&(!plant.Deployed||plant.Side!=truck.Side)){
   truck.Dock=null;truck.DockReserved=false;truck.Path.Clear();truck.Unload=0;plant=null;
  }
  if(plant==null){
   if(truck.Dispatch>0)return;
   if(!ChooseRefinery(truck,null)){truck.Path.Clear();truck.Dispatch=1;return;}plant=truck.Dock;
  }
  if(truck.DockReserved&&Dist(truck.P,truck.WorkPoint)<1&&Dist(truck.WorkPoint,DockPoint(plant))<.05f){
   truck.Path.Clear();plant.Door=1;truck.Unload+=dt;
   if(truck.Unload>=1.6f){
    int receipt=(int)Math.Round(truck.Cargo);Funds[truck.Side]+=receipt;OreReceipts[truck.Side]+=receipt;plant.LastReceipt=receipt;plant.ReceiptAt=Time;
    truck.Cargo=0;truck.Unload=0;truck.Dock=null;truck.DockReserved=false;truck.DockTicket=0;truck.Returning=false;truck.Dispatch=0;truck.DockRetryAfter=0;truck.DockBlockedFor=0;truck.Patch=null;truck.OrderedOre=null;
   }return;
  }
  if(truck.Dispatch>0)return;truck.Dispatch=.5f;
  if(truck.DockBlockedFor>=2&&AlternateDock(truck,plant))return;
  bool near=Dist(truck.P,DockPoint(plant))<140,free=DockFree(truck,plant);
  if(near&&!truck.DockReserved&&!free){
   if(truck.Path.Count>0&&Dist(truck.Path.Last(),DockPoint(plant))<.05f)truck.Path.Clear();
   if(truck.Path.Count==0&&!DockQueueRoute(truck,plant))truck.DockBlockedFor+=.5f;return;
  }
  // A reservation starts at the refinery apron, not at a distant ore patch.
  if(truck.DockRetryAfter<=Time&&near&&free){
   if(truck.DockReserved&&truck.Path.Count>0)return;
   foreach(var idle in Live(truck.Side).Where(u=>u!=truck&&!u.T.Building&&!u.Mining&&u.Path.Count==0&&u.Target==null&&!u.FirePoint.HasValue&&TrafficContact(truck.T,DockPoint(plant),u.T,u.P)).ToArray())RallyNewUnit(plant,idle);
   if(RouteToWork(truck,DockPoint(plant),true)){truck.DockReserved=true;truck.DockBlockedFor=0;return;}
   ClearDockApproach(truck,plant);
   truck.DockReserved=false;truck.DockRetryAfter=Time+2;
   truck.DockBlockedFor+=.5f;
  }
  if(!truck.DockReserved&&truck.Path.Count==0){
   if(free&&RouteToWork(truck,DockPoint(plant),true)){truck.DockReserved=near;truck.DockBlockedFor=0;return;}
   if(!DockQueueRoute(truck,plant))truck.DockBlockedFor+=.5f;
  }
 }
 void ClearDockApproach(Piece head,Piece plant){
  var gate=DockPoint(plant);
  foreach(var waiting in Live(head.Side).Where(u=>u!=head&&u.Mining&&u.Returning&&u.Dock==plant&&!u.DockReserved&&EarlierDock(head,u)&&
   (u.Path.Count==0||u.TrafficBlocked>.4f)&&Dist(u.P,gate)<145).ToArray()){
   for(int i=0;i<12;i++){
    var slot=new PointF(gate.X+(i%2==0?-1:1)*(110+i/6*30),gate.Y+132+i/2%3*32);
    if(Units.Any(u=>u!=waiting&&u.Deployed&&!u.T.Building&&(Dist(u.P,slot)<26||u.Path.Count>0&&Dist(u.Path.Last(),slot)<26)))continue;
    if(PlanRoute(waiting,slot,MineTraffic(waiting,slot))){waiting.WorkPoint=slot;waiting.Dispatch=1.5f;break;}
   }
  }
 }
 bool AlternateDock(Piece truck,Piece failed){
  return ChooseRefinery(truck,failed);
 }
}
}
