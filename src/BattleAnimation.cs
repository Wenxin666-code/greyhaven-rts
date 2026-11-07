using System;
using System.Drawing;
namespace Greyhaven {
partial class CommandWindow {
 internal const int BuildFrames=24,CollapseFrames=16;
 internal const float CollapseSeconds=1.20f;
 static Rectangle AnimationCell(int frame){return new Rectangle(frame%8*FrameSize,frame/8*FrameSize,FrameSize,FrameSize);}
 void EnsureAnimation(string key){
  if(painted.ContainsKey(key))return;
  painted[key]=Raw("anim-"+key);painted[key+"-paint"]=Raw("anim-"+key+"-paint");
 }
 string BuildingVisual(Piece o,out Rectangle source){
  if(o.T.Civilian){source=new Rectangle(0,o.Alive?0:FrameSize*2,FrameSize,FrameSize);return o.T.Key;}
  if(o.Alive&&o.Packing>0){string key=o.T.Key+"-build";EnsureAnimation(key);source=AnimationCell(Math.Min(BuildFrames-1,(int)(o.Packing/Campaign.AssemblySeconds*(BuildFrames-1))));return key;}
  if(o.Alive&&o.Ready<1){string key=o.T.Key+"-build";EnsureAnimation(key);source=AnimationCell(Math.Min(BuildFrames-1,(int)(o.Ready*(BuildFrames-1))));return key;}
  if(!o.Alive&&o.Dead<CollapseSeconds){
   bool partial=o.Ready<1;string key=o.T.Key+(partial?"-build":"-collapse");EnsureAnimation(key);
   int frame=partial?(int)(o.Ready*(1-o.Dead/CollapseSeconds)*(BuildFrames-1)):(int)(o.Dead/CollapseSeconds*(CollapseFrames-1));
   source=AnimationCell(Math.Max(0,frame));return key;
  }
  source=new Rectangle(0,o.Alive?0:FrameSize*2,FrameSize,FrameSize);return o.T.Key;
 }
 void DrawBuildingState(Graphics g,Piece o,PointF p,float size){
  Rectangle source;var key=BuildingVisual(o,out source);var box=BuildingRect(Campaign.Footprint(o.T,o.P),p,size/o.T.Size);
  DrawStamp(g,key,source,box,o.Alive||o.Dead<CollapseSeconds?(Color?)UnitColor(o.Side):null);
 }
 bool BuildingInkHit(Piece o,RectangleF box,PointF screen){Rectangle source;var key=BuildingVisual(o,out source);return PixelHit(key,source,box,screen);}
 PointF RecoilPoint(Piece o,PointF p){
  float age=world.VisualTime-o.FiredAt;if(!o.Alive||age<0||age>.24f||o.T.Building)return p;
  float amount=(float)Math.Sin(Math.PI*age/.24f)*(o.T.Foot?.35f:o.T.Key=="tank"||o.T.Key=="arty"?1.1f:.45f)*zoom;
  var target=ScreenOf(o.LastFirePoint);float dx=target.X-p.X,dy=target.Y-p.Y,len=(float)Math.Sqrt(dx*dx+dy*dy);
  if(len>1){p.X-=dx/len*amount;p.Y-=dy/len*amount;}return p;
 }
}
}
