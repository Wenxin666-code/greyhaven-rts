using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 public float HoldTime {get{return Tutorial?12:45;}}
 public static readonly string[] Lessons={"展开指挥部","建立电站","建立兵营","生产一个步兵班","占领西岸仓库","建造车辆工厂","让步兵战车出厂","夺取港务中心","保护泊位"};
 public string LessonKey {get{return new[]{"hq","power","barracks","squad","west","factory","ifv","port","hold"}[Lesson];}}
 void AdvanceLesson(){
  while(Lesson<8){
   string key=LessonKey;bool complete;
   switch(key){
    case "squad":complete=InfantryDelivered>0;break;
    case "ifv":complete=VehiclesDelivered>0;break;
    case "west":complete=Sites[0].Owner==0;break;
    case "port":complete=Sites[2].Owner==0;break;
    default:complete=Live(0).Any(o=>o.T.Key==key);break;
   }
   if(!complete)break;Lesson++;
  }
 }
 public string QueueState(Piece o){
  if(o==null||o.Queue.Count==0)return "空闲";
  if(o.Work>=o.Queue.Peek().Seconds)return "出口受阻，移开门口部队";
  bool low=Power(o.Side)<0;
  int seconds=(int)Math.Ceiling((o.Queue.Peek().Seconds-o.Work)/(low?.45f:1f));
  return (low?"缺电减速 · ":"")+seconds+" 秒";
 }
 public string Coach(){
  var enemy=Live(1).FirstOrDefault(e=>Live(0).Any(o=>o.T.Key=="hq"&&Dist(e.P,o.P)<145));
  if(enemy!=null)return "基地附近发现敌军。框选守军，右键点敌人集火。";
  var stuck=Live(0).FirstOrDefault(o=>o.Queue.Count>0&&o.Work>=o.Queue.Peek().Seconds);
  if(stuck!=null)return stuck.T.Name+" #"+stuck.Id+" 已完工但出口堵塞，请移开门口的部队。";
  if(Power(0)<0)return "供电不足，生产以 45% 速度运行。增建电站可恢复速度。";
  if(Sites[2].Owner==0&&Live(1).Any(e=>!e.T.Building&&Dist(e.P,Sites[2].P)<72))return "泊位有敌军，靠岸计时暂停。优先清理港务中心附近的红军。";
  return null;
 }
 public bool CancelOrder(Piece o,int slot){
  if(o==null||!o.Deployed||o.Side!=0||slot<0||slot>=o.Queue.Count)return false;
  var jobs=o.Queue.ToArray();var t=jobs[slot];o.Queue.Clear();
  for(int i=0;i<jobs.Length;i++)if(i!=slot)o.Queue.Enqueue(jobs[i]);
  if(slot==0)o.Work=0;Funds[0]+=t.Price;Radio="已取消"+t.Name+"，退回 "+t.Price+" 军费。";return true;
 }
}
}
