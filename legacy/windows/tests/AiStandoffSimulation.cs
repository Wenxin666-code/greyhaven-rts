using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Greyhaven;
class AiStandoffSimulation {
 static int Main(string[] args){try{
  Campaign.RunStandoffSimulation(args[0],int.Parse(args[1]),args.Length>2?args[2]:"passive",args.Length>3?float.Parse(args[3],CultureInfo.InvariantCulture):.1f);
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 void ObserverDevelop(){
  var brain=Brain(0);var known=AiKnown(0);
  var mcv=Live(0).FirstOrDefault(p=>p.T.Key=="mcv");if(mcv!=null&&!Live(0).Any(p=>p.T.Key=="hq"))DeployEnemyBase(mcv);
  AiConstruction(0,brain,known);AiFortify(0,brain,known);AiRecruit(0,brain,known);
  foreach(var u in Live(0).Where(p=>!p.T.Building&&p.T.Hit>0&&p.Target==null&&p.Path.Count==0&&!p.GuardHome.HasValue).ToArray())Guard(u);
 }
 internal static void RunStandoffSimulation(string folder,int seconds,string mode,float step){
  Directory.CreateDirectory(folder);var w=new Campaign(false,false,true,true);var watch=Stopwatch.StartNew();
  var alive=new HashSet<int>(w.Units.Where(p=>p.Alive).Select(p=>p.Id));var dead=new int[MaxFactions];var lostValue=new int[MaxFactions];
  var firstLoss=Enumerable.Repeat(-1f,MaxFactions).ToArray();float nextObserve=0,nextLog=60;int snapshots=0;
  using(var rows=new StreamWriter(Path.Combine(folder,"timeline.csv"),false,System.Text.Encoding.UTF8))
  using(var events=new StreamWriter(Path.Combine(folder,"events.csv"),false,System.Text.Encoding.UTF8)){
   rows.WriteLine("game_seconds,side,alive,hq,power,refinery,factory,barracks,post,miners,army,home_army,garrison,wings,attacks,raids,income,cash,dead,lost_value");
   events.WriteLine("game_seconds,event,side,type,value");
   for(int tick=0;tick<(int)Math.Ceiling(seconds/step)&&!w.Won&&!w.Lost;tick++){
    if(w.Time>=nextObserve){nextObserve=w.Time+.75f;if(mode!="idle")w.ObserverDevelop();}
    w.Tick(step);
    foreach(var p in w.Units){
     if(p.Alive)alive.Add(p.Id);
     else if(p.Side>=0&&alive.Remove(p.Id)){dead[p.Side]++;lostValue[p.Side]+=p.T.Price;events.WriteLine(String.Join(",",new object[]{w.Time.ToString("F2",CultureInfo.InvariantCulture),"destroyed",p.Side,p.T.Key,p.T.Price}));}
    }
    for(int side=0;side<w.FactionCount;side++)if(firstLoss[side]<0&&(w.Remaining(side)==0||w.HasSurrendered(side))){firstLoss[side]=w.Time;events.WriteLine(w.Time.ToString("F2",CultureInfo.InvariantCulture)+",eliminated,"+side+",,0");}
    if(w.Time+step*.5f>=snapshots*15){
     snapshots++;
     for(int side=0;side<w.FactionCount;side++){
      var units=w.Units.Where(p=>p.Alive&&p.Side==side).ToArray();var army=units.Where(p=>!p.T.Building&&p.T.Hit>0).ToArray();var b=w.Brain(side);
      int atHome=army.Count(p=>p.Carrier!=null||Dist(p.P,w.Home(side))<480);
      rows.WriteLine(String.Join(",",new object[]{w.Time.ToString("F2",CultureInfo.InvariantCulture),side,units.Length,units.Count(p=>p.T.Key=="hq"),units.Count(p=>p.T.Key=="power"),units.Count(p=>p.T.Key=="refinery"),units.Count(p=>p.T.Key=="factory"),units.Count(p=>p.T.Key=="barracks"),units.Count(p=>p.T.Key=="post"),units.Count(p=>p.T.Key=="miner"),army.Length,atHome,army.Count(p=>p.Carrier!=null&&p.Carrier.T.Civilian),b.Wings.Count,b.Launches,b.Raids,w.OreReceipts[side],w.Funds[side],dead[side],lostValue[side]}));
     }rows.Flush();events.Flush();
    }
    if(w.Time>=nextLog){
     if(w.Time<301){
      using(var detail=new StreamWriter(Path.Combine(folder,"mining-"+(int)w.Time+".txt"))){
       for(int side=1;side<w.FactionCount;side++)foreach(var u in w.Live(side).Where(p=>p.T.Key=="miner"))detail.WriteLine(side+" miner="+u.Id+" pos="+u.P+" cargo="+u.Cargo+" mining="+u.Mining+" path="+u.Path.Count+" goal="+u.Goal+" blocked="+u.TrafficBlocked+" dock="+(u.Dock==null?"none":u.Dock.Id.ToString())+" patch="+(u.Patch==null?"none":u.Patch.P.ToString())+" evading="+w.Brain(side).Evading.ContainsKey(u));
      foreach(var u in w.Live(2).Where(p=>p.T.Building))detail.WriteLine("2 structure="+u.Id+" type="+u.T.Key+" pos="+u.P+" lot="+Footprint(u.T,u.P)+" exit="+Middle(ExitApron(u.T,Footprint(u.T,u.P))));
      }
     }
     Console.WriteLine("t="+(int)w.Time+"s active="+String.Join("/",Enumerable.Range(1,5).Select(s=>w.Remaining(s)>0&&!w.HasSurrendered(s)?"1":"0"))+" posts="+String.Join("/",Enumerable.Range(1,5).Select(s=>w.AiCount(s,"post")))+" army="+String.Join("/",Enumerable.Range(1,5).Select(s=>w.Live(s).Count(p=>!p.T.Building&&p.T.Hit>0)))+" losses="+String.Join("/",dead.Skip(1))+" wall="+watch.Elapsed.TotalSeconds.ToString("F1",CultureInfo.InvariantCulture));nextLog+=60;
    }
   }
  }
  var summary="{\"requested_game_seconds\":"+seconds+",\"simulated_game_seconds\":"+w.Time.ToString("F2",CultureInfo.InvariantCulture)+",\"step\":"+step.ToString(CultureInfo.InvariantCulture)+",\"mode\":\""+mode+"\",\"won\":"+w.Won.ToString().ToLowerInvariant()+",\"lost\":"+w.Lost.ToString().ToLowerInvariant()+",\"wall_seconds\":"+watch.Elapsed.TotalSeconds.ToString("F2",CultureInfo.InvariantCulture)+",\"first_elimination\":["+String.Join(",",firstLoss.Select(n=>n.ToString("F2",CultureInfo.InvariantCulture)))+"],\"unit_deaths\":["+String.Join(",",dead)+"],\"lost_value\":["+String.Join(",",lostValue)+"]}";
  File.WriteAllText(Path.Combine(folder,"summary.json"),summary);Console.WriteLine(summary);
 }
}
}
