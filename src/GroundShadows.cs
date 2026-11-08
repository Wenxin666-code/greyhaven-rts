using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Greyhaven {
partial class CommandWindow {
 // Small, neutral silhouettes share the normal stamp cache. They are drawn in
 // a ground-only pass, before any sprite, and never participate in picking.
 const int ShadowBody=128,ShadowMargin=32,ShadowCanvas=192;
 readonly System.Collections.Generic.Queue<string> groundShadowKeys=new System.Collections.Generic.Queue<string>();
 string GroundShadow(string key,Rectangle source,bool foot,int stage){
  string id="ground-shadow/"+key+"/"+source.X+"/"+source.Y+"/"+stage;
  if(painted.ContainsKey(id))return id;
  var mask=new Bitmap(ShadowBody,ShadowBody,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(mask))using(var attr=new ImageAttributes()){
   var tint=new ColorMatrix(new[]{new[]{0f,0f,0f,0f,0f},new[]{0f,0f,0f,0f,0f},new[]{0f,0f,0f,0f,0f},new[]{0f,0f,0f,.32f,0f},new[]{.06f,.075f,.08f,0f,1f}});
   attr.SetColorMatrix(tint);attr.SetWrapMode(WrapMode.Clamp);
   if(stage<8){float built=stage/8f,reveal=.16f+.84f*built*built*(3-2*built);g.SetClip(new RectangleF(0,ShadowBody*(1-reveal),ShadowBody,ShadowBody*reveal));}
   g.DrawImage(painted[key],new[]{new PointF(0,0),new PointF(ShadowBody,0),new PointF(0,ShadowBody)},source,GraphicsUnit.Pixel,attr);
  }
  var shadow=new Bitmap(ShadowCanvas,ShadowCanvas,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(shadow)){
   // Anchor the projected silhouette at the feet/foundation. The upper-left
   // light throws short shadows down-right in the isometric ground plane.
   float anchor=ShadowBody*(foot?.91f:GroundY/(float)FrameSize);
   float shear=foot?.58f:.42f,depth=foot?.30f:.28f;
   g.InterpolationMode=InterpolationMode.Bilinear;
   g.DrawImage(mask,new[]{
    new PointF(ShadowMargin+shear*anchor,ShadowMargin+anchor+depth*anchor),
    new PointF(ShadowMargin+ShadowBody+shear*anchor,ShadowMargin+anchor+depth*anchor),
    new PointF(ShadowMargin+shear*(anchor-ShadowBody),ShadowMargin+anchor+depth*(anchor-ShadowBody))});
  }
  mask.Dispose();
  while(groundShadowKeys.Count>=256){string old=groundShadowKeys.Dequeue();painted[old].Dispose();painted.Remove(old);}
  painted[id]=shadow;groundShadowKeys.Enqueue(id);return id;
 }
 RectangleF GroundShadowArea(RectangleF body){return new RectangleF(body.X-body.Width*ShadowMargin/ShadowBody,body.Y-body.Height*ShadowMargin/ShadowBody,body.Width*ShadowCanvas/ShadowBody,body.Height*ShadowCanvas/ShadowBody);}
 void DrawGroundShadow(Graphics g,string key,Rectangle source,RectangleF body,bool foot,int stage){
  var box=GroundShadowArea(body);
  if(!g.IsVisible(box))return;
  DrawStamp(g,GroundShadow(key,source,foot,stage),new Rectangle(0,0,ShadowCanvas,ShadowCanvas),box);
 }
 void DrawGroundShadow(Graphics g,Piece o,PointF ground,float size){
  if(o.T.Foot){
   if(!o.Alive)return; // A fallen body already rests on the ground.
   DrawGroundShadow(g,o.T.Key,SourceCell(o.T,0,(o.Dir+14)%16),SoldierBox(ground,size),true,8);
  }else if(o.T.Building){
   if(!o.Alive&&o.Dead>=TerminalWreckAt)return;
   var body=BuildingRect(Campaign.Footprint(o.T,o.P),ground,size/o.T.Size);
   // Cull before resolving a damaged/construction frame, not after decoding it.
   if(!g.IsVisible(GroundShadowArea(body)))return;
   Rectangle source;var key=BuildingVisual(o,out source);
   DrawGroundShadow(g,key,source,body,false,8);
  }
 }
}
}
