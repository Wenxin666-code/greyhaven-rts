using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 PointF EnemySite(int side,float x,float y){var home=Home(side);return new PointF(home.X+x,home.Y+y);}
 readonly int[] enemyTurns=new int[MaxFactions];readonly float[] enemyClocks=new float[MaxFactions];
 bool EnemyFacility(int side,string key,PointF location){
  var type=Info(key);var existing=Units.FirstOrDefault(u=>u.Side==side&&u.Alive&&u.T==type);
  if(existing!=null)return existing.Deployed;
  if(Require(type,side)!=null)return false;
  for(int ring=0;ring<=22;ring++)for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++){
   if(Math.Max(Math.Abs(x),Math.Abs(y))!=ring)continue;
   var site=new PointF(location.X+x*Tile,location.Y+y*Tile);
   if(CanPlace(type,site,null,side)){BuildFor(type,site,side);return false;}
  }
  return false;
 }
 // Surviving factories can recover a base; surviving barracks can still reinforce.
 void EnemyRecoveryProduction(int side){
  var factory=PrimaryFactory(side,"factory");
  if(Live(side).Any(p=>p.Queue.Any(t=>t.Key=="mcv")))return;
  if(factory!=null&&Funds[side]>=Info("mcv").Price){Train(Info("mcv"),side,null);return;}
  if(Time<enemyClocks[side])return;
  string[] choices={"ifv","at","squad"};
  var options=choices.Select(Info).Where(t=>Require(t,side)==null).ToArray();
  if(options.Length==0)return;var next=options[enemyTurns[side]%options.Length];
  if(Train(next,side,null)){enemyTurns[side]++;enemyClocks[side]=Time+14*(Assisted?1.7f:1);}
 }
 void DeployEnemyBase(Piece mcv){
  if(DeployVehicle(mcv)||mcv.Path.Count>0)return;
  // A newly produced MCV must clear the factory before deploying its foundation.
  for(int ring=1;ring<=12;ring++)for(int y=-ring;y<=ring;y++)for(int x=-ring;x<=ring;x++){
   if(Math.Max(Math.Abs(x),Math.Abs(y))!=ring)continue;
   var p=Middle(Footprint(Info("hq"),new PointF(mcv.P.X+x*Tile,mcv.P.Y+y*Tile)));
   if(CanPlace(Info("hq"),p,mcv,mcv.Side)&&Move(mcv,p,false))return;
  }
 }
 void EnemyEconomy(int side=1){
  if(River!=null){SmartEconomy(side);return;}
  if(HasSurrendered(side))return;
  if(!Live(side).Any(u=>u.T.Key=="hq")){
   var mcv=Live(side).FirstOrDefault(u=>u.T.Key=="mcv");if(mcv!=null)DeployEnemyBase(mcv);
   else if(!Units.Any(u=>u.Alive&&u.Side==side&&(u.T.Key=="hq"||u.T.Key=="mcv")))EnemyRecoveryProduction(side);
   return;
  }
  if(!EnemyFacility(side,"power",EnemySite(side,84,-68))||!EnemyFacility(side,"refinery",EnemySite(side,12,192)))return;
  if(!Live(side).Any(u=>u.T.Key=="miner")){
   if(Live(side).Any(u=>u.T.Key=="refinery"&&!u.StarterDelivered))return;
   if(!Live(side).Any(u=>u.Queue.Any(t=>t.Key=="miner"))){
    if(EnemyFacility(side,"factory",EnemySite(side,0,100)))Train(Info("miner"),side,null);
   }return;
  }
  if(OreReceipts[side]==0||!EnemyFacility(side,"barracks",EnemySite(side,72,2)))return;
  // Field the first squad, then reserve funds for the factory before buying more.
  if(enemyTurns[side]==0){if(Train(Info("squad"),side,null))enemyTurns[side]++;return;}
  if(!EnemyFacility(side,"factory",EnemySite(side,0,100)))return;
  if(Time>240&&(Funds[side]>1100||Construction(side,0)!=null)&&!Live(side).Any(u=>u.T.Key=="satellite")){
   if(!EnemyFacility(side,"radar",EnemySite(side,-96,40)))return;
   if(Power(side)<6&&!Units.Any(u=>u.Alive&&u.Side==side&&u.T.Key=="power"&&u.Ready<1)){
    var p=EnemySite(side,-84,-96);for(int i=0;i<20;i++){var site=new PointF(p.X+i%5*Tile,p.Y+i/5*Tile);if(CanPlace(Info("power"),site,null,side)){BuildFor(Info("power"),site,side);break;}}return;
   }
   if(Power(side)>=6)EnemyFacility(side,"satellite",EnemySite(side,-96,120));
  }
  if(Time<enemyClocks[side])return;
  string[] order={"ifv","at","tank","squad","engineer","arty"};
  var next=Info(order[(enemyTurns[side]-1)%order.Length]);
  if(Train(next,side,null)){enemyTurns[side]++;enemyClocks[side]=Time+14*(Assisted?1.7f:1);}
 }
}
}
