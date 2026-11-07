using System.Drawing;

namespace Greyhaven {
partial class CommandWindow {
 bool commandRules;
 static readonly string[] roster={"squad","at","engineer","ifv","tank","arty","mcv","miner"};
 static readonly string[] roles={
  "单人步枪兵，适合侦察与交火。|可搭乘战车、进驻己方房屋。",
  "擅长打击装甲目标，对步兵威力较低。|可搭乘战车、进驻己方房屋。",
  "进入建筑占领或修复，完成后消耗工兵。|登上步兵战车后成为维修车，可再下车。",
  "装载工兵后改为维修车辆，不能开火。|步兵点战车登车；选战车按 D 卸载。",
  "装甲升级后生产，适合正面作战。|车体转正后开炮。",
  "装甲升级后生产，射程最远。|适合后方支援和压制地面。",
  "选车再点击或按 D 展开为指挥部。|选基地点地面，可收起搬迁；不修复血量。",
  "寻找矿石、采集、返厂卸货。|可指定矿点，也可提前返厂交货。"
 };
 void DrawRules(Graphics g){
  Txt(g,"规则介绍",58,85,44,pale,true);Txt(g,"FIELD MANUAL   /   GREYHAVEN",61,149,14,gold,true);
  Button(g,64,206,220,48,"01    战场规则",()=>{unitRoster=false;commandRules=false;},!unitRoster&&!commandRules);
  Button(g,64,267,220,48,"02    单位图鉴",()=>{unitRoster=true;commandRules=false;},unitRoster&&!commandRules);
  Button(g,64,328,220,48,"03    经典指令",()=>commandRules=true,commandRules);
  Button(g,64,678,220,44,"‹  返回主界面",()=>{rulesOpen=false;hits.Clear();hudDirty=true;});
  Metal(g,318,83,1082,649);
  Txt(g,commandRules?"经典指令 · 红警 2 操作":unitRoster?"单位图鉴":"战场规则",347,105,28,pale,true);
  if(commandRules){
   string[] keys={"左键 / 右键","Shift + 左键 / T","Ctrl + 1～9 / 1～9","Ctrl + Shift + 左键","G / Ctrl + Alt + 左键","Ctrl / Alt + 左键","S / X / D","按住 Z · 点击 · 松开","F / H / 空格","Q / W / E / R","P / M / N","Ctrl + F1～F4 / F1～F4","K / L"};
   string[] help={"选择、移动或点名攻击 / 取消选择和待下达指令","增减选择 / 屏幕内同类；快速再按 T 选全图同类","设置编组 / 召回编组","攻击移动：沿途交战，结束后继续前往目的地","原地警戒 / 到地点警戒，或护卫己方单位、建筑","强制开火（可伤友军）/ 强制移动；坦克可碾压敌方步兵","停止当前任务 / 散开 / 展开或装卸","规划多个路径点；点回首个标记可循环巡逻","镜头跟随所选单位 / 回基地 / 最近事件","建筑 / 防御 / 步兵 / 车辆；Q、W 可取出就绪建筑","全选部队 / 下一个单位 / 上一个单位","保存镜头 / 召回镜头；F5 手册，Esc 暂停，Alt+Enter 全屏/窗口","建筑维修 / 出售模式；左键作用到建筑，右键退出"};
   for(int i=0;i<keys.Length;i++){float y=160+i*41;Line(g,348,y,1364,y,C("#506165"));Txt(g,keys[i],351,y+12,14,gold,true);Txt(g,help[i],628,y+12,14,pale);}
   return;
  }
  if(unitRoster){
   for(int i=0;i<roster.Length;i++){
    var t=Campaign.Info(roster[i]);int x=344+(i%2)*520,y=170+(i/2)*132;
    Plate(g,new RectangleF(x,y,500,118),panel,C("#546469"));Portrait(g,t,new RectangleF(x+9,y+18,96,72));
    Txt(g,t.Name,x+118,y+10,21,pale,true);Txt(g,"$ "+t.Price,x+405,y+14,16,gold,true);
    Txt(g,"生产设施   "+Campaign.Info(t.Producer).Name,x+119,y+43,13,teal);
    var lines=roles[i].Split('|');
    Txt(g,lines[0],x+118,y+66,15,dim);Txt(g,lines[1],x+118,y+87,15,dim);
   }
  }else{
   string[] titles={"01   建立基地","02   采矿与军费","03   生产与交战","04   歼灭与胜利","05   镜头操作","06   河湾镇 · 占领驻防"};
   string[] lines={"侧栏生产后点击「就绪」放置，落地短暂展开。基地可收起搬迁；生产按进度扣款。",
    "矿车满载 180 返厂；矿格逐渐采空。教学每格 240，河湾镇每格 480；本关矿石不再生。",
    "左键生产／继续，右键暂停、再右键取消。缺钱等待；双击工厂设主要出口，额外工厂加速。",
    "歼灭所有敌方单位与建筑。河湾镇三方互相敌对，必须消灭两个敌方；中立房屋不算目标。",
    "雷达提供小地图；间谍卫星 $750、耗电 6，需雷达和车辆工厂，供电时全图可见。",
    "工兵进入空房占领并消耗；步兵驻防 4 / 6 / 8 人。驻军需先清除才能夺房；D 撤出。"};
   for(int i=0;i<titles.Length;i++){int y=168+90*i;
    Line(g,348,y,1364,y,C("#506165"));Txt(g,titles[i],350,y+13,20,gold,true);Txt(g,lines[i],351,y+51,17,pale);}
  }
 }
}
}
