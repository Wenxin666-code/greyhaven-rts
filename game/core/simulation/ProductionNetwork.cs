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
   LastOrder=SharesQueue(p.T.Key)?SharedQueue(p.Side,p.T.Key):p;
   Radio=p.T.Name+" #"+p.Id+" 已设为优先出队点。";
  }
  return true;
 }
 public float ProductionRate(Piece p){
  float power=PowerProductionRate(p.Side);
  if(p.T.Key!="hq"&&!SharesQueue(p.T.Key))return power;
  int factories=Live(p.Side).Count(u=>u.T==p.T&&(p.T.Key=="hq"||!u.Selling));
  return power/(float)Math.Pow(.8,Math.Max(0,factories-1));
 }
 // The serial queue keeps its paid head when the player changes the primary exit.
 static bool SharesQueue(string key){return key=="factory"||key=="barracks";}
 public Piece VehicleQueue(int side){return SharedQueue(side,"factory");}
 public Piece InfantryQueue(int side){return SharedQueue(side,"barracks");}
 Piece SharedQueue(int side,string key,Piece skip=null){
  return Live(side).Where(p=>p!=skip&&p.T.Key==key&&!p.Selling)
   .OrderByDescending(p=>p.Queue.Count>0).ThenByDescending(p=>p.Primary).ThenBy(p=>p.Id).FirstOrDefault();
 }
 bool UnitProductionExit(Piece source,TypeInfo type,out Piece outlet,out PointF position){
  outlet=source;
  if(!SharesQueue(source.T.Key))return Exit(source,type,out position);
  outlet=PrimaryFactory(source.Side,source.T.Key)??source;
  if(Exit(outlet,type,out position))return true;
  foreach(var alternate in Live(source.Side).Where(p=>p.T==source.T&&!p.Selling)){
   if(alternate!=outlet&&Exit(alternate,type,out position)){outlet=alternate;return true;}
  }
  return false;
 }
 Piece NextUnitFactory(int side,string key,Piece preferred=null,Piece skip=null){
  var line=SharesQueue(key)?SharedQueue(side,key,skip):Live(side).Where(p=>p!=skip&&p.T.Key==key&&!p.Selling)
   .OrderByDescending(p=>p==preferred).ThenBy(p=>p.Id).FirstOrDefault();
  return line!=null&&line.Queue.Count<ProductionQueueLimit?line:null;
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
