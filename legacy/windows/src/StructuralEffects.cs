using System;
using System.Drawing;
namespace Greyhaven {partial class CommandWindow {
 // Model-space attachment points share the exact baked camera and foundation.
 // Effects are clocked by the same visual progress as their rigid components.
 void StructuralEffects(Graphics g,string key,RectangleF box,float progress,bool collapse,float readiness=1){
  StructuralSite[][] sites;if(!StructuralV10.Sites.TryGetValue(key,out sites))return;
  var spec=BuildingV08.Specs[key];float z=box.Width*LotSpan/(FrameSize*(spec.Width+spec.Depth)*Campaign.Tile*Axis);
  if(!collapse){
   int count=0;foreach(var site in sites[0]){float phase=(progress-site.Time+.02f)/.09f;if(phase<0||phase>=1)continue;
    Effect(g,"weld",SocketPoint(box,site.Point),13*z,phase);if(++count==3)break;
   }
   if(progress<.25f)Effect(g,"dust",SocketPoint(box,new PointF(160,220)),40*z,progress/.25f);
   return;
  }
  FatalBuildingFx(g,key,box,progress*DestructionSeconds,readiness);
 }
 float StructuralReadiness(Piece o){return o.BaseTransition?Smooth((o.Ready-.22f)/.78f):o.Ready;}
 void StructuralActorEffects(Graphics g,Piece o,PointF p){
  var box=BuildingRect(Campaign.Footprint(o.T,o.P),DeploymentPoint(o,p),zoom);
  if(o.Alive){
   if(o.Packing>0)return;
   if(o.Ready<1){StructuralEffects(g,o.T.Key,box,StructuralReadiness(o),false);return;}
   BuildingFireFx(g,o.T.Key,box,world.VisualTime+o.Id*.173f,BuildingDamageLevel(o));return;
  }
  FatalBuildingFx(g,o.T.Key,box,o.Dead,StructuralReadiness(o),o.Id,UnitColor(o.Side));
 }
}}
