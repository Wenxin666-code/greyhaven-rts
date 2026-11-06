using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public class TypeInfo {
 public string Key,Name,Producer; public int Price,Hp,Hit,Range,Size; public float Speed,Seconds,Reload;
 public float LockRange {get{return Hit==0?0:Building?Range:Math.Min(Range*2,16*Campaign.Tile);}}
 public float Clearance {get{return Foot?5:Key=="miner"?5.25f:8.8f;}}
 public bool Building {get{return Speed==0;}} public bool Foot {get{return Key=="squad"||Key=="at"||Key=="engineer";}}
 public TypeInfo(string key,string name,string from,int price,int hp,int hit,int range,int size,float speed,float time,float reload){
  Key=key;Name=name;Producer=from;Price=price;Hp=hp;Hit=hit;Range=range;Size=size;Speed=speed;Seconds=time;Reload=reload;
 }
}
public enum ContactOrder { None, Directed, Automatic, ReturnFire }
public class Piece {
 public float TrafficBlocked;
 public PointF? GuardHome;public Piece Escort;public PointF EscortOffset;public bool GuardTravelling,GuardReturning,ForceMoving;
 public readonly List<PointF> Waypoints=new List<PointF>();public int WaypointIndex;public bool Patrol;
 public bool DockReserved;public float DockTicket,DockRetryAfter;
 public int LastReceipt;public float ReceiptAt;public PointF WorkPoint,TravelSample;public float TrafficSampleTime;public bool Mining,StarterDelivered,Returning,Surveying;public float Cargo,Unload,Dispatch,MillPhase;public Piece Dock;public OrePatch Patch;
 public ContactOrder Contact;public bool ResumeMarch;public PointF LastContact;public float ScanIn;
 public int RouteVersion,Id,Side,Dir,Group; public TypeInfo T; public PointF P; public float Hp,Ready=1,Cool,Dead,Replan,Turn,Door,Rollout;
 public bool Alive {get{return Hp>0;}} public bool Deployed {get{return Alive&&Ready>=1&&Carrier==null;}}
 public Queue<PointF> Path=new Queue<PointF>(); public Queue<TypeInfo> Queue=new Queue<TypeInfo>(); public float Work,TrafficWait;
 public readonly List<PointF> Berths=new List<PointF>();public PointF? FirePoint;public PointF? Rally;public March Formation;public Piece Target,Carrier; public PointF Goal; public bool Advancing;
}
public class Site {
 public string Name;public PointF P;

