using System;
using System.Drawing;
using System.Collections.Generic;
namespace Greyhaven {
partial class CommandWindow {
 readonly Dictionary<string,Bitmap> painted=new Dictionary<string,Bitmap>();
 void PaintFrame(Graphics g,TypeInfo t,int side,int dir,RectangleF r){
  Bitmap sheet=painted[t.Key];
  int cols=sheet.Width/256,frame=cols==4?(dir/4)%4:((dir*cols+8)/16)%cols;
  g.DrawImage(sheet,r,new RectangleF(frame*256,side*256,256,256),GraphicsUnit.Pixel);
 }
 void DrawLive(Graphics g,TypeInfo t,int side,int dir,PointF p,float s,float built){
  float h=s*(.18f+.82f*built);
  if(t.Foot){
   int n=t.Key=="squad"?4:2;
   for(int j=0;j<n;j++){float x=p.X+((j%2)-.5f)*s*.26f,y=p.Y+(j/2-.5f)*s*.18f;
    PaintFrame(g,t,side,dir,new RectangleF(x-s*.22f,y-h*.40f,s*.44f,h*.44f));}
  }else if(t.Building){
   var footprint=Campaign.Footprint(t,PointF.Empty);float w=footprint.Width+footprint.Height;w*=Axis*(s/t.Size);
   PaintFrame(g,t,side,dir,new RectangleF(p.X-w/2,p.Y-w*.76f*built,w,w*(.18f+.82f*built)));
  }else PaintFrame(g,t,side,dir,new RectangleF(p.X-s*.5f,p.Y-h*.59f,s,h));
 }
 void DrawFieldSprite(Graphics g,Piece o,PointF p,float s){
  int dir=(o.Dir+14)%16;
  if(!o.Alive){Sprite(g,o.T,o.Side,dir,o.T.Foot&&o.Dead<.4f?1+o.Side:0,new RectangleF(p.X-s/2,p.Y-s*.59f,s,s));return;}
  if(o.T.Foot&&o.Path.Count>0)p.Y-=(float)Math.Abs(Math.Sin(world.Time*10+o.Id))*zoom;
  DrawLive(g,o.T,o.Side,dir,p,s,o.Ready);
  if(o.T.Key=="factory"&&o.Ready>=1){
   float open=Math.Min(1,o.Door*3);
   if(o.Queue.Count>0)open=Math.Max(open,Math.Max(0,1-(o.Queue.Peek().Seconds-o.Work)/.6f));
   float w=168*Axis*zoom,left=p.X-w/2,top=p.Y-w*.76f;
   var st=g.Save();g.TranslateTransform(left,top);g.ScaleTransform(w/256,w/256);
   var tl=new PointF(55,136);float h=42*(1-open);
   using(var brush=new SolidBrush(C("#49565b")))g.FillPolygon(brush,new[]{tl,new PointF(145,151),new PointF(145,151+h),new PointF(55,136+h)});
   for(int j=4;j<h;j+=6)Line(g,55,136+j,145,151+j,C("#77817e"),1);g.Restore(st);
  }
 }
}
}
