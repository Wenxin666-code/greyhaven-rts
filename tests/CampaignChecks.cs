using System;
using System.Linq;
using System.Drawing;
using Greyhaven;
class CampaignChecks {
 static void Check(bool ok,string description){if(!ok)throw new Exception(description);}
 static void Wait(Campaign w,float seconds){for(int i=0;i<Math.Ceiling(seconds*10);i++)w.Tick(.1f);}
 static Piece Begin(Campaign w){var m=w.Live(0).First(p=>p.T.Key=="mcv");Check(w.Deploy(m),"deploy");Wait(w,6);return m;}
 static Piece Build(Campaign w,string k,int x,int y){Check(w.StartConstruction(Campaign.Info(k)),"start "+k);for(int i=0;i<1200&&!w.Construction(Campaign.Info(k)).Ready;i++)w.Tick(.1f);Check(w.Build(Campaign.Info(k),new PointF(x,y)),k+" / "+w.Radio);Wait(w,Campaign.AssemblySeconds+.2f);return w.Live(0).Last(p=>p.T.Key==k);}
 static void Play(bool armor){
  var w=new Campaign();Begin(w);
  Build(w,"power",260,550);Build(w,"refinery",66,450);Build(w,"barracks",120,610);Build(w,"factory",342,551);
  Check(!w.Build(Campaign.Info("power"),new PointF(260,550)),"overlap blocked");
  for(int i=0;i<450&&w.Funds[0]<260;i++)w.Tick(.1f);
  w.Upgrade();Check(w.Upgraded[0],"upgrade after ore delivery");
  for(int i=0;i<450&&w.Funds[0]<195;i++)w.Tick(.1f);
  Check(w.Train(Campaign.Info("miner"),0,null),"add a second miner");
  int order=0;string[] list=armor?new[]{"tank","engineer","ifv","squad","tank","at"}:new[]{"ifv","squad","at","arty","engineer","squad"};
  while(!w.Won&&!w.Lost&&w.Time<560){
   string k=list[order%list.Length];if(w.Train(Campaign.Info(k),0,null))order++;
   // With progressive funding, the infantry/artillery plan assembles its first battery before assault.
   if(w.Time>(armor?107:165)){
    int i=0;foreach(var o in w.Live(0).Where(o=>!o.T.Building&&o.T.Key!="miner").ToArray()){
     var enemy=w.Units.Where(e=>e.Side==1&&e.Alive&&e.Carrier==null&&w.Visible(e)).OrderBy(e=>Campaign.Dist(o.P,e.P)).FirstOrDefault();
     if(o.T.Hit>0&&enemy!=null){if(o.Target==null)w.Attack(o,enemy);}
     else if(o.Path.Count==0){
      // Sweep the expanded base approach, then its southern ore route for survivors.
      var p=w.Time<260?new PointF(Campaign.EnemyHome.X-170+i%4*15,Campaign.EnemyHome.Y+100+i/4%4*16)
       :new PointF(Campaign.EnemyHome.X-90+i%4*20,Campaign.EnemyHome.Y+350+i/4%4*16);
      // Construction can now extend from other buildings and occupy old scripted waypoints.
     for(int attempt=0;attempt<25;attempt++){
      double a=attempt*Math.PI/4;float radius=attempt==0?0:24*(1+(attempt-1)/8);
      var destination=new PointF(p.X+(float)Math.Cos(a)*radius,p.Y+(float)Math.Sin(a)*radius);
      if(w.CanMoveTo(o,destination)&&w.Move(o,destination,true))break;
     }
     }
     i++;
    }
   }
   Wait(w,2);
  }
  Console.WriteLine((armor?"Armour":"Infantry/artillery")+" win="+w.Won+" at "+(int)w.Time+"s, recruited="+order+" remaining="+w.Remaining(1));
  if(!w.Won)Console.WriteLine(String.Join("\n",w.Live(0).Where(u=>!u.T.Building&&u.T.Key!="miner").Take(18).Select(u=>u.T.Key+" "+u.P+" path="+u.Path.Count+" target="+(u.Target==null?"-":u.Target.T.Key+"@"+u.Target.P)))+"\nEnemy "+String.Join(" ",w.Live(1).GroupBy(u=>u.T.Key).Select(g=>g.Key+"="+g.Count())));

  Check(w.Won,"mission needs to be winnable");
 }
 static void Training(){
  var w=new Campaign(true);Wait(w,600);Check(w.Lesson==0&&w.Live(1).Count()==1&&!w.Lost,"no time pressure while learning");Begin(w);
  Build(w,"power",260,550);Check(w.LessonKey=="refinery","refinery lesson");
  Build(w,"refinery",66,450);Wait(w,30);Check(w.LessonKey=="barracks"&&w.OreReceipts[0]>0,"earn the first delivery");
  var b=Build(w,"barracks",120,610);Check(w.LessonKey=="squad","construction completes before teaching production");
  Check(w.Train(Campaign.Info("squad"),0,b),"squad queued");Check(w.LessonKey=="squad","order alone is not graduation");Wait(w,6);
  var foot=w.LastReady;Check(w.LessonKey=="factory"&&foot.T.Key=="squad","delivery");

  var f=Build(w,"factory",342,551);w.Train(Campaign.Info("ifv"),0,f);Wait(w,9);Check(w.LessonKey=="eliminate","vehicle delivered");
  foreach(var u in w.Live(0).Where(o=>!o.T.Building&&o.T.Key!="miner").ToArray())w.Move(u,new PointF(w.Harbor.P.X+(u.Id%3)*12,w.Harbor.P.Y+u.Id%2*16),true);
  Wait(w,110);Check(w.Won&&w.Remaining(1)==0,"full tutorial ends at extraction");Console.WriteLine("All ten tutorial milestones passed, including a ten-minute idle start.");
 }
 static void Navigation(){
  var w=new Campaign(true);var mover=w.Spawn("tank",0,300,443);var dest=new PointF(500,443);
  Check(w.Move(mover,dest,false)&&mover.Path.Count==1,"clear ground is one straight segment");
  var obs=w.Spawn("factory",0,395,444);var box=Campaign.Footprint(obs.T,obs.P);Check(box.Width==72&&box.Height==48&&box.Left%24==0&&box.Top%24==0,"compact factory footprint aligned");
  var expanded=box;expanded.Inflate(mover.T.Clearance-.1f,mover.T.Clearance-.1f);int bends=0;
  for(int i=0;i<180;i++){w.Tick(.1f);bends=Math.Max(bends,mover.Path.Count);Check(!expanded.Contains(mover.P),"no penetration after new construction");}
  Check(bends>=2&&bends<=5&&Campaign.Dist(mover.P,dest)<1,"replan around existing walls then arrive");
  Check(!w.Move(mover,new PointF(39,30),false),"water blocked");
  var barracks=w.Spawn("barracks",0,480,564);var gate=Campaign.Gate(Campaign.Footprint(barracks.T,barracks.P));var block=new System.Collections.Generic.List<Piece>();
  foreach(float dx in new[]{0f,-.31f,.31f})block.Add(w.Spawn("squad",0,barracks.P.X+dx*gate.Width,gate.Bottom-6));
  for(int i=0;i<4;i++)Check(w.Train(Campaign.Info(i%2==0?"squad":"at"),0,barracks),"reserve infantry slot");
  Check(barracks.Queue.Count==4,"four queued orders accepted without ground seats");
  int cash=w.Funds[0];Check(w.CancelOrder(barracks,1)&&w.Funds[0]==cash,"unstarted queue entry has no paid cost");
  Check(w.QueueState(barracks).Contains("缺电"),"low power estimate");Wait(w,65);
  Check(barracks.Queue.Count==0&&w.InfantryDelivered==2,"infantry waits for and uses the actual exit");
  Check(w.Move(block[0],Campaign.Middle(Campaign.ExitApron(barracks.T,Campaign.Footprint(barracks.T,barracks.P))),false),"exit apron is traversable, not forbidden parking");
  Console.WriteLine("Reservation, blocked doorway, refund and staging checks passed.");
 }
 static void ReconAndMarch(){
  var w=new Campaign(true);var enemy=w.Live(1).First();w.Spawn("mcv",1,1600,130);Check(!w.Visible(enemy)&&Campaign.Info("scout")==null,"unexplored enemy and retired scout");
  var a=w.Spawn("ifv",0,350,680);var b=w.Spawn("squad",0,350,710);var goal=new PointF(650,695);
  Check(w.MarchTo(new[]{a,b},goal,true,true)==2&&a.Formation==b.Formation,"mixed march order");
  Check(Math.Abs(a.Formation.Speed(a)-b.T.Speed)<.01f&&Campaign.Dist(a.Goal,b.Goal)>20,"slowest speed and distinct slots");
  Wait(w,18);Check(Campaign.Dist(a.P,a.Goal)<2&&Campaign.Dist(b.P,b.Goal)<2,"mixed group arrived together "+a.P+"/"+a.Goal+" path "+String.Join(";",a.Path)+" foot "+b.P+"/"+b.Goal+" path "+String.Join(";",b.Path));
  var initial=b.P;var sight=w.Spawn("engineer",0,enemy.P.X-125,enemy.P.Y);Wait(w,1);
  Check(w.Visible(enemy),"foot patrol opens shroud");w.Move(sight,initial,false);Wait(w,45);Check(w.Visible(enemy),"opened ground remains visible after withdrawal");
  var outpost=new PointF(1580,870);Check(w.Move(a,outpost,false),"extended southern flank navigable");Wait(w,55);Check(Campaign.Dist(a.P,outpost)<2,"expanded area reachable");
  var v1=w.Spawn("tank",0,300,810);var v2=w.Spawn("ifv",0,510,810);w.Move(v1,new PointF(510,810),false);w.Move(v2,new PointF(300,810),false);
  float min=999;for(int i=0;i<400;i++){w.Tick(.05f);min=Math.Min(min,Campaign.Dist(v1.P,v2.P));}
  Check(min>=v1.T.TrafficRadius+v2.T.TrafficRadius-.01f&&v1.P.X>504&&v2.P.X<306,"head-on vehicles must pass, not overlap: "+min+" / "+v1.P+" path "+String.Join(";",v1.Path)+" / "+v2.P+" path "+String.Join(";",v2.Path));
  Console.WriteLine("Permanent shroud, formation pacing, expanded flank and head-on passing checks passed.");
 }
 static void InfantryWaves(){
  var w=new Campaign(true);Begin(w);var b=Build(w,"barracks",120,610);var lot=Campaign.Footprint(b.T,b.P);var yard=Campaign.ExitApron(b.T,lot);
  Check(!w.CanPlace(Campaign.Info("power"),b.P,null),"actual building footprint remains blocked");
  for(int wave=0;wave<3;wave++){
   for(int i=0;i<4;i++)Check(w.Train(Campaign.Info("squad"),0,b),"four consecutive orders");
   Check(b.Queue.Count==4,"four orders need no artificial assembly cells");Wait(w,32);
   Check(b.Queue.Count==0&&w.InfantryDelivered==4*(wave+1),"batch must clear without player intervention");
  }Console.WriteLine("12 individual soldiers delivered and rallied without manual gate clearing.");
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
  Check(w.Move(unit,goal,true),"attack march accepted");Wait(w,.4f);
  Check(unit.Target==building&&unit.ResumeMarch,"attack march locks enemy structure outside firing range");
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
  Build(w,"power",260,550);var cash=w.Funds[0];Check(w.StartConstruction(Campaign.Info("refinery")),"start refinery");Wait(w,Campaign.Info("refinery").Seconds+.2f);Check(w.Build(Campaign.Info("refinery"),new PointF(66,450)),"place completed refinery");
  Check(w.Funds[0]==cash-390&&!w.Live(0).Any(o=>o.T.Key=="miner"),"no truck before completion");
  Wait(w,10);var truck=w.Live(0).Single(o=>o.T.Key=="miner");
  var plant=w.Live(0).Single(o=>o.T.Key=="refinery");w.Halt(truck);
  int start=w.Funds[0];Wait(w,18);
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
 static void ExhaustedFields(){
  var w=new Campaign(true);w.Ore.Clear();w.Ore.Add(new OrePatch(425,710,19));w.Ore.Add(new OrePatch(485,750,27));
  var plant=w.Spawn("refinery",0,390,600);Wait(w,140);
  Check(w.Ore.All(p=>p.Stock<=.01f&&w.OreAt(p.P)==null)&&w.OreReceipts[0]==46,"mine first vein, switch to next, unload both partial loads");
  int balance=w.Funds[0];Wait(w,50);Check(w.Funds[0]==balance&&w.OreReceipts[0]==46&&plant.LastReceipt>0,"exhausted deposits never regenerate or pay twice");
  Check(w.Sites.Single()==w.Harbor&&w.Harbor.Name=="港务中心","only the neutral harbor landmark remains");
  var scout=w.Live(0).First(u=>u.T.Foot);
  Check(w.Route(scout,new PointF(120,168))&&w.Route(scout,new PointF(600,432)),"former warehouse footprints are traversable");
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
  Check(a>=2&&b>=2,"each miner keeps delivering despite blocked centers: "+a+" / "+b+" "+String.Join(" | ",w.Live(0).Where(u=>!u.T.Building).Select(u=>u.T.Key+" "+u.P+" path="+String.Join(";",u.Path)+" dock="+u.DockReserved+" fail="+u.DockBlockedFor)));
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
 static void EliminationChecks(){
  var w=new Campaign(true);var guard=w.Live(1).Single();var structure=w.Spawn("factory",1,1030,565);structure.Ready=.02f;
  var miner=w.Spawn("miner",1,1200,400);w.Spawn("squad",0,w.Harbor.P.X,w.Harbor.P.Y);
  Wait(w,60);Check(!w.Won&&!w.Lost,"harbor alone never wins");
  guard.Hp=0;miner.Hp=0;w.Tick(.1f);Check(!w.Won&&w.Remaining(1)==1,"unfinished enemy structure counts");
  structure.Hp=0;w.Tick(.1f);Check(w.Won&&!w.Lost,"last enemy destroyed");
  w=new Campaign(true);w.Units[0].Hp=0;w.Tick(.1f);Check(!w.Lost,"surviving infantry stay in play");
  foreach(var o in w.Live(0).ToArray())o.Hp=0;w.Tick(.1f);Check(w.Lost&&!w.Won,"player elimination");
 }
 static void CrowdedOreRun(float dt){
  var w=new Campaign(true);w.Ore.Clear();var vein=new OrePatch(460,802,18000);w.Ore.Add(vein);
  w.Spawn("refinery",0,360,660);Wait(w,.5f);
  for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)w.Spawn("mcv",0,vein.P.X+12*x,vein.P.Y+12*y);
  w.Spawn("squad",0,vein.P.X,vein.P.Y);
  var trucks=new[]{w.Live(0).Single(u=>u.T.Key=="miner"),w.Spawn("miner",0,400,710),w.Spawn("miner",0,418,727),w.Spawn("miner",0,475,727)};
  var receipts=new int[trucks.Length];foreach(var u in trucks)w.MineOrder(u,vein,null);
  for(int i=0;i<(int)Math.Ceiling(180/dt);i++){
   var before=trucks.Select(t=>t.Cargo).ToArray();w.Tick(dt);
   for(int j=0;j<trucks.Length;j++)if(before[j]>0&&trucks[j].Cargo==0)receipts[j]++;
   Check(trucks.All(t=>t.Path.Count<40),"traffic detours must not accumulate repeated loops");
  }
  Console.WriteLine("Crowded mine shipments per truck: "+String.Join(", ",receipts));
  Check(receipts.All(n=>n>=2),"mine traffic: "+String.Join(" | ",trucks.Select(u=>u.P+" cargo="+u.Cargo+" returning="+u.Returning+" path="+String.Join(";",u.Path)+" goal="+u.WorkPoint+" turn="+u.Turn)));
  Check(Math.Abs(vein.Initial-vein.Stock-trucks.Sum(t=>t.Cargo)-w.OreReceipts[0])<4,"cargo conservation");
 }
 static void NarrowGate(){
  var w=new Campaign(true);var plant=w.Spawn("factory",0,520,650);var lot=Campaign.Footprint(plant.T,plant.P);
  var miner=w.Spawn("miner",0,lot.Left-35,lot.Top-6.5f);var goal=new PointF(lot.Right+28,lot.Top-6.5f);
  w.Halt(miner);Check(w.Move(miner,goal,false)&&miner.Path.Count==1,"miner fits past building edge");
  var tank=w.Spawn("tank",0,lot.Left-35,lot.Top-24);
  Check(!w.Move(tank,new PointF(plant.P.X,lot.Top-6.5f),false),"tank must still respect its wider hull");
  Wait(w,12);Check(Campaign.Dist(miner.P,goal)<.1f,"miner completed tight passage");
 }
 static void ExpandedMap(){
  foreach(var key in new[]{"squad","tank","miner","ifv"}){
   var w=new Campaign(true);var unit=w.Spawn(key,0,1640,920);w.Halt(unit);
   var goal=new PointF(Campaign.Width-100,Campaign.Height-120);
   Check(w.Move(unit,goal,false),"expanded region route "+key);Wait(w,80);
   Check(Campaign.Dist(unit.P,goal)<.02f&&unit.Path.Count==0,"expanded route arrival "+key);
   Check(w.Explored(0,goal),"fog extends to new region");
   Check(!w.Move(unit,new PointF(Campaign.Width+10,1320),false),"east map boundary");
   Check(!w.Move(unit,new PointF(2420,Campaign.Height+10),false),"south map boundary");
  }
  Console.WriteLine("Infantry and three vehicle classes crossed the old map bounds, arrived without overshoot and revealed the new terrain.");
 }
 static void ConvoyStress(){
  foreach(float dt in new[]{.05f,1f/60}){
   var w=new Campaign(true);var group=Enumerable.Range(0,20).Select(i=>w.Spawn(i%3==0?"ifv":"tank",0,380+i%5*32,940+i/5*32)).ToArray();
   w.Spawn("factory",0,780,1056);
   Check(w.MarchTo(group,new PointF(1120,1110),false,false)==group.Length,"all convoy routes assigned");
   double slowest=0;
   for(int frame=0;frame<(int)(70/dt);frame++){
    var timer=System.Diagnostics.Stopwatch.StartNew();w.Tick(dt);slowest=Math.Max(slowest,timer.Elapsed.TotalMilliseconds);
   }
   Check(group.All(u=>Campaign.Dist(u.P,u.Goal)<12),"convoy stuck: "+String.Join(";",group.Where(u=>Campaign.Dist(u.P,u.Goal)>=12).Select(u=>u.Id+" "+u.P+" -> "+u.Goal+" path="+u.Path.Count)));
   Console.WriteLine("20-vehicle convoy around factory arrived at {0} updates/s; max simulation step {1:F2}ms.",1/dt,slowest);
  }
 }
 static void DockQueueStress(){
  var w=new Campaign(true);w.Ore.Clear();
  // Ore is now occupied and harvested per ground cell, not from a wide ring
  // around one abstract pile. Give six trucks six actual working cells.
  for(int y=0;y<2;y++)for(int x=0;x<3;x++)w.Ore.Add(new OrePatch(480+x*24,790+y*24,25000f/6));
  var plant=w.Spawn("refinery",0,360,660);plant.StarterDelivered=true;
  var trucks=Enumerable.Range(0,6).Select(i=>w.Spawn("miner",0,280+i%3*32,780+i/3*32)).ToArray();
  foreach(var t in trucks){t.Cargo=180;w.MineOrder(t,null,plant);}
  var deliveries=new int[6];
  for(int frame=0;frame<3600;frame++){
   var cargo=trucks.Select(t=>t.Cargo).ToArray();w.Tick(.05f);
   Check(trucks.Count(t=>t.DockReserved&&t.Dock==plant)<=1,"one actual dock reservation per refinery");
   for(int i=0;i<6;i++)if(cargo[i]>0&&trucks[i].Cargo==0)deliveries[i]++;
  }
  Check(deliveries.All(n=>n>=3),"six miners starved in unloading queue: "+String.Join(",",deliveries)+" "+String.Join(" | ",trucks.Select(t=>t.Id+":"+w.MiningStatus(t)+" p="+t.P+" patch="+(t.Patch==null?"none":t.Patch.P.ToString())+" work="+t.WorkPoint+" route="+String.Join(";",t.Path))));
  plant.Hp=0;var replacement=w.Spawn("refinery",0,640,660);replacement.StarterDelivered=true;
  foreach(var t in trucks){t.Cargo=180;w.MineOrder(t,null,plant);}
  int before=w.OreReceipts[0];Wait(w,65);Check(w.OreReceipts[0]>=before+6*180,"queued trucks recover after refinery destruction");
  Console.WriteLine("Six miners shared one refinery without starvation: "+String.Join(",",deliveries)+" loads; replacement refinery recovery passed.");
 }
 static void FiringBoundary(){
  var w=new Campaign(true);var unit=w.Spawn("ifv",0,500,900);var near=w.Spawn("factory",1,620,900);var far=w.Spawn("squad",1,650,900);far.Cool=100;
  var start=unit.P;Check(w.Move(unit,new PointF(900,900),true),"attack move");Wait(w,.2f);
  Check(unit.Target==near&&near.Hp<near.T.Hp&&unit.P==start,"fire at reachable building edge before chasing distant armed unit");
  w=new Campaign(true);unit=w.Spawn("ifv",0,500,900);near=w.Spawn("mcv",1,620,900);w.Attack(unit,near);
  bool shot=false;for(int i=0;i<50;i++){w.Tick(.02f);if(near.Hp<near.T.Hp){shot=true;break;}}
  Check(shot&&Campaign.Dist(unit.P,near.P)>=unit.T.Range-3,"stop and fire at range instead of advancing to 78 percent range");
  Console.WriteLine("Immediate in-range acquisition, building-edge range, and stopping at the firing boundary passed.");
 }

 static Campaign EmptyBattle(){var w=new Campaign(true);w.Units.Clear();w.Spawn("mcv",0,120,200);w.Spawn("mcv",1,2400,1300);return w;}
 static void ClassicCombatChecks(){
  var w=EmptyBattle();var u=w.Spawn("squad",0,500,1000);var enemy=w.Spawn("mcv",1,550,990);enemy.Hp=10000;
  var goal=new PointF(900,1000);w.Move(u,goal,false);Wait(w,1);
  Check(u.Target==null&&u.Goal==goal&&enemy.Hp==10000&&u.P.X>540,"ordinary infantry move must not stop and chase");
  w.Move(u,goal,true);Wait(w,.3f);Check(u.Target==enemy&&enemy.Hp<10000,"attack move stops and shoots encountered target");
  enemy.Hp=0;Wait(w,12);Check(Campaign.Dist(u.P,goal)<1,"attack move resumes original destination");
  w=EmptyBattle();u=w.Spawn("tank",0,500,1000);enemy=w.Spawn("mcv",1,550,960);enemy.Hp=10000;
  w.Move(u,goal,false);Wait(w,1);Check(u.Target==null&&enemy.Hp<10000&&u.P.X>500,"tank opportunity fire preserves route");
  w=EmptyBattle();u=w.Spawn("ifv",0,500,1000);enemy=w.Spawn("mcv",1,650,1000);enemy.Hp=10000;
  var home=u.P;w.Guard(u);Wait(w,2);Check(u.Target==enemy&&Campaign.Dist(u.P,home)>20,"guard approaches within guard radius");
  enemy.P=new PointF(900,1000);Wait(w,8);Check(Campaign.Dist(u.P,home)<12&&u.Target==null,"guard must return when target leaves guarded area");
  Check(w.GuardAt(u,new PointF(550,1100)),"guard destination accepted");Wait(w,5);Check(!u.GuardTravelling&&u.GuardHome==new PointF(550,1100),"guard arrives and arms");
  enemy.Hp=0;var friend=w.Spawn("miner",0,600,1100);w.Halt(friend);Check(w.EscortUnit(u,friend),"escort accepted");w.Move(friend,new PointF(950,1100),false);Wait(w,12);
  Check(u.Escort==friend&&Campaign.Dist(u.P,friend.P)<95,"escort follows moving friendly: "+u.P+" vs "+friend.P);friend.Hp=0;Wait(w,2);Check(u.Escort==null,"escort handles destroyed target");
  w=EmptyBattle();u=w.Spawn("squad",0,500,1000);var points=new[]{new PointF(600,1000),new PointF(600,1100)};
  Check(w.SetWaypoints(u,points,false),"waypoints accepted");Wait(w,7);Check(Campaign.Dist(u.P,points[1])<1&&u.Waypoints.Count==0,"sequential waypoints finish");
  w.SetWaypoints(u,points,true);Wait(w,7);Check(u.Patrol&&u.Waypoints.Count==2,"patrol persists");w.Halt(u);Wait(w,.5f);Check(u.Waypoints.Count==0&&!u.Patrol&&u.Path.Count==0,"stop cancels patrol");
  w=EmptyBattle();u=w.Spawn("tank",0,500,1000);enemy=w.Spawn("squad",1,600,1000);enemy.Cool=100;w.ForceMove(u,new PointF(700,1000));Wait(w,6);Check(!enemy.Alive,"force-move tank crushes infantry");
  Console.WriteLine("Classic move/attack-move, opportunity fire, guard return, escort, patrol, stop and force-move passed.");
 }
 static void MixedCohesion(){
  foreach(float dt in new[]{.05f,1f/60}){
   var w=EmptyBattle();var troops=Enumerable.Range(0,12).Select(i=>w.Spawn(i%3==0?"squad":i%3==1?"tank":"ifv",0,450+i%4*32,950+i/4*32)).ToArray();
   var starts=troops.Select(u=>u.P).ToArray();Check(w.MarchTo(troops,new PointF(1050,1030),false,true)==12,"mixed routes");float spread=0;
   for(int n=0;n<(int)(9/dt);n++){w.Tick(dt);var progress=troops.Select((u,i)=>u.P.X-starts[i].X).ToArray();spread=Math.Max(spread,progress.Max()-progress.Min());}
   Check(spread<110,"mixed convoy progress spread: "+spread);Wait(w,30);Check(troops.All(u=>Campaign.Dist(u.P,u.Goal)<12),"mixed convoy arrival");
   Console.WriteLine("Mixed infantry/tank/IFV cohesion at {0} Hz: progress spread {1:F1} world units.",1/dt,spread);
  }
 }
 static void ReachabilityChecks(){
  var w=EmptyBattle();var u=w.Spawn("tank",0,1100,912);var goal=new PointF(1200,1100);w.Move(u,goal,false);var old=u.Path.ToArray();
  Check(w.CanMoveTo(u,new PointF(912,912)),"initial ground reachable");
  for(int i=0;i<4;i++){w.Spawn("power",1,840+i*48,840);w.Spawn("power",1,840+i*48,984);}
  for(int i=1;i<3;i++){w.Spawn("power",1,840,840+i*48);w.Spawn("power",1,984,840+i*48);}
  Check(!w.CanMoveTo(u,new PointF(912,912)),"enclosed open ground is unreachable");
  Check(!w.CanMoveTo(u,new PointF(840,840))&&!w.CanMoveTo(u,new PointF(300,50)),"building/water unreachable");
  Check(u.Goal==goal&&u.Path.SequenceEqual(old),"hover preview must not mutate active route");
  var timer=System.Diagnostics.Stopwatch.StartNew();for(int n=0;n<500;n++)w.CanMoveTo(u,new PointF(912,912));timer.Stop();
  var wall=w.Units.First(o=>o.T.Key=="power"&&o.P.X==984&&o.P.Y==888);wall.Hp=1;
  var gun=w.Spawn("arty",0,1100,880);w.Attack(gun,wall);w.Tick(.1f);Check(!wall.Alive,"destroy wall for topology change");
  Check(w.CanMoveTo(u,new PointF(912,912)),"destroying wall refreshes reachability");
  foreach(var p in new[]{new PointF(912,912),new PointF(720,1000),new PointF(840,840),new PointF(-10,500)}){
   var clone=new Piece{T=u.T,Hp=u.T.Hp,Side=u.Side,P=u.P};Check(w.CanMoveTo(u,p)==w.Move(clone,p,false),"preview/path agreement "+p);
  }
  Console.WriteLine("Reachability preview is read-only, detects sealed areas, updates after destruction; 500 cached queries {0:F1}ms.",timer.Elapsed.TotalMilliseconds);
 }
 static void FollowingChecks(){
  foreach(float dt in new[]{.05f,1f/60})foreach(int bearing in new[]{0,2,4,6,8,10,12,14}){
   var w=new Campaign(true);w.Units.RemoveAll(u=>u.Side==0);var group=new Piece[5];
   double a=bearing*Math.PI/8;float fx=(float)Math.Sin(a),fy=(float)Math.Cos(a);
   for(int i=0;i<group.Length;i++){var u=w.Spawn(i==0?"arty":"tank",0,1000-fx*i*32,700-fy*i*32);u.Turn=u.Dir=bearing;group[i]=u;
    Check(w.Move(u,new PointF(1000+fx*(240-i*32),700+fy*(240-i*32)),false),"convoy order");}
   float min=100;for(int n=0;n<18/dt;n++){w.Tick(dt);for(int i=1;i<group.Length;i++){
    float gap=Campaign.Dist(group[i-1].P,group[i].P);min=Math.Min(min,gap);Check(gap>=Campaign.Tile-.05f,"one-cell following gap "+gap);
   }}
   Check(group.All(u=>u.Path.Count==0),"following convoy arrived");
  }
  Console.WriteLine("Five-vehicle slow-leader convoy: eight bearings at 20/60 Hz retain one cell of headway and all arrive.");
  foreach(float dt in new[]{.05f,1f/60}){
   var w=new Campaign(true);w.Units.RemoveAll(u=>u.Side==0);var tanks=new Piece[6];
   for(int i=0;i<6;i++){var u=w.Spawn("tank",0,606-i%3*32,337+i/3*32);u.Turn=u.Dir=4;tanks[i]=u;Check(w.Move(u,new PointF(766+i/3*32,480-i%3*32),false),"intersection convoy order");}
   for(int n=0;n<40/dt;n++){
    w.Tick(dt);for(int i=0;i<6;i++)for(int j=i+1;j<6;j++)Check(!Campaign.TrafficContact(tanks[i].T,tanks[i].P,tanks[j].T,tanks[j].P),"intersection vehicle overlap");
   }
   Check(tanks.All(u=>u.Path.Count==0),"intersection convoy reaches goals beyond parked vehicles");
  }
  Console.WriteLine("Six-tank intersection: waiting, turning and final-destination replanning passed at 20/60 Hz without body overlap.");
 }
 static void TightCellsAndRecovery(){
  var w=EmptyBattle();var tanks=Enumerable.Range(0,9).Select(i=>w.Spawn("tank",0,800+i%3*32,1100+i/3*32)).ToArray();
  Check(w.MarchTo(tanks,new PointF(1200,1100),false,true)==9,"compact 3x3 formation orders");
  var goals=tanks.Select(u=>u.Goal).ToArray();
  Check(Math.Abs(goals.Max(p=>p.X)-goals.Min(p=>p.X)-48)<.01&&Math.Abs(goals.Max(p=>p.Y)-goals.Min(p=>p.Y)-48)<.01,"vehicle destinations occupy adjacent cells, not 42-unit gaps");
  for(int n=0;n<900;n++){w.Tick(.05f);for(int i=0;i<9;i++)for(int j=i+1;j<9;j++)Check(!Campaign.TrafficContact(tanks[i].T,tanks[i].P,tanks[j].T,tanks[j].P),"compact convoy cell penetration");}
  Check(tanks.All(u=>Campaign.Dist(u.P,u.Goal)<.2f),"compact convoy reaches all nine cells");
  w=EmptyBattle();w.Ore.Clear();var plant=w.Spawn("refinery",0,1200,1000);plant.StarterDelivered=true;
  var truck=w.Spawn("miner",0,500,1100);truck.Cargo=180;w.MineOrder(truck,null,plant);w.Tick(.1f);
  var points=new[]{truck.P}.Concat(truck.Path).ToArray();int segment=Enumerable.Range(0,points.Length-1).OrderByDescending(i=>Campaign.Dist(points[i],points[i+1])).First();
  var block=w.Spawn("mcv",0,(points[segment].X+points[segment+1].X)/2,(points[segment].Y+points[segment+1].Y)/2);
  for(int n=0;n<1200&&w.OreReceipts[0]==0;n++){w.Tick(.05f);Check(!Campaign.TrafficContact(truck.T,truck.P,block.T,block.P),"haul bypass must not cross blocker");}
  Check(w.OreReceipts[0]==180,"mid-route stopped vehicle must be bypassed with real delivery");
  foreach(bool alternate in new[]{true,false}){
   w=EmptyBattle();w.Ore.Clear();plant=w.Spawn("refinery",0,900,900);plant.StarterDelivered=true;
   Piece other=null;if(alternate){other=w.Spawn("refinery",0,1250,1100);other.StarterDelivered=true;}
   var dock=Campaign.DockPoint(plant);var blockers=new[]{-36f,0,36}.Select(x=>w.Spawn("mcv",1,dock.X+x,dock.Y)).ToArray();
   truck=w.Spawn("miner",0,780,1050);truck.Cargo=180;w.MineOrder(truck,null,plant);Wait(w,45);
   if(alternate)Check(other.LastReceipt==180&&plant.LastReceipt==0,"blocked refinery must switch to reachable friendly refinery");
   else{
    Check(w.OreReceipts[0]==0&&truck.Cargo==180,"sealed dock cannot teleport cargo or money");
    foreach(var blocker in blockers)blocker.Hp=0;Wait(w,35);Check(w.OreReceipts[0]==180,"opening dock must resume delivery without another player command");
   }
  }
  w=EmptyBattle();w.Funds[0]=5000;w.Spawn("hq",0,600,850);w.Spawn("power",0,470,950);var barracks=w.Spawn("barracks",0,600,1000);
  var apron=Campaign.ExitApron(barracks.T,Campaign.Footprint(barracks.T,barracks.P));var jam=w.Spawn("mcv",1,Campaign.Middle(apron).X,Campaign.Middle(apron).Y);
  for(int i=0;i<4;i++)Check(w.Train(Campaign.Info("squad"),0,barracks),"queue accepted despite blocked door");Wait(w,26);
  Check(barracks.Queue.Count==4&&!w.Live(0).Any(u=>u.T.Foot),"blocked outlet retains completed paid unit inside");
  jam.Hp=0;int seen=0;var ids=new System.Collections.Generic.HashSet<int>();
  for(int n=0;n<700;n++){w.Tick(.05f);foreach(var u in w.Live(0).Where(u=>u.T.Foot))if(ids.Add(u.Id)){Check(apron.Contains(u.P),"new soldier must appear in the marked physical exit");seen++;}}
  Check(seen==4&&barracks.Queue.Count==0,"four units exit and clear automatically");
  Console.WriteLine("Adjacent-cell formation, moving-route obstruction, refinery failover/reopening, and real blocked production exits passed.");
 }
 static int Main(){try{TightCellsAndRecovery();FollowingChecks();ReachabilityChecks();ClassicCombatChecks();MixedCohesion();ConvoyStress();DockQueueStress();FiringBoundary();ExpandedMap();Run();return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 static void Run(){NarrowGate();CrowdedOreRun(.05f);CrowdedOreRun(1f/60);MinerMidRouteBlock();EnemySupplyChain();MinerTraffic();ExhaustedFields();ProspectChecks();OreChecks();ContactChecks();StreetChecks();InfantryWaves();HeadingChecks();ReconAndMarch();Navigation();Training();Play(true);Play(false);EliminationChecks();
  var w=new Campaign();var car=w.Spawn("ifv",0,212,431);w.Load(car);Check(w.Units.Any(o=>o.Carrier==car),"embark");w.Load(car);Check(!w.Units.Any(o=>o.Carrier==car),"disembark");
  Console.WriteLine("All checks passed.");}
}
