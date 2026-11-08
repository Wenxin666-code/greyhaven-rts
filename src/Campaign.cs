using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public class TypeInfo {
 public string Key,Name,Producer; public int Price,Hp,Hit,Range,Size; public float Speed,Seconds,Reload;
 public float LockRange {get{return Hit==0?0:Building?Range:Math.Min(Range*2,16*Campaign.Tile);}}
 public float Clearance {get{return Foot?5:Key=="miner"?5.25f:8.8f;}}
 // Compact cell occupancy is independent of artwork and turret overhang;
 // narrow-road clearance remains a separate navigation allowance.
 public float TrafficRadius {get{return Foot?Clearance*.75f:Building?0:Campaign.Tile*.46f;}}
 public float VisualRadius {get{return Foot||Building?0:UnitGroundMetric.Radius(Key);}}
 public bool Civilian {get{return Key=="residence"||Key=="shop"||Key=="municipal";}}
 public int FoundationWidth {get{return Key=="post"?1:Key=="hq"||Key=="factory"||Key=="shop"||Key=="municipal"?3:2;}}
 public int FoundationHeight {get{return Key=="post"?1:Key=="municipal"?3:2;}}
 public bool Building {get{return Speed==0;}} public bool Foot {get{return Key=="squad"||Key=="at"||Key=="engineer";}}
 public TypeInfo(string key,string name,string from,int price,int hp,int hit,int range,int size,float speed,float time,float reload){
  Key=key;Name=name;Producer=from;Price=price;Hp=hp;Hit=hit;Range=range;Size=size;Speed=speed;Seconds=key=="hq"?time:Math.Max(.5f,price*.042f);Reload=reload;
 }
}
public enum ContactOrder { None, Directed, Automatic, ReturnFire }
public class Piece {
 public bool BaseTransition,PendingDeploy;public float DeployFacing;public PointF DeployOrigin;
 public bool Assembly,ProductionPaused,Repairing,Selling;public int WorkPaid,RepairPaid;public float Packing,RepairClock,RepairCost;public PointF? Relocation;
 public float Experience,CheerAt=-100;public int Rank {get{return Experience>=1.99999f?2:Experience>=.99999f?1:0;}}
 public Piece SupportTarget;public bool Primary;public OrePatch OrderedOre;
 public Piece Entering;public float CaptureWork;public bool Capturing;
 public float FiredAt=-100,HitAt=-100,CompletedAt=-100,RepairAt=-100,MovedAt=-100,Travel;
 public PointF LastFirePoint,RepairPoint;
 public float TrafficBlocked,FollowingWait,DockBlockedFor,StalledFor,RouteRetryAt,ProgressAt,NoProgressFor;public PointF ProgressAnchor;
 public PointF? GuardHome;public Piece Escort;public PointF EscortOffset;public bool GuardTravelling,GuardReturning,ForceMoving;
 public readonly List<PointF> Waypoints=new List<PointF>();public int WaypointIndex;public bool Patrol;public int PatrolStart;
 public bool DockReserved;public float DockTicket,DockRetryAfter;
 public int LastReceipt;public float ReceiptAt;public PointF WorkPoint,TravelSample;public float TrafficSampleTime;public bool Mining,StarterDelivered,Returning,Surveying;public float Cargo,Unload,Dispatch,MillPhase;public Piece Dock;public OrePatch Patch;
 public ContactOrder Contact;public bool ResumeMarch;public PointF LastContact;public float ScanIn;
 public int RouteVersion,Id,Side,Dir,Group; public TypeInfo T; public PointF P; public float Hp,Ready=1,Cool,Dead,Replan,Turn,Door,Rollout;
 public bool Alive {get{return Hp>0;}} public bool Deployed {get{return Alive&&Ready>=1&&Packing<=0&&Carrier==null;}}
 public Queue<PointF> Path=new Queue<PointF>(); public Queue<TypeInfo> Queue=new Queue<TypeInfo>(); public float Work,TrafficWait;
 public PointF? FirePoint;public PointF? Rally;public March Formation;public Piece Target,Carrier; public PointF Goal; public bool Advancing;
}
public class Site {
 public string Name;public PointF P;

