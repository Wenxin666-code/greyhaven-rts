using System;
using System.Drawing;
using System.Collections.Generic;
namespace Greyhaven {
partial class CommandWindow {
 readonly Dictionary<string,Bitmap> painted=new Dictionary<string,Bitmap>();
 const int FrameSize=320,LotSpan=224,GroundX=160,GroundY=220;
 static RectangleF BuildingRect(RectangleF footprint,PointF ground,float magnification){
  float k=(footprint.Width+footprint.Height)*Axis*magnification/LotSpan;
  return new RectangleF(ground.X-GroundX*k,ground.Y-GroundY*k,FrameSize*k,FrameSize*k);
 }
 static RectangleF VehicleRect(TypeInfo type,PointF ground,float size){
  size/=.76f;return new RectangleF(ground.X-size/2,ground.Y-size*188f/FrameSize,size,size);
 }
 static Rectangle SourceCell(TypeInfo t,int row,int dir){
  if(t.Building)return new Rectangle(0,row*FrameSize,FrameSize,FrameSize);
  if(t.Foot)return new Rectangle(dir/4*256,row*256,256,256);
  if(t.Key=="tank")return TankCell(row,dir*4);
  return new Rectangle(dir%8*FrameSize,(row*2+dir/8)*FrameSize,FrameSize,FrameSize);
 }
 static int TankFrame(Piece o){return ((int)Math.Round(o.Turn*4)+56)%64;}
 static Rectangle TankCell(int side,int angle){return new Rectangle(angle%8*192,(side*8+angle/8)*192,192,192);}
 void PaintFrame(Graphics g,TypeInfo t,int side,int dir,RectangleF r){
  var crop=SourceCell(t,side,dir);
  g.DrawImage(painted[t.Key],r,crop,GraphicsUnit.Pixel);
 }
 readonly Dictionary<string,Rectangle> cardCrops=new Dictionary<string,Rectangle>();
 void DrawCardArt(Graphics g,TypeInfo t,RectangleF slot){
  var sheet=painted[t.Key];Rectangle source;
  if(!cardCrops.TryGetValue(t.Key,out source)){
   var r=SourceCell(t,0,14);int l=r.Right,top=r.Bottom,right=r.Left,bottom=r.Top;
   for(int y=r.Top;y<r.Bottom;y+=2)for(int x=r.Left;x<r.Right;x+=2)if(sheet.GetPixel(x,y).A>64){
    l=Math.Min(l,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
   }
   source=Rectangle.Intersect(r,Rectangle.FromLTRB(l-3,top-3,right+4,bottom+4));cardCrops[t.Key]=source;
  }
  float k=Math.Min(slot.Width/source.Width,slot.Height/source.Height),w=source.Width*k,h=source.Height*k;
  g.DrawImage(sheet,new RectangleF(slot.X+(slot.Width-w)/2,slot.Y+(slot.Height-h)/2,w,h),source,GraphicsUnit.Pixel);
 }
 void BuildingArt(Graphics g,string key,int row,RectangleF box,float built){
  var state=g.Save();
  if(built<1){float reveal=.19f+.81f*built;g.IntersectClip(new RectangleF(box.X,box.Bottom-box.Height*reveal,box.Width,box.Height*reveal));}
  g.DrawImage(painted[key],box,new RectangleF(0,row*FrameSize,FrameSize,FrameSize),GraphicsUnit.Pixel);g.Restore(state);
 }
 void DrawLive(Graphics g,TypeInfo t,int side,int dir,PointF p,float s,float built){
  if(t.Foot){
   int n=t.Key=="squad"?4:2;
   for(int j=0;j<n;j++){float x=p.X+((j%2)-.5f)*s*.26f,y=p.Y+(j/2-.5f)*s*.18f;
    PaintFrame(g,t,side,dir,new RectangleF(x-s*.22f,y-s*.40f,s*.44f,s*.44f));}
  }else if(t.Building)BuildingArt(g,t.Key,side,BuildingRect(Campaign.Footprint(t,PointF.Empty),p,s/t.Size),built);
  else PaintFrame(g,t,side,dir,VehicleRect(t,p,s));
 }
 void DrawFieldSprite(Graphics g,Piece o,PointF p,float size){
  int dir=(o.Dir+14)%16;
  if(o.T.Key=="tank"){
   int frame=TankFrame(o);
   g.DrawImage(painted["tank"],VehicleRect(o.T,p,size),TankCell(o.Alive?o.Side:2,frame),GraphicsUnit.Pixel);return;
  }
  if(!o.Alive){
   if(o.T.Building)BuildingArt(g,o.T.Key,2,BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom),1);
   else if(!o.T.Foot)PaintFrame(g,o.T,2,dir,VehicleRect(o.T,p,size));
   else Sprite(g,o.T,o.Side,dir,o.Dead<.4f?1+o.Side:0,new RectangleF(p.X-size/2,p.Y-size*.59f,size,size));
   return;
  }
  if(o.T.Foot&&o.Path.Count>0)p.Y-=(float)Math.Abs(Math.Sin(world.Time*10+o.Id))*zoom;
  DrawLive(g,o.T,o.Side,dir,p,size,o.Ready);
  if(o.T.Key=="factory"&&o.Ready>=1){
   float open=Math.Min(1,o.Door*3);
   if(o.Queue.Count>0)open=Math.Max(open,Math.Max(0,1-(o.Queue.Peek().Seconds-o.Work)/.6f));
   var box=BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom);var st=g.Save();
   g.TranslateTransform(box.Left,box.Top);g.ScaleTransform(box.Width/FrameSize,box.Height/FrameSize);
   // Reprojected upper corners of the roller door. Travel stays vertical in every zoom level.
   float h=39.49f*(1-open);
   using(var brush=new SolidBrush(C("#49565b")))g.FillPolygon(brush,new[]{new PointF(82.43f,179.00f),new PointF(149.8f,209.47f),new PointF(149.8f,209.47f+h),new PointF(82.43f,179.00f+h)});
   for(int j=4;j<h;j+=6)Line(g,82.43f,179.00f+j,149.8f,209.47f+j,C("#77817e"),1);g.Restore(st);
  }
 }
 bool MobileHit(Piece o,PointF p,float size,PointF screen){
  int dir=(o.Dir+14)%16;
  if(o.T.Foot){
   if(Math.Abs(p.X-screen.X)<size*.12f&&Math.Abs(p.Y-screen.Y)<size*.12f)return true;
   for(int i=0;i<(o.T.Key=="squad"?4:2);i++){
    var box=new RectangleF(p.X+((i%2)-.5f)*size*.26f-size*.22f,p.Y+(i/2-.5f)*size*.18f-size*.4f,size*.44f,size*.44f);
    if(PixelHit(o.T.Key,SourceCell(o.T,o.Side,dir),box,screen))return true;
   }return false;
  }
  var source=o.T.Key=="tank"?TankCell(o.Side,TankFrame(o)):SourceCell(o.T,o.Side,dir);
  return PixelHit(o.T.Key,source,VehicleRect(o.T,p,size),screen);
 }
 bool PixelHit(string key,Rectangle src,RectangleF box,PointF screen){
  if(!box.Contains(screen))return false;int x=(int)((screen.X-box.X)*src.Width/box.Width),y=(int)((screen.Y-box.Y)*src.Height/box.Height);
  return painted[key].GetPixel(src.X+Math.Min(src.Width-1,x),src.Y+Math.Min(src.Height-1,y)).A>45;
 }
 bool InkHit(string key,int row,RectangleF box,PointF screen){
  if(!box.Contains(screen))return false;
  int x=(int)((screen.X-box.X)*FrameSize/box.Width),y=(int)((screen.Y-box.Y)*FrameSize/box.Height);
  return painted[key].GetPixel(Math.Min(FrameSize-1,x),row*FrameSize+Math.Min(FrameSize-1,y)).A>40;
 }
}
}
