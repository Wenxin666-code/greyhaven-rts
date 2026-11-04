using System;
using System.Linq;
using System.Drawing;
using Greyhaven;
class CampaignChecks {
 static void Check(bool ok,string description){if(!ok)throw new Exception(description);}
 static void Wait(Campaign w,float seconds){for(int i=0;i<Math.Ceiling(seconds*10);i++)w.Tick(.1f);}
 static Piece Begin(Campaign w){var m=w.Live(0).First(p=>p.T.Key=="mcv");Check(w.Deploy(m),"deploy");Wait(w,6);return m;}
 static Piece Build(Campaign w,string k,int x,int y){Check(w.Build(Campaign.Info(k),new PointF(x,y)),k+" / "+w.Radio);Wait(w,Campaign.Info(k).Seconds+1);return w.Live(0).Last(p=>p.T.Key==k);}
 static void Play(bool armor){
  var w=new Campaign();Begin(w);
  var foot=w.Live(0).Where(p=>p.T.Foot).ToArray();
  Check(w.Move(foot[0],w.Sites[0].P,true),"path west");Check(w.Move(foot[1],w.Sites[1].P,true),"path middle");
  Build(w,"power",260,550);Build(w,"refinery",66,450);Build(w,"barracks",120,610);Build(w,"factory",342,551);
  Check(!w.Build(Campaign.Info("power"),new PointF(260,550)),"overlap blocked");
  Check(w.Sites[0].Owner==0&&w.Sites[1].Owner==0,"capture both objectives");
  for(int i=0;i<450&&w.Funds[0]<260;i++)w.Tick(.1f);
  w.Upgrade();Check(w.Upgraded[0],"upgrade after ore delivery");
  for(int i=0;i<450&&w.Funds[0]<195;i++)w.Tick(.1f);
  Check(w.Train(Campaign.Info("miner"),0,null),"add a second miner");
  int order=0;string[] list=armor?new[]{"tank","engineer","ifv","squad","tank","at"}:new[]{"ifv","squad","at","arty","engineer","squad"};
  while(!w.Won&&!w.Lost&&w.Time<560){
   string k=list[order%list.Length];if(w.Train(Campaign.Info(k),0,null))order++;
   if(w.Time>(armor?107:95)){
    int i=0;foreach(var o in w.Live(0).Where(o=>!o.T.Building&&o.T.Key!="miner").ToArray()){
     var p=w.Sites[2].P;
     if(o.T.Key=="arty")p=new PointF(w.Sites[2].P.X-146,319);
     else p=new PointF(p.X+(i%4-1)*12,p.Y+(i/4%4-1)*13);
     if(Campaign.Dist(o.P,p)>55&&o.Path.Count==0)w.Move(o,p,true);
     i++;
    }
   }
   Wait(w,2);
  }
  Console.WriteLine((armor?"Armour":"Infantry/artillery")+" win="+w.Won+" at "+(int)w.Time+"s, recruited="+order+" hold="+(int)w.Hold);
  Check(w.Won,"mission needs to be winnable");
 }
 static void Training(){
  var w=new Campaign(true);Wait(w,600);Check(w.Lesson==0&&w.Live(1).Count()==1&&!w.Lost,"no time pressure while learning");Begin(w);
  Build(w,"power",260,550);Check(w.LessonKey=="refinery","refinery lesson");
  Build(w,"refinery",66,450);Wait(w,30);Check(w.LessonKey=="barracks"&&w.OreReceipts[0]>0,"earn the first delivery");
  var b=Build(w,"barracks",120,610);Check(w.LessonKey=="squad","construction completes before teaching production");
  Check(w.Train(Campaign.Info("squad"),0,b),"squad queued");Check(w.LessonKey=="squad","order alone is not graduation");Wait(w,6);
  var foot=w.LastReady;Check(w.LessonKey=="west"&&foot.T.Key=="squad","delivery");
  Check(w.Move(foot,w.Sites[0].P,true),"reach west");Wait(w,40);Check(w.LessonKey=="factory","capture advances tutorial");
  var f=Build(w,"factory",342,551);w.Train(Campaign.Info("ifv"),0,f);Wait(w,9);Check(w.LessonKey=="port","vehicle delivered");
  foreach(var u in w.Live(0).Where(o=>!o.T.Building&&o.T.Key!="miner").ToArray())w.Move(u,new PointF(w.Sites[2].P.X+(u.Id%3)*12,w.Sites[2].P.Y+u.Id%2*16),true);
  Wait(w,110);Check(w.Won&&w.LessonKey=="hold","full tutorial ends at extraction");Console.WriteLine("All twelve tutorial milestones passed, including a ten-minute idle start.");
 }
 static void Navigation(){
  var w=new Campaign(true);var mover=w.Spawn("tank",0,300,443);var dest=new PointF(500,443);
  Check(w.Move(mover,dest,false)&&mover.Path.Count==1,"clear ground is one straight segment");
  var obs=w.Spawn("factory",0,395,444);var box=Campaign.Footprint(obs.T,obs.P);Check(box.Width==96&&box.Height==72&&box.Left%24==0&&box.Top%24==0,"factory footprint aligned");
  var expanded=box;expanded.Inflate(10,10);int bends=0;
  for(int i=0;i<180;i++){w.Tick(.1f);bends=Math.Max(bends,mover.Path.Count);Check(!expanded.Contains(mover.P),"no penetration after new construction");}
  Check(bends>=2&&bends<=5&&Campaign.Dist(mover.P,dest)<1,"replan around existing walls then arrive");
  Check(!w.Move(mover,new PointF(39,30),false),"water blocked");
  var barracks=w.Spawn("barracks",0,480,564);var gate=Campaign.Gate(Campaign.Footprint(barracks.T,barracks.P));var block=new System.Collections.Generic.List<Piece>();
  foreach(float dx in new[]{0f,-.31f,.31f})block.Add(w.Spawn("squad",0,barracks.P.X+dx*gate.Width,gate.Bottom-6));
  for(int i=0;i<4;i++)Check(w.Train(Campaign.Info(i%2==0?"squad":"at"),0,barracks),"reserve infantry slot");
  Check(barracks.Berths.Distinct().Count()==4,"each paid order owns a distinct cell");
  var reserved=barracks.Berths[1];int cash=w.Funds[0];Check(w.CancelOrder(barracks,1)&&w.Funds[0]==cash+135&&!barracks.Berths.Contains(reserved),"refund and release exact cell");
  Check(w.QueueState(barracks).Contains("缺电"),"low power estimate");Wait(w,65);
  Check(barracks.Queue.Count==0&&barracks.Berths.Count==0&&w.InfantryDelivered==2,"infantry complete even with the old gate blocked");
  Check(!w.Move(block[0],Campaign.Middle(Campaign.Muster(Campaign.Footprint(barracks.T,barracks.P))),false),"paid assembly area is not a parking lot");
  Console.WriteLine("Reservation, blocked doorway, refund and staging checks passed.");
 }
 static void ReconAndMarch(){
  var w=new Campaign(true);var enemy=w.Live(1).First();Check(!w.Visible(enemy)&&Campaign.Info("scout")==null,"unexplored enemy and retired scout");
  var a=w.Spawn("ifv",0,350,680);var b=w.Spawn("squad",0,350,710);var goal=new PointF(650,695);
  Check(w.MarchTo(new[]{a,b},goal,true,true)==2&&a.Formation==b.Formation,"mixed march order");
  Check(Math.Abs(a.Formation.Speed(a)-b.T.Speed)<.01f&&Campaign.Dist(a.Goal,b.Goal)>20,"slowest speed and distinct slots");
  Wait(w,18);Check(Campaign.Dist(a.P,a.Goal)<2&&Campaign.Dist(b.P,b.Goal)<2,"mixed group arrived together");
  var initial=b.P;var sight=w.Spawn("engineer",0,enemy.P.X-125,enemy.P.Y);Wait(w,1);
  Check(w.Visible(enemy),"foot patrol opens shroud");w.Move(sight,initial,false);Wait(w,45);Check(w.Visible(enemy),"opened ground remains visible after withdrawal");
  var outpost=new PointF(1580,870);Check(w.Move(a,outpost,false),"extended southern flank navigable");Wait(w,55);Check(Campaign.Dist(a.P,outpost)<2,"expanded area reachable");
  var v1=w.Spawn("tank",0,300,810);var v2=w.Spawn("ifv",0,510,810);w.Move(v1,new PointF(510,810),false);w.Move(v2,new PointF(300,810),false);
  float min=999;for(int i=0;i<400;i++){w.Tick(.05f);min=Math.Min(min,Campaign.Dist(v1.P,v2.P));}
  Check(min>=15&&v1.P.X>504&&v2.P.X<306,"head-on vehicles must pass, not overlap");
  Console.WriteLine("Permanent shroud, formation pacing, expanded flank and head-on passing checks passed.");
 }
 static void InfantryWaves(){
  var w=new Campaign(true);Begin(w);var b=Build(w,"barracks",120,610);var lot=Campaign.Footprint(b.T,b.P);var yard=Campaign.Muster(lot);
  Check(!w.CanPlace(Campaign.Info("power"),Campaign.Middle(yard),null),"no adjacent building may occupy the mustering cells");
  for(int wave=0;wave<3;wave++){
   for(int i=0;i<4;i++)Check(w.Train(Campaign.Info("squad"),0,b),"four consecutive paid orders");
   Check(b.Berths.Distinct().Count()==4&&b.Berths.All(p=>yard.Contains(p)),"four reserved land cells");Wait(w,32);
   Check(b.Queue.Count==0&&b.Berths.Count==0&&w.InfantryDelivered==4*(wave+1),"batch must clear without player intervention");
  }Console.WriteLine("12 infantry squads delivered and rallied without manual gate clearing.");
 }
 static void HeadingChecks(){
  foreach(string k in new[]{"tank","ifv","arty","mcv"})for(int dir=0;dir<16;dir++){
   var w=new Campaign(true);var v=w.Spawn(k,0,700,730);double a=dir*Math.PI/8;var start=v.P;
   var end=new PointF(start.X+50*(float)Math.Sin(a),start.Y+50*(float)Math.Cos(a));
   Check(w.Move(v,end,false),"unobstructed movement");
   int steps=0;for(int i=0;i<110;i++){var prev=v.P;w.Tick(.025f);if(Campaign.Dist(prev,v.P)<.005f)continue;steps++;
    {float dx=v.P.X-prev.X,dy=v.P.Y-prev.Y;bool axis=Math.Abs(dx)<.0002||Math.Abs(dy)<.0002||Math.Abs(Math.Abs(dx)-Math.Abs(dy))<.0002;
     Check(axis&&v.Dir%2==0,"tank off axis "+dir+" d="+dx+","+dy+" at "+v.P+" path "+String.Join(" ",v.Path)+" frame "+v.Dir);double desired=(Math.Atan2(dx,dy)*8/Math.PI+16)%16;Check(Math.Abs((v.Dir-desired+24)%16-8)<.02,"tracks versus velocity");}
    }
   Check(steps>0&&Campaign.Dist(v.P,end)<.3f,"depart and reach arbitrary point: "+k+" / "+dir);
  }Console.WriteLine("16 destinations per vehicle verified, with eight legal travel bearings on all four vehicles.");
 }
 static void StreetChecks(){
  foreach(float dt in new[]{1f/30,1f/60,1f/120}){
   var w=new Campaign(true);var t=w.Spawn("tank",0,650,360);
   Check(w.Move(t,new PointF(714,365),false)&&t.Path.Peek().Y==360,"keep the main leg parallel to the kerb");
   foreach(var goal in new[]{new PointF(767,481),new PointF(660,359)}){
    var old=t.P;Check(w.Move(t,goal,false)&&t.Path.Count==2,"turn at actual street intersection");
    var corner=t.Path.Peek();Check(Campaign.Roads[0].Contains(corner)&&Campaign.Roads[1].Contains(corner),"junction bounds");
    for(int i=0;i<20/dt&&t.Path.Count>0;i++){
     w.Tick(dt);Check(Campaign.Roads[0].Contains(t.P)||Campaign.Roads[1].Contains(t.P),"cut through a block");
     if(Campaign.Dist(t.P,old)>.001f)Check((t.Dir%4)==0,"street-axis frame");old=t.P;
    }
    Check(Campaign.Dist(t.P,goal)<.02f,"missed street destination");
   }
  }Console.WriteLine("Street, junction and return drives passed at 30, 60 and 120 updates/s.");
 }
 static void ContactChecks(){
  var w=new Campaign(true);var unit=w.Spawn("tank",0,380,770);
  var building=w.Spawn("power",1,504,768);var goal=new PointF(680,855);
  Check(w.Move(unit,goal,false),"ordinary march accepted");Wait(w,.4f);
  Check(unit.Target==building&&unit.ResumeMarch,"march locks enemy structure outside firing range");
  Wait(w,3);Check(building.Hp<building.T.Hp,"advance into range and fire at structure");
  building.Hp=0;Wait(w,18);Check(Campaign.Dist(unit.P,goal)<1,"resume interrupted order");
  w=new Campaign(true);var foot=w.Spawn("squad",0,400,750);var gun=w.Spawn("arty",1,612,750);
  w.Attack(gun,foot);w.Spawn("engineer",1,400,920);gun.Cool=.05f;
  w.Tick(.1f);Check(foot.Target==gun&&foot.Contact==ContactOrder.ReturnFire&&!w.Explored(0,gun.P),"retaliate to distant unseen shot");
  gun.Cool=100;float hp=gun.Hp;Wait(w,8);
  Check(gun.Hp<hp&&Campaign.Dist(foot.P,gun.P)<=foot.T.Range,"close distance and return fire");
  var at=foot.P;Wait(w,.5f);Check(Campaign.Dist(at,foot.P)<.01f,"stop within weapon range");
  w=new Campaign(true);var tower=w.Spawn("post",0,408,756);gun=w.Spawn("arty",1,610,755);var pos=tower.P;
  w.Spawn("engineer",1,445,755);w.Attack(gun,tower);Wait(w,1);
  Check(tower.P==pos&&tower.Target!=gun,"stationary gun cannot chase");
  Console.WriteLine("Moving acquisition, structure attack, resuming orders and out-of-sight return fire passed.");
 }
 static void OreChecks(){
  var w=new Campaign(true);
  foreach(var fresh in new[]{w,new Campaign(),new Campaign(false,true)})Check(!fresh.Units.Any(o=>o.T.Key=="miner"||o.T.Key=="refinery"),"neither side starts with mining assets");
  Begin(w);Check(!w.Build(Campaign.Info("refinery"),new PointF(66,450)),"power prerequisite");
  Build(w,"power",260,550);var cash=w.Funds[0];Check(w.Build(Campaign.Info("refinery"),new PointF(66,450)),"pay to build");
  Check(w.Funds[0]==cash-390&&!w.Live(0).Any(o=>o.T.Key=="miner"),"no truck before completion");
  Wait(w,10);var truck=w.Live(0).Single(o=>o.T.Key=="miner");
  var plant=w.Live(0).Single(o=>o.T.Key=="refinery");w.Halt(truck);
  w.Sites[0].Owner=w.Sites[1].Owner=0;int start=w.Funds[0];Wait(w,18);
  Check(w.Funds[0]==start&&w.OreReceipts[0]==0,"no passive money from HQ, objectives or parked vehicle");
  w.MineOrder(truck,null,null);for(int i=0;i<350&&truck.Cargo<Campaign.LoadValue;i++)w.Tick(.1f);
  Check(truck.Cargo==Campaign.LoadValue&&w.Funds[0]==start,"cargo stays on truck");
  for(int i=0;i<180&&w.Funds[0]==start;i++)w.Tick(.1f);
  Check(w.Funds[0]==start+Campaign.LoadValue&&truck.Cargo==0,"one docking payment");
  var mine=w.Spawn("miner",0,93,588);int before=w.OreReceipts[0];Wait(w,60);
  Check(w.OreReceipts[0]>=before+4*Campaign.LoadValue,"two vehicles cycle through one dock");
  w.Halt(truck);w.Halt(mine);mine.Cargo=70;w.MineOrder(mine,null,plant);plant.Hp=0;
  before=w.OreReceipts[0];Wait(w,6);Check(mine.Cargo==70&&w.OreReceipts[0]==before,"no deposit at destroyed refinery");
  var choice=(from x in Enumerable.Range(4,12) from y in Enumerable.Range(18,9)
   let pos=new PointF(x*24,y*24) where w.CanPlace(Campaign.Info("refinery"),pos,null) orderby Campaign.Dist(pos,plant.P) select pos).First();
  var replacement=Build(w,"refinery",(int)choice.X,(int)choice.Y);Wait(w,25);
  Check(replacement.StarterDelivered&&w.Live(0).Count(o=>o.T.Key=="miner")==3&&w.OreReceipts[0]>=before+70,"replacement dock and one starter");
  var enemy=new Campaign();Wait(enemy,48);Check(enemy.OreReceipts[1]>0,"enemy must deliver ore too");
  Console.WriteLine("Ore extraction, interrupted work, no passive income, shared dock, replacement and enemy deliveries passed.");
 }
 static void ProspectChecks(){
  var w=new Campaign(true);w.Ore.Clear();var hidden=new OrePatch(490,770,182);
  w.Ore.Add(hidden);var plant=w.Spawn("refinery",0,390,592);w.Tick(.1f);var truck=w.Live(0).Single(o=>o.T.Key=="miner");
  Check(!w.Explored(0,hidden.P),"test vein initially hidden");w.Tick(.1f);
  Check(truck.Surveying&&truck.Path.Count>0&&truck.Patch==null,"leave dock to explore without targeting unseen ore");
  for(int i=0;i<4000&&w.OreReceipts[0]==0;i++){
   w.Tick(.1f);if(truck.Patch!=null)Check(w.Explored(0,truck.Patch.P),"target must be revealed");
  }
  Check(plant.LastReceipt==180&&hidden.Stock<3,"discover, collect and bank from a hidden field");
 }
 static void WarehouseCapture(){
  var w=new Campaign(true);var site=w.Sites[0];var original=(int[])w.Funds.Clone();
  foreach(var yard in w.Sites.Take(2))Check(yard.Body.X%Campaign.Tile==0&&yard.Body.Y%Campaign.Tile==0&&!Campaign.Roads.Any(r=>r.IntersectsWith(yard.Body)),"warehouse off road and on grid");
  var foot=w.Spawn("squad",0,site.P.X,site.P.Y);Wait(w,8);
  Check(site.Owner==0&&w.Funds.SequenceEqual(original),"entry and capture must not pay");
  foot.P=new PointF(260,580);w.Spawn("squad",1,site.P.X,site.P.Y);Wait(w,20);
  Check(site.Owner==1&&w.Funds.SequenceEqual(original),"holding and recapturing must not pay");
 }
 static void MinerTraffic(){
  var w=new Campaign(true);w.Ore.Clear();var patch=new OrePatch(450,810,6000);w.Ore.Add(patch);
  var plant=w.Spawn("refinery",0,374,670);Wait(w,1);var first=w.Live(0).Single(o=>o.T.Key=="miner");
  var gate=Campaign.DockPoint(plant);w.Spawn("mcv",0,gate.X,gate.Y);w.Spawn("mcv",0,patch.P.X,patch.P.Y);
  var second=w.Spawn("miner",0,500,785);w.MineOrder(first,patch,null);w.MineOrder(second,patch,null);
  int a=0,b=0;for(int i=0;i<1700;i++){
   float ca=first.Cargo,cb=second.Cargo;w.Tick(.1f);
   if(ca>0&&first.Cargo==0)a++;if(cb>0&&second.Cargo==0)b++;
  }
  Check(a>=2&&b>=2,"each miner keeps delivering despite blocked centers: "+a+" / "+b);
  Console.WriteLine("Blocked-center traffic: two miners delivered "+a+" and "+b+" loads in 170s.");
  float total=patch.Stock+first.Cargo+second.Cargo+w.OreReceipts[0];
  Check(Math.Abs(total-patch.Initial)<2,"ore conservation with alternate berths");
  Check(w.OreReceipts[0]>=Campaign.LoadValue*4,"blocked dock and ore center must still allow repeated shipments: "+w.OreReceipts[0]);
 }
 static void MinerMidRouteBlock(){
  var w=new Campaign(true);var plant=w.Spawn("refinery",0,360,655);Wait(w,1);
  var truck=w.Live(0).Single(u=>u.T.Key=="miner");truck.Cargo=Campaign.LoadValue;
  truck.P=new PointF(550,738);w.MineOrder(truck,null,plant);w.Tick(.1f);
  var destination=truck.WorkPoint;
  w.Spawn("mcv",0,destination.X,destination.Y);Wait(w,35);
  Check(w.OreReceipts[0]>=Campaign.LoadValue,"blocked destination after dispatch: release and reroute");
 }
 static void EnemySupplyChain(){
  var w=new Campaign();Check(w.Live(1).Single().T.Key=="mcv"&&!w.Upgraded[1],"enemy starts with only a construction vehicle");
  // Shut off mining to prove the first combat order depends on physical delivery.
  for(int i=0;i<700&&!w.Live(1).Any(o=>o.T.Key=="miner");i++)w.Tick(.1f);
  var truck=w.Live(1).Single(o=>o.T.Key=="miner");w.Halt(truck);int cash=w.Funds[1];
  Check(cash==1150-Campaign.Info("power").Price-Campaign.Info("refinery").Price,"enemy pays for first structures");
  Wait(w,90);Check(w.Funds[1]==cash&&w.OreReceipts[1]==0&&!w.Live(1).Any(o=>o.T.Hit>0||o.Queue.Count>0),"without ore no troops or orders");
  w.MineOrder(truck,null,null);bool infantry=false,armor=false;
  for(int i=0;i<3900;i++){w.Tick(.1f);infantry|=w.Live(1).Any(o=>o.T.Foot);armor|=w.Live(1).Any(o=>o.T.Key=="tank");if(infantry&&armor)break;}
  Check(w.OreReceipts[1]>0&&infantry&&armor&&w.Upgraded[1],"supply chain: "+w.Funds[1]+" receipts "+w.OreReceipts[1]+" T "+w.Time+" cargo "+truck.Cargo+" path "+truck.Path.Count+" "+String.Join(" ",w.Live(1).Select(o=>o.T.Key+"@"+o.P)));
  Console.WriteLine("Enemy bootstrap: interrupted income stopped recruitment; mining resumed and funded infantry and armor.");
 }
 static int Main(){try{Run();return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
 static void Run(){MinerMidRouteBlock();EnemySupplyChain();MinerTraffic();WarehouseCapture();ProspectChecks();OreChecks();ContactChecks();StreetChecks();InfantryWaves();HeadingChecks();ReconAndMarch();Navigation();Training();Play(true);Play(false);var w=new Campaign();var h=Begin(w);var enemy=w.Spawn("tank",1,h.P.X+82,h.P.Y);w.Attack(enemy,h);Wait(w,190);Check(w.Lost,"base loss");
  w=new Campaign();var car=w.Spawn("ifv",0,212,431);w.Load(car);Check(w.Units.Any(o=>o.Carrier==car),"embark");w.Load(car);Check(!w.Units.Any(o=>o.Carrier==car),"disembark");
  Console.WriteLine("All checks passed.");}
}