 public readonly RectangleF Body;
 public Site(string name,int x,int y,int bodyX,int bodyY){
  Name=name;P=new PointF(x,y);Body=new RectangleF(bodyX,bodyY,2*Campaign.Tile,2*Campaign.Tile);
 }
}
public class Shot {public PointF A,B;public float Life;public int Side;public bool Shell;}
public partial class Campaign {
 public static readonly TypeInfo[] Types={
  new TypeInfo("hq","指挥部","",0,1550,0,0,132,0,5,0),
  new TypeInfo("power","电站","hq",170,470,0,0,108,0,5,0),
  new TypeInfo("barracks","兵营","hq",210,650,0,0,124,0,6,0),
  new TypeInfo("factory","车辆工厂","hq",380,980,0,0,155,0,10,0),
  new TypeInfo("radar","雷达站","hq",240,510,0,0,113,0,8,0),
  new TypeInfo("post","防御阵地","hq",235,610,17,118,102,0,7,1.2f),
  new TypeInfo("squad","步枪兵","barracks",85,110,10,73,48,48,5,1.25f),
  new TypeInfo("at","反装甲兵","barracks",135,85,30,115,50,43.2f,8,2.5f),
  new TypeInfo("engineer","工兵","barracks",95,90,0,0,48,48,6,1),
  new TypeInfo("ifv","步兵战车","factory",185,210,16,96,50,96,8,1.3f),
  new TypeInfo("tank","主战坦克","factory",290,355,38,115,57,72,12,2),
  new TypeInfo("arty","自行火炮","factory",315,150,54,222,53,57.6f,13,3.5f),
  new TypeInfo("mcv","工程车","hq",340,290,0,0,55,60,13,0),
  new TypeInfo("refinery","矿石精炼厂","hq",390,720,0,0,136,0,9,0),
  new TypeInfo("miner","采矿车","factory",195,310,0,0,44,72,9,0)};
 public static TypeInfo Info(string k){return Array.Find(Types,t=>t.Key==k);}
 public readonly List<Piece> Units=new List<Piece>();public readonly List<Shot> Shots=new List<Shot>();
 public readonly Site[] Sites={new Site("港务中心",1330,292,1265,227)};
 public Site Harbor {get{return Sites[0];}}
 public int[] Funds={1150,1150};public bool[] Upgraded={false,false};public float Time; public bool Landing,Won,Lost;public string Radio="展开工程车，在西南建立登陆基地。";
 int seq,aiTurn;float nextEnemyDecision=1,aiClock,nextWave=92;
 public readonly bool Tutorial,Assisted;
 public int Lesson,InfantryDelivered,VehiclesDelivered;public Piece LastOrder,LastReady;public float ReadyAt;
 public Campaign(bool tutorial=false,bool assisted=false){
  Tutorial=tutorial;Assisted=assisted||tutorial;if(Assisted){Funds[0]=1700;nextWave=175;}
  Spawn("mcv",0,152,514);Spawn("squad",0,214,418);Spawn("squad",0,258,410);

  if(tutorial){Spawn("squad",1,1390,287);Reveal();return;}
  Spawn("mcv",1,EnemyHome.X,EnemyHome.Y);Reveal();
 }
 public static float Dist(PointF a,PointF b){return (float)Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));}
 public Piece Spawn(string k,int side,float x,float y){
  var t=Info(k);var o=new Piece{Id=++seq,T=t,Side=side,P=new PointF(x,y),Hp=t.Hp,Mining=k=="miner",Turn=side==0?3:13,Dir=side==0?3:13};if(t.Building){o.P=Middle(Footprint(t,o.P));o.Dir=0;o.Turn=0;Changed();}Units.Add(o);return o;
 }
 public IEnumerable<Piece> Live(int side){return Units.Where(o=>o.Side==side&&o.Deployed);}
 public int Power(int side){return Live(side).Sum(o=>o.T.Key=="hq"?4:o.T.Key=="power"?8:o.T.Key=="factory"?-5:o.T.Key=="barracks"?-2:o.T.Key=="radar"?-3:o.T.Key=="post"?-2:0);}
 public bool Visible(Piece o){return o.Side==0||Explored(0,o.P);}
 static float Sight(Piece p){return p.T.Foot?198:p.T.Building?163:p.T.Key=="arty"?133:166;}
 int CommittedUnits(int side){
  return Units.Where(o=>o.Alive&&o.Side==side).Sum(o=>(o.T.Building?0:1)+o.Queue.Count+(o.T.Key=="refinery"&&!o.StarterDelivered?1:0));
 }
 public string Require(TypeInfo t,int side){
  if(!Live(side).Any(p=>p.T.Key==t.Producer))return "需要"+(t.Producer==""?"工程车":Info(t.Producer).Name);
  if((t.Key=="tank"||t.Key=="arty")&&!Upgraded[side])return "需要装甲升级";
  if((t.Key=="factory"||t.Key=="refinery")&&!Live(side).Any(p=>p.T.Key=="power"))return "先建电站";
  if(Funds[side]<t.Price)return "军费不足";
  if(t.Key=="refinery"&&CommittedUnits(side)>=55)return "部队满编";
  if(!t.Building){
   if(t.Producer=="barracks"&&!Live(side).Any(p=>p.T.Key=="barracks"&&p.Queue.Count<4&&FreeBerth(p).HasValue))return "集合位已满";
   if(!Live(side).Any(p=>p.T.Key==t.Producer&&p.Queue.Count<4))return "队列已满";
   if(CommittedUnits(side)>=55)return "部队满编";
  }
  return null;
 }
 static float Radius(TypeInfo t){return t.Size*.27f;}
 public bool Deploy(Piece o){
  return o!=null&&o.Side==0&&DeployVehicle(o);
 }
 bool DeployVehicle(Piece o){
  if(!o.Deployed||o.T.Key!="mcv")return false;
  if(!CanPlace(Info("hq"),o.P,o,o.Side)){if(o.Side==0)Radio="此处空间不足。将工程车移到一块空地后再展开。";return false;}
  Changed();o.T=Info("hq");o.P=Middle(Footprint(o.T,o.P));o.Turn=0;o.Dir=0;o.Hp=o.T.Hp;o.Ready=.01f;o.Path.Clear();o.Target=null;if(o.Side==0){Landing=true;Radio="基地正在展开。先建电站，再建矿石精炼厂。完工后，首辆矿车会从厂门出发找矿。";}return true;
 }
 public bool Build(TypeInfo t,PointF p){
  return BuildFor(t,p,0);
 }
 bool BuildFor(TypeInfo t,PointF p,int side){
  var reason=Require(t,side);
  if(!t.Building||reason!=null||!CanPlace(t,p,null,side)){
   if(side==0)Radio=reason??"请选择指挥部附近的已探索空地，避开单位、道路和矿石。";return false;
  }
  Funds[side]-=t.Price;Spawn(t.Key,side,p.X,p.Y).Ready=.01f;if(side==0)Radio=t.Name+"开始施工。";return true;
 }
 public bool Train(TypeInfo t,int side,Piece preferred){
  string reason=Require(t,side);var src=Live(side).Where(o=>o.T.Key==t.Producer&&o.Queue.Count<4&&(t.Producer!="barracks"||FreeBerth(o).HasValue)).OrderBy(o=>o==preferred?-1:o.Queue.Count).FirstOrDefault();
  if(t.Building||reason!=null||src==null||CommittedUnits(side)>=55){if(side==0)Radio=reason??(src==null?"同类生产设施的四个位置均已排满。":"部队及待产订单已达 55 上限。");return false;}
  if(src.T.Key=="barracks")src.Berths.Add(FreeBerth(src).Value);Funds[side]-=t.Price;src.Queue.Enqueue(t);if(side==0){LastOrder=src;Radio=src.T.Name+" #"+src.Id+" 已接单："+t.Name+"，排第 "+src.Queue.Count+" 位。";}return true;
 }
 public void Upgrade(){Radio=UpgradeArmor(0)?"装甲升级完成，可以生产坦克和自行火炮。":"升级需要已建成的车辆工厂和 260 军费。";}
 bool UpgradeArmor(int side){
  if(Upgraded[side]||Funds[side]<260||!Live(side).Any(o=>o.T.Key=="factory"))return false;
  Funds[side]-=260;Upgraded[side]=true;return true;
 }
 public bool Move(Piece o,PointF p,bool attack){
  if(!o.Deployed||o.T.Building)return false;if(!ParkingAllowed(o.Side,p)||!Route(o,p))return false;ClearMission(o);o.Mining=false;o.Dock=null;o.DockReserved=false;o.DockTicket=0;o.DockRetryAfter=0;o.Unload=0;o.FirePoint=null;o.Formation=null;o.Target=null;o.Contact=ContactOrder.None;o.ResumeMarch=false;o.Advancing=attack;o.Goal=p;return true;
 }
 public void Attack(Piece o,Piece target){if(!o.Deployed||o.T.Hit==0||target==o)return;ClearMission(o);o.FirePoint=null;o.Formation=null;o.Target=target;o.Contact=ContactOrder.Directed;o.ResumeMarch=false;o.Advancing=true;o.Path.Clear();o.Replan=0;o.Goal=target.P;}
 void ApproachPoint(Piece o,PointF target){
  float length=Dist(o.P,target);if(length<1)return;float range=Math.Max(20,o.T.Range-2);
  double angle=Math.Atan2(o.P.Y-target.Y,o.P.X-target.X);
  for(int j=0;j<8;j++){double a=angle+(j%2==0?1:-1)*((j+1)/2)*.5;var dest=new PointF(target.X+(float)Math.Cos(a)*range,target.Y+(float)Math.Sin(a)*range);if(Route(o,dest))return;}
 }
 public void Load(Piece car){
  if(car==null||car.T.Key!="ifv"||!car.Alive)return;var cargo=Units.FirstOrDefault(o=>o.Alive&&o.Carrier==car);
  if(cargo!=null){PointF drop;if(Exit(car,cargo.T,out drop)){cargo.Carrier=null;cargo.P=drop;Radio="步兵已下车。";}else Radio="周围缺少下车空间。";return;}
  var foot=Live(car.Side).Where(o=>o.T.Foot&&Dist(o.P,car.P)<70).OrderBy(o=>Dist(o.P,car.P)).FirstOrDefault();
  if(foot==null){Radio="先把一名步兵移到战车旁。";return;}Halt(foot);foot.Carrier=car;Radio=foot.T.Name+"已登车。";
 }
 bool ExitClear(Piece source,TypeInfo exiting,PointF point){
  return !Blocked(point,exiting.Clearance,null)&&!Units.Any(u=>u!=source&&u.Deployed&&!u.T.Building&&Dist(u.P,point)<exiting.Clearance+u.T.Clearance);
 }
 bool Exit(Piece o,TypeInfo exiting,out PointF p){
  if(o.T.Key=="barracks"&&o.Berths.Count>0){
   p=o.Berths[0];if(BerthClear(o,p))return true;
   var alt=FreeBerth(o);if(alt.HasValue){p=alt.Value;o.Berths[0]=p;return true;}
   var apron=Muster(Footprint(o.T,o.P));foreach(var idle in Live(o.Side).Where(u=>!u.T.Building&&u.Path.Count==0&&u.Target==null&&!u.FirePoint.HasValue&&apron.Contains(u.P)).ToArray())RallyNewUnit(o,idle);return false;
  }
  if(o.T.Building){
   var gate=Gate(Footprint(o.T,o.P));foreach(float offset in new[]{0f,-.31f,.31f}){
    var candidate=new PointF(o.P.X+gate.Width*offset,gate.Bottom-6);
    if(ExitClear(o,exiting,candidate)){p=candidate;return true;}
   }p=o.P;return false;
  }
  for(int j=0;j<24;j++){double a=(o.Dir+j%8*2)*Math.PI/8;float d=(o.T.Building?Radius(o.T)+20:27)+j/8*19;
   p=new PointF(o.P.X+(float)Math.Sin(a)*d,o.P.Y+(float)Math.Cos(a)*d*.85f);
   var candidate=p;if(ExitClear(o,exiting,candidate))return true;
  }p=o.P;return false;
 }
 void Kill(Piece o){if(o.T.Building)Changed();o.Hp=0;o.Path.Clear();o.Queue.Clear();o.Berths.Clear();foreach(var c in Units.Where(c=>c.Carrier==o)){c.Carrier=null;c.P=o.P;c.Hp=0;}
  if(o.Side==0)Radio=o.T.Name+"已损失。";}
 void Fire(Piece o,Piece e){
  o.Cool=o.T.Reload;float factor=1;
  if(o.T.Key=="at")factor=e.T.Foot?.32f:e.T.Building?.72f:1.85f;
  if(o.T.Key=="ifv"&&e.T.Foot)factor=1.55f;if(o.T.Key=="squad"&&e.T.Key=="tank")factor=.29f;
  if(o.T.Foot&&o.P.X<735&&o.P.Y<324)factor*=1.12f;
  Shots.Add(new Shot{A=o.P,B=TargetPoint(o.P,e),Side=o.Side,Life=.23f,Shell=o.T.Key=="arty"});
  if(e.Side==0)ReportStrike(e);
  e.Hp-=o.T.Hit*factor;if(e.Hp<=0)Kill(e);else OnIncomingFire(e,o);
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
  if(!o.Alive){o.Dead+=dt;return;}if(o.Carrier!=null){o.P=o.Carrier.P;return;}
  if(o.Ready<1){o.Ready=Math.Min(1,o.Ready+dt/o.T.Seconds);return;}
  o.Cool-=dt;o.Replan-=dt;o.TrafficWait-=dt;o.Door=Math.Max(0,o.Door-dt);o.Rollout=Math.Max(0,o.Rollout-dt);
  if(o.T.Key=="refinery")RunRefinery(o,dt);
  if(o.Mining)RunMiner(o,dt);
  if(o.Queue.Count>0){var t=o.Queue.Peek();o.Work=Math.Min(t.Seconds,o.Work+dt*(powered?1:.45f));
   if(o.Work>=t.Seconds){PointF p;if(Exit(o,t,out p)){var u=Spawn(t.Key,o.Side,p.X,p.Y);u.Dir=o.Dir;u.Turn=o.Dir;u.Rollout=.6f;o.Door=1;o.Work=0;o.Queue.Dequeue();if(o.Berths.Count>0)o.Berths.RemoveAt(0);if(u.T.Key!="miner")RallyNewUnit(o,u);if(o.Side==0){LastReady=u;ReadyAt=Time;if(Time-lastStrike>3)SetEvent(u.P,u.T.Name+"出列");if(t.Key=="squad")InfantryDelivered++;if(t.Key=="ifv")VehiclesDelivered++;Radio=t.Name+"已从"+o.T.Name+" #"+o.Id+" 出列。";}}}}
  if(o.T.Key=="engineer"&&o.Cool<=0){
   var wounded=Live(o.Side).FirstOrDefault(f=>!f.T.Foot&&f.Hp<f.T.Hp&&Dist(o.P,f.P)<67);
   if(wounded!=null){wounded.Hp=Math.Min(wounded.T.Hp,wounded.Hp+13);o.Cool=1;}
  }
  MissionTick(o);
  bool stop=false;
  if(o.FirePoint.HasValue)stop=GroundFireTick(o,dt);
  else if(o.T.Hit>0)stop=FightContact(o,dt);
  if(!stop&&o.Path.Count>0){if(o.RouteVersion!=topology&&!Route(o,o.Path.Last())){o.Path.Clear();return;}var p=o.Path.Peek();
   float speed=o.Formation==null?o.T.Speed:o.Formation.Speed(o);
   if(!o.T.Foot)DriveTracks(o,p,speed,dt);
   else{
    float d=Dist(o.P,p),travel=speed*dt;
    var next=d<=travel?p:new PointF(o.P.X+(p.X-o.P.X)*travel/d,o.P.Y+(p.Y-o.P.Y)*travel/d);
    var step=Avoid(o,next);o.TrafficBlocked=Dist(o.P,step)<.001f?o.TrafficBlocked+dt:0;
    if(Dist(o.P,step)>.001f){o.Turn=Bearing(o.P,step);o.Dir=(int)Math.Round(o.Turn)%16;o.P=step;}
   }
   if(o.Path.Count>0&&(Dist(o.P,o.Path.Peek())<(o.T.Foot?.1f:.001f)||(!o.Mining&&o.Path.Count==1&&Dist(o.P,o.Path.Peek())<9&&Units.Any(u=>u!=o&&u.Deployed&&Dist(u.P,o.Path.Peek())<15))))o.Path.Dequeue();}
 }
 bool VisibleTo(int side,Piece e){return Explored(side,e.P);}
 public int Remaining(int side){return Units.Count(o=>o.Alive&&o.Side==side);}
 void EnemyOrders(){
  var known=Units.Where(o=>o.Side==0&&o.Alive&&o.Carrier==null&&Explored(1,o.P)).ToArray();
  var beach=new PointF(152,514);
  foreach(var unit in Live(1).Where(o=>!o.T.Building&&o.T.Hit>0)){
   if(unit.Target!=null&&unit.Target.Alive)continue;
   var target=known.OrderBy(o=>Dist(unit.P,o.P)).FirstOrDefault();
   if(target!=null){Attack(unit,target);continue;}
   if(!Explored(1,beach)){Move(unit,beach,true);continue;}
   if(ExploreGround(unit))unit.Advancing=true;
  }
 }
 public void Tick(float dt){
  if(Won||Lost)return;Time+=dt;visionTimer-=dt;if(visionTimer<=0){visionTimer=.20f;Reveal();}
  Shots.ForEach(s=>s.Life-=dt);Shots.RemoveAll(s=>s.Life<=0);bool[] power={Power(0)>=0,Power(1)>=0};
  foreach(var o in Units.ToArray())UnitTick(o,dt,power[o.Side]);
  Units.RemoveAll(o=>!o.Alive&&o.Dead>24);
  Lost=Remaining(0)==0;Won=!Lost&&Remaining(1)==0;
  if(Won||Lost)return;
  if(!Tutorial&&Time>=nextEnemyDecision){
   nextEnemyDecision=Time+2;EnemyEconomy();
  }
  if(!Tutorial&&Time>=nextWave){EnemyOrders();nextWave=Time+54*(Assisted?1.5f:1);}
  if(Tutorial)AdvanceLesson();
 }
}
}
