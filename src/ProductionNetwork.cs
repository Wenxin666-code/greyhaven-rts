using System;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 public bool ProductionBuilding(Piece p){return p!=null&&p.Deployed&&(p.T.Key=="factory"||p.T.Key=="barracks"||p.T.Key=="hq");}
 public Piece PrimaryFactory(int side,string key){return Live(side).Where(u=>u.T.Key==key).OrderByDescending(u=>u.Primary).ThenBy(u=>u.Id).FirstOrDefault();}
 public bool SetPrimary(Piece p){
  if(!ProductionBuilding(p))return false;var old=PrimaryFactory(p.Side,p.T.Key);
  foreach(var other in Live(p.Side).Where(u=>u.T==p.T))other.Primary=other==p;
  if(old!=null&&old!=p){p.Queue=old.Queue;old.Queue=new System.Collections.Generic.Queue<TypeInfo>();p.Work=old.Work;p.WorkPaid=old.WorkPaid;p.ProductionPaused=old.ProductionPaused;old.Work=0;old.WorkPaid=0;old.ProductionPaused=false;}
  if(p.Side==0){LastOrder=p;Radio=p.T.Name+" #"+p.Id+" 已设为主要生产建筑。";}return true;
 }
 public float ProductionRate(Piece p){int n=Live(p.Side).Count(u=>u.T==p.T);return (Power(p.Side)<0?.45f:1)/(float)Math.Pow(.8,Math.Max(0,n-1));}
 // Paid orders survive loss of the chosen outlet if another factory remains.
 void TransferProduction(Piece old){
  if(!ProductionBuilding(old)||old.Queue.Count==0)return;
  var next=Live(old.Side).Where(u=>u!=old&&u.T==old.T).OrderBy(u=>u.Id).FirstOrDefault();
  if(next!=null){next.Primary=true;next.Queue=old.Queue;next.Work=old.Work;next.WorkPaid=old.WorkPaid;next.ProductionPaused=old.ProductionPaused;}
  else if(old.Side>=0)Funds[old.Side]+=old.WorkPaid;
  old.Queue=new System.Collections.Generic.Queue<TypeInfo>();old.Work=0;old.WorkPaid=0;old.ProductionPaused=false;old.Primary=false;
 }
 readonly bool[] satelliteVision=new bool[3];
 public bool SatelliteOnline(int side){return satelliteVision[side];}
 void UpdateSatellites(){for(int side=0;side<3;side++){
  bool active=Power(side)>=0&&Live(side).Any(u=>u.T.Key=="satellite");
  if(active!=satelliteVision[side]){satelliteVision[side]=active;if(side==0)FogRevision++;}
 }}
}
}
