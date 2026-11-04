using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public class TypeInfo {
 public string Key,Name,Producer; public int Price,Hp,Hit,Range,Size; public float Speed,Seconds,Reload;
 public bool Building {get{return Speed==0;}} public bool Foot {get{return Key=="squad"||Key=="at"||Key=="engineer";}}
 public TypeInfo(string key,string name,string from,int price,int hp,int hit,int range,int size,float speed,float time,float reload){
  Key=key;Name=name;Producer=from;Price=price;Hp=hp;Hit=hit;Range=range;Size=size;Speed=speed;Seconds=time;Reload=reload;
 }
}
public class Piece {
 public int RouteVersion,Id,Side,Dir,Group; public TypeInfo T; public PointF P; public float Hp,Ready=1,Cool,Dead,Replan,Turn,Door,Rollout;
 public bool Alive {get{return Hp>0;}} public bool Deployed {get{return Alive&&Ready>=1&&Carrier==null;}}
 public Queue<PointF> Path=new Queue<PointF>(); public Queue<TypeInfo> Queue=new Queue<TypeInfo>(); public float Work;
 public PointF? Rally;public March Formation;public Piece Target,Carrier; public PointF Goal; public bool Advancing;
}
public class Site {
 public string Name;public PointF P;public int Owner=-1;public float Capture;
 public RectangleF Body {get{return new RectangleF(P.X-65,P.Y-65,48,48);}}
 public Site(string name,int x,int y){Name=name;P=new PointF(x,y);}
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
  new TypeInfo("squad","步兵班","barracks",85,110,10,73,58,32,5,1.25f),
  new TypeInfo("at","反装甲组","barracks",135,85,30,115,61,29,8,2.5f),
  new TypeInfo("engineer","工程组","barracks",95,90,0,0,58,34,6,1),
  new TypeInfo("ifv","步兵战车","factory",185,210,16,96,82,45,8,1.3f),
  new TypeInfo("tank","主战坦克","factory",290,355,38,115,94,36,12,2),
  new TypeInfo("arty","自行火炮","factory",315,150,54,222,87,31,13,3.5f),
  new TypeInfo("mcv","工程车","hq",340,290,0,0,91,31,13,0)};
 public static TypeInfo Info(string k){return Array.Find(Types,t=>t.Key==k);}
 public readonly List<Piece> Units=new List<Piece>();public readonly List<Shot> Shots=new List<Shot>();
 public readonly Site[] Sites={new Site("西岸仓库",172,214),new Site("中央货场",660,444),new Site("港务中心",1330,292)};
 public int[] Funds={1150,540};public bool[] Upgraded={false,true};public float Time,Hold,Scan; public bool Landing,Won,Lost;public string Radio="展开工程车，在西南建立登陆基地。";
 int seq,aiTurn,wave;float income,aiClock,nextWave=92;bool counter;
 public readonly bool Tutorial,Assisted;
 public int Lesson,InfantryDelivered,VehiclesDelivered;public Piece LastOrder,LastReady;public float ReadyAt;
 public Campaign(bool tutorial=false,bool assisted=false){
  Tutorial=tutorial;Assisted=assisted||tutorial;if(Assisted){Funds[0]=1700;nextWave=175;}
  Spawn("mcv",0,152,514);Spawn("squad",0,214,418);Spawn("squad",0,258,410);
  if(tutorial){Spawn("squad",1,1390,287);Sites[2].Owner=1;Sites[2].Capture=-5;Reveal();return;}
  Spawn("hq",1,1510,530);Spawn("factory",1,1320,619);Spawn("barracks",1,1198,547);Spawn("power",1,1530,638);Spawn("post",1,1335,399);
  Spawn("squad",1,1258,340);Spawn("ifv",1,1418,437);Sites[2].Owner=1;Sites[2].Capture=-5;Reveal();
 }
 public static float Dist(PointF a,PointF b){return (float)Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y));}
 public Piece Spawn(string k,int side,float x,float y){
  var t=Info(k);var o=new Piece{Id=++seq,T=t,Side=side,P=new PointF(x,y),Hp=t.Hp,Turn=side==0?3:13,Dir=side==0?3:13};if(t.Building){o.P=Middle(Footprint(t,o.P));o.Dir=0;o.Turn=0;Changed();}Units.Add(o);return o;
 }
 public IEnumerable<Piece> Live(int side){return Units.Where(o=>o.Side==side&&o.Deployed);}
 public int Power(int side){return Live(side).Sum(o=>o.T.Key=="hq"?4:o.T.Key=="power"?8:o.T.Key=="factory"?-5:o.T.Key=="barracks"?-2:o.T.Key=="radar"?-3:o.T.Key=="post"?-2:0);}
 public bool Visible(Piece o){return o.Side==0||Explored(0,o.P);}
 static float Sight(Piece p){return p.T.Foot?198:p.T.Building?163:p.T.Key=="arty"?133:166;}
 public string Require(TypeInfo t,int side){
  if(!Live(side).Any(p=>p.T.Key==t.Producer))return "需要"+(t.Producer==""?"工程车":Info(t.Producer).Name);
  if((t.Key=="tank"||t.Key=="arty")&&!Upgraded[side])return "需要装甲升级";
  if(t.Key=="factory"&&!Live(side).Any(p=>p.T.Key=="power"))return "先建电站";
  if(Funds[side]<t.Price)return "军费不足";
  if(!t.Building){
   if(!Live(side).Any(p=>p.T.Key==t.Producer&&p.Queue.Count<4))return "队列已满";
   if(Units.Where(o=>o.Alive&&o.Side==side).Sum(o=>(o.T.Building?0:1)+o.Queue.Count)>=55)return "部队满编";
  }
  return null;
 }
 static float Radius(TypeInfo t){return t.Size*.27f;}
 public bool Deploy(Piece o){
  if(o==null||!o.Alive||o.Side!=0||o.T.Key!="mcv")return false;
  if(!CanPlace(Info("hq"),o.P,o)){Radio="此处空间不足。将工程车移到一块空地后再展开。";return false;}
  Changed();o.T=Info("hq");o.P=Middle(Footprint(o.T,o.P));o.Turn=0;o.Dir=0;o.Hp=o.T.Hp;o.Ready=.01f;o.Path.Clear();o.Target=null;Landing=true;Radio="基地正在展开。接下来建电站和兵营，让步兵占领补给点。";return true;
 }
 public bool Build(TypeInfo t,PointF p){
  var reason=Require(t,0);if(reason!=null||!CanPlace(t,p,null)){Radio=reason??"请选择指挥部周围的空地，避开单位、建筑和补给点。";return false;}
  Funds[0]-=t.Price;Spawn(t.Key,0,p.X,p.Y).Ready=.01f;Radio=t.Name+"开始施工。";return true;
 }
 public bool Train(TypeInfo t,int side,Piece preferred){
  string reason=Require(t,side);var src=Live(side).Where(o=>o.T.Key==t.Producer&&o.Queue.Count<4).OrderBy(o=>o==preferred?-1:o.Queue.Count).FirstOrDefault();
  if(t.Building||reason!=null||src==null||Units.Where(o=>o.Alive&&o.Side==side).Sum(o=>(o.T.Building?0:1)+o.Queue.Count)>=55){if(side==0)Radio=reason??(src==null?"同类生产设施的四个位置均已排满。":"部队及待产订单已达 55 上限。");return false;}
  Funds[side]-=t.Price;src.Queue.Enqueue(t);if(side==0){LastOrder=src;Radio=src.T.Name+" #"+src.Id+" 已接单："+t.Name+"，排第 "+src.Queue.Count+" 位。";}return true;
 }
 public void Upgrade(){if(!Upgraded[0]&&Funds[0]>=260&&Live(0).Any(o=>o.T.Key=="factory")){Funds[0]-=260;Upgraded[0]=true;Radio="装甲升级完成，可以生产坦克和自行火炮。";}else Radio="升级需要已建成的车辆工厂和 260 军费。";}
 public bool Move(Piece o,PointF p,bool attack){
  if(!o.Deployed||o.T.Building)return false;if(!Route(o,p))return false;o.Formation=null;o.Target=null;o.Advancing=attack;o.Goal=p;return true;
 }
 public void Attack(Piece o,Piece target){if(!o.Deployed||o.T.Hit==0)return;o.Formation=null;o.Target=target;o.Advancing=true;o.Path.Clear();o.Replan=0;o.Goal=target.P;}
 void Approach(Piece o,Piece target){
  float length=Dist(o.P,target.P);if(length<1)return;float range=Math.Max(43,o.T.Range*.78f);
  double angle=Math.Atan2(o.P.Y-target.P.Y,o.P.X-target.P.X);
  for(int j=0;j<8;j++){double a=angle+(j%2==0?1:-1)*((j+1)/2)*.5;var dest=new PointF(target.P.X+(float)Math.Cos(a)*range,target.P.Y+(float)Math.Sin(a)*range);if(Route(o,dest))return;}
 }
 public void Load(Piece car){
  if(car==null||car.T.Key!="ifv"||!car.Alive)return;var cargo=Units.FirstOrDefault(o=>o.Alive&&o.Carrier==car);
  if(cargo!=null){PointF drop;if(Exit(car,out drop)){cargo.Carrier=null;cargo.P=drop;Radio="步兵已下车。";}else Radio="周围缺少下车空间。";return;}
  var foot=Live(car.Side).Where(o=>o.T.Foot&&Dist(o.P,car.P)<70).OrderBy(o=>Dist(o.P,car.P)).FirstOrDefault();
  if(foot==null){Radio="先把一个步兵班组移到战车旁。";return;}foot.Carrier=car;foot.Path.Clear();foot.Target=null;Radio=foot.T.Name+"已登车。";
 }
 bool Exit(Piece o,out PointF p){
  if(o.T.Building){
   var gate=Gate(Footprint(o.T,o.P));foreach(float offset in new[]{0f,-.31f,.31f}){
    var candidate=new PointF(o.P.X+gate.Width*offset,gate.Bottom-6);
    if(!Blocked(candidate,11,null)&&!Units.Any(u=>u!=o&&u.Deployed&&!u.T.Building&&Dist(u.P,candidate)<17)){p=candidate;return true;}
   }p=o.P;return false;
  }
  for(int j=0;j<24;j++){double a=(o.Dir+j%8*2)*Math.PI/8;float d=(o.T.Building?Radius(o.T)+20:27)+j/8*19;
   p=new PointF(o.P.X+(float)Math.Sin(a)*d,o.P.Y+(float)Math.Cos(a)*d*.85f);
   var candidate=p;if(!Blocked(candidate,11,null)&&!Units.Any(u=>u!=o&&u.Deployed&&!u.T.Building&&Dist(u.P,candidate)<14))return true;
  }p=o.P;return false;
 }
 void Kill(Piece o){if(o.T.Building)Changed();o.Hp=0;o.Path.Clear();o.Queue.Clear();foreach(var c in Units.Where(c=>c.Carrier==o)){c.Carrier=null;c.P=o.P;c.Hp=0;}
  if(o.Side==0)Radio=o.T.Name+"已损失。";}
 void Fire(Piece o,Piece e){
  o.Cool=o.T.Reload;float factor=1;
  if(o.T.Key=="at")factor=e.T.Foot?.32f:e.T.Building?.72f:1.85f;
  if(o.T.Key=="ifv"&&e.T.Foot)factor=1.55f;if(o.T.Key=="squad"&&e.T.Key=="tank")factor=.29f;
  if(o.T.Foot&&o.P.X<735&&o.P.Y<324)factor*=1.12f;
  Shots.Add(new Shot{A=o.P,B=e.P,Side=o.Side,Life=.23f,Shell=o.T.Key=="arty"});
  e.Hp-=o.T.Hit*factor;if(e.Hp<=0)Kill(e);
 }
 static void Face(Piece o,PointF p,float dt){double angle=Math.Atan2(p.X-o.P.X,p.Y-o.P.Y);float want=(float)(angle*8/Math.PI+16)%16;
  float diff=(want-o.Turn+24)%16-8;o.Turn=(o.Turn+Math.Sign(diff)*Math.Min(Math.Abs(diff),dt*15)+16)%16;o.Dir=(int)Math.Round(o.Turn)%16;}
 void UnitTick(Piece o,float dt,bool powered){
  if(!o.Alive){o.Dead+=dt;return;}if(o.Carrier!=null){o.P=o.Carrier.P;return;}
  if(o.Ready<1){o.Ready=Math.Min(1,o.Ready+dt/o.T.Seconds);return;}
  o.Cool-=dt;o.Replan-=dt;o.Door=Math.Max(0,o.Door-dt);o.Rollout=Math.Max(0,o.Rollout-dt);
  if(o.Queue.Count>0){var t=o.Queue.Peek();o.Work=Math.Min(t.Seconds,o.Work+dt*(powered?1:.45f));
   if(o.Work>=t.Seconds){PointF p;if(Exit(o,out p)){var u=Spawn(t.Key,o.Side,p.X,p.Y);u.Dir=o.Dir;u.Turn=o.Dir;u.Rollout=.6f;o.Door=1;o.Work=0;o.Queue.Dequeue();RallyNewUnit(o,u);if(o.Side==0){LastReady=u;ReadyAt=Time;if(t.Key=="squad")InfantryDelivered++;if(t.Key=="ifv")VehiclesDelivered++;Radio=t.Name+"已从"+o.T.Name+" #"+o.Id+" 出列。";}}}}
  var e=o.Target;
  if(e!=null&&(!e.Deployed||!VisibleTo(o.Side,e))){o.Target=null;e=null;}
  if(o.T.Key=="engineer"&&o.Cool<=0){
   var wounded=Live(o.Side).FirstOrDefault(f=>!f.T.Foot&&f.Hp<f.T.Hp&&Dist(o.P,f.P)<67);
   if(wounded!=null){wounded.Hp=Math.Min(wounded.T.Hp,wounded.Hp+13);o.Cool=1;}
  }
  bool stop=false;
  if(o.T.Hit>0&&(o.Path.Count==0||o.Advancing||e!=null)){
   if(e==null)e=Live(1-o.Side).Where(f=>Dist(o.P,f.P)<=o.T.Range&&VisibleTo(o.Side,f)).OrderBy(f=>f.T.Hit==0?1:0).ThenBy(f=>Dist(o.P,f.P)).FirstOrDefault();
   if(e!=null){
    if(Dist(o.P,e.P)<=o.T.Range+2){stop=true;if(!o.T.Building)Face(o,e.P,dt);if(o.Cool<=0)Fire(o,e);}
    else if(!o.T.Building&&o.Replan<=0){Approach(o,e);o.Replan=1.8f;}
   }
  }
  if(!stop&&o.Path.Count>0){if(o.RouteVersion!=topology&&!Route(o,o.Path.Last())){o.Path.Clear();return;}var p=o.Path.Peek();
   float d=Dist(o.P,p),speed=(o.Formation==null?o.T.Speed:o.Formation.Speed(o))*dt;var next=d<=speed?p:new PointF(o.P.X+(p.X-o.P.X)*speed/d,o.P.Y+(p.Y-o.P.Y)*speed/d);var step=Avoid(o,next);if(Dist(o.P,step)>.001f){Face(o,step,dt);float target=(float)(Math.Atan2(step.X-o.P.X,step.Y-o.P.Y)*8/Math.PI+16)%16;
    if(o.T.Foot||Math.Abs((target-o.Turn+24)%16-8)<=.5f){o.Dir=(int)Math.Round(target)%16;o.P=step;}}if(o.Path.Count>0&&(Dist(o.P,p)<.1f||(o.Path.Count==1&&Dist(o.P,p)<9&&Units.Any(u=>u!=o&&u.Deployed&&Dist(u.P,p)<15))))o.Path.Dequeue();}
 }
 bool VisibleTo(int side,Piece e){return Explored(side,e.P);}
 void EnemyOrders(bool urgent){
  var army=Live(1).Where(o=>!o.T.Building&&o.T.Hit>0).ToArray();
  var target=urgent?Sites[2].P:wave%3==1?Sites[1].P:wave%3==2?Sites[0].P:Live(0).Where(o=>o.T.Key=="hq"&&Explored(1,o.P)).Select(o=>o.P).DefaultIfEmpty(Sites[1].P).First();
  int i=0;foreach(var o in army.Skip(urgent?0:1)){float a=i++*2.4f;for(int j=0;j<7;j++){float r=45+j*8;if(Move(o,new PointF(target.X+(float)Math.Cos(a+j)*r,target.Y+(float)Math.Sin(a+j)*r),true))break;}}
 }
 public int Income(int side){return (Live(side).Any(o=>o.T.Key=="hq")?5:0)+Sites.Take(2).Count(s=>s.Owner==side)*5;}
 public void Tick(float dt){
  if(Won||Lost)return;Time+=dt;visionTimer-=dt;if(visionTimer<=0){visionTimer=.20f;Reveal();}Scan=Math.Max(0,Scan-dt);
  Shots.ForEach(s=>s.Life-=dt);Shots.RemoveAll(s=>s.Life<=0);bool[] power={Power(0)>=0,Power(1)>=0};
  foreach(var o in Units.ToArray())UnitTick(o,dt,power[o.Side]);
  Units.RemoveAll(o=>!o.Alive&&o.Dead>24);
  foreach(var s in Sites){
   bool blue=Live(0).Any(o=>o.T.Foot&&Dist(o.P,s.P)<49),red=Live(1).Any(o=>o.T.Foot&&Dist(o.P,s.P)<49);
   if(blue!=red){s.Capture=Math.Max(-5,Math.Min(5,s.Capture+(blue?1:-1)*dt));
    int owner=s.Capture>=5?0:s.Capture<=-5?1:s.Owner;
    if(owner!=s.Owner){s.Owner=owner;Radio=s.Name+(owner==0?"已由我方控制。":"已被敌军夺取。");}}
  }
  income+=dt;if(income>=1){income-=1;for(int s=0;s<2;s++)Funds[s]+=Income(s);}
  if(!Tutorial&&Time>(Assisted?70:22)&&(aiClock-=dt)<=0){aiClock=(counter?8:14)*(Assisted?1.7f:1);string[] order={"squad","ifv","at","tank","squad","engineer"};
   Train(Info(order[aiTurn++%order.Length]),1,null);}
  if(!Tutorial&&Time>=nextWave){wave++;EnemyOrders(counter);nextWave=Time+(counter?18:54)*(Assisted?1.5f:1);}
  if(Sites[2].Owner==0){
   if(!counter){counter=true;EnemyOrders(true);Radio="港务中心已接管！守住泊位 45 秒，等待运输船。";}
   bool contested=Live(1).Any(o=>!o.T.Building&&Dist(o.P,Sites[2].P)<72);
   if(!contested&&(!Tutorial||Lesson>=7))Hold+=dt;if(Hold>=HoldTime)Won=true;
  }else{Hold=0;counter=false;}
  if(Tutorial)AdvanceLesson();
  if(!Units.Any(o=>o.Side==0&&o.Alive&&(o.T.Key=="hq"||o.T.Key=="mcv")))Lost=true;
 }
}
}
