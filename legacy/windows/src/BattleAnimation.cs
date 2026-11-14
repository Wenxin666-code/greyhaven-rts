using System;
using System.Drawing;
namespace Greyhaven {
partial class CommandWindow {
 internal const int BuildFrames=BuildingV08.Frames;
 internal const float DestructionSeconds=Campaign.BuildingDestructionSeconds;
 string BuildingVisual(Piece o,out Rectangle source){
  if(BuildingV08.IsBuilding(o.T.Key)){
   source=B08Source();
   if(!o.Alive){
    float ready=StructuralReadiness(o);
    if(o.Dead<TerminalWreckAt){if(o.Ready<1)return B08Frame(o.T.Key,"build",Math.Min(BuildFrames-1,(int)(ready*(BuildFrames-1))));return EnsureB08("b08/"+o.T.Key+"/damage-3");}
    return "removed-building";
   }
   if(o.BaseTransition&&!o.Selling&&(o.Ready<1||o.Packing>0)){float q=BaseProgress(o);return B08Frame(o.T.Key,"deploy",Math.Max(0,Math.Min(BuildingV08.DeployFrames-1,(int)(Clamp01(q)*(BuildingV08.DeployFrames-1)))));}
   if(o.Packing>0||o.Ready<1){float progress=o.Packing>0?o.Packing/Campaign.AssemblySeconds:o.Ready;return B08Frame(o.T.Key,"build",Math.Max(0,Math.Min(BuildFrames-1,(int)(progress*(BuildFrames-1)))));}
   int damage=BuildingDamageLevel(o);if(damage>0)return EnsureB08("b08/"+o.T.Key+"/damage-"+damage);
   source=new Rectangle(0,0,FrameSize,FrameSize);return o.T.Key;
  }
  source=new Rectangle(0,o.Alive?0:FrameSize*2,FrameSize,FrameSize);return o.T.Key;
 }
 void DrawBuildingState(Graphics g,Piece o,PointF p,float size){
  if(!o.Alive&&o.Dead>=TerminalWreckAt)return;
  p=DeploymentPoint(o,p);
  Rectangle source;var key=BuildingVisual(o,out source);var box=BuildingRect(Campaign.Footprint(o.T,o.P),p,size/o.T.Size);
  DrawStamp(g,key,source,box,o.Alive||o.Dead<DestructionSeconds?(Color?)UnitColor(o.Side):null);
 }
 bool BuildingInkHit(Piece o,RectangleF box,PointF screen){if(!box.Contains(screen))return false;Rectangle source;var key=BuildingVisual(o,out source);return PixelHit(key,source,box,screen);}
 PointF RecoilPoint(Piece o,PointF p){
  float age=world.VisualTime-o.FiredAt;if(!o.Alive||age<0||age>.24f||o.T.Building)return p;
  float amount=(float)Math.Sin(Math.PI*age/.24f)*(o.T.Foot?.35f:o.T.Key=="tank"||o.T.Key=="arty"?1.1f:.45f)*zoom;
  var target=ScreenOf(o.LastFirePoint);float dx=target.X-p.X,dy=target.Y-p.Y,len=(float)Math.Sqrt(dx*dx+dy*dy);
  if(len>1){p.X-=dx/len*amount;p.Y-=dy/len*amount;}return p;
 }
}
}
