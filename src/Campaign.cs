using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public class TypeInfo {
 public string Key,Name,Producer; public int Price,Hp,Hit,Range,Size; public float Speed,Seconds,Reload;
 public string RuleId="custom";public ArmorKind Armor=ArmorKind.Concrete;public int SightCells=5,Energy,SpeedRating;public WeaponProfile Weapon,EliteWeapon;
 public bool Trainable,SelfHealing;public VeteranAbility VeteranAbilities,EliteAbilities;
 public TypeInfo(string key,string name,string from,int size){Key=key;Name=name;Producer=from;Size=size;ReferenceBalance.Configure(this);}
 public float LockRange {get{return Hit==0?0:Building?Range:Math.Min(Range*2,16*Campaign.Tile);}}
 public float Clearance {get{return Foot?5:Key=="miner"?5.25f:8.8f;}}
 // Compact cell occupancy is independent of artwork and turret overhang;
 // narrow-road clearance remains a separate navigation allowance.
 public float TrafficRadius {get{return Foot?Clearance*.75f:Building?0:Campaign.Tile*.46f;}}
 public float VisualRadius {get{return Foot||Building?0:UnitGroundMetric.Radius(Key);}}
 public bool Garrison {get{return Civilian||Key=="bunker";}}
 public bool DeployableFoot {get{return Key=="squad"||Key=="at";}}
 public bool Defense {get{return Key=="post"||Key=="bunker";}}
 public bool Civilian {get{return Key=="residence"||Key=="shop"||Key=="municipal";}}
 public int FoundationWidth {get{return Key=="post"?1:Key=="hq"||Key=="factory"||Key=="shop"||Key=="municipal"||Key=="relay"?3:2;}}
 public int FoundationHeight {get{return Key=="post"?1:Key=="municipal"||Key=="relay"?3:2;}}
 public bool Building {get{return Speed==0;}} public bool Foot {get{return Key=="squad"||Key=="at"||Key=="engineer"||Key=="sniper";}}
 public TypeInfo(string key,string name,string from,int price,int hp,int hit,int range,int size,float speed,float time,float reload){
  Key=key;Name=name;Producer=from;Price=price;Hp=hp;Hit=hit;Range=range;Size=size;Speed=speed;Seconds=key=="hq"?time:Math.Max(.5f,price*.042f);Reload=reload;
 }
}
public enum ContactOrder { None, Directed, Automatic, ReturnFire }
public class Piece {
 internal PointF VisionPosition;internal int VisionSide=-2;internal float VisionRadius;
 public bool Entrenched;public float Stance,WeaponAim;
 public bool BaseTransition,PendingDeploy;public float DeployFacing;public PointF DeployOrigin;
 public bool Assembly,ProductionPaused,Repairing,Selling;public int WorkPaid,RepairPaid;public float Packing,RepairClock,RepairCost;public PointF? Relocation;
 public double Experience;public float CheerAt=-100;public int Rank {get{return Experience>=2-1e-10?2:Experience>=1-1e-10?1:0;}}
 public Piece BurstTarget;public WeaponProfile BurstWeapon;public int BurstRemaining;public float BurstAt;public PointF BurstPoint;
 public bool SupportAutomatic;public float SupportScanAt;
 public Piece SupportTarget;public bool Primary;public OrePatch OrderedOre;
 public Piece Entering;public float CaptureWork;public bool Capturing;
 public float FiredAt=-100,HitAt=-100,CompletedAt=-100,RepairAt=-100,MovedAt=-100,Travel;
 public PointF LastFirePoint,RepairPoint;
 public float TurretTurn;
 internal Piece MovingFireTarget;
 public float TrafficBlocked,FollowingWait,DockBlockedFor,StalledFor,RouteRetryAt,ProgressAt,NoProgressFor;public PointF ProgressAnchor;
 public PointF? GuardHome;public Piece Escort;public PointF EscortOffset;public bool GuardTravelling,GuardReturning,ForceMoving;
 public readonly List<PointF> Waypoints=new List<PointF>();public int WaypointIndex;public bool Patrol;public int PatrolStart;
 public bool DockReserved;public float DockTicket,DockRetryAfter,DockAccessAt;public Piece DepartingDock;
 public int LastReceipt;public float ReceiptAt;public PointF WorkPoint,TravelSample;public float TrafficSampleTime;public bool Mining,StarterDelivered,Returning,Surveying;public float Cargo,Unload,Dispatch,MillPhase;public Piece Dock;public OrePatch Patch;
 public ContactOrder Contact;public bool ResumeMarch;public PointF LastContact;public float ScanIn;
 public int RouteVersion,Id,Side,Dir,Group; public TypeInfo T; public PointF P; public float Hp,Ready=1,Cool,Dead,Replan,Turn,Door,Rollout;
 public bool Alive {get{return Hp>0;}} public bool Deployed {get{return Alive&&Ready>=1&&Packing<=0&&Carrier==null;}}
 public Queue<PointF> Path=new Queue<PointF>(); public Queue<TypeInfo> Queue=new Queue<TypeInfo>(); public float Work,TrafficWait;
 public PointF? FirePoint;public PointF? Rally;public March Formation;public Piece Target; Piece carrier;
 public Piece Passenger {get;private set;}
 public Piece Carrier {get{return carrier;}set{
  if(carrier==value)return;var previous=carrier;carrier=value;
  if(previous!=null&&previous.Passenger==this){previous.Passenger=null;previous.GunnerChanged();}
  if(value!=null&&value.T.Key=="ifv"){value.Passenger=this;value.GunnerChanged();}
 }}
 void GunnerChanged(){BurstRemaining=0;BurstTarget=null;BurstWeapon=null;SupportTarget=null;SupportAutomatic=false;
  if(Passenger!=null&&Passenger.T.Key=="engineer"){Target=null;FirePoint=null;Contact=ContactOrder.None;}
 }
 internal int OreSearchToken;internal bool OreSearching;
 internal Piece RallySource;internal int RallySlot;internal float RallyRetryAt; public PointF Goal; public bool Advancing;
}
public class Site {
 public string Name;public PointF P;