 public readonly RectangleF Body;
 public Site(string name,int x,int y,int bodyX,int bodyY){
  Name=name;P=new PointF(x,y);Body=new RectangleF(bodyX,bodyY,2*Campaign.Tile,2*Campaign.Tile);
 }
}
public class Shot {public PointF A,B;public float Life,Born,Heading,ImpactLift;public int Side,Id,SourceId,TargetId;public bool Shell,HitFoot,Garrison;public string Weapon;}
public partial class Campaign {
 public const int MaxFactions=6;
 public int FactionCount {get{return River==null?2:River.Starts.Length;}}
 public bool IslandMission {get{return River!=null&&River.IsIsland;}}
 public static readonly TypeInfo[] Types={
  new TypeInfo("hq","指挥部","",0,1550,0,0,132,0,5,0),
  new TypeInfo("power","电站","hq",170,470,0,0,108,0,5,0),
  new TypeInfo("barracks","兵营","hq",210,650,0,0,124,0,6,0),
  new TypeInfo("factory","车辆工厂","hq",380,980,0,0,155,0,10,0),
  new TypeInfo("radar","雷达站","hq",240,510,0,0,113,0,8,0),
  new TypeInfo("satellite","间谍卫星","hq",750,800,0,0,118,0,18,0),
  new TypeInfo("post","防御阵地","hq",235,610,17,118,102,0,7,1.2f),
  new TypeInfo("squad","步枪兵","barracks",85,110,10,73,Campaign.Tile*5/2,48,5,1.25f),
  new TypeInfo("at","反装甲兵","barracks",135,85,30,115,Campaign.Tile*5/2+2,43.2f,8,2.5f),
  new TypeInfo("engineer","工兵","barracks",95,90,0,0,Campaign.Tile*5/2,48,6,1),
  new TypeInfo("ifv","步兵战车","factory",185,210,16,96,50,96,8,1.3f),
  new TypeInfo("tank","主战坦克","factory",290,355,38,115,57,72,12,2),
  new TypeInfo("arty","自行火炮","factory",315,150,54,222,53,57.6f,13,3.5f),
  new TypeInfo("mcv","工程车","factory",340,290,0,0,55,60,13,0),
  new TypeInfo("refinery","矿石精炼厂","hq",390,720,0,0,136,0,9,0),
  new TypeInfo("miner","采矿车","factory",195,310,0,0,44,48,9,0)};
 public static readonly TypeInfo[] CivilTypes={new TypeInfo("residence","民居","",0,550,0,0,108,0,1,0),new TypeInfo("shop","商住楼","",0,720,0,0,132,0,1,0),new TypeInfo("municipal","镇公所","",0,1050,0,0,150,0,1,0)};
 public static TypeInfo Info(string k){return Array.Find(Types,t=>t.Key==k)??Array.Find(CivilTypes,t=>t.Key==k);}
 public readonly List<Piece> Units=new List<Piece>();public readonly List<Shot> Shots=new List<Shot>();
 public readonly Site[] Sites;
 public Site Harbor {get{return Sites.Length==0?null:Sites[0];}}
 public int[] Funds=Enumerable.Repeat(1150,MaxFactions).ToArray();public float Time; public bool Landing,Won,Lost;public string Radio="展开工程车，在西南建立登陆基地。";
 int seq;float nextEnemyDecision=1,nextWave=92;
 public readonly bool Tutorial,Assisted;
 public int Lesson,InfantryDelivered,VehiclesDelivered;public Piece LastOrder,LastReady;public float ReadyAt;
 public Campaign(bool tutorial=false,bool assisted=false,bool riverbend=false,bool island=false){
  River=riverbend||island?new RiverbendMap(island):null;Sites=River!=null?new Site[0]:new[]{new Site("港务中心",1330,292,1265,227)};
  discovered=new bool[MaxFactions,MapFogCols,MapFogRows];
  Tutorial=tutorial;Assisted=assisted||tutorial;if(Assisted){Funds[0]=1700;nextWave=175;}
  if(River!=null){SetupRiverbend();Reveal();return;}
  Spawn("mcv",0,152,514);Spawn("squad",0,214,418);Spawn("squad",0,258,410);

  if(tutorial){Spawn("squad",1,1390,287);Reveal();return;}
  Spawn("mcv",1,EnemyHome.X,EnemyHome.Y);Reveal();
 }
 public static float Dist(PointF a,PointF b){return (float)Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));}
 public Piece Spawn(string k,int side,float x,float y){
  var t=Info(k);var o=new Piece{Id=++seq,T=t,Side=side,P=new PointF(x,y),Hp=t.Hp,Mining=k=="miner",Turn=side==0?3:13,Dir=side==0?3:13};if(t.Building){o.P=Middle(Footprint(t,o.P));o.Dir=0;o.Turn=0;Changed();}Units.Add(o);return o;
 }
 public IEnumerable<Piece> Live(int side){return Units.Where(o=>o.Side==side&&o.Deployed);}
 public int Power(int side){return Live(side).Sum(o=>o.T.Key=="hq"?4:o.T.Key=="power"?8:o.T.Key=="factory"?-5:o.T.Key=="barracks"?-2:o.T.Key=="radar"?-3:o.T.Key=="satellite"?-6:o.T.Key=="post"?-2:0);}
 public bool Visible(Piece o){return o.Side==0||Explored(0,o.P);}
 static float Sight(Piece p){return p.T.Foot?198:p.T.Building?163:p.T.Key=="arty"?133:166;}
 public const int ProductionQueueLimit=30;
 public string Require(TypeInfo t,int side){
  if(!Live(side).Any(p=>p.T.Key==t.Producer))return "需要"+(t.Producer==""?"工程车":Info(t.Producer).Name);
  if(t.Key=="satellite"&&(!Live(side).Any(p=>p.T.Key=="radar")||!Live(side).Any(p=>p.T.Key=="factory")))return "需要雷达和车辆工厂";
  if((t.Key=="factory"||t.Key=="refinery")&&!Live(side).Any(p=>p.T.Key=="power"))return "先建电站";
  if(!t.Building&&NextUnitFactory(side,t.Producer)==null)return "所有工厂队列已满（每厂 30 单）";
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
  src.Queue.Enqueue(t);if(side==0){LastOrder=src;Radio=src.T.Name+" #"+src.Id+" 已接单："+t.Name+"，排第 "+src.Queue.Count+" 位。";}return true;
 }
 public bool Move(Piece o,PointF p,bool attack){
  if(!o.Deployed||o.T.Building)return false;if(!ParkingAllowed(o.Side,p)||!Route(o,p))return false;ClearMission(o);o.Mining=false;o.Dock=null;o.DockReserved=false;o.DockTicket=0;o.DockRetryAfter=0;o.DockBlockedFor=0;o.Unload=0;o.FirePoint=null;o.Formation=null;o.Target=null;o.Contact=ContactOrder.None;o.ResumeMarch=false;o.Advancing=attack;o.Goal=p;return true;
 }
 public void Attack(Piece o,Piece target){if(!o.Deployed||!Armed(o)||target==o)return;ClearMission(o);o.FirePoint=null;o.Formation=null;o.Target=target;o.Contact=ContactOrder.Directed;o.ResumeMarch=false;o.Advancing=true;o.Path.Clear();o.Replan=0;o.Goal=target.P;}
 void ApproachPoint(Piece o,PointF target){
  FindFiringPosition(o,target);
 }
 public void Load(Piece car){
  if(car==null||car.T.Key!="ifv"||!car.Alive)return;var cargo=Units.FirstOrDefault(o=>o.Alive&&o.Carrier==car);
  if(cargo!=null){PointF drop;if(Exit(car,cargo.T,out drop)){cargo.Carrier=null;cargo.P=drop;Radio="步兵已下车。";}else Radio="周围缺少下车空间。";return;}
  var foot=Live(car.Side).Where(o=>o.T.Foot&&Dist(o.P,car.P)<70).OrderBy(o=>Dist(o.P,car.P)).FirstOrDefault();
  if(foot==null){Radio="先把一名步兵移到战车旁。";return;}Halt(foot);foot.Carrier=car;if(RepairVehicle(car))Halt(car);Radio=foot.T.Name+"已登车。";
 }
 bool ExitClear(Piece source,TypeInfo exiting,PointF point){
  return !Blocked(point,exiting.Clearance,null)&&!Units.Any(u=>u!=source&&u.Deployed&&!u.T.Building&&TrafficContact(exiting,point,u.T,u.P));
 }
 bool Exit(Piece o,TypeInfo exiting,out PointF p){
  if(o.T.Building){
   var apron=ExitApron(o.T,Footprint(o.T,o.P));p=Middle(apron);
   if(ExitClear(o,exiting,p))return true;
   // A finished order waits inside; only idle friendly blockers yield the exit.
   foreach(var idle in Live(o.Side).Where(u=>!u.T.Building&&!u.Mining&&u.Path.Count==0&&u.Target==null&&Inflated(apron,exiting.TrafficRadius).Contains(u.P)).ToArray())RallyNewUnit(o,idle);
   return false;
  }
  for(int j=0;j<24;j++){double a=(o.Dir+j%8*2)*Math.PI/8;float d=(o.T.Building?Radius(o.T)+20:27)+j/8*19;
   p=new PointF(o.P.X+(float)Math.Sin(a)*d,o.P.Y+(float)Math.Cos(a)*d*.85f);
   var candidate=p;if(ExitClear(o,exiting,candidate))return true;
  }p=o.P;return false;
 }
 void Kill(Piece o){if(o.T.Building){TransferProduction(o);Changed();}o.Hp=0;o.Path.Clear();o.Queue.Clear();if(o.T.Civilian)EvacuateHouse(o);else foreach(var c in Units.Where(c=>c.Carrier==o)){c.Carrier=null;c.P=o.P;c.Hp=0;}
  if(o.Side==0)Radio=o.T.Name+"已损失。";}
 void Fire(Piece o,Piece e){
  o.Cool=o.T.Reload;float factor=1;
  if(o.T.Key=="at")factor=e.T.Foot?.32f:e.T.Building?.72f:1.85f;
  if(o.T.Key=="ifv"&&e.T.Foot)factor=1.55f;if(o.T.Key=="squad"&&e.T.Key=="tank")factor=.29f;
  if(River==null&&o.T.Foot&&o.P.X<735&&o.P.Y<324)factor*=1.12f;
  RecordShot(o,TargetPoint(o.P,e),e);
  if(e.Side==0)ReportStrike(e);RememberAiAttack(e,o);
  e.HitAt=VisualTime;e.Hp-=o.T.Hit*factor;if(e.Hp<=0){if(o.Side!=e.Side&&e.Side>=0&&!o.T.Building&&o.T.Price>0)o.Experience+=e.T.Price/(o.T.Price*3f);Kill(e);}else OnIncomingFire(e,o.Carrier!=null&&o.Carrier.T.Civilian?o.Carrier:o);
 }
 static float Bearing(PointF from,PointF to){return (float)(Math.Atan2(to.X-from.X,to.Y-from.Y)*8/Math.PI+16)%16;}
 static float TurnToward(Piece o,float heading,float dt){
  float diff=(heading-o.Turn+24)%16-8,seconds=Math.Abs(diff)/15;
  o.Turn=seconds<=dt?heading:(o.Turn+Math.Sign(diff)*dt*15+16)%16;
  o.Dir=(int)Math.Round(o.Turn)%16;return Math.Max(0,dt-seconds);
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
  if(!o.Alive)return;if(o.Carrier!=null){o.P=o.Carrier.P;if(o.Carrier.T.Civilian)TickGarrison(o,dt);return;}
  if(o.Side<0)return;
  if(o.Packing>0){TickPacking(o,dt);return;}
  if(TickEntry(o,dt))return;
  if(o.T.Key=="mcv"&&o.PendingDeploy){float delta=((24-o.Turn)%16)-8,step=Math.Max(-8*dt,Math.Min(8*dt,delta));o.Turn=(o.Turn+step+16)%16;o.Dir=(int)Math.Round(o.Turn)%16;if(Math.Abs(delta)<=8*dt+.001f){o.Turn=0;o.Dir=0;BeginBaseExpansion(o);}return;}
  if(o.Ready<1){o.Ready=Math.Min(1,o.Ready+dt/(o.Assembly?AssemblyDuration(o):o.T.Seconds));if(o.Ready>=1){o.CompletedAt=VisualTime;o.BaseTransition=false;}return;}
  o.Cool-=dt;o.Replan-=dt;o.TrafficWait-=dt;o.Door=Math.Max(0,o.Door-dt);o.Rollout=Math.Max(0,o.Rollout-dt);
  if(o.T.Key=="refinery")RunRefinery(o,dt);
  if(o.Mining)RunMiner(o,dt);
  TickBuildingRepair(o,dt);
  if(o.Queue.Count>0&&!o.ProductionPaused){var t=o.Queue.Peek();PaidWork(o.Side,t,dt*ProductionRate(o),ref o.Work,ref o.WorkPaid);
   if(o.Work>=t.Seconds){PointF p;if(Exit(o,t,out p)){var u=Spawn(t.Key,o.Side,p.X,p.Y);u.Dir=o.Dir;u.Turn=o.Dir;u.Rollout=.6f;o.Door=1;o.Work=0;o.WorkPaid=0;o.Queue.Dequeue();if(u.T.Key!="miner")RallyNewUnit(o,u);if(o.Side==0){LastReady=u;ReadyAt=Time;if(Time-lastStrike>3)SetEvent(u.P,u.T.Name+"出列");if(t.Key=="squad")InfantryDelivered++;if(t.Key=="ifv")VehiclesDelivered++;Radio=t.Name+"已从"+o.T.Name+" #"+o.Id+" 出列。";}}}}
  MissionTick(o);
  bool stop=TickRepairVehicle(o,dt);
  if(Armed(o)&&o.FirePoint.HasValue)stop=GroundFireTick(o,dt);
  else if(Armed(o))stop=FightContact(o,dt);
  if(!stop&&o.Path.Count>0){if(o.RouteVersion!=topology&&!Route(o,o.Path.Last())){o.Path.Clear();return;}var p=o.Path.Peek();
   float speed=o.Formation==null?o.T.Speed:o.Formation.Speed(o);
   if(!o.T.Foot)DriveTracks(o,p,FollowingSpeed(o,p,speed,dt),dt);
   else{
    float d=Dist(o.P,p),travel=speed*dt;
    var next=d<=travel?p:new PointF(o.P.X+(p.X-o.P.X)*travel/d,o.P.Y+(p.Y-o.P.Y)*travel/d);
    var step=Avoid(o,next);o.TrafficBlocked=Dist(o.P,step)<.001f?o.TrafficBlocked+dt:0;
    if(Dist(o.P,step)>.001f){o.Turn=Bearing(o.P,step);o.Dir=(int)Math.Round(o.Turn)%16;o.P=step;}
   }
   if(o.Path.Count>0&&(Dist(o.P,o.Path.Peek())<(o.T.Foot?.1f:.001f)||(!o.Mining&&o.Path.Count==1&&Dist(o.P,o.Path.Peek())<9&&o.TrafficBlocked>1&&Units.Any(u=>u!=o&&u.Deployed&&u.Path.Count==0&&!u.T.Building&&TrafficContact(o.T,o.Path.Peek(),u.T,u.P)))))o.Path.Dequeue();}
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
  if(Won||Lost)return;TickVisuals(dt);Time+=dt;visionTimer-=dt;if(visionTimer<=0){visionTimer=.20f;Reveal();}
  TickConstruction(dt);BalanceProduction();
  bool[] power=Enumerable.Range(0,MaxFactions).Select(s=>Power(s)>=0).ToArray();
  foreach(var o in Units.ToArray()){var before=o.P;UnitTick(o,dt,o.Side<0||power[o.Side]);if(o.Alive&&o.Carrier==null){float travel=Dist(before,o.P);o.Travel+=travel;if(travel>.001f)o.MovedAt=VisualTime;TrackRouteProgress(o);o.StalledFor=o.Path.Count>0&&travel<o.T.Speed*dt*.15f?o.StalledFor+dt:Math.Max(0,o.StalledFor-dt*2);}}
  TickAiMarchRequests();CheckEnemySurrender(dt);
  Lost=Remaining(0)==0;Won=!Lost&&!Units.Any(o=>o.Alive&&o.Side>0);
  if(Won||Lost){OutcomeAt=VisualTime;return;}
  if(!Tutorial&&River!=null)TickStrategicAis();
  else if(!Tutorial&&Time>=nextEnemyDecision){nextEnemyDecision=Time+2;EnemyEconomy(1);}
  if(!Tutorial&&River==null&&Time>=nextWave){EnemyOrders(1);nextWave=Time+54*(Assisted?1.5f:1);}
  if(Tutorial)AdvanceLesson();
 }
}
}
