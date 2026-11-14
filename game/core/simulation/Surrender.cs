using System;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 public const float SurrenderDelay=3;
 readonly bool[] surrendered=new bool[MaxFactions];
 readonly float[] unableToFightFor=new float[MaxFactions];
 public bool HasSurrendered(int side){return side>0&&side<surrendered.Length&&surrendered[side];}
 // Count alive assets, including construction, packing and passengers. Temporary
 // power/funding shortages and blocked exits do not mean a lost production base.
 bool CanStillResist(int side){
  return Units.Any(u=>u.Alive&&u.Side==side&&
   (u.T.Key=="hq"||u.T.Key=="mcv"||u.T.Key=="barracks"||u.T.Key=="factory"||
    !u.T.Building&&(u.T.Hit>0||u.T.Key=="engineer")));
 }
 void CheckEnemySurrender(float dt){
  if(Tutorial)return;
  for(int side=1;side<FactionCount;side++){
   if(surrendered[side])continue;
   if(Remaining(side)==0||CanStillResist(side)){unableToFightFor[side]=0;continue;}
   unableToFightFor[side]+=dt;
   if(unableToFightFor[side]>=SurrenderDelay)Surrender(side);
  }
 }
 void Surrender(int side){
  surrendered[side]=true;
  var remaining=Units.Where(u=>u.Alive&&u.Side==side).ToArray();
  var focus=remaining.FirstOrDefault(u=>u.T.Building)??remaining.FirstOrDefault();
  // No transfers/refunds or completed orders may survive the eliminated side.
  for(int category=0;category<2;category++)construction[side,category]=null;
  foreach(var u in remaining){u.Queue.Clear();u.Work=0;u.WorkPaid=0;u.Repairing=false;u.Selling=false;}
  foreach(var u in remaining.OrderBy(u=>u.T.Building))if(u.Alive){if(IsRelayStation(u)){u.Side=-1;u.Target=null;Changed();}else Kill(u);}
  UpdateSatellites();
  string message=FactionName(side)+"已投降，残余设施正在自毁。";
  if(focus!=null)SetEvent(focus.P,message);Radio=message;
 }
 public string EnemyStatus(int side){return HasSurrendered(side)?"已投降":Remaining(side)==0?"已歼灭":Remaining(side).ToString();}
}
}
