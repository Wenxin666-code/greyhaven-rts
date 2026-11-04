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
 static void Main(){Play(true);Play(false);var w=new Campaign();var h=Begin(w);var enemy=w.Spawn("tank",1,h.P.X+82,h.P.Y);w.Attack(enemy,h);Wait(w,190);Check(w.Lost,"base loss");
  w=new Campaign();var car=w.Spawn("ifv",0,212,431);w.Load(car);Check(w.Units.Any(o=>o.Carrier==car),"embark");w.Load(car);Check(!w.Units.Any(o=>o.Carrier==car),"disembark");
  Console.WriteLine("All checks passed.");}
}
