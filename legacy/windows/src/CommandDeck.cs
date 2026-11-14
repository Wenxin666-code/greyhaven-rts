using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Greyhaven {
partial class CommandWindow {
 bool rulesOpen,unitRoster;
 bool missionPicker,frontEnterHeld,riverMissionSelected,islandMissionSelected,relayMissionSelected;
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
 void LaunchMission(){StartFirstChapter();}
 void FrontKey(Keys key){
  if(mapReview){RiverKey(key);return;}
  if(modelReview){ModelKey(key);return;}
  if(rulesOpen){
   if(key==Keys.Escape){rulesOpen=false;hits.Clear();}
   if(key==Keys.Left||key==Keys.Right){int page=veterancyRules?3:commandRules?2:unitRoster?1:0;page=(page+(key==Keys.Right?1:3))%4;unitRoster=page==1;commandRules=page==2;veterancyRules=page==3;}
   hudDirty=true;Invalidate();return;
  }
  if(key==Keys.Escape){
   if(!missionPicker){Close();return;}
   missionPicker=false;hits.Clear();
  }
  else if(key==Keys.Enter){if(missionPicker){LaunchSelectedMission();}else OpenMissions();}
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
  Button(g,64,620,335,46,"规则介绍",()=>{unitRoster=false;commandRules=false;veterancyRules=false;rulesOpen=true;hits.Clear();hudDirty=true;});
  Button(g,64,682,190,40,"退出游戏",Close);
  Button(g,270,682,190,40,"模型预览",OpenModelReview);
 }
 void LaunchSelectedMission(){if(relayMissionSelected)StartRelayMission();else if(islandMissionSelected)StartIslandMission();else if(riverMissionSelected)StartRiverMission();else LaunchMission();}
 void DrawPicker(Graphics g){
  var mission=relayMissionSelected?MissionBriefing.Relay:islandMissionSelected?MissionBriefing.Island:riverMissionSelected?MissionBriefing.Riverbend:MissionBriefing.Prologue;
  Txt(g,"行动选择",58,90,46,pale,true);Txt(g,"MISSION DIRECTORY   /   01 — 04",61,155,14,gold,true);
  Button(g,64,214,373,64,"01    断线之夜",()=>{riverMissionSelected=false;islandMissionSelected=false;relayMissionSelected=false;hudDirty=true;},!riverMissionSelected);
  Button(g,64,295,373,64,"02    河湾镇",()=>{riverMissionSelected=true;islandMissionSelected=false;relayMissionSelected=false;hudDirty=true;},riverMissionSelected&&!islandMissionSelected&&!relayMissionSelected);
  Button(g,64,376,373,64,"03    孤岛要塞",()=>{riverMissionSelected=true;islandMissionSelected=true;relayMissionSelected=false;hudDirty=true;},islandMissionSelected);
  Button(g,64,457,373,64,"04    雾脊中继站",()=>{riverMissionSelected=true;islandMissionSelected=false;relayMissionSelected=true;hudDirty=true;},relayMissionSelected);
  Txt(g,mission.Location,84,540,15,dim);
  Button(g,84,574,330,46,"查看地图",OpenMapReview);

  Button(g,64,674,180,45,"‹  返回主界面",()=>{missionPicker=false;hits.Clear();hudDirty=true;});
  Metal(g,492,91,908,628);Rect(g,492,91,6,83,gold);
  Txt(g,mission.Code,527,116,14,gold,true);
  Txt(g,mission.Title,524,144,38,pale,true);
  var map=new RectangleF(528,219,414,245);if(riverMissionSelected)DrawRiverBrief(g,map);else DrawTrainingBrief(g,map);
  using(var veil=new SolidBrush(Color.FromArgb(75,ink)))g.FillRectangle(veil,map);
  Txt(g,mission.Location,540,438,13,pale,true);
  Txt(g,"行动简报",970,224,21,pale,true);
  BriefingParagraph(g,mission.Story,new RectangleF(970,264,386,200),17,pale);
  Line(g,528,489,1356,489,C("#506165"));
  Txt(g,"任务目标",528,510,15,gold,true);
  BriefingParagraph(g,mission.Orders,new RectangleF(528,540,828,31),18,pale);
  Txt(g,"胜利条件",528,581,13,dim,true);
  BriefingParagraph(g,mission.Victory,new RectangleF(611,578,745,29),16,pale);
  Button(g,1078,620,278,62,relayMissionSelected?"进入雾脊中继站":islandMissionSelected?"进入孤岛要塞":riverMissionSelected?"进入河湾镇":"开始序章",LaunchSelectedMission,true);Txt(g,"›",1320,625,30,ink,true);
 }
}
}
