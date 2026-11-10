using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace Greyhaven {
partial class CommandWindow {
 readonly Dictionary<Tuple<string,Rectangle>,Rectangle> inkBounds=new Dictionary<Tuple<string,Rectangle>,Rectangle>();
 // Cache opaque bounds per direction, not per frame/zoom. This attaches bars to
 // the visible roof/head rather than the transparent square around the sprite.
 Rectangle SpriteInk(string key,Rectangle source){
  var id=Tuple.Create(key,source);Rectangle found;if(inkBounds.TryGetValue(id,out found))return found;
  var bitmap=painted[key];var bits=bitmap.LockBits(source,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
  int l=source.Width,t=source.Height,r=-1,b=-1;var row=new byte[source.Width*4];
  try{for(int y=0;y<source.Height;y++){Marshal.Copy(IntPtr.Add(bits.Scan0,y*bits.Stride),row,0,row.Length);for(int x=0;x<source.Width;x++)if(row[x*4+3]>=64){l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}}}
  finally{bitmap.UnlockBits(bits);}
  found=r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,b+1);inkBounds.Add(id,found);return found;
 }
 RectangleF VisibleUnitBox(Piece unit,PointF p,float size){
  if(unit.T.Building)p=DeploymentPoint(unit,p);
  var t=unit.T;int dir=(unit.Dir+14)%16;
  var source=t.Key=="tank"?TankCell(0,TankFrame(unit)):SourceCell(t,unit.Side,dir);
  var box=t.Building?BuildingRect(Campaign.Footprint(t,unit.P),p,zoom):t.Foot?SoldierBox(SoldierFeet(unit,p,zoom,world.Time),size):VehicleRect(t,p,size);
  var key=t.Building?BuildingVisual(unit,out source):LiveArtKey(unit);
  if(CombatPose(unit)){key=EnsureB08(CombatFrame(unit));var crop=CombatCrop(key);source=new Rectangle(Point.Empty,crop.Size);box=FootCropBox(GaitBox(p,size),crop);}
  else if(t.Foot&&unit.Alive&&unit.Stance>0){key=EnsureB08("stance/"+t.Key+"/"+dir/2+"/"+(int)Math.Round(unit.Stance*32));source=new Rectangle(0,0,384,384);box=GaitBox(p,size);}
  var ink=SpriteInk(key,source);
  var result=new RectangleF(box.X+ink.X*box.Width/source.Width,box.Y+ink.Y*box.Height/source.Height,ink.Width*box.Width/source.Width,ink.Height*box.Height/source.Height);
  return result;
 }
 float PixelScale {get{return drawingClient?scale:1;}}
 Point DevicePoint(PointF p){float s=PixelScale;return new Point((int)Math.Round((drawingClient?ox:0)+p.X*s),(int)Math.Round((drawingClient?oy:0)+p.Y*s));}
 Rectangle DeviceBox(RectangleF r){var a=DevicePoint(new PointF(r.Left,r.Top));var b=DevicePoint(new PointF(r.Right,r.Bottom));return Rectangle.FromLTRB(a.X,a.Y,Math.Max(a.X+1,b.X),Math.Max(a.Y+1,b.Y));}
 // All indicator geometry below is integer device pixels. Never a 2.2 logical
 // pixel anti-aliased pen magnified by the window transform.
 void PixelRing(Graphics g,PointF p,float rx,float ry,Color color){
  Point c=DevicePoint(p);int xRadius=Math.Max(2,(int)Math.Round(rx*PixelScale)),yRadius=Math.Max(1,(int)Math.Round(ry*PixelScale));
  var state=g.Save();g.ResetTransform();g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.None;
  using(var brush=new SolidBrush(color)){
   // Midpoint ellipse: four one-pixel samples per step, hard staircase edges.
   Action<int,int> plot=(x,y)=>{g.FillRectangle(brush,c.X+x,c.Y+y,1,1);g.FillRectangle(brush,c.X-x,c.Y+y,1,1);g.FillRectangle(brush,c.X+x,c.Y-y,1,1);g.FillRectangle(brush,c.X-x,c.Y-y,1,1);};
   double rx2=xRadius*xRadius,ry2=yRadius*yRadius,xp=0,yp=yRadius,dx=0,dy=2*rx2*yp,d=ry2-rx2*yRadius+.25*rx2;
   while(dx<dy){plot((int)xp,(int)yp);xp++;dx+=2*ry2;if(d<0)d+=dx+ry2;else{yp--;dy-=2*rx2;d+=dx-dy+ry2;}}
   d=ry2*(xp+.5)*(xp+.5)+rx2*(yp-1)*(yp-1)-rx2*ry2;
   while(yp>=0){plot((int)xp,(int)yp);yp--;dy-=2*rx2;if(d>0)d+=rx2-dy;else{xp++;dx+=2*ry2;d+=dx-dy+rx2;}}
  }g.Restore(state);
 }
 void PixelCorners(Graphics g,RectangleF rect,Color color){
  var r=DeviceBox(rect);int arm=Math.Max(2,Math.Min(6,Math.Min(r.Width,r.Height)/3));var state=g.Save();g.ResetTransform();g.SmoothingMode=SmoothingMode.None;
  using(var brush=new SolidBrush(color))foreach(int x in new[]{r.Left,r.Right-1})foreach(int y in new[]{r.Top,r.Bottom-1}){
   g.FillRectangle(brush,x==r.Left?x:x-arm+1,y,arm,1);g.FillRectangle(brush,x,y==r.Top?y:y-arm+1,1,arm);
  }g.Restore(state);
 }
 void SelectionPixels(Graphics g,Piece unit,PointF p,float size){
  // Building selection uses its actual foundation pins after sprites are drawn.
  if(unit.T.Building)return;
  float rx=unit.T.Foot?size*.13f:unit.T.VisualRadius*Axis*(float)Math.Sqrt(2)*zoom*.85f;
  PixelRing(g,p,rx,rx*.44f,teal);
 }
 static Color HealthColor(float fraction){return fraction<=.25f?Color.FromArgb(240,54,42):fraction<=.5f?Color.FromArgb(242,203,46):Color.FromArgb(69,219,71);}
 Rectangle HealthBox(Piece u,Rectangle body){
  int width=u.T.Building?Math.Max(28,Math.Min(108,(int)(body.Width*.57f))):u.T.Foot?Math.Max(9,Math.Min(24,body.Width+4)):Math.Max(16,Math.Min(56,(int)(body.Width*.8f)));
  int interior=u.T.Building?3:body.Height<22?1:2;
  return new Rectangle(body.Left+(body.Width-width)/2,body.Top-interior-5,width,interior+2);
 }
 void DrawUnitHealth(Graphics g,Piece unit,PointF position,float size){
  if(!unit.Alive)return;
  // A health label cannot be visible outside this conservative sprite + label
  // envelope. Reject it before resolving/scanning any offscreen damaged art.
  var anchor=unit.T.Building?DeploymentPoint(unit,position):position;
  var envelope=unit.T.Building?BuildingRect(Campaign.Footprint(unit.T,unit.P),anchor,zoom):unit.T.Foot?SoldierBox(anchor,size):VehicleRect(unit.T,anchor,size);
  envelope.Inflate(130/Math.Max(.5f,PixelScale),20/Math.Max(.5f,PixelScale));if(!g.IsVisible(envelope))return;
  var body=DeviceBox(VisibleUnitBox(unit,position,size));var r=HealthBox(unit,body);
  float fraction=Math.Max(0,Math.Min(1,unit.Ready<1?unit.Ready:unit.Hp/unit.T.Hp));
  Color color=unit.Ready<1?gold:HealthColor(fraction);var state=g.Save();g.ResetTransform();g.SmoothingMode=SmoothingMode.None;g.PixelOffsetMode=PixelOffsetMode.None;
  using(var outline=new SolidBrush(Color.FromArgb(8,12,9)))using(var empty=new SolidBrush(Color.FromArgb(40,47,40)))using(var fill=new SolidBrush(color)){
   g.FillRectangle(outline,r);g.FillRectangle(empty,r.X+1,r.Y+1,r.Width-2,r.Height-2);
   int length=Math.Min(r.Width-2,(int)Math.Ceiling((r.Width-2)*fraction));
   for(int x=0;x<length;x+=3)g.FillRectangle(fill,r.X+1+x,r.Y+1,Math.Min(2,length-x),r.Height-2);
  }g.Restore(state);
 }
}
}
