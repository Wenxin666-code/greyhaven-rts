using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 static PointF EnemySite(float x,float y){return new PointF(EnemyHome.X+x,EnemyHome.Y+y);}
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
  if(!EnemyFacility("power",EnemySite(84,-68))||!EnemyFacility("refinery",EnemySite(12,192)))return;
  if(!Live(1).Any(u=>u.T.Key=="miner")){
   if(Live(1).Any(u=>u.T.Key=="refinery"&&!u.StarterDelivered))return;
   if(!Live(1).Any(u=>u.Queue.Any(t=>t.Key=="miner"))){
    if(EnemyFacility("factory",EnemySite(0,100)))Train(Info("miner"),1,null);
   }return;
  }
  if(OreReceipts[1]==0||!EnemyFacility("barracks",EnemySite(72,2)))return;
  // Field the first squad, then reserve funds for the factory before buying more.
  if(aiTurn==0){if(Train(Info("squad"),1,null))aiTurn++;return;}
  if(!EnemyFacility("factory",EnemySite(0,100)))return;
  if(Time<aiClock)return;
  string[] order={"ifv","at","tank","squad","engineer","arty"};
  var next=Info(order[(aiTurn-1)%order.Length]);
  if((next.Key=="tank"||next.Key=="arty")&&!Upgraded[1]&&!UpgradeArmor(1))return;
  if(Train(next,1,null)){aiTurn++;aiClock=Time+14*(Assisted?1.7f:1);}
 }
}
}
