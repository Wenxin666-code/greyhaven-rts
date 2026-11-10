using System.Drawing;

namespace Greyhaven {
partial class CommandWindow {
 bool commandRules,veterancyRules;
 static readonly string[] roster={"squad","at","engineer","ifv","tank","arty","mcv","miner","sniper"};
 static readonly string[] roles={
  "D 部署沙袋机枪阵地，射程增至 5 格。|移动先收起；可驻房屋与碉堡。",
  "平时用机枪；D 部署后使用反装甲导弹。|部署射程 8 格，收起后才能移动。",
  "进入建筑占领或修复，完成后消耗工兵。|登上步兵战车后成为维修车，可再下车。",
  "步枪兵改机枪，反装甲兵改导弹。|工兵改维修，狙击手改狙击；D 卸载。",
  "主战坦克不能载人，适合正面作战。|车体转正后开炮。",
  "车辆工厂直接生产，射程最远。|适合后方支援和压制地面。",
  "选车再点击或按 D 展开为指挥部。|选基地点地面，可收起搬迁；不修复血量。",
  "寻找矿石、采集、返厂卸货。|可指定矿点，也可提前返厂交货。",
  "需兵营＋雷达，远程精确反步兵。|可搭乘战车；不能驻房屋或碉堡。"
 };
 void DrawRules(Graphics g){
  Txt(g,"规则介绍",58,85,44,pale,true);Txt(g,"FIELD MANUAL   /   GREYHAVEN",61,149,14,gold,true);
  Button(g,64,206,220,48,"01    战场规则",()=>{unitRoster=false;commandRules=false;veterancyRules=false;},!unitRoster&&!commandRules&&!veterancyRules);
  Button(g,64,267,220,48,"02    单位图鉴",()=>{unitRoster=true;commandRules=false;veterancyRules=false;},unitRoster&&!commandRules&&!veterancyRules);
  Button(g,64,328,220,48,"03    经典指令",()=>{commandRules=true;veterancyRules=false;},commandRules&&!veterancyRules);
  Button(g,64,389,220,48,"04    晋升与扩建",()=>{veterancyRules=true;commandRules=false;},veterancyRules);
  Button(g,64,678,220,44,"‹  返回主界面",()=>{rulesOpen=false;hits.Clear();hudDirty=true;});
  Metal(g,318,83,1082,649);
  Txt(g,veterancyRules?"晋升与扩建":commandRules?"经典指令 · 红警 2 操作":unitRoster?"单位图鉴":"战场规则",347,105,28,pale,true);
  if(veterancyRules){DrawVeterancyRules(g);return;}
  if(commandRules){
   string[] keys={"左键 / 右键","Shift + 左键 / T","Ctrl + 0～9 / 0～9","Ctrl + Shift + 左键","G / Ctrl + Alt + 左键","Ctrl / Alt + 左键","S / X / D","按住 Z · 点击 · 松开","F / H / 空格","Q / W / E / R","P / N / M","Ctrl + F1～F4 / F1～F4","K / L"};
   string[] help={"选择、移动或点名攻击 / 单击取消，按住拖动滚屏","增减选择 / 屏幕内同类；快速再按 T 选全图同类","设置／召回编组；Shift 加选，Alt 定位，连按定位","攻击移动：沿途交战，结束后继续前往目的地","原地警戒 / 到地点警戒，或护卫己方单位、建筑","强制开火（可伤友军）/ 强制移动；坦克可碾压敌方步兵","停止当前任务 / 散开 / 展开或装卸","可给多队规划；点回已有点巡逻，选点 Delete 删除","镜头跟随所选单位 / 回基地 / 最近事件","建筑 / 防御 / 步兵 / 车辆；Q、W 可取出就绪建筑","全选战斗单位 / 下一个单位 / 上一个单位","保存／召回镜头；Esc 选项中查看和更改快捷键","建筑维修 / 出售模式；左键作用到建筑，右键退出"};
   for(int i=0;i<keys.Length;i++){float y=160+i*41;Line(g,348,y,1364,y,C("#506165"));Txt(g,keys[i],351,y+12,14,gold,true);Txt(g,help[i],628,y+12,14,pale);}
   return;
  }
  if(unitRoster){
   for(int i=0;i<roster.Length;i++){
    var t=Campaign.Info(roster[i]);int x=344+(i%2)*520,y=170+(i/2)*105;
    Plate(g,new RectangleF(x,y,500,100),panel,C("#546469"));Portrait(g,t,new RectangleF(x+9,y+12,96,72));
    Txt(g,t.Name,x+118,y+6,20,pale,true);Txt(g,"$ "+t.Price,x+405,y+14,16,gold,true);
    Txt(g,"生命 "+t.Hp+"  视野 "+t.SightCells+" 格"+(t.Hit>0?"  射程 "+(t.Range/(float)Campaign.Tile).ToString("0.#")+" 格":""),x+119,y+34,12,teal);
    var lines=roles[i].Split('|');
    Txt(g,lines[0],x+118,y+55,14,dim);Txt(g,lines[1],x+118,y+76,14,dim);
   }
  }else{
   string[] titles={"01   建立基地与防线","02   采矿与军费","03   生产与交战","04   歼灭与胜利","05   镜头操作","06   地形关卡 · 占领驻防"};
   string[] lines={"两栋地基之间：普通建筑最多隔 2 个空格，炮台 4 个；炮台、民居不延伸扩建范围。",
    "矿车满载 "+Campaign.LoadValue+" 返厂；矿格逐渐采空。教学每格 1200，地形关卡每格 2400；矿石不再生。",
    "左键生产／继续；右键先减排队单，当前单暂停后再取消。每厂最多 30 单，多厂并行，缺钱等待。",
    "敌方无基地／工程车、兵营／工厂、战斗部队或工兵，持续 3 秒后投降并自毁。",
    "雷达提供小地图；间谍卫星 $"+Campaign.Info("satellite").Price+"、耗电 "+(-Campaign.Info("satellite").Energy)+"，需雷达和车辆工厂，供电时全图可见。",
    "房屋先由工兵占领；另可建 $500 碉堡，驻 5 名步枪兵、无需电力；D 撤出。"};
   for(int i=0;i<titles.Length;i++){int y=168+90*i;
    Line(g,348,y,1364,y,C("#506165"));Txt(g,titles[i],350,y+13,20,gold,true);Txt(g,lines[i],351,y+51,17,pale);}
  }
 }
 void DrawVeterancyRules(Graphics g){
  string[] titles={"01   扩建距离看地基边缘","02   战绩决定晋升","03   老兵的强化","04   精英与武器","05   自动恢复","06   载员与经验归属"};
  string[] first={
   "普通建筑最多隔 2 个空格，基础炮台最多隔 4 个；可紧贴放置，不强制留空。",
   "累计击毁价值达到自身价格的 3 倍升老兵、6 倍升精英；精英即为上限。",
   "火力 ×1.1、移速 ×1.2、承伤 ÷1.5；步枪兵和反装甲兵射击间隔 ×0.6。",
   "保留老兵能力，车辆也获得射击间隔 ×0.6；同一加成不会重复相乘。",
   "精英约每 0.93 游戏秒恢复 1 点生命，逐步恢复至满血。",
   "战车击杀的经验归战车；驻防射击的经验归开火步兵，不给整队平分。"};
  string[] second={
   "斜向也按地格轴计距。炮台、民居和任务站不能接力扩建；红色地格仍不可占用。",
   "例如 $700 主战坦克需要 $2100 / $4200 战绩。最后一击或碾压者获得经验。",
   "主战坦克、步兵战车、火炮在老兵阶段保持原射击间隔；生命上限、视野不增加。",
   "精英坦克两发连射，空载步兵战车四发导弹；武器提升依兵种而定。",
   "不扣军费，不要求脱离战斗；矿车自带此能力。工兵、基地车与建筑不靠战斗晋升。",
   "击毁载人战车计入死亡乘员价值；房屋撤出的幸存者不计。维修、友军伤亡不涨经验。"};
  for(int i=0;i<titles.Length;i++){int y=164+91*i;
   Line(g,348,y,1364,y,C("#506165"));Txt(g,titles[i],350,y+10,20,gold,true);
   Txt(g,first[i],351,y+42,16,pale);Txt(g,second[i],351,y+66,14,dim);
  }
 }
}
}
