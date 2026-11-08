using System;
using System.Drawing;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 bool modelReview,modelSpin=true,modelPower=true;float modelAngle=56;Color modelColor=Color.FromArgb(62,133,190);
 int modelUnit=4,modelBuildingMode;float modelClock,modelMotorTime,modelLastClock;string modelMachineKey;bool modelLoaded,modelBuildings,modelWalking;
 static readonly string[] buildingRoster={"hq","power","barracks","factory","radar","post","refinery","satellite","residence","shop","municipal"};
 void OpenModelReview(){modelReview=true;modelBuildings=false;modelUnit=4;modelLoaded=false;modelSpin=true;modelAngle=56;modelColor=UnitColor(0);hits.Clear();hudDirty=true;Invalidate();}
 Rectangle ReviewFrame(TypeInfo t){return t.Key=="tank"?TankCell(0,(int)modelAngle%64):SourceCell(t,0,t.Foot?((int)Math.Round(modelAngle/8)%8)*2:(int)Math.Round(modelAngle/4)%16);}
 void CloseModelReview(){modelReview=false;hits.Clear();ClearStampCache();hudDirty=true;}
 void ModelKey(Keys key){
  if(key==Keys.Escape)CloseModelReview();
  if(key==Keys.Space)modelSpin=!modelSpin;
  if(!modelBuildings&&(key==Keys.Left||key==Keys.Right)){modelSpin=false;modelAngle=(modelAngle+(key==Keys.Left?63:1))%64;}
  hudDirty=true;Invalidate();
 }
 void DrawGaitPreview(Graphics g,TypeInfo t,RectangleF standing){
  int view=(int)Math.Round(modelAngle/8)%8,frame=(int)(modelClock*24)%GaitV09.Frames;float w=standing.Width*3/2.7f;var full=new RectangleF(standing.X+(standing.Width-w)/2,standing.Y+(standing.Width-w)*.91f,w,w);var crop=GaitV09.Crop(t.Key,view);var box=new RectangleF(full.X+full.Width*crop.X/GaitV09.Cell,full.Y+full.Height*crop.Y/GaitV09.Cell,full.Width*crop.Width/GaitV09.Cell,full.Height*crop.Height/GaitV09.Cell);DrawStamp(g,"gait/"+t.Key+"/"+view+"/"+frame,new Rectangle(Point.Empty,crop.Size),box,modelColor);
 }
 void DrawModelBuilding(Graphics g,string key,RectangleF box,bool effects=true){
  if(modelMachineKey!=key||modelClock<modelLastClock){modelMachineKey=key;modelMotorTime=0;}if(modelPower)modelMotorTime+=Math.Max(0,modelClock-modelLastClock);modelLastClock=modelClock;
  if(modelBuildingMode==0){DrawB08Idle(g,key,box,modelColor,modelMotorTime,key=="factory"?(float)(.5-.5*Math.Cos(modelClock*Math.PI)):0);if(effects)FacilityFx(g,key,box,modelMotorTime,modelPower,true);return;}
  if(modelBuildingMode==3&&key=="hq"){var deploy=B08Frame(key,"deploy",Math.Min(95,(int)(Math.Min(1,(modelClock%5)/3.2f)*95)));DrawStamp(g,deploy,B08Source(),box,modelColor);return;}
  if(modelBuildingMode==2){
   float clock=modelClock%7;
   if(clock<2.4f){int level=Math.Min(3,(int)(clock/.6f));DrawB08Idle(g,key,box,modelColor,modelMotorTime,0,level);if(effects)BuildingFireFx(g,key,box,clock,level);}
   else{float age=clock-2.4f;DrawFatalBuilding(g,key,box,modelColor,age);if(effects)FatalBuildingFx(g,key,box,age,1,BlastSeed(key),modelColor);}
   return;
  }
  float progress=Math.Min(1,(modelClock%4.2f)/2.8f);var name=B08Frame(key,"build",Math.Min(BuildFrames-1,(int)(progress*(BuildFrames-1))));
  DrawStamp(g,name,B08Source(),box,modelColor);if(effects)StructuralEffects(g,key,box,progress,false);

 }
 RectangleF ModelBuildingBox(TypeInfo type,RectangleF pad){
  var ink=SpriteInk(type.Key,new Rectangle(0,0,FrameSize,FrameSize));float top=ink.Top;var spec=BuildingV08.Specs[type.Key];foreach(var sockets in spec.Sockets){float size=sockets.Key=="smoke"?52:sockets.Key=="steam"?26:sockets.Key=="dust"?31:sockets.Key=="signal"?8:0;float height=size*LotSpan/((spec.Width+spec.Depth)*Campaign.Tile*Axis);foreach(var point in sockets.Value)top=Math.Min(top,point.Y-height);}
  if(modelBuildingMode==2){PointF[] fires;if(StructuralV10.FireSites.TryGetValue(type.Key,out fires))foreach(var point in fires)top=Math.Min(top,point.Y-42*LotSpan/((spec.Width+spec.Depth)*Campaign.Tile*Axis));}
  float k=Math.Min((pad.Width-90)/(ink.Width+40),(pad.Height-45)/(ink.Bottom-top));return new RectangleF(pad.Left+pad.Width/2-FrameSize*k/2,pad.Top+12-top*k,FrameSize*k,FrameSize*k);
 }
 void DrawModelReview(Graphics g){
  var keys=modelBuildings?buildingRoster:roster;var t=Campaign.Info(keys[modelUnit]);var artKey=t.Key=="miner"&&modelLoaded?"miner-full":t.Key;
  Metal(g,38,82,1396,648);Txt(g,t.Name+" · 模型预览",65,104,30,pale,true);
  Txt(g,modelBuildings?"查看建筑结构、占格比例和势力涂装":"查看转向、装甲细节和势力涂装",67,149,16,dim);
  Button(g,990,119,175,43,"部队模型",()=>{modelBuildings=false;modelUnit=4;modelSpin=true;ClearStampCache();},!modelBuildings);
  Button(g,1180,119,175,43,"建筑模型",()=>{modelBuildings=true;modelUnit=0;modelSpin=true;modelClock=0;modelBuildingMode=0;ClearStampCache();},modelBuildings);
  for(int i=0;i<keys.Length;i++){
   int n=i;Button(g,65+(modelBuildings?i%6*148:i*111),182+(modelBuildings?i/6*45:0),modelBuildings?140:104,40,Campaign.Info(keys[i]).Name,()=>{modelUnit=n;modelLoaded=false;modelClock=0;ClearStampCache();},i==modelUnit);
  }
  var pad=new RectangleF(65,modelBuildings?274:235,884,modelBuildings?362:401);Rect(g,pad.X,pad.Y,pad.Width,pad.Height,C("#263137"));
  // Quiet floor axes make the ground projection legible without distracting terrain.
  var floor=g.Save();g.IntersectClip(pad);
  for(int i=-5;i<=5;i++){
   Line(g,168+i*45,485-i*22,580+i*45,691-i*22,Color.FromArgb(25,dim));
   Line(g,175+i*45,499+i*22,587+i*45,293+i*22,Color.FromArgb(25,dim));
  }
  g.Restore(floor);
  var crop=ReviewFrame(t);var box=t.Building?ModelBuildingBox(t,pad):t.Foot?new RectangleF(300,215,395,395):new RectangleF(210,172,600,600);
  if(t.Building)DrawModelBuilding(g,t.Key,box);else if(t.Foot&&modelWalking)DrawGaitPreview(g,t,box);else DrawStamp(g,artKey,crop,box,modelColor);
  Txt(g,modelBuildings?"占地 "+t.FoundationWidth+" × "+t.FoundationHeight+" 格 · 固定战场朝向":"同一模型的完整转向视图",83,606,15,dim);
  if(!modelBuildings){
  Button(g,69,653,140,43,modelSpin?(modelWalking&&t.Foot?"暂停动画":"暂停转动"):(modelWalking&&t.Foot?"播放动画":"自动转动"),()=>modelSpin=!modelSpin);
  Button(g,224,653,100,43,"‹ 左转",()=>{modelSpin=false;modelAngle=(modelAngle+63)%64;});
  Button(g,339,653,100,43,"右转 ›",()=>{modelSpin=false;modelAngle=(modelAngle+1)%64;});
  }
  if(modelBuildings){
   bool headquarters=t.Key=="hq";int width=headquarters?102:120;
   Button(g,69,653,width,43,"待机运转",()=>{modelBuildingMode=0;modelClock=0;},modelBuildingMode==0);
   Button(g,headquarters?181:201,653,width,43,"搭建",()=>{modelBuildingMode=1;modelClock=0;},modelBuildingMode==1);
   Button(g,headquarters?293:333,653,width,43,"摧毁",()=>{modelBuildingMode=2;modelClock=0;},modelBuildingMode==2);
   if(headquarters)Button(g,405,653,105,43,"展开慢放",()=>{modelBuildingMode=3;modelClock=0;},modelBuildingMode==3);
   Button(g,headquarters?636:600,653,headquarters?110:125,43,modelPower?"供电中":"停电",()=>modelPower=!modelPower);
   Button(g,headquarters?522:465,653,width,43,modelSpin?"暂停":"播放",()=>modelSpin=!modelSpin);
  }
  Button(g,modelBuildings?758:454,653,180,43,"返回主界面",CloseModelReview);
  Txt(g,"势力涂装",990,210,23,gold,true);
  string[] names={"蓝","红","绿","金","紫","青","橙","白"};
  for(int i=0;i<paintSwatches.Length;i++){
   int n=i;float x=988+i%4*97,y=256+i/4*64;
   Button(g,x,y,84,50,names[i],()=>{modelColor=paintSwatches[n];ClearStampCache();},modelColor.ToArgb()==paintSwatches[i].ToArgb());
   Rect(g,x+57,y+15,15,20,paintSwatches[i]);
  }
  Button(g,988,399,185,42,"自定义颜色",()=>{using(var picker=new ColorDialog{Color=modelColor,FullOpen=true})if(picker.ShowDialog(this)==DialogResult.OK){modelColor=picker.Color;ClearStampCache();}});
  Button(g,1187,399,185,42,"应用到己方势力",()=>SetUnitColor(0,modelColor));
  Txt(g,modelBuildings?"涂装区随颜色变化；金属、混凝土和玻璃保持原色。":"涂装区随颜色变化；金属、履带和玻璃保持原色。",988,465,14,dim);
  Txt(g,"部队与建筑共用势力色；中性材质保持原色。",988,492,14,dim);
  Txt(g,"战场大小示意",989,548,16,pale,true);
  if(t.Building){
   DrawModelBuilding(g,t.Key,BuildingRect(Campaign.Footprint(t,PointF.Empty),new PointF(1180,668),DefaultZoom),false);
   Txt(g,"默认镜头 · 原有占格比例",1090,706,13,dim);
  }else{
  if(t.Foot&&modelWalking){DrawGaitPreview(g,t,SoldierBox(new PointF(1048,619),t.Size*DefaultZoom));DrawGaitPreview(g,t,SoldierBox(new PointF(1213,619),t.Size*2.4f));}else{
  DrawStamp(g,artKey,crop,t.Foot?SoldierBox(new PointF(1048,619),t.Size*DefaultZoom):VehicleRect(t,new PointF(1048,619),t.Size*DefaultZoom),modelColor);
  DrawStamp(g,artKey,crop,t.Foot?SoldierBox(new PointF(1213,619),t.Size*2.4f):VehicleRect(t,new PointF(1213,619),t.Size*2.4f),modelColor);
  }
  if(t.Foot)Button(g,660,653,180,43,modelWalking?"查看站立":"查看行走",()=>{modelWalking=!modelWalking;modelClock=0;});
  if(t.Key=="miner")Button(g,660,653,180,43,modelLoaded?"切换空载":"切换满载",()=>modelLoaded=!modelLoaded);
  Txt(g,"默认镜头",1015,635,13,dim);Txt(g,"放大镜头",1180,650,13,dim);
  }
 }
}
}
