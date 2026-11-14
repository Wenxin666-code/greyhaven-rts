using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 // Completing a delivery includes leaving the service cell. An empty truck
 // must not keep the entrance occupied when ore is exhausted or all veins busy.
 bool LeaveRefinery(Piece truck){
  var plant=truck.DepartingDock;if(plant==null)return false;
  var gate=DockPoint(plant);
  if(!plant.Alive||Dist(truck.P,gate)>Tile*2){truck.DepartingDock=null;return false;}
  if(truck.Path.Count>0||truck.Dispatch>0)return true;
  truck.Dispatch=.5f;
  var ore=Ore.Where(p=>p.Stock>.01f&&Explored(truck.Side,p.P)).OrderBy(p=>Dist(p.P,gate)).FirstOrDefault();
  int preferred=ore!=null&&ore.P.X<gate.X?-1:1;
  for(int ring=0;ring<3;ring++)foreach(int direction in new[]{preferred,-preferred})foreach(float depth in new[]{24f,48f,72f}){
   var point=new PointF(gate.X+direction*(48+ring*24),gate.Y+depth);
   if(Units.Any(u=>u!=truck&&u.Deployed&&!u.T.Building&&(TrafficContact(truck.T,point,u.T,u.P)||u.Mining&&u.Path.Count>0&&TrafficContact(truck.T,point,u.T,u.Path.Last()))))continue;
   if(PlanRoute(truck,point,MineTraffic(truck,point))){truck.WorkPoint=point;return true;}
  }
  return true;
 }
}
}
