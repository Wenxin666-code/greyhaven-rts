using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Greyhaven {
partial class CommandWindow {
 bool rulesOpen,unitRoster;
 bool missionPicker,frontEnterHeld,riverMissionSelected;
 void Portrait(Graphics g,TypeInfo t,RectangleF box){
  DrawCardArt(g,t,box);
 }
 void Metal(Graphics g,float x,float y,float w,float h){
  string key="metal-"+(int)w+"-"+(int)h;
  if(!painted.ContainsKey(key)){
   var tile=new Bitmap((int)w,(int)h,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
   var bounds=new RectangleF(0,0,w,h);
   using(var cg=Graphics.FromImage(tile)){
    using(var brush=new TextureBrush(painted["plate"],WrapMode.TileFlipXY))cg.FillRectangle(brush,bounds);
    using(var shade=new LinearGradientBrush(bounds,Color.FromArgb(80,ink),Color.FromArgb(170,ink),90f))cg.FillRectangle(shade,bounds);
    Line(cg,1,1,w-1,1,C("#697579"));Line(cg,0,h-2,w,h-2,C("#070e12"),2);
   }
   painted[key]=tile;
  }
  DrawStamp(g,key,new Rectangle(0,0,(int)w,(int)h),new RectangleF(x,y,w,h));
 }
 void Emblem(Graphics g,float x,float y,float size){
  using(var p=new Pen(pale,2)){
   g.DrawPolygon(p,new[]{new PointF(x,y-size*.8f),new PointF(x+size,y-size*.2f),new PointF(x,y+size*.4f),new PointF(x-size,y-size*.2f)});
   for(int i=1;i<=2;i++)g.DrawLines(p,new[]{new PointF(x-size,y-size*.2f+i*4),new PointF(x,y+size*.4f+i*4),new PointF(x+size,y-size*.2f+i*4)});
  }
 }
 void OpenMissions(){
  briefing=true;paused=false;help=false;missionPicker=true;drag=false;panning=false;Capture=false;
  hits.Clear();hudDirty=true;Invalidate();
 }
 void LaunchMission(){StartMission(true,true);hits.Clear();hudDirty=true;Invalidate();}
 void FrontKey(Keys key){
  if(mapReview){RiverKey(key);return;}
  if(modelReview){ModelKey(key);return;}
  if(rulesOpen){
   if(key==Keys.Escape){rulesOpen=false;hits.Clear();}
   if(key==Keys.Left||key==Keys.Right){int page=commandRules?2:unitRoster?1:0;page=(page+(key==Keys.Right?1:2))%3;unitRoster=page==1;commandRules=page==2;}
   hudDirty=true;Invalidate();return;
  }
  if(key==Keys.Escape&&missionPicker){missionPicker=false;hits.Clear();}
  else if(key==Keys.Enter){if(missionPicker){if(riverMissionSelected)StartRiverMission();else LaunchMission();}else OpenMissions();}
  hudDirty=true;Invalidate();
 }
 void DrawFront(Graphics g){
  if(mapReview){DrawMapReview(g);return;}
  var hero=painted["harbor"];DrawStamp(g,"harbor",new Rectangle(0,0,hero.Width,hero.Height),new RectangleF(0,0,W,H));
  using(var shade=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(missionPicker?191:172,ink),Color.FromArgb(missionPicker?143:0,ink),0f))g.FillRectangle(shade,0,0,W,H);
  Metal(g,0,0,W,55);Emblem(g,42,26,13);Rect(g,74,8,4,37,gold);
  Txt(g,"灰港战区司令部",96,14,20,pale,true);Txt(g,"GREYHAVEN   /   OPERATIONS COMMAND",285,20,12,dim,true);
  Txt(g,"●  单人行动",W-143,17,15,teal,true);
  Metal(g,0,H-45,W,45);Txt(g,"GH   /   港区联合行动群",48,H-32,13,dim,true);
  Txt(g,modelReview?(modelBuildings?"选择建筑与势力颜色     Esc 返回":"← → 转向     空格 自动转动     Esc 返回"):rulesOpen?"← → 切换栏目     Esc 返回主界面":missionPicker?"Enter 进入所选关卡     Esc 返回主界面":"PC   /   WINDOWS  x64     •     FIELD EDITION",W-430,H-32,13,dim,true);
  if(modelReview){DrawModelReview(g);return;}
  if(rulesOpen){DrawRules(g);return;}
  if(missionPicker){DrawPicker(g);return;}
  Rect(g,61,203,49,4,gold);Txt(g,"GH—01     /     GREYHAVEN FRONT",62,221,18,gold,true);
  Txt(g,"断线前沿",53,253,89,pale,true);Txt(g,"灰 港 登 陆",64,367,29,pale,true);
  Line(g,64,428,462,428,Color.FromArgb(150,dim));
  Txt(g,"建立滩头基地，消灭港区的敌方部队与建筑。",64,450,19,pale);
  Button(g,64,545,335,61,"开始游戏",OpenMissions,true);Txt(g,"›",365,550,30,ink,true);
  Button(g,64,620,335,46,"规则介绍",()=>{unitRoster=false;rulesOpen=true;hits.Clear();hudDirty=true;});
  Button(g,64,682,190,40,"退出游戏",Close);
  Button(g,270,682,190,40,"模型预览",OpenModelReview);
 }
 void DrawPicker(Graphics g){
  Txt(g,"行动选择",58,90,46,pale,true);Txt(g,"MISSION DIRECTORY   /   01 — 02",61,155,14,gold,true);
  Button(g,64,224,373,75,"01    野外训练",()=>{riverMissionSelected=false;hudDirty=true;},!riverMissionSelected);
  Button(g,64,320,373,75,"02    河湾镇",()=>{riverMissionSelected=true;hudDirty=true;},riverMissionSelected);
  Txt(g,"三方交战 / 河流 / 高地 / 居民区",84,402,15,dim);
  Button(g,84,433,330,46,"查看新地图",OpenMapReview);

  Button(g,64,674,180,45,"‹  返回主界面",()=>{missionPicker=false;hits.Clear();hudDirty=true;});
  Metal(g,492,91,908,628);Rect(g,492,91,6,83,gold);
  Txt(g,riverMissionSelected?"OPERATION 02   /   RIVERBEND":"FIELD SCHOOL  01   /   TRAINING",527,116,14,gold,true);
  Txt(g,riverMissionSelected?"河湾镇 · 三方交战":"野外训练",524,144,38,pale,true);
  var map=new RectangleF(528,219,496,290);if(riverMissionSelected)DrawRiverBrief(g,map);else DrawTrainingBrief(g,map);
  using(var veil=new SolidBrush(Color.FromArgb(75,ink)))g.FillRectangle(veil,map);
  Txt(g,riverMissionSelected?"220 × 150 格   /   GH–02":"教学港区   /   GH–01",540,478,13,pale,true);
  Txt(g,"行动简报",1051,224,21,pale,true);
  var notes=riverMissionSelected?new[]{"我方：西南 · 蓝色","敌方一：东北 · 红色","敌方二：东南 · 绿色","三方独立采矿、生产。","工兵占房，步兵驻防。","消灭两方全部单位与建筑。"}:new[]{"教官引导完成 10 个目标。","学习自建矿厂、生产排队、","步战协同并消灭敌方守军。","可以随时定位当前步骤。"};
  for(int i=0;i<notes.Length;i++)Txt(g,notes[i],1052,274+i*31,17,dim);
  Txt(g,"初次指挥建议先完成第一教学关",528,543,19,teal,true);
  Txt(g,"河湾镇：三方独立建设；工兵占房、步兵驻防。",528,595,16,dim);
  Button(g,1078,620,278,62,riverMissionSelected?"进入河湾镇":"进入训练",riverMissionSelected?(Action)StartRiverMission:LaunchMission,true);Txt(g,"›",1320,625,30,ink,true);
 }
 void DrawInspector(Graphics g){
  var unit=selection.FirstOrDefault();
  if(unit==null)return;
  float x=15,y=H-103;var box=new RectangleF(x,y,508,90);
  Plate(g,box,C("#19282e"),C("#849195"));hits.Add(new HitBox{Box=box,Run=()=>{}});
  Portrait(g,unit.T,new RectangleF(x+6,y+7,96,72));
  Txt(g,selection.Count==1?(world.RepairVehicle(unit)?"工兵维修车":unit.T.Name):"编队 × "+selection.Count,x+114,y+9,20,pale,true);
  if(unit.T.Key=="miner"){
   var status=world.MiningStatus(unit).Split('·');Txt(g,status[0].Trim(),x+114,y+35,12,gold,true);
   if(status.Length>1)Txt(g,status[1].Trim(),x+114,y+54,11,dim);
  }else Txt(g,"HP   "+(int)Math.Ceiling(Math.Max(0,unit.Hp))+" / "+unit.T.Hp,x+114,y+40,13,dim,true);
  float barY=y+(unit.T.Key=="miner"?76:66);
  Rect(g,x+114,barY,147,5,ink);Rect(g,x+114,barY,147*Math.Max(0,Math.Min(1,unit.Hp/unit.T.Hp)),5,teal);
  float bx=x+284;if(unit.T.Civilian)Button(g,bx,y+7,207,34,"撤出驻军 "+world.HouseCount(unit)+" / "+world.HouseCapacity(unit)+"  [D]",()=>world.EvacuateHouse(unit));
  else if(unit.T.Key=="hq")Button(g,bx,y+7,207,34,"收起基地   [D]",()=>world.Relocate(unit,unit.P));
  else if(unit.T.Key=="mcv")Button(g,bx,y+7,207,34,"展开指挥部   [D]",()=>world.Deploy(unit),true);
  else if(unit.T.Key=="ifv")Button(g,bx,y+7,207,34,"装载 / 卸载   [D]",()=>world.Load(unit));
  else if(unit.T.Key=="miner")Button(g,bx,y+7,207,34,"恢复自动采矿",()=>world.MineOrder(unit,null,null));
  else if(world.ProductionBuilding(unit))Button(g,bx,y+7,207,34,world.PrimaryFactory(unit.Side,unit.T.Key)==unit?"主要生产建筑":"设为主要生产建筑",()=>world.SetPrimary(unit));
  else Button(g,bx,y+7,207,34,"攻击移动",selection.Any(o=>o.Deployed&&!o.T.Building&&world.Armed(o))?(Action)(()=>{place=null;attack=true;}):null,attack);
  Button(g,bx,y+47,100,33,"停止 [S]",unit.T.Building?(Action)null:()=>{foreach(var o in selection)world.Halt(o);attack=false;markedPoint=null;markedEnemy=null;});
  Button(g,bx+107,y+47,100,33,"警戒 [G]",unit.Deployed?(System.Action)GuardSelection:null);
 }
}
}
