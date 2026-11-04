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
  Build(w,"power",260,550);Build(w,"barracks",120,610);Build(w,"factory",342,551);
  Check(!w.Build(Campaign.Info("power"),new PointF(260,550)),"overlap blocked");
  Check(w.Sites[0].Owner==0&&w.Sites[1].Owner==0,"capture and income");
  w.Upgrade();Check(w.Upgraded[0],"upgrade");
  int order=0;string[] list=armor?new[]{"tank","engineer","ifv","squad","tank","at"}:new[]{"ifv","squad","at","arty","engineer","squad"};
  while(!w.Won&&!w.Lost&&w.Time<560){
   string k=list[order%list.Length];if(w.Train(Campaign.Info(k),0,null))order++;
   if(w.Time>(armor?107:95)){
    int i=0;foreach(var o in w.Live(0).Where(o=>!o.T.Building).ToArray()){
     var p=w.Sites[2].P;
     if(o.T.Key=="arty")p=new PointF(900,319);
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
  Build(w,"power",260,550);var b=Build(w,"barracks",120,610);Check(w.Lesson==3,"construction completes before teaching production");
  Check(w.Train(Campaign.Info("squad"),0,b),"squad queued");Check(w.Lesson==3,"order alone is not graduation");Wait(w,6);
  var foot=w.LastReady;Check(w.Lesson==4&&foot.T.Key=="squad","delivery");
  Check(w.Move(foot,w.Sites[0].P,true),"reach west");Wait(w,40);Check(w.Lesson==5,"capture advances tutorial");
  var f=Build(w,"factory",342,551);w.Train(Campaign.Info("ifv"),0,f);Wait(w,9);Check(w.Lesson==7,"vehicle delivered");
  foreach(var u in w.Live(0).Where(o=>!o.T.Building).ToArray())w.Move(u,new PointF(w.Sites[2].P.X+(u.Id%3)*12,w.Sites[2].P.Y+u.Id%2*16),true);
  Wait(w,110);Check(w.Won&&w.Lesson==8,"full tutorial ends at extraction");Console.WriteLine("All nine tutorial milestones passed, including a ten-minute idle start.");
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
  w.Train(Campaign.Info("squad"),0,barracks);w.Train(Campaign.Info("at"),0,barracks);Check(w.QueueState(barracks).Contains("缺电"),"low power estimate");Wait(w,13);float progress=barracks.Work;int cash=w.Funds[0];
  Check(barracks.Queue.Count==2&&w.QueueState(barracks).Contains("出口"),"exit blockage is visible");
  Check(w.CancelOrder(barracks,1)&&w.Funds[0]==cash+135&&barracks.Work==progress&&!w.CancelOrder(barracks,1),"refund once and preserve head progress");
  w.Move(block[0],new PointF(600,637),false);Wait(w,4);Check(barracks.Queue.Count==0&&w.LastReady!=null,"unblocking gate releases finished order");
  Console.WriteLine("Straight path, live obstacle rerouting, tile grid, exit blocking and refund checks passed.");
 }
 static int Main(){try{Run();return 0;}catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
 static void Run(){Navigation();Training();Play(true);Play(false);var w=new Campaign();var h=Begin(w);var enemy=w.Spawn("tank",1,h.P.X+82,h.P.Y);w.Attack(enemy,h);Wait(w,190);Check(w.Lost,"base loss");
  w=new Campaign();var car=w.Spawn("ifv",0,212,431);w.Load(car);Check(w.Units.Any(o=>o.Carrier==car),"embark");w.Load(car);Check(!w.Units.Any(o=>o.Carrier==car),"disembark");
  Console.WriteLine("All checks passed.");}
}
