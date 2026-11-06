using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 static bool EarlierDock(Piece a,Piece b){return a.DockTicket<b.DockTicket||a.DockTicket==b.DockTicket&&a.Id<b.Id;}
 int DockPosition(Piece truck){return 1+Units.Count(o=>o!=truck&&o.Deployed&&o.Mining&&o.Returning&&o.Dock==truck.Dock&&EarlierDock(o,truck));}
 string DockStatus(Piece truck){
  if(!Live(truck.Side).Any(o=>o.T.Key=="refinery"))return "缺少精炼厂";
  if(truck.Dock==null)return "安排返厂";
  if(truck.DockReserved)return "驶入卸矿位";
  if(truck.DockRetryAfter>Time)return "卸矿通道受阻";
  return Dist(truck.P,DockPoint(truck.Dock))>140?"返厂排队":"卸矿排队第 "+DockPosition(truck)+" 位";
 }
 bool DockQueueRoute(Piece truck,Piece plant){
  var center=DockPoint(plant);int index=DockPosition(truck)-1;
  float[] lanes={-72,-40,40,72};
  for(int attempt=0;attempt<12;attempt++){
   int slot=index+attempt;var dest=new PointF(center.X+lanes[slot%4],center.Y+64+slot/4*28);
   if(Units.Any(u=>u!=truck&&u.Deployed&&!u.T.Building&&(Dist(u.P,dest)<19||u.Path.Count>0&&Dist(u.Path.Last(),dest)<19)))continue;
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
   plant=Live(truck.Side).Where(o=>o.T.Key=="refinery").OrderBy(o=>Dist(truck.P,o.P)+Units.Count(u=>u!=truck&&u.Deployed&&u.Returning&&u.Dock==o)*80).FirstOrDefault();
   truck.Dock=plant;if(plant==null){truck.Path.Clear();return;}
  }
  if(truck.DockReserved&&Dist(truck.P,truck.WorkPoint)<5&&Dist(truck.WorkPoint,DockPoint(plant))<=24){
   truck.Path.Clear();plant.Door=1;truck.Unload+=dt;
   if(truck.Unload>=1.6f){
    int receipt=(int)Math.Round(truck.Cargo);Funds[truck.Side]+=receipt;OreReceipts[truck.Side]+=receipt;plant.LastReceipt=receipt;plant.ReceiptAt=Time;
    truck.Cargo=0;truck.Unload=0;truck.Dock=null;truck.DockReserved=false;truck.DockTicket=0;truck.Returning=false;truck.Dispatch=0;truck.DockRetryAfter=0;
   }return;
  }
  if(truck.Dispatch>0)return;truck.Dispatch=.5f;
  // A reservation starts at the refinery apron, not at a distant ore patch.
  if(truck.DockRetryAfter<=Time&&Dist(truck.P,DockPoint(plant))<140&&DockFree(truck,plant)){
   if(truck.DockReserved&&truck.Path.Count>0)return;
   if(RouteToWork(truck,DockPoint(plant),true)){truck.DockReserved=true;return;}
   truck.DockReserved=false;truck.DockRetryAfter=Time+2;
  }
  if(!truck.DockReserved&&truck.Path.Count==0)DockQueueRoute(truck,plant);
 }
}
}