 public readonly RectangleF Body;
 public Site(string name,int x,int y,int bodyX,int bodyY){
  Name=name;P=new PointF(x,y);Body=new RectangleF(bodyX,bodyY,2*Campaign.Tile,2*Campaign.Tile);
 }
}
public class Shot {public float Stance,Aim;public PointF A,B;public float Life,Born,Heading,TurretHeading,ImpactLift;public int Side,Id,SourceId,TargetId;public bool Shell,HitFoot,Garrison;public string Weapon;public bool Rocket;}
public partial class Campaign {
 public const int MaxFactions=6;
 public int FactionCount {get{return River==null?2:River.Starts.Length;}}
 public bool IslandMission {get{return River!=null&&River.IsIsland;}}
 public static readonly TypeInfo[] Types={
  new TypeInfo("hq","指挥部","",132),
  new TypeInfo("power","电站","hq",108),
  new TypeInfo("barracks","兵营","hq",124),
  new TypeInfo("factory","车辆工厂","hq",155),
  new TypeInfo("radar","雷达站","hq",113),
  new TypeInfo("satellite","间谍卫星","hq",118),
  new TypeInfo("post","防御阵地","hq",102),
  new TypeInfo("bunker","驻兵碉堡","hq",108),
  new TypeInfo("squad","步枪兵","barracks",60),
  new TypeInfo("at","反装甲兵","barracks",62),
  new TypeInfo("engineer","工兵","barracks",60),
  new TypeInfo("sniper","狙击手","barracks",60),
  new TypeInfo("ifv","步兵战车","factory",50),
  new TypeInfo("tank","主战坦克","factory",57),
  new TypeInfo("arty","自行火炮","factory",53),
  new TypeInfo("mcv","工程车","factory",55),
  new TypeInfo("refinery","矿石精炼厂","hq",136),
  new TypeInfo("miner","采矿车","factory",44)};
 public static readonly TypeInfo[] CivilTypes={new TypeInfo("residence","民居","",0,550,0,0,108,0,1,0),new TypeInfo("shop","商住楼","",0,720,0,0,132,0,1,0),new TypeInfo("municipal","镇公所","",0,1050,0,0,150,0,1,0)};
 public static TypeInfo Info(string k){return Array.Find(Types,t=>t.Key==k)??Array.Find(CivilTypes,t=>t.Key==k)??(k=="relay"?RelayType:null);}
 public readonly List<Piece> Units=new List<Piece>();public readonly List<Shot> Shots=new List<Shot>();
 public readonly Site[] Sites;
 public Site Harbor {get{return Sites.Length==0?null:Sites[0];}}
 public int[] Funds=Enumerable.Repeat(ReferenceBalance.StartingCredits,MaxFactions).ToArray();public float Time; public bool Landing,Won,Lost;public string Radio="展开工程车，在西南建立登陆基地。";
 int seq;float nextEnemyDecision=1,nextWave=92;
 public readonly bool Tutorial,Assisted;
 public int Lesson,InfantryDelivered,VehiclesDelivered;public Piece LastOrder,LastReady;public float ReadyAt;
 public Campaign(bool tutorial=false,bool assisted=false,bool riverbend=false,bool island=false,bool relay=false,bool chapterOne=false){
  River=chapterOne?RiverbendMap.ChapterOne():relay?RiverbendMap.Relay():riverbend||island?new RiverbendMap(island):null;
  var port=RiverbendMap.HarborOffice;
  Sites=chapterOne?new[]{new Site("港务中心",port.X*Tile,(port.Y+5)*Tile,(port.X-1)*Tile,(port.Y-1)*Tile)}:
   River!=null?new Site[0]:new[]{new Site("港务中心",1330,292,1265,227)};
  discovered=new bool[MaxFactions,MapFogCols,MapFogRows];
  Tutorial=tutorial||chapterOne;Assisted=assisted||Tutorial;if(Assisted){Funds[0]=ReferenceBalance.TutorialCredits;nextWave=175;}
  if(chapterOne){SetupFirstChapter();Reveal();return;}
  if(River!=null){SetupRiverbend();RevealStartingOre();Reveal();return;}
  Spawn("mcv",0,152,514);Spawn("squad",0,214,418);Spawn("squad",0,258,410);

  if(tutorial){var guard=Spawn("squad",1,1390,287);Guard(guard);RevealStartingOre();Reveal();return;}
  Spawn("mcv",1,EnemyHome.X,EnemyHome.Y);RevealStartingOre();Reveal();
 }
 public static float Dist(PointF a,PointF b){return (float)Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));}
 public Piece Spawn(string k,int side,float x,float y){
  var t=Info(k);int dir=side==0?3:13;
  var o=new Piece{Id=++seq,T=t,Side=side,P=new PointF(x,y),Hp=t.Hp,Mining=k=="miner",Turn=dir,TurretTurn=dir,Dir=dir};if(t.Building){o.P=Middle(Footprint(t,o.P));o.Dir=0;o.Turn=0;Changed();}Units.Add(o);return o;
 }
 public IEnumerable<Piece> Live(int side){return Units.Where(o=>o.Side==side&&o.Deployed);}
 public int Power(int side){return PowerProduced(side)-PowerRequired(side);}
 public int PowerProduced(int side){return Live(side).Where(o=>o.T.Energy>0).Sum(o=>(int)(o.T.Energy*o.Hp/o.T.Hp));}
 public int PowerRequired(int side){return -Live(side).Where(o=>o.T.Energy<0).Sum(o=>o.T.Energy);}
 public float PowerProductionRate(int side){int need=PowerRequired(side),have=PowerProduced(side);return have>=need?1:Math.Max(.5f,Math.Min(.8f,have/(float)Math.Max(1,need)));}
 public bool Visible(Piece o){return o.Side==0||Explored(0,o.P);}
 internal static float Sight(Piece p){return p.T.SightCells*Tile;}
 public const int ProductionQueueLimit=30;
 public string Require(TypeInfo t,int side){
  if(!Live(side).Any(p=>p.T.Key==t.Producer))return "需要"+(t.Producer==""?"工程车":Info(t.Producer).Name);
  if(t.Key=="sniper"&&!Live(side).Any(p=>p.T.Key=="radar"))return "需要兵营和雷达站";
  if(t.Key=="satellite"&&(!Live(side).Any(p=>p.T.Key=="radar")||!Live(side).Any(p=>p.T.Key=="factory")))return "需要雷达和车辆工厂";
  if((t.Key=="factory"||t.Key=="refinery")&&!Live(side).Any(p=>p.T.Key=="power"))return "先建电站";
  if(!t.Building&&NextUnitFactory(side,t.Producer)==null)
   return t.Producer=="factory"?"车辆队列已满（30 单）":"所有兵营队列已满（每营 30 单）";
  return null;
 }
 static float Radius(TypeInfo t){return t.Size*.27f;}
 public bool Deploy(Piece o){
  return o!=null&&o.Side==0&&DeployVehicle(o);
 }
 bool DeployVehicle(Piece o){
  if(o==null||!o.Deployed||o.T.Key!="mcv")return false;
  if(o.PendingDeploy)return true;
  if(!CanDeploy(o)){if(o.Side==0)Radio="此处空间不足。将工程车移到一块空地后再展开。";return false;}
  float delta=((24-o.Turn)%16)-8;
  if(Math.Abs(delta)>.04f){var origin=o.P;float facing=o.Turn;Halt(o);o.DeployOrigin=origin;o.DeployFacing=facing;o.PendingDeploy=true;if(o.Side==0){Landing=true;Radio="工程车正在停车转正，随后展开基地。";}return true;}
  return BeginBaseExpansion(o);
 }
 bool BeginBaseExpansion(Piece o){
  if(!o.Deployed||o.T.Key!="mcv")return false;
  if(!CanDeploy(o)){if(o.Side==0)Radio="此处空间不足。将工程车移到一块空地后再展开。";return false;}
  float health=o.Hp/o.T.Hp;if(!o.PendingDeploy){o.DeployOrigin=o.P;o.DeployFacing=o.Turn;}o.BaseTransition=true;Halt(o);Changed();o.T=Info("hq");o.P=Middle(Footprint(o.T,o.P));o.Turn=0;o.Dir=0;o.Hp=Math.Max(1,o.T.Hp*health);o.Assembly=true;o.Ready=.001f;o.Path.Clear();o.Target=null;if(o.Side==0){Landing=true;Radio="基地正在展开。先建电站，再建矿石精炼厂。完工后，首辆矿车会从厂门出发找矿。";}return true;
 }
 public bool Build(TypeInfo t,PointF p){return PlaceConstruction(t,p,0);}
 bool BuildFor(TypeInfo t,PointF p,int side){
  if(!StartConstruction(t,side))return false;return PlaceConstruction(t,p,side);
 }
 public bool Train(TypeInfo t,int side,Piece preferred){
  string reason=Require(t,side);var src=NextUnitFactory(side,t.Producer,preferred);
  if(t.Building||reason!=null||src==null){if(side==0)Radio=reason??"需要已建成的生产设施。";return false;}
  src.Queue.Enqueue(t);if(side==0){LastOrder=src;Radio=(src.T.Key=="factory"?"车辆队列":src.T.Name+" #"+src.Id)+" 已接单："+t.Name+"，排第 "+src.Queue.Count+" 位。";}return true;
 }
 public bool Move(Piece o,PointF p,bool attack){
  return Move(o,p,attack,null);
 }
 bool Move(Piece o,PointF p,bool attack,RectangleF[] traffic,float padding=0){
  if(!o.Deployed||o.T.Building)return false;if(!ParkingAllowed(o.Side,p)||!PlanRoute(o,p,traffic,padding:padding))return false;ReleaseInfantry(o);ClearMission(o);o.Mining=false;o.Dock=null;o.DockReserved=false;o.DockTicket=0;o.DockRetryAfter=0;o.DockBlockedFor=0;o.Unload=0;o.FirePoint=null;o.Formation=null;o.Target=null;o.Contact=ContactOrder.None;o.ResumeMarch=false;o.Advancing=attack;o.Goal=p;return true;
 }
 public void Attack(Piece o,Piece target,bool force=false){if(!o.Deployed||!Armed(o)||target==o||IsRelayStation(target)&&!force)return;ClearMission(o);o.FirePoint=null;o.Formation=null;o.Target=target;o.Contact=ContactOrder.Directed;o.ResumeMarch=false;o.Advancing=true;o.Path.Clear();o.Replan=0;o.Goal=target.P;}
 void ApproachPoint(Piece o,PointF target){
  FindFiringPosition(o,target);
 }
 public void Load(Piece car){
  if(car==null||car.T.Key!="ifv"||!car.Alive)return;var cargo=Units.FirstOrDefault(o=>o.Alive&&o.Carrier==car);
  if(cargo!=null){Radio=TryUnloadPassenger(car,cargo)?"步兵已下车，战车恢复空车导弹。":"周围缺少下车空间。";return;}
  Radio="空车：选择步兵，再点击战车登车。";
 }
 bool ExitClear(Piece source,TypeInfo exiting,PointF point){
  return !Blocked(point,exiting.Clearance,null)&&!Units.Any(u=>u!=source&&u.Deployed&&!u.T.Building&&TrafficContact(exiting,point,u.T,u.P));
 }
 bool Exit(Piece o,TypeInfo exiting,out PointF p){
  if(o.T.Building){
   var apron=ExitApron(o.T,Footprint(o.T,o.P));p=Middle(apron);
   if(ExitClear(o,exiting,p))return true;
   // Idle blockers use the normal rally. Riverbend's AI also steps
   // combat units off its own production doors when fighting at the base.
   var door=p;
   foreach(var unit in Live(o.Side).Where(u=>!u.T.Building&&!u.Mining&&u.Entering==null&&
     TrafficContact(exiting,door,u.T,u.P)).ToArray()){
    if(unit.Path.Count==0&&unit.Target==null)RallyNewUnit(o,unit);
    else if(RiverbendOperation!=null&&unit.Side>0&&!RiverbendImpoundGuard(unit)&&
     (unit.Path.Count==0||unit.StalledFor>=1))YieldRiverbendExit(unit,door);
   }
   return false;
  }
  for(int j=0;j<24;j++){double a=(o.Dir+j%8*2)*Math.PI/8;float d=(o.T.Building?Radius(o.T)+20:27)+j/8*19;
   p=new PointF(o.P.X+(float)Math.Sin(a)*d,o.P.Y+(float)Math.Cos(a)*d*.85f);
   var candidate=p;if(ExitClear(o,exiting,candidate))return true;
  }p=o.P;return false;
 }
 void Kill(Piece o){if(HarborOperation!=null)HarborOperation.ClearTruckBarrier(o);if(o.T.Building){TransferProduction(o);Changed();}o.Hp=0;o.Path.Clear();o.Queue.Clear();if(o.T.Garrison)EvacuateHouse(o);else foreach(var c in Units.Where(c=>c.Carrier==o)){c.Carrier=null;c.P=o.P;c.Hp=0;}
  if(o.Side==0)Radio=o.T.Name+"已损失。";}
 static float Bearing(PointF from,PointF to){return (float)(Math.Atan2(to.X-from.X,to.Y-from.Y)*8/Math.PI+16)%16;}
 static float RotateToward(ref float angle,float heading,float dt){
  float diff=(heading-angle+24)%16-8,seconds=Math.Abs(diff)/15;
  angle=seconds<=dt?heading:(angle+Math.Sign(diff)*dt*15+16)%16;
  return Math.Max(0,dt-seconds);
 }
 static float TurnToward(Piece o,float heading,float dt){
  float remaining=RotateToward(ref o.Turn,heading,dt);
  o.Dir=(int)Math.Round(o.Turn)%16;return remaining;
 }
 static void Face(Piece o,PointF p,float dt){TurnToward(o,Bearing(o.P,p),dt);}
 void DriveTracks(Piece o,PointF end,float speed,float dt){
  float dx=end.X-o.P.X,dy=end.Y-o.P.Y,span=Math.Max(Math.Abs(dx),Math.Abs(dy));
  if(span<.001f){o.P=end;return;}
  float heading=((int)Math.Round(Bearing(o.P,end)/2)*2)%16;
  float remaining=TurnToward(o,heading,dt);
  if(remaining<=0)return;
  bool alongX=Math.Abs(dx)>span*.5f,alongY=Math.Abs(dy)>span*.5f;
  float travel=speed*remaining*(alongX&&alongY?.70710678f:1);
  float left=Math.Max(0,span-travel);
  var next=new PointF(end.X-(alongX?Math.Sign(dx)*left:0),end.Y-(alongY?Math.Sign(dy)*left:0));
  o.P=TrackStep(o,end,next,dt);
 }
 void UnitTick(Piece o,float dt,bool powered){
  if(!o.Alive)return;if(o.Carrier!=null){o.P=o.Carrier.P;if(o.Carrier.T.Garrison)TickGarrison(o,dt);return;}
  if(o.Side<0){
   if(HarborOperation!=null&&o.Path.Count>0){
    o.TrafficWait-=dt;TickUnitMovement(o,dt,HarborOperation.RoadSpeed(o));
   }
   return;
  }
  if(o.Packing>0){TickPacking(o,dt);return;}
  if(TickEntry(o,dt))return;
  if(o.T.Key=="mcv"&&o.PendingDeploy){float delta=((24-o.Turn)%16)-8,step=Math.Max(-8*dt,Math.Min(8*dt,delta));o.Turn=(o.Turn+step+16)%16;o.Dir=(int)Math.Round(o.Turn)%16;if(Math.Abs(delta)<=8*dt+.001f){o.Turn=0;o.Dir=0;BeginBaseExpansion(o);}return;}
  if(o.Ready<1){o.Ready=Math.Min(1,o.Ready+dt/(o.Assembly?AssemblyDuration(o):o.T.Seconds));if(o.Ready>=1){o.CompletedAt=VisualTime;o.BaseTransition=false;}return;}
  TickSelfHealing(o);
  TickInfantryStance(o,dt);TickWeaponAim(o,dt);
  o.Cool-=dt;TickBurst(o,dt);o.Replan-=dt;o.TrafficWait-=dt;o.Door=Math.Max(0,o.Door-dt);o.Rollout=Math.Max(0,o.Rollout-dt);
  if(o.T.Key=="refinery")RunRefinery(o,dt);
  if(o.Mining)RunMiner(o,dt);
  TickBuildingRepair(o,dt);
  if(o.Queue.Count>0&&!o.ProductionPaused){
   var t=o.Queue.Peek();
   // Snap sub-millisecond rounding residue at the end of a production step.
   float delta=dt*ProductionRate(o);
   if(o.T.Key=="factory"&&t.Seconds-o.Work-delta<.001f)delta=t.Seconds-o.Work;
   PaidWork(o.Side,t,delta,ref o.Work,ref o.WorkPaid);
   PointF position;Piece outlet;
   if(o.Work>=t.Seconds&&UnitProductionExit(o,t,out outlet,out position)){
    var u=Spawn(t.Key,o.Side,position.X,position.Y);
    u.Dir=outlet.Dir;u.Turn=outlet.Dir;u.TurretTurn=u.Turn;u.Rollout=.6f;outlet.Door=1;
    o.Work=0;o.WorkPaid=0;o.Queue.Dequeue();
    if(u.T.Key!="miner")RallyNewUnit(outlet,u);
    if(o.Side==0){
     LastReady=u;ReadyAt=Time;if(Time-lastStrike>3)SetEvent(u.P,u.T.Name+"出列");
     if(t.Key=="squad")InfantryDelivered++;
     if(t.Key=="ifv")VehiclesDelivered++;
     Radio=t.Name+"已从"+outlet.T.Name+" #"+outlet.Id+" 出列。";
    }
   }
  }
  MissionTick(o);
  bool stop=TickRepairVehicle(o,dt);
  if(Armed(o)&&o.FirePoint.HasValue)stop=GroundFireTick(o,dt);
  else if(Armed(o))stop=FightContact(o,dt);
  if(!stop)TickUnitMovement(o,dt);
 }
 void TickUnitMovement(Piece o,float dt,float speedScale=1){
  if(o.Stance<=0&&!o.Entrenched&&o.Path.Count>0){if(o.RouteVersion!=topology&&!Route(o,o.Path.Last())){o.Path.Clear();return;}var p=o.Path.Peek();
   float speed=(o.Formation==null?ReferenceBalance.Speed(o):o.Formation.Speed(o))*speedScale;
   if(!o.T.Foot)DriveTracks(o,p,FollowingSpeed(o,p,speed,dt),dt);
   else{
    float d=Dist(o.P,p),travel=speed*dt;
    var next=d<=travel?p:new PointF(o.P.X+(p.X-o.P.X)*travel/d,o.P.Y+(p.Y-o.P.Y)*travel/d);
    var step=Avoid(o,next);o.TrafficBlocked=Dist(o.P,step)<.001f?o.TrafficBlocked+dt:0;
    if(Dist(o.P,step)>.001f){o.Turn=Bearing(o.P,step);o.Dir=(int)Math.Round(o.Turn)%16;o.P=step;}
   }
   if(o.Path.Count>0&&(Dist(o.P,o.Path.Peek())<(o.T.Foot?.1f:.001f)||(!o.Mining&&o.Path.Count==1&&Dist(o.P,o.Path.Peek())<9&&o.TrafficBlocked>1&&Units.Any(u=>u!=o&&u.Deployed&&u.Path.Count==0&&!u.T.Building&&TrafficContact(o.T,o.Path.Peek(),u.T,u.P))))){
    o.Path.Dequeue();
    if(o.Path.Count==0&&o.Target==null&&o.Waypoints.Count==0)o.Advancing=false;
   }
  }
 }
 bool VisibleTo(int side,Piece e){return Explored(side,e.P);}
 public int Remaining(int side){return Units.Count(o=>o.Alive&&o.Side==side);}
 void EnemyOrders(int side=1){
  if(River!=null){SmartTactics(side,true);return;}
  var known=Units.Where(o=>o.Side>=0&&o.Side!=side&&o.Alive&&o.Carrier==null&&Explored(side,o.P)).ToArray();
  var beach=River==null?new PointF(152,514):Enumerable.Range(0,3).Where(s=>s!=side&&Remaining(s)>0).Select(Home).OrderBy(p=>Dist(p,Home(side))).FirstOrDefault();
  foreach(var unit in Live(side).Where(o=>!o.T.Building&&o.T.Hit>0&&o.Entering==null)){
   if(unit.Target!=null&&unit.Target.Alive)continue;
   var target=known.OrderBy(o=>Dist(unit.P,o.P)).FirstOrDefault();
   if(target!=null){Attack(unit,target);continue;}
   if(!Explored(side,beach)){Move(unit,beach,true);continue;}
   if(ExploreGround(unit))unit.Advancing=true;
  }
 }
 public void Tick(float dt){
  if(Won||Lost)return;TickVisuals(dt);Time+=dt;AdvanceSelfHealClock(dt);visionTimer-=dt;if(visionTimer<=0){visionTimer=.20f;Reveal();}
  TickConstruction(dt);BalanceProduction();
  bool[] power=Enumerable.Range(0,MaxFactions).Select(s=>Power(s)>=0).ToArray();
  foreach(var o in Units.ToArray()){var before=o.P;UnitTick(o,dt,o.Side<0||power[o.Side]);if(o.Alive&&o.Carrier==null){float travel=Dist(before,o.P);o.Travel+=travel;if(travel>.001f)o.MovedAt=VisualTime;TrackRouteProgress(o);o.StalledFor=o.Path.Count>0&&travel<o.T.Speed*dt*.15f?o.StalledFor+dt:Math.Max(0,o.StalledFor-dt*2);}}
  TickAiMarchRequests();CheckEnemySurrender(dt);
  if(RelayMission)TickRelayObjective(dt);else{Lost=Remaining(0)==0;Won=!Lost&&!Units.Any(o=>o.Alive&&o.Side>0);}
  if(Won||Lost){OutcomeAt=VisualTime;return;}
  if(!Tutorial&&River!=null)TickStrategicAis();
  else if(!Tutorial&&Time>=nextEnemyDecision){nextEnemyDecision=Time+2;EnemyEconomy(1);}
  if(!Tutorial&&River==null&&Time>=nextWave){EnemyOrders(1);nextWave=Time+54*(Assisted?1.5f:1);}
  if(Tutorial)AdvanceLesson();
  if(RiverbendOperation!=null)RiverbendOperation.UpdateBattleGuidance();
  TickStoryIntelligence();
 }
}
}
