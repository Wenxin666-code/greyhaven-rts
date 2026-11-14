using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public sealed class ConstructionJob {
 public TypeInfo Type;public float Work;public int Paid;public bool Paused;
 public bool Ready {get{return Work>=Type.Seconds;}}
}
public partial class Campaign {
 public const float AssemblySeconds=1.2f,BaseDeploySeconds=AssemblySeconds;
 public static float AssemblyDuration(Piece o){return o.T.Key=="hq"&&o.BaseTransition?BaseDeploySeconds:AssemblySeconds;}
 readonly ConstructionJob[,] construction=new ConstructionJob[MaxFactions,2];
 public static int BuildCategory(TypeInfo t){return t.Defense?1:0;}
 public ConstructionJob Construction(int side,int category){return construction[side,category];}
 public ConstructionJob Construction(TypeInfo t,int side=0){var job=Construction(side,BuildCategory(t));return job!=null&&job.Type==t?job:null;}
 public string ConstructionReason(TypeInfo t,int side=0){
  var own=Construction(t,side);if(own!=null)return null;
  var active=Construction(side,BuildCategory(t));if(active!=null)return "同栏已有生产项目";
  return Require(t,side);
 }
 public bool StartConstruction(TypeInfo t,int side=0){
  if(!t.Building||t.Key=="hq"||t.Key=="relay"||t.Civilian)return false;
  var own=Construction(t,side);if(own!=null){own.Paused=false;return true;}
  var reason=ConstructionReason(t,side);if(reason!=null){if(side==0)Radio=reason;return false;}
  construction[side,BuildCategory(t)]=new ConstructionJob{Type=t};
  if(side==0)Radio=t.Name+"开始生产；完成后点击就绪图标放置。";return true;
 }
 public bool HoldConstruction(TypeInfo t,int side=0){
  var job=Construction(t,side);if(job==null)return false;
  if(!job.Paused&&!job.Ready){job.Paused=true;if(side==0)Radio="生产已暂停；左键继续，再右键取消。";}
  else{Funds[side]+=job.Paid;construction[side,BuildCategory(t)]=null;if(side==0)Radio="已取消"+t.Name+"，退回已付军费 "+job.Paid+"。";}
  return true;
 }
 // Only completed work is charged; lack of money freezes progress, never creates debt.
 void PaidWork(int side,TypeInfo t,float delta,ref float work,ref int paid){
  if(Funds[side]<=0&&paid<t.Price)return;
  float next=Math.Min(t.Seconds,work+delta);
  if(t.Price>0)next=Math.Min(next,(paid+Funds[side])*t.Seconds/t.Price);
  next=Math.Max(work,next);int due=next>=t.Seconds?t.Price:(int)Math.Ceiling(next/t.Seconds*t.Price-.0001f);
  due=Math.Max(paid,Math.Min(paid+Funds[side],due));Funds[side]-=due-paid;paid=due;work=next;
 }
 void TickConstruction(float dt){
  for(int side=0;side<MaxFactions;side++)for(int category=0;category<2;category++){
   var job=construction[side,category];if(job==null||job.Ready||job.Paused)continue;
   var source=PrimaryFactory(side,"hq");if(source==null)continue;
   PaidWork(side,job.Type,dt*ProductionRate(source),ref job.Work,ref job.Paid);
   if(job.Ready&&side==0)Radio=job.Type.Name+"就绪：点击图标，或按 "+(category==0?"Q":"W")+" 放置。";
  }
 }
 public string ConstructionState(ConstructionJob job,int side=0,float timeScale=1){
  if(job.Ready)return "就绪";if(job.Paused)return "已暂停";
  var source=PrimaryFactory(side,"hq");if(source==null)return "等待指挥部";
  if(Funds[side]==0)return "等待资金";
  return (Power(side)<0?"缺电 · ":"")+(int)Math.Ceiling((job.Type.Seconds-job.Work)/Math.Max(.01f,ProductionRate(source)*timeScale))+" 秒";
 }
 bool PlaceConstruction(TypeInfo t,PointF p,int side){
  var job=Construction(t,side);if(job==null||!job.Ready){if(side==0)Radio="先在侧栏完成建筑生产。";return false;}
  if(!Live(side).Any(u=>u.T.Key=="hq")||!CanPlace(t,p,null,side)){if(side==0)Radio="此处不能放置；就绪建筑仍保留在侧栏。";return false;}
  var o=Spawn(t.Key,side,p.X,p.Y);o.Assembly=true;o.Ready=.001f;construction[side,BuildCategory(t)]=null;
  if(side==0)Radio=t.Name+"已放置，正在展开。";return true;
 }
 public bool FoundationLevel(TypeInfo t,PointF p){return TerrainBuildable(Footprint(t,p));}
 public bool CanDeploy(Piece o){return o!=null&&o.Deployed&&o.T.Key=="mcv"&&CanPlace(Info("hq"),o.P,o,o.Side);}
 public bool CanRelocate(Piece o,PointF destination){
  if(o==null||!o.Deployed||o.T.Key!="hq")return false;
  var probe=new Piece{T=Info("mcv"),Hp=1,Side=o.Side,P=o.P};return PlanRoute(probe,destination,null,o);
 }
 public bool Relocate(Piece o,PointF destination){
  if(!CanRelocate(o,destination)){if(o!=null&&o.Side==0)Radio="基地车无法到达此处，保留当前基地。";return false;}
  TransferProduction(o);Halt(o);o.BaseTransition=true;o.DeployOrigin=o.P;o.DeployFacing=0;o.Packing=BaseDeploySeconds;
  o.Relocation=destination==o.P?(PointF?)null:destination;o.Repairing=false;Changed();
  if(o.Side==0)Radio=o.Relocation.HasValue?"基地收起后前往指定位置；建筑生产等待可用指挥部。":"基地正在收起，完成后可指挥基地车移动。";
  return true;
 }
 void TickPacking(Piece o,float dt){
  o.Packing=Math.Max(0,o.Packing-dt);if(o.Packing>0)return;
  if(o.Selling){o.Hp=0;o.Dead=25;Changed();return;}
  float health=o.Hp/o.T.Hp;o.T=Info("mcv");o.Hp=Math.Max(1,health*o.T.Hp);o.Ready=1;o.Assembly=false;o.BaseTransition=false;Changed();
  var target=o.Relocation;o.Relocation=null;if(target.HasValue&&!Move(o,target.Value,false)&&o.Side==0)Radio="基地已收起，前路临时受阻，请重新指定目的地。";
 }
 public bool CanSell(Piece o){return o!=null&&o.Deployed&&o.Side==0&&o.T.Building&&!o.T.Civilian&&!IsRelayStation(o);}
 public bool Sell(Piece o){
  if(!CanSell(o))return false;if(o.T.Garrison){EvacuateHouse(o);if(HouseCount(o)>0){Radio="出口受阻，请先撤出驻军再出售。";return false;}}TransferProduction(o);int value=o.T.Key=="hq"?Info("mcv").Price:o.T.Price;
  Funds[o.Side]+=(int)(value*.5f);o.Repairing=false;o.Selling=true;o.BaseTransition=false;o.Packing=AssemblySeconds;Halt(o);Changed();Radio="正在出售"+o.T.Name+"。";return true;
 }
 public bool ToggleRepair(Piece o){
  if(o==null||!o.Deployed||o.Side!=0||!o.T.Building||o.Hp>=o.T.Hp&&!o.Repairing)return false;
  o.Repairing=!o.Repairing;Radio=o.Repairing?"建筑维修开启，逐步消耗军费。":"已停止建筑维修。";return true;
 }
 void TickBuildingRepair(Piece o,float dt){
  if(!o.Repairing||!o.Deployed)return;if(o.Hp>=o.T.Hp){o.Repairing=false;return;}
  o.RepairClock-=dt;if(o.RepairClock>0)return;
  int cost=o.T.Key=="hq"?Info("mcv").Price:o.T.Price;float heal=Math.Min(8,o.T.Hp-o.Hp),next=o.RepairCost+cost*.15f*heal/o.T.Hp;
  int charge=(int)Math.Ceiling(next-.0001f)-o.RepairPaid;
  if(Funds[o.Side]<charge)return;Funds[o.Side]-=charge;o.RepairPaid+=charge;o.RepairCost=next;o.Hp+=heal;o.RepairAt=VisualTime;o.RepairClock=.96f;
 }
}
}
