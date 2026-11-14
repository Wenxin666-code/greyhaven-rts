using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 public bool ProductionBuilding(Piece p){return p!=null&&p.Deployed&&!p.Selling&&(p.T.Key=="factory"||p.T.Key=="barracks"||p.T.Key=="hq");}
 public Piece PrimaryFactory(int side,string key){return Live(side).Where(u=>u.T.Key==key&&!u.Selling).OrderByDescending(u=>u.Primary).ThenBy(u=>u.Id).FirstOrDefault();}
 public bool SetPrimary(Piece p){
  if(!ProductionBuilding(p))return false;
  foreach(var other in Live(p.Side).Where(u=>u.T==p.T))other.Primary=other==p;
  if(p.Side==0){
   LastOrder=p.T.Key=="factory"?VehicleQueue(p.Side):p;
   Radio=p.T.Name+" #"+p.Id+(p.T.Key=="factory"?" 已设为优先出车点。":" 已设为优先接单设施。");
  }
  return true;
 }
 public float ProductionRate(Piece p){
  float power=PowerProductionRate(p.Side);
  if(p.T.Key!="hq"&&p.T.Key!="factory")return power;
  int factories=Live(p.Side).Count(u=>u.T==p.T&&(p.T.Key=="hq"||!u.Selling));
  return power/(float)Math.Pow(.8,Math.Max(0,factories-1));
 }
 // The shared queue stays on its current host while the primary exit can change.
 public Piece VehicleQueue(int side){return VehicleQueue(side,null);}
 Piece VehicleQueue(int side,Piece skip){
  return Live(side).Where(p=>p!=skip&&p.T.Key=="factory"&&!p.Selling)
   .OrderByDescending(p=>p.Queue.Count>0).ThenByDescending(p=>p.Primary).ThenBy(p=>p.Id).FirstOrDefault();
 }
 bool UnitProductionExit(Piece source,TypeInfo type,out Piece outlet,out PointF position){
  outlet=source;
  if(source.T.Key!="factory")return Exit(source,type,out position);
  outlet=PrimaryFactory(source.Side,"factory")??source;
  if(Exit(outlet,type,out position))return true;
  foreach(var alternate in Live(source.Side).Where(p=>p.T.Key=="factory"&&!p.Selling).ToArray()){
   if(alternate!=outlet&&Exit(alternate,type,out position)){outlet=alternate;return true;}
  }
  return false;
 }
 static float ProductionLoad(Piece p){return Math.Max(0,p.Queue.Sum(t=>t.Seconds)-p.Work);}
 Piece NextUnitFactory(int side,string key,Piece preferred=null,Piece skip=null){
  if(key=="factory"){
   var line=VehicleQueue(side,skip);
   return line!=null&&line.Queue.Count<ProductionQueueLimit?line:null;
  }
  return Live(side).Where(p=>p!=skip&&p.T.Key==key&&!p.Selling&&p.Queue.Count<ProductionQueueLimit)
   .OrderBy(p=>p.ProductionPaused).ThenBy(p=>p.Queue.Count>0&&p.Work>=p.Queue.Peek().Seconds)
   .ThenBy(ProductionLoad).ThenByDescending(p=>p.Primary).ThenByDescending(p=>p==preferred).ThenBy(p=>p.Id).FirstOrDefault();
 }
 void BalanceProduction(){
  // A newly completed or reopened idle factory can take unstarted backlog.
  // Never move the paid head, and never bypass a player's explicit pause.
  foreach(var group in Units.Where(p=>ProductionBuilding(p)&&p.T.Key=="barracks"&&!p.ProductionPaused).GroupBy(p=>p.Side)){
   var factories=group.OrderBy(p=>p.Id).ToArray();
   foreach(var idle in factories.Where(p=>p.Queue.Count==0)){
    var donor=factories.Where(p=>p!=idle&&p.Queue.Count>1).OrderByDescending(ProductionLoad).ThenBy(p=>p.Id).FirstOrDefault();
    if(donor==null)break;
    var jobs=donor.Queue.ToArray();donor.Queue.Clear();for(int i=0;i<jobs.Length-1;i++)donor.Queue.Enqueue(jobs[i]);
    idle.Queue.Enqueue(jobs[jobs.Length-1]);idle.Work=0;idle.WorkPaid=0;
   }
  }
 }
 void TransferProduction(Piece old){
  // Damage may already have reduced HP to zero before Kill calls this method.
  if(old==null||old.Side<0||old.Queue.Count==0||(old.T.Key!="factory"&&old.T.Key!="barracks"&&old.T.Key!="hq"))return;
  var jobs=old.Queue.ToArray();int start=0;var next=NextUnitFactory(old.Side,old.T.Key,null,old);
  if(next!=null&&next.Queue.Count==0){
   next.Queue.Enqueue(jobs[0]);next.Work=old.Work;next.WorkPaid=old.WorkPaid;next.ProductionPaused=old.ProductionPaused;if(old.Side==0&&LastOrder==old)LastOrder=next;start=1;
  }else Funds[old.Side]+=old.WorkPaid;
  // A busy survivor keeps its own paid work. Restarted work is refunded first;
  // only uncharged orders are distributed, within each survivor's queue limit.
  for(int i=start;i<jobs.Length;i++){
   next=NextUnitFactory(old.Side,old.T.Key,null,old);if(next==null)break;next.Queue.Enqueue(jobs[i]);
   if(old.Side==0&&LastOrder==old)LastOrder=next;
  }
  old.Queue.Clear();old.Work=0;old.WorkPaid=0;old.ProductionPaused=false;old.Primary=false;
 }
 readonly bool[] satelliteVision=new bool[MaxFactions];
 public bool SatelliteOnline(int side){return satelliteVision[side];}
 void UpdateSatellites(){for(int side=0;side<MaxFactions;side++){
  bool active=Power(side)>=0&&Live(side).Any(u=>u.T.Key=="satellite");
  if(active!=satelliteVision[side]){satelliteVision[side]=active;if(side==0)FogRevision++;}
 }}
}
}
