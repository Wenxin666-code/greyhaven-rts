using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 bool EnemyFacility(string key,PointF location){
  var type=Info(key);var existing=Units.FirstOrDefault(u=>u.Side==1&&u.Alive&&u.T==type);
  if(existing!=null)return existing.Deployed;
  if(Require(type,1)!=null)return false;
  for(int ring=0;ring<=22;ring++)for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++){
   if(Math.Max(Math.Abs(x),Math.Abs(y))!=ring)continue;
   var site=new PointF(location.X+x*Tile,location.Y+y*Tile);
   if(CanPlace(type,site,null,1)){BuildFor(type,site,1);return false;}
  }
  return false;
 }
 void EnemyEconomy(){
  if(!Live(1).Any(u=>u.T.Key=="hq")){
   var mcv=Live(1).FirstOrDefault(u=>u.T.Key=="mcv");if(mcv!=null)DeployVehicle(mcv);return;
  }
  if(!EnemyFacility("power",new PointF(1632,432))||!EnemyFacility("refinery",new PointF(1560,692)))return;
  if(!Live(1).Any(u=>u.T.Key=="miner")){
   if(Live(1).Any(u=>u.T.Key=="refinery"&&!u.StarterDelivered))return;
   if(!Live(1).Any(u=>u.Queue.Any(t=>t.Key=="miner"))){
    if(EnemyFacility("factory",new PointF(1548,600)))Train(Info("miner"),1,null);
   }return;
  }
  if(OreReceipts[1]==0||!EnemyFacility("barracks",new PointF(1620,502)))return;
  // Field the first squad, then reserve funds for the factory before buying more.
  if(aiTurn==0){if(Train(Info("squad"),1,null))aiTurn++;return;}
  if(!EnemyFacility("factory",new PointF(1548,600)))return;
  if(Time<aiClock)return;
  string[] order={"ifv","at","tank","squad","engineer","arty"};
  var next=Info(order[(aiTurn-1)%order.Length]);
  if((next.Key=="tank"||next.Key=="arty")&&!Upgraded[1]&&!UpgradeArmor(1))return;
  if(Train(next,1,null)){aiTurn++;aiClock=Time+(counter?8:14)*(Assisted?1.7f:1);}
 }
}
}
