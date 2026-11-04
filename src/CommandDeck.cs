using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Greyhaven {
partial class CommandWindow {
 bool rulesOpen,unitRoster;
 bool missionPicker,combatMission,frontEnterHeld,relaxed=true;
 static readonly string[] portraitKeys={"squad","at","engineer","ifv","tank","miner"};
 void Portrait(Graphics g,TypeInfo t,RectangleF box){
  int index=Array.IndexOf(portraitKeys,t.Key);
  if(index<0||t.Key=="squad"){DrawCardArt(g,t,box);return;}
  DrawStamp(g,"portraits",new Rectangle(index*98,0,98,72),box);
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
 void LaunchMission(){StartMission(!combatMission,!combatMission||relaxed);hits.Clear();hudDirty=true;Invalidate();}
 void FrontKey(Keys key){
  if(rulesOpen){
   if(key==Keys.Escape){rulesOpen=false;hits.Clear();}
   if(key==Keys.Left||key==Keys.Right)unitRoster=!unitRoster;
   hudDirty=true;Invalidate();return;
  }
  if(key==Keys.Escape&&missionPicker){missionPicker=false;hits.Clear();}
  else if(key==Keys.Enter){if(missionPicker)LaunchMission();else OpenMissions();}
  else if(missionPicker&&(key==Keys.Up||key==Keys.Down))combatMission=!combatMission;
  else if(missionPicker&&combatMission&&(key==Keys.Left||key==Keys.Right))relaxed=!relaxed;
  hudDirty=true;Invalidate();
 }
 void DrawFront(Graphics g){
  var hero=painted["harbor"];DrawStamp(g,"harbor",new Rectangle(0,0,hero.Width,hero.Height),new RectangleF(0,0,W,H));
  using(var shade=new LinearGradientBrush(new Rectangle(0,0,W,H),Color.FromArgb(missionPicker?191:172,ink),Color.FromArgb(missionPicker?143:0,ink),0f))g.FillRectangle(shade,0,0,W,H);
  Metal(g,0,0,W,55);Emblem(g,42,26,13);Rect(g,74,8,4,37,gold);
  Txt(g,"灰港战区司令部",96,14,20,pale,true);Txt(g,"GREYHAVEN   /   OPERATIONS COMMAND",285,20,12,dim,true);
  Txt(g,"●  单人行动",W-143,17,15,teal,true);
  Metal(g,0,H-45,W,45);Txt(g,"GH   /   港区联合行动群",48,H-32,13,dim,true);
  Txt(g,rulesOpen?"← → 切换栏目     Esc 返回主界面":missionPicker?"↑ ↓ 选择任务     ← → 难度     Enter 出击     Esc 返回":"PC   /   WINDOWS  x64     •     FIELD EDITION",W-430,H-32,13,dim,true);
  if(rulesOpen){DrawRules(g);return;}
  if(missionPicker){DrawPicker(g);return;}
  Rect(g,61,203,49,4,gold);Txt(g,"GH—01     /     GREYHAVEN FRONT",62,221,18,gold,true);
  Txt(g,"断线前沿",53,253,89,pale,true);Txt(g,"灰 港 登 陆",64,367,29,pale,true);
  Line(g,64,428,462,428,Color.FromArgb(150,dim));
  Txt(g,"建立滩头基地，消灭港区的敌方部队与建筑。",64,450,19,pale);
  Button(g,64,545,335,61,"开始游戏",OpenMissions,true);Txt(g,"›",365,550,30,ink,true);
  Button(g,64,620,335,46,"规则介绍",()=>{unitRoster=false;rulesOpen=true;hits.Clear();hudDirty=true;});
  Button(g,64,682,190,40,"退出游戏",Close);
 }
 void DrawPicker(Graphics g){
  Txt(g,"行动选择",58,90,46,pale,true);Txt(g,"MISSION DIRECTORY   /   01",61,155,14,gold,true);
  Button(g,64,224,373,75,"01    野外训练",()=>combatMission=false,!combatMission);
  Button(g,64,315,373,75,"02    灰港登陆",()=>combatMission=true,combatMission);
  Button(g,64,674,180,45,"‹  返回主界面",()=>{missionPicker=false;hits.Clear();hudDirty=true;});
  Metal(g,492,91,908,628);Rect(g,492,91,6,83,gold);
  Txt(g,combatMission?"OPERATION  02   /   LANDING":"FIELD SCHOOL  01   /   TRAINING",527,116,14,gold,true);
  Txt(g,combatMission?"灰港登陆":"野外训练",524,144,38,pale,true);
  var map=new RectangleF(528,219,496,290);
  g.DrawImage(radarGround,map);using(var veil=new SolidBrush(Color.FromArgb(108,ink)))g.FillRectangle(veil,map);
  for(int i=1;i<8;i++)Line(g,map.X+map.Width*i/8,map.Top,map.X+map.Width*i/8,map.Bottom,Color.FromArgb(36,pale));
  for(int i=1;i<6;i++)Line(g,map.Left,map.Y+map.Height*i/6,map.Right,map.Y+map.Height*i/6,Color.FromArgb(36,pale));
  foreach(var site in world.Sites){
   float x=map.X+site.P.X*map.Width/Campaign.Width,y=map.Y+site.P.Y*map.Height/Campaign.Height;
   Rect(g,x-4,y-4,8,8,gold);g.DrawRectangle(Pens.DarkSlateGray,x-6,y-6,12,12);
   var label=new RectangleF(x-42,y+10,84,23);Rect(g,label.X,label.Y,label.Width,label.Height,Color.FromArgb(207,ink));Txt(g,site.Name,label.X+10,label.Y+2,14,pale,true);
  }
  Txt(g,"港区地形   /   GH–01",540,478,13,pale,true);
  Txt(g,"行动简报",1051,224,21,pale,true);
  string[] notes=combatMission?new[]{"从西南滩头建立基地。","采矿扩军，搜索东侧敌军。","摧毁全部敌方单位和建筑，","包括矿车及施工中的设施。"}:new[]{"教官引导完成 10 个目标。","学习自建矿厂、生产排队、","步战协同并消灭敌方守军。","可以随时定位当前步骤。"};
  for(int i=0;i<notes.Length;i++)Txt(g,notes[i],1052,274+i*31,17,dim);
  Txt(g,combatMission?"战役难度":"训练设置",528,542,17,pale,true);
  if(combatMission){
   Button(g,643,528,150,42,"宽松",()=>relaxed=true,relaxed);Button(g,808,528,150,42,"标准",()=>relaxed=false,!relaxed);
   Txt(g,relaxed?"起始军费 1700   ·   首波敌袭 175 秒":"起始军费 1150   ·   首波敌袭 92 秒",528,595,16,dim);
  }else Txt(g,"单人教学   ·   无主动进攻波次",644,543,17,teal,true);
  Button(g,1078,620,278,62,combatMission?"发起登陆":"进入训练",LaunchMission,true);Txt(g,"›",1320,625,30,ink,true);
 }
 void DrawInspector(Graphics g){
  var unit=selection.FirstOrDefault();
  if(unit==null)return;
  float x=15,y=H-103;var box=new RectangleF(x,y,508,90);
  Plate(g,box,C("#19282e"),C("#849195"));hits.Add(new HitBox{Box=box,Run=()=>{}});
  Portrait(g,unit.T,new RectangleF(x+6,y+7,96,72));
  Txt(g,selection.Count==1?unit.T.Name:"编队 × "+selection.Count,x+114,y+9,20,pale,true);
  Txt(g,unit.T.Key=="miner"?world.MiningStatus(unit):"HP   "+(int)Math.Ceiling(Math.Max(0,unit.Hp))+" / "+unit.T.Hp,x+114,y+40,13,dim,true);
  Rect(g,x+114,y+66,147,7,ink);Rect(g,x+114,y+66,147*Math.Max(0,Math.Min(1,unit.Hp/unit.T.Hp)),7,teal);
  float bx=x+284;if(unit.T.Key=="mcv")Button(g,bx,y+7,207,34,"展开指挥部   [D]",()=>world.Deploy(unit),true);
  else if(unit.T.Key=="ifv")Button(g,bx,y+7,207,34,"装载 / 卸载   [D]",()=>world.Load(unit));
  else if(unit.T.Key=="miner")Button(g,bx,y+7,207,34,"恢复自动采矿",()=>world.MineOrder(unit,null,null));
  else Button(g,bx,y+7,207,34,"攻击移动   [A]",selection.Any(o=>o.Deployed&&!o.T.Building&&o.T.Hit>0)?(Action)(()=>{place=null;attack=true;}):null,attack);
  Button(g,bx,y+47,207,33,"停止   [S]",unit.T.Building?(Action)null:()=>{foreach(var o in selection)world.Halt(o);attack=false;markedPoint=null;markedEnemy=null;});
 }
}
}
