using System;
using System.Drawing;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 bool modelReview,modelSpin=true;float modelAngle=56;Color modelColor=Color.FromArgb(62,133,190);
 int modelUnit=4;bool modelLoaded,modelBuildings;
 static readonly string[] buildingRoster={"hq","power","barracks","factory","radar","post","refinery","satellite"};
 void OpenModelReview(){modelReview=true;modelBuildings=false;modelUnit=4;modelLoaded=false;modelSpin=true;modelAngle=56;modelColor=UnitColor(0);hits.Clear();hudDirty=true;Invalidate();}
 Rectangle ReviewFrame(TypeInfo t){return t.Key=="tank"?TankCell(0,(int)modelAngle%64):SourceCell(t,0,t.Foot?((int)Math.Round(modelAngle/8)%8)*2:(int)Math.Round(modelAngle/4)%16);}
 void CloseModelReview(){modelReview=false;hits.Clear();ClearStampCache();hudDirty=true;}
 void ModelKey(Keys key){
  if(key==Keys.Escape)CloseModelReview();
  if(key==Keys.Space&&!modelBuildings)modelSpin=!modelSpin;
  if(!modelBuildings&&(key==Keys.Left||key==Keys.Right)){modelSpin=false;modelAngle=(modelAngle+(key==Keys.Left?63:1))%64;}
  hudDirty=true;Invalidate();
 }
 void DrawModelReview(Graphics g){
  var keys=modelBuildings?buildingRoster:roster;var t=Campaign.Info(keys[modelUnit]);var artKey=t.Key=="miner"&&modelLoaded?"miner-full":t.Key;
  Metal(g,38,82,1396,648);Txt(g,t.Name+" · 模型预览",65,104,30,pale,true);
  Txt(g,modelBuildings?"查看建筑结构、占格比例和势力涂装":"查看转向、装甲细节和势力涂装",67,149,16,dim);
  Button(g,990,119,175,43,"部队模型",()=>{modelBuildings=false;modelUnit=4;modelSpin=true;ClearStampCache();},!modelBuildings);
  Button(g,1180,119,175,43,"建筑模型",()=>{modelBuildings=true;modelUnit=0;modelSpin=false;ClearStampCache();},modelBuildings);
  for(int i=0;i<keys.Length;i++){
   int n=i;Button(g,65+i*111,182,104,40,Campaign.Info(keys[i]).Name,()=>{modelUnit=n;modelLoaded=false;ClearStampCache();},i==modelUnit);
  }
  var pad=new RectangleF(65,235,884,401);Rect(g,pad.X,pad.Y,pad.Width,pad.Height,C("#263137"));
  // Quiet floor axes make the ground projection legible without distracting terrain.
  var floor=g.Save();g.IntersectClip(pad);
  for(int i=-5;i<=5;i++){
   Line(g,168+i*45,485-i*22,580+i*45,691-i*22,Color.FromArgb(25,dim));
   Line(g,175+i*45,499+i*22,587+i*45,293+i*22,Color.FromArgb(25,dim));
  }
  g.Restore(floor);
  var crop=ReviewFrame(t);var box=t.Building?new RectangleF(210,110,600,600):t.Foot?new RectangleF(300,215,395,395):new RectangleF(210,172,600,600);
  DrawStamp(g,artKey,crop,box,modelColor);
  Txt(g,modelBuildings?"占地 "+t.FoundationWidth+" × "+t.FoundationHeight+" 格 · 固定战场朝向":"同一模型的完整转向视图",83,606,15,dim);
  if(!modelBuildings){
  Button(g,69,653,140,43,modelSpin?"暂停转动":"自动转动",()=>modelSpin=!modelSpin);
  Button(g,224,653,100,43,"‹ 左转",()=>{modelSpin=false;modelAngle=(modelAngle+63)%64;});
  Button(g,339,653,100,43,"右转 ›",()=>{modelSpin=false;modelAngle=(modelAngle+1)%64;});
  }
  Button(g,454,653,180,43,"返回主界面",CloseModelReview);
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
   DrawStamp(g,artKey,crop,BuildingRect(Campaign.Footprint(t,PointF.Empty),new PointF(1180,668),DefaultZoom),modelColor);
   Txt(g,"默认镜头 · 原有占格比例",1090,706,13,dim);
  }else{
  DrawStamp(g,artKey,crop,t.Foot?SoldierBox(new PointF(1048,619),t.Size*DefaultZoom):VehicleRect(t,new PointF(1048,619),t.Size*DefaultZoom),modelColor);
  DrawStamp(g,artKey,crop,t.Foot?SoldierBox(new PointF(1213,619),t.Size*2.4f):VehicleRect(t,new PointF(1213,619),t.Size*2.4f),modelColor);
  if(t.Key=="miner")Button(g,660,653,180,43,modelLoaded?"切换空载":"切换满载",()=>modelLoaded=!modelLoaded);
  Txt(g,"默认镜头",1015,635,13,dim);Txt(g,"放大镜头",1180,650,13,dim);
  }
 }
}
}
