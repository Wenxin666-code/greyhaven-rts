using System.Drawing;

namespace Greyhaven {
partial class CommandWindow {
 bool commandRules;
 static readonly string[] roster={"squad","at","engineer","ifv","tank","arty","mcv","miner"};
 static readonly string[] roles={
  "单人步枪兵，适合侦察与交火。|可以搭乘步兵战车。",
  "擅长打击装甲目标，对步兵威力较低。|可搭乘步兵战车。",
  "自动维修附近受损的己方车辆和建筑。|没有武器，建议由战斗单位掩护。",
  "搭载或卸下步兵，对敌方步兵伤害更高。|靠近步兵后按 D 装载，再按 D 卸载。",
  "装甲升级后生产，适合正面作战。|车体转正后开炮。",
  "装甲升级后生产，射程最远。|适合后方支援和压制地面。",
  "在合适的空地按 D 展开为指挥部。|在指挥部附近可继续建设基地。",
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
   string[] keys={"左键 / 右键","Shift + 左键 / T","Ctrl + 1～9 / 1～9","Ctrl + Shift + 左键","G / Ctrl + Alt + 左键","Ctrl / Alt + 左键","S / X / D","按住 Z · 点击 · 松开","F / H / 空格","Q / W / E / R","P / M / N","Ctrl + F1～F4 / F1～F4"};
   string[] help={"选择、移动或点名攻击 / 取消选择和待下达指令","增减选择 / 屏幕内同类；快速再按 T 选全图同类","设置编组 / 召回编组","攻击移动：沿途交战，结束后继续前往目的地","原地警戒 / 到地点警戒，或护卫己方单位、建筑","强制开火（可伤友军）/ 强制移动；坦克可碾压敌方步兵","停止当前任务 / 散开 / 展开或装卸","规划多个路径点；点回首个标记可循环巡逻","镜头跟随所选单位 / 回基地 / 最近事件","建筑 / 防御设施 / 步兵 / 车辆生产页","全选部队 / 下一个单位 / 上一个单位","保存镜头 / 召回镜头；F5 手册，Esc 暂停"};
   for(int i=0;i<keys.Length;i++){float y=165+i*43;Line(g,348,y,1364,y,C("#506165"));Txt(g,keys[i],351,y+12,14,gold,true);Txt(g,help[i],628,y+12,14,pale);}
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
   string[] titles={"01   建立基地","02   采矿与军费","03   生产与交战","04   歼灭与胜利","05   镜头操作"};
   string[] lines={"选工程车按 D 展开。先建电站与精炼厂，再用兵营训练步兵、用车辆工厂生产载具。",
    "每个矿点有 4200 矿，不会再生。矿车满载 180 返厂，矿点采空时不足一车也会交货。",
    "左键选择及下令，右键取消选择。Ctrl+Shift 点地进攻，G 警戒，S 停止。工兵会修理车辆和建筑。",
    "消灭敌方所有存活单位与建筑即可获胜，包括矿车、工程车和在建建筑。港务中心是中立地标。",
    "边缘滚屏、滚轮缩放。雷达站完工且供电正常后开启小地图；没有扫描揭图功能。H 回基地，空格定位事件。"};
   for(int i=0;i<titles.Length;i++){int y=168+105*i;
    Line(g,348,y,1364,y,C("#506165"));Txt(g,titles[i],350,y+13,20,gold,true);Txt(g,lines[i],351,y+51,17,pale);}
  }
 }
}
}
