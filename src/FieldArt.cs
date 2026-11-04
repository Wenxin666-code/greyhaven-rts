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
 const int TankCellSize=288;
 static Rectangle TankCell(int side,int angle){return new Rectangle(angle%8*TankCellSize,(side*8+angle/8)*TankCellSize,TankCellSize,TankCellSize);}
 // Reuse the final physical-size sprite while the camera scale is unchanged.
 bool drawingClient;
 float stampScale,stampZoom;long stampBytes;
 readonly Dictionary<Tuple<string,Rectangle,Size>,Bitmap> stamps=new Dictionary<Tuple<string,Rectangle,Size>,Bitmap>();
 void ClearStampCache(){foreach(var img in stamps.Values)img.Dispose();stamps.Clear();stampBytes=0;}
 void DrawStamp(Graphics g,string key,Rectangle source,RectangleF box){
  if(!drawingClient){g.DrawImage(painted[key],box,source,GraphicsUnit.Pixel);return;}
  if(!g.IsVisible(box))return;
  if(stampScale!=scale||stampZoom!=zoom){ClearStampCache();stampScale=scale;stampZoom=zoom;}
  var size=new Size(Math.Max(1,(int)Math.Round(box.Width*scale)),Math.Max(1,(int)Math.Round(box.Height*scale)));
  var id=Tuple.Create(key,source,size);Bitmap stamp;
  if(!stamps.TryGetValue(id,out stamp)){
   long bytes=4L*size.Width*size.Height;
   if(stampBytes+bytes>64L*1024*1024){g.DrawImage(painted[key],box,source,GraphicsUnit.Pixel);return;}
   stamp=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
   using(var target=Graphics.FromImage(stamp)){
    target.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.Bilinear;
    target.DrawImage(painted[key],new Rectangle(Point.Empty,size),source,GraphicsUnit.Pixel);
   }
   stamps.Add(id,stamp);stampBytes+=bytes;
  }
  var state=g.Save();g.ResetTransform();g.DrawImageUnscaled(stamp,(int)Math.Round(ox+box.X*scale),(int)Math.Round(oy+box.Y*scale));g.Restore(state);
 }
 void PaintFrame(Graphics g,TypeInfo t,int side,int dir,RectangleF r){
  var crop=SourceCell(t,side,dir);
  DrawStamp(g,t.Key,crop,r);
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
  DrawStamp(g,t.Key,source,new RectangleF(slot.X+(slot.Width-w)/2,slot.Y+(slot.Height-h)/2,w,h));
 }
 void BuildingArt(Graphics g,string key,int row,RectangleF box,float built){
  var state=g.Save();
  if(built<1){float reveal=.16f+.84f*built*built*(3-2*built);g.IntersectClip(new RectangleF(box.X,box.Bottom-box.Height*reveal,box.Width,box.Height*reveal));}
  DrawStamp(g,key,new Rectangle(0,row*FrameSize,FrameSize,FrameSize),box);g.Restore(state);
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
   DrawStamp(g,"tank",TankCell(o.Alive?o.Side:2,frame),VehicleRect(o.T,p,size));return;
  }
  if(!o.Alive){
   if(o.T.Building)BuildingArt(g,o.T.Key,2,BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom),1);
   else if(!o.T.Foot)PaintFrame(g,o.T,2,dir,VehicleRect(o.T,p,size));
   else Sprite(g,o.T,o.Side,dir,o.Dead<.4f?1+o.Side:0,new RectangleF(p.X-size/2,p.Y-size*.59f,size,size));
   return;
  }
  if(o.T.Key=="refinery"&&o.Ready>=1){
   int frame=(int)(o.MillPhase*16)%16;var rect=BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom);
   DrawStamp(g,"refining",new Rectangle(frame%8*FrameSize,(2*o.Side+frame/8)*FrameSize,FrameSize,FrameSize),rect);return;
  }
  if(o.T.Key=="miner"&&o.Cargo>10){
   DrawStamp(g,"miner-full",SourceCell(o.T,o.Side,dir),VehicleRect(o.T,p,size));return;
  }
  if(o.T.Foot&&o.Path.Count>0)p.Y-=(float)Math.Abs(Math.Sin(world.Time*10+o.Id))*zoom;
  DrawLive(g,o.T,o.Side,dir,p,size,o.Ready);
  if(o.T.Building&&o.Ready<1){
   var box=BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom);
   float t=o.Ready,front=box.Bottom-box.Height*(.16f+.84f*t*t*(3-2*t));
   float span=box.Width*.21f,travel=(float)Math.Sin(world.Time*2.4f+o.Id)*span;
   Line(g,p.X-span,front,p.X+span,front,Color.FromArgb(135,gold),1);
   Line(g,p.X+travel-3,front,p.X+travel+3,front,gold,2);Line(g,p.X+travel,front-3,p.X+travel,front+3,pale);
  }
  if(o.T.Key=="factory"&&o.Ready>=1){
   float open=Math.Min(1,o.Door*3);
   if(o.Queue.Count>0)open=Math.Max(open,Math.Max(0,1-(o.Queue.Peek().Seconds-o.Work)/.6f));
   open=open*open*(3-2*open);var box=BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom);var st=g.Save();
   g.TranslateTransform(box.Left,box.Top);g.ScaleTransform(box.Width/FrameSize,box.Height/FrameSize);
   // Reprojected upper corners of the roller door. Travel stays vertical in every zoom level.
   const float door=39.49f;float h=door*(1-open);
   if(open>.01f){
    using(var shade=new SolidBrush(C("#202929")))g.FillPolygon(shade,new[]{new PointF(82.43f,179+h),new PointF(149.8f,209.47f+h),new PointF(149.8f,209.47f+door),new PointF(82.43f,179+door)});
    Line(g,84,179+h,148,208.9f+h,C("#8a9182"),2.2f);
    Rect(g,82,177,4,2,gold);Rect(g,149,207,4,2,gold);
   }
   if(h>.2f){
    using(var brush=new SolidBrush(C("#49565b")))g.FillPolygon(brush,new[]{new PointF(82.43f,179),new PointF(149.8f,209.47f),new PointF(149.8f,209.47f+h),new PointF(82.43f,179+h)});
    for(float j=h-3;j>0;j-=6)Line(g,82.43f,179+j,149.8f,209.47f+j,C("#77817e"),1);
   }
   g.Restore(st);
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
