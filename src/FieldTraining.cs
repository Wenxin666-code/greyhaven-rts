using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 static readonly string[] LessonKeys={"hq","power","refinery","ore","receipt","barracks","squad","factory","ifv","eliminate"};
 public static readonly string[] Lessons={"展开指挥部","建立电站","建造矿石精炼厂","找到矿点并开始采集","运回第一车矿石","建立兵营","训练一名步枪兵","建造车辆工厂","让步兵战车出厂","消灭敌方守军"};
 public string LessonKey {get{return LessonKeys[Lesson];}}
 public string LessonTitle {get{return (Lesson+1)+" / "+Lessons.Length+"　"+Lessons[Lesson];}}
 void AdvanceLesson(){
  while(Lesson<LessonKeys.Length-1){
   string key=LessonKey;bool complete;
   switch(key){
    case "ore":complete=FirstOreCollected;break;
    case "receipt":complete=OreReceipts[0]>0;break;
    case "squad":complete=InfantryDelivered>0;break;
    case "ifv":complete=VehiclesDelivered>0;break;
    default:complete=Live(0).Any(o=>o.T.Key==key);break;
   }
   if(!complete)break;Lesson++;
  }
 }
 public string QueueState(Piece o){
  if(o==null||o.Queue.Count==0)return "空闲";
  if(o.Work>=o.Queue.Peek().Seconds)return "出口受阻，正在等待通行";
  if(o.ProductionPaused)return "已暂停";if(Funds[o.Side]==0)return "等待资金";
  bool low=Power(o.Side)<0;
  int seconds=(int)Math.Ceiling((o.Queue.Peek().Seconds-o.Work)/ProductionRate(o));
  return (low?"缺电减速 · ":"")+seconds+" 秒";
 }
 public string Coach(){
  var enemy=Live(1).FirstOrDefault(e=>Live(0).Any(o=>o.T.Key=="hq"&&Dist(e.P,o.P)<145));
  if(enemy!=null)return "基地附近发现敌军。框选守军，左键点敌人集火。";
  var stuck=Live(0).FirstOrDefault(o=>o.Queue.Count>0&&o.Work>=o.Queue.Peek().Seconds);
  if(stuck!=null&&stuck.T.Key=="barracks")return "兵营出口被占用，已完成的士兵在营内等待；闲置友军会让出出口。";
  if(stuck!=null)return stuck.T.Name+" #"+stuck.Id+" 已完工但出口堵塞，请移开门口的部队。";
  if(Landing&&Funds[0]<195&&!Live(0).Any(o=>o.T.Key=="miner"&&o.Mining))return "矿车停工或已损失。选中现有矿车恢复采矿；车辆工厂可以补造矿车。";
  if(Power(0)<0)return "供电不足，生产以 45% 速度运行。增建电站可恢复速度。";
  return null;
 }
 public bool CancelOrder(Piece o,int slot){
  if(o==null||!o.Deployed||o.Side!=0||slot<0||slot>=o.Queue.Count)return false;
  var jobs=o.Queue.ToArray();var t=jobs[slot];o.Queue.Clear();
  for(int i=0;i<jobs.Length;i++)if(i!=slot)o.Queue.Enqueue(jobs[i]);
  int refund=slot==0?o.WorkPaid:0;if(slot==0){o.Work=0;o.WorkPaid=0;o.ProductionPaused=false;}Funds[0]+=refund;Radio="已取消"+t.Name+"，退回已付军费 "+refund+"。";return true;
 }
}
}
