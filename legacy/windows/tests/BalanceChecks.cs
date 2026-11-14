using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Greyhaven;

class BalanceChecks {
 static int Main(string[] args){try{Campaign.CheckUnifiedBalance(args.Length==0?null:args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {public partial class Campaign {
 static void BalanceAssert(bool value,string message){if(!value)throw new Exception(message);}
 static void Near(float actual,float expected,string message,float tolerance=.002f){BalanceAssert(Math.Abs(actual-expected)<=tolerance,message+": "+actual+" != "+expected);}
 static Campaign BalanceWorld(){var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();w.Spawn("mcv",1,2700,1700);return w;}
 internal static void CheckUnifiedBalance(string output){
  // Source facts are fixed independently of the implementation's conversion functions.
  string[] keys={"squad","at","engineer","ifv","tank","arty","mcv","miner","post"};
  int[] hp={125,100,75,200,300,200,1000,1000,400},cost={200,400,500,600,700,750,3000,1400,500},sight={5,6,4,8,8,8,6,4,7};
  float[] speed={14.0625f,9.84375f,14.0625f,35.15625f,23.90625f,16.875f,14.0625f,14.0625f,0};
  float[] ranges={4,4,0,6,5,12,0,0,5.5f},reload={20/15f,20/15f,0,50/15f,4,100/15f,0,0,26/15f};
  for(int i=0;i<keys.Length;i++){var t=Info(keys[i]);BalanceAssert(t.Hp==hp[i]&&t.Price==cost[i]&&t.SightCells==sight[i],keys[i]+" reference values");Near(t.Speed,speed[i],keys[i]+" move");Near(t.Range,ranges[i]*Tile,keys[i]+" range");Near(t.Reload,reload[i],keys[i]+" ROF");Near(t.Seconds,cost[i]*.042f,keys[i]+" production");}
  var w=BalanceWorld();var rifle=w.Spawn("squad",0,800,1100);var tank=w.Spawn("tank",1,860,1100);
  w.Fire(rifle,tank);Near(tank.T.Hp-tank.Hp,3.75f,"Rifle vs heavy armor");
  var at=w.Spawn("at",0,800,1160);tank.Hp=tank.T.Hp;w.ToggleInfantry(at);w.TickInfantryStance(at,StanceSeconds);w.Fire(at,tank);Near(tank.T.Hp-tank.Hp,40,"Missile vs heavy armor");
  var target=w.Spawn("squad",1,900,1250);tank.P=new PointF(840,1250);tank.Cool=0;w.Fire(tank,target);Near(target.T.Hp-target.Hp,16.25f,"AP vs infantry");
  var tower=w.Spawn("post",0,1000,1200);target.Hp=target.T.Hp;w.Fire(tower,target);Near(target.T.Hp-target.Hp,50,"Sentry vs infantry");
  tank.Hp=tank.T.Hp;w.Fire(tower,tank);Near(tank.T.Hp-tank.Hp,12.5f,"Sentry vs heavy armor");
  w=BalanceWorld();var car=w.Spawn("ifv",0,800,1100);tank=w.Spawn("tank",1,860,1100);w.Fire(car,tank);
  Near(tank.Hp,291.25f,"First missile");w.TickBurst(car,.19f);Near(tank.Hp,291.25f,"Second missile not immediate");w.TickBurst(car,.011f);Near(tank.Hp,282.5f,"Second missile");w.TickBurst(car,1);Near(tank.Hp,282.5f,"No third missile");
  tank.Hp=1000;w.Fire(car,tank);w.Halt(car);w.TickBurst(car,1);Near(tank.Hp,991.25f,"Stop cancels pending volley");
  var arty=w.Spawn("arty",0,1000,1100);tank.P=new PointF(1030,1100);BalanceAssert(!CanShootAt(arty,tank),"Artillery minimum range");
  arty.Target=tank;BalanceAssert(w.FindFiringPosition(arty,tank.P),"Can reposition from minimum range");BalanceAssert(Dist(arty.Path.Last(),tank.P)>=48,"Firing position outside dead zone");
  w=BalanceWorld();tower=w.Spawn("post",0,800,1100);tank=w.Spawn("tank",1,1000,1100);
  foreach(var edge in ReferenceBalance.Circle(tower.P,tower.T.Range)){
   Near(Dist(tower.P,edge),132,"Continuous circle radius");
   float dx=edge.X-tower.P.X,dy=edge.Y-tower.P.Y;tank.P=new PointF(tower.P.X+dx*.999f,tower.P.Y+dy*.999f);BalanceAssert(CanShootAt(tower,tank),"Inside circle fires at every bearing");
   tank.P=new PointF(tower.P.X+dx*1.001f,tower.P.Y+dy*1.001f);BalanceAssert(!CanShootAt(tower,tank),"Outside circle cannot fire at any bearing");
  }
  Near(Sight(tower),168,"Turret sight distinct from range");Near(Sight(tank),192,"Tank sight");
  w=BalanceWorld();tank=w.Spawn("tank",0,800,1100);tank.Turn=4;tank.Dir=4;var start=tank.P;
  for(int i=0;i<20;i++)w.DriveTracks(tank,new PointF(1200,1100),tank.T.Speed,.05f);
  Near(Dist(start,tank.P),23.90625f,"Measured tank travel in one game second",.02f);
  w=BalanceWorld();var plant=w.Spawn("power",0,800,900);var factory=w.Spawn("factory",0,1000,900);var refinery=w.Spawn("refinery",0,1200,900);
  Near(w.Power(0),125,"Electrical units");Near(w.ProductionRate(factory),1,"Full power production");plant.Hp=plant.T.Hp*.25f;Near(w.ProductionRate(factory),2/3f,"Low power follows available capacity");plant.Hp=1;Near(w.ProductionRate(factory),.5f,"Low power minimum");
  plant.Hp=plant.T.Hp;var second=w.Spawn("factory",0,1000,1200);w.Funds[0]=5000;
  var tankOrder=Info("tank");Near(w.ProductionRate(factory),1.25f,"Second factory shortens the shared order");
  BalanceAssert(w.Train(tankOrder,0,factory)&&w.Train(tankOrder,0,second),"Queue two tanks");
  w.UnitTick(factory,tankOrder.Seconds*.8f,true);w.UnitTick(second,tankOrder.Seconds*.8f,true);
  BalanceAssert(w.Live(0).Count(u=>u.T.Key=="tank")==1&&factory.Queue.Count==1&&second.Queue.Count==0,"One tank per order");
  BalanceAssert(w.Funds[0]==4300,"First of two queued tanks costs 700");
  BalanceAssert(LoadValue==1000,"40 ore bales at 25 credits");var patch=new OrePatch(0,0,1200);float extracted=patch.Extract(1000);Near(extracted,1000,"Load extraction");Near(patch.Stock,200,"Resource conservation");
  w=BalanceWorld();car=w.Spawn("ifv",0,800,1100);var engineer=w.Spawn("engineer",0,800,1100);engineer.Carrier=car;tank=w.Spawn("tank",0,830,1100);tank.Hp=100;
  BalanceAssert(!w.Armed(car),"Repair IFV unarmed");w.RepairOrder(car,tank);w.TickRepairVehicle(car,.1f);Near(tank.Hp,150,"Repair 50");Near(car.Cool,80/15f,"Repair interval");
  if(output!=null){Directory.CreateDirectory(output);var lines=new[]{"单位,规则条目,价格,生命,护甲,Speed,格每游戏秒,视野格,射程格,伤害每发,连射发数,间隔游戏秒,标准速度间隔实秒,生产游戏秒,电力"}.Concat(Types.Select(t=>string.Join(",",new[]{t.Name,t.RuleId,t.Price.ToString(),t.Hp.ToString(),t.Armor.ToString(),t.SpeedRating.ToString(),(t.Speed/Tile).ToString("0.####"),t.SightCells.ToString(),(t.Range/(float)Tile).ToString("0.##"),t.Hit.ToString(),(t.Weapon==null?0:t.Weapon.Burst).ToString(),t.Reload.ToString("0.####"),(t.Reload/MatchTempo.DefaultRate).ToString("0.####"),t.Seconds.ToString("0.##"),t.Energy.ToString()}))).ToArray();File.WriteAllLines(Path.Combine(output,"applied-values.csv"),lines,System.Text.Encoding.UTF8);}
  Console.WriteLine("PASS: numeric references, time/distance conversion, armor roles, burst timing/cancel, dead zone reposition, 128-angle actual range boundary, sight, movement, power, paid shared vehicle production, ore conservation and repair IFV.");
 }
}}
