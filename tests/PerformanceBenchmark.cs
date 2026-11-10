using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Greyhaven;

class PerformanceBenchmark {
 static int Main(string[] args){try{
  Campaign.MeasureThirdMission(args[0],int.Parse(args[1]),float.Parse(args[2],CultureInfo.InvariantCulture));return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 internal static void MeasureThirdMission(string output,int seconds,float step){
  Directory.CreateDirectory(output);
  var world=new Campaign(false,false,true,true);float nextObserver=0,nextSample=60;
  var times=new System.Collections.Generic.List<double>();var elapsed=Stopwatch.StartNew();
  using(var rows=new StreamWriter(Path.Combine(output,"ticks.csv")))
  using(var states=new StreamWriter(Path.Combine(output,"states.csv"))){
   rows.WriteLine("game_seconds,update_ms,alive");states.WriteLine("game_seconds,state_sha256");
   for(int i=0;i<Math.Ceiling(seconds/(double)step)&&!world.Won&&!world.Lost;i++){
    var timer=Stopwatch.StartNew();
    if(world.Time>=nextObserver){nextObserver=world.Time+.75f;world.ObserverDevelop();}
    world.Tick(step);double ms=timer.Elapsed.TotalMilliseconds;times.Add(ms);
    rows.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0:F3},{1:F4},{2}",world.Time,ms,world.Units.Count(p=>p.Alive)));
    if(world.Time>=nextSample){
     states.WriteLine(world.Time.ToString("F3",CultureInfo.InvariantCulture)+","+world.PerformanceStateDigest());states.Flush();rows.Flush();
     Console.WriteLine("game="+world.Time.ToString("F0")+"s wall="+elapsed.Elapsed.TotalSeconds.ToString("F1")+"s");nextSample+=60;
    }
   }
   states.WriteLine(world.Time.ToString("F3",CultureInfo.InvariantCulture)+","+world.PerformanceStateDigest());
  }
  times.Sort();var c=CultureInfo.InvariantCulture;
  string result="{\"step\":"+step.ToString("R",c)+",\"game_seconds\":"+world.Time.ToString("F3",c)+",\"updates\":"+times.Count+",\"mean_ms\":"+times.Average().ToString("F4",c)+",\"p95_ms\":"+times[(int)(times.Count*.95)].ToString("F4",c)+",\"p99_ms\":"+times[(int)(times.Count*.99)].ToString("F4",c)+",\"max_ms\":"+times.Last().ToString("F4",c)+",\"over_50ms\":"+times.Count(t=>t>50)+"}";
  File.WriteAllText(Path.Combine(output,"summary.json"),result);Console.WriteLine(result);
 }
 string PerformanceStateDigest(){
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
   writer.Write(Time);foreach(int cash in Funds)writer.Write(cash);
   foreach(var p in Units){
    writer.Write(p.Id);writer.Write(p.Side);writer.Write(p.T.Key);writer.Write(p.P.X);writer.Write(p.P.Y);writer.Write(p.Hp);writer.Write(p.Ready);writer.Write(p.Turn);
    writer.Write(p.Work);writer.Write(p.WorkPaid);writer.Write(p.Target==null?0:p.Target.Id);writer.Write(p.Carrier==null?0:p.Carrier.Id);
    writer.Write(p.Cargo);writer.Write(p.Returning);writer.Write(p.Mining);writer.Write(p.Unload);writer.Write(p.Path.Count);
    foreach(var q in p.Path){writer.Write(q.X);writer.Write(q.Y);}writer.Write(p.Queue.Count);foreach(var q in p.Queue)writer.Write(q.Key);
   }
   foreach(var p in Ore)writer.Write(p.Stock);var fog=new byte[Buffer.ByteLength(discovered)];Buffer.BlockCopy(discovered,0,fog,0,fog.Length);writer.Write(fog);
   using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");
  }
 }
}
}
