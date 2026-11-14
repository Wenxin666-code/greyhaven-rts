using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
namespace Greyhaven {
partial class CommandWindow {
 internal void CheckBuildingPaint(string folder){
  drawingClient=false;
  CheckBuildingAssets();
  if(folder==null)return;
  using(var board=new Bitmap(1800,1160))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
   Txt(g,"建筑重绘 · 统一硬表面工业风格",32,20,29,pale,true);
   Txt(g,"独立势力色 / 同一斜视投影 / 原有占格 / 实际游戏素材",33,65,18,dim);
   string[] keys={"hq","barracks","factory","power","radar","refinery","post","harbormaster"};
   for(int i=0;i<keys.Length;i++){
    var key=keys[i];float left=i%4*450,top=103+i/4*420;
    var t=Campaign.Info(key);var lot=t==null?new RectangleF(0,0,48,48):Campaign.Footprint(t,PointF.Empty);
    float z=260/((lot.Width+lot.Height)*Axis);var at=new PointF(left+225,top+230);
    var box=BuildingRect(lot,at,z);
    DrawGroundShadow(g,key,new Rectangle(0,0,320,320),box,false,8);BuildingArt(g,key,0,box,1);
    Txt(g,t==null?"港务中心（中立）":t.Name,left+28,top+307,23,pale,true);
    Txt(g,(int)(lot.Width/24)+" × "+(int)(lot.Height/24)+" 格",left+28,top+343,17,gold);
   }
   Line(g,32,948,1768,948,dim);Txt(g,"同一建筑 · 任意势力色",32,965,19,gold,true);
   for(int i=0;i<8;i++)DrawStamp(g,"hq",new Rectangle(0,0,320,320),new RectangleF(215+i*190,950,185,185),paintSwatches[i]);
   board.Save(Path.Combine(folder,"建筑重绘-v09.png"),ImageFormat.Png);
  }
 }
}
}
