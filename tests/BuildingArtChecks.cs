using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
namespace Greyhaven {
partial class CommandWindow {
 internal void CheckBuildingPaint(string folder){
  drawingClient=false;
  foreach(var t in Campaign.Types.Where(t=>t.Building)){
   var source=SourceCell(t,0,0);int changed=0,opaque=0,mask=0;
   if(painted[t.Key].Size!=painted[t.Key+"-paint"].Size)throw new Exception("building layer sizes "+t.Key);
   using(var a=new Bitmap(320,320))using(var b=new Bitmap(320,320)){
    using(var g=Graphics.FromImage(a))DrawLayeredSource(g,t.Key,source,new Rectangle(0,0,320,320),paintSwatches[0]);
    using(var g=Graphics.FromImage(b))DrawLayeredSource(g,t.Key,source,new Rectangle(0,0,320,320),paintSwatches[4]);
    for(int y=0;y<320;y+=2)for(int x=0;x<320;x+=2){
     var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);int alpha=painted[t.Key+"-paint"].GetPixel(x,y).A;
     if(p.A!=q.A)throw new Exception("building faction changes geometry "+t.Key);
     if(alpha==0&&p!=q)throw new Exception("neutral building material recolored "+t.Key);
     if(p.A>80)opaque++;if(alpha>80)mask++;if(p!=q)changed++;
    }
   }
   if(changed<100||mask<opaque*.10)throw new Exception("insufficient building faction area "+t.Key);
   Console.WriteLine(t.Key+" paint coverage: "+(100f*mask/opaque).ToString("F1")+"%; neutral material and alpha invariant");
  }
  // Production belt must actually advance, retain its slab, and permit arbitrary faction colors.
  int moving=0;var anim=painted["refining"];
  for(int y=0;y<320;y+=2)for(int x=0;x<320;x+=2)if(anim.GetPixel(x,y)!=anim.GetPixel(x+4*320,y))moving++;
  if(moving<20)throw new Exception("refinery belt frames do not move");
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
   board.Save(Path.Combine(folder,"建筑重绘-v05.png"),ImageFormat.Png);
  }
 }
}
}
