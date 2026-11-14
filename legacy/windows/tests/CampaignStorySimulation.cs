using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Greyhaven;
class CampaignStorySimulation {
 static int Main(string[] args){try{Campaign.SimulateCampaignStory(args[0],int.Parse(args[1]),int.Parse(args[2]));return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {public partial class Campaign {
 internal static void SimulateCampaignStory(string folder,int mission,int seconds){
  Directory.CreateDirectory(folder);var w=new Campaign(false,false,true,mission==3);w.PrepareBattleNavigation();
  var timer=Stopwatch.StartNew();float observe=0,log=0;var ids=new HashSet<int>(w.Units.Select(u=>u.Id));var produced=new int[MaxFactions,4];var firstOut=Enumerable.Repeat(-1f,MaxFactions).ToArray();
  using(var rows=new StreamWriter(Path.Combine(folder,"timeline.csv"),false,System.Text.Encoding.UTF8)){
   rows.WriteLine("seconds,side,hq,refineries,miners,posts,army,home_guards,field_guards,garrison,launches,raids,withdrawals,income,funds,produced_ifv,produced_tank,produced_arty,produced_foot,target");
   for(int step=0;step<seconds*10&&!w.Won&&!w.Lost;step++){
    if(w.Time>=observe){observe=w.Time+.75f;w.ObserverDevelop();}w.Tick(.1f);
    foreach(var u in w.Units)if(ids.Add(u.Id)&&u.Side>0){int kind=u.T.Key=="ifv"?0:u.T.Key=="tank"?1:u.T.Key=="arty"?2:u.T.Foot?3:-1;if(kind>=0)produced[u.Side,kind]++;}
    if(w.Funds.Any(n=>n<0))throw new Exception("negative faction funds");
    for(int side=1;side<w.FactionCount;side++)if(firstOut[side]<0&&(w.HasSurrendered(side)||w.Remaining(side)==0))firstOut[side]=w.Time;
    if(w.Time>=log||step==seconds*10-1){
     log+=60;
     for(int side=1;side<w.FactionCount;side++){
      var brain=w.Brain(side);int field=0;
#if !STORY_BASELINE
      field=brain.FieldGuard.Count;
#endif
      rows.WriteLine(String.Join(",",new object[]{w.Time.ToString("F1",CultureInfo.InvariantCulture),side,w.AiCount(side,"hq"),w.AiCount(side,"refinery"),w.AiCount(side,"miner"),w.AiCount(side,"post"),w.Live(side).Count(p=>!p.T.Building&&p.T.Hit>0),brain.HomeGuard.Count,field,w.Units.Count(p=>p.Alive&&p.Side==side&&p.Carrier!=null&&p.Carrier.T.Civilian),brain.Launches,brain.Raids,brain.Withdrawals,w.OreReceipts[side],w.Funds[side],produced[side,0],produced[side,1],produced[side,2],produced[side,3],brain.Opponent}));
     }
     rows.Flush();Console.WriteLine("mission="+mission+" t="+(int)w.Time+" active="+String.Join("/",Enumerable.Range(1,w.FactionCount-1).Select(s=>w.Remaining(s)>0&&!w.HasSurrendered(s)?"1":"0"))+" launches="+String.Join("/",Enumerable.Range(1,w.FactionCount-1).Select(s=>w.Brain(s).Launches))+" wall="+timer.Elapsed.TotalSeconds.ToString("F1",CultureInfo.InvariantCulture));
    }
   }
  }
  File.WriteAllText(Path.Combine(folder,"summary.json"),"{\"mission\":"+mission+",\"seconds\":"+w.Time.ToString("F1",CultureInfo.InvariantCulture)+",\"step\":0.1,\"passive_player\":true,\"first_elimination\":["+String.Join(",",firstOut.Select(t=>t.ToString("F1",CultureInfo.InvariantCulture)))+"],\"wall_seconds\":"+timer.Elapsed.TotalSeconds.ToString("F1",CultureInfo.InvariantCulture)+"}");
  using(var details=new StreamWriter(Path.Combine(folder,"final-state.txt"))){for(int side=1;side<w.FactionCount;side++){
   details.WriteLine(w.AiSummary(side)+" reserve="+w.AiReserve(side)+" budget="+w.AiBudget(side,w.AiReserve(side)));
   foreach(var u in w.Live(side).Where(u=>u.T.Building||u.T.Key=="miner"))details.WriteLine(u.T.Key+" "+u.Id+" p="+u.P+" queue="+String.Join("/",u.Queue.Select(t=>t.Key))+" work="+u.Work+" hp="+u.Hp);
#if !STORY_BASELINE
   var brain=w.Brain(side);details.WriteLine("field deployments="+brain.FieldDeployments+" active="+brain.FieldGuard.Count+" point="+brain.FieldPoint+" returning="+brain.FieldReturning);
#endif
  }}
 }
}}
