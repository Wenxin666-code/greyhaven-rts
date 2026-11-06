using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;

namespace Greyhaven {
public class March {
 public readonly List<Piece> Members=new List<Piece>();
 public readonly Dictionary<Piece,PointF> Starts=new Dictionary<Piece,PointF>();
 public PointF Forward;
 float Progress(Piece o){PointF start;if(!Starts.TryGetValue(o,out start))return 0;return (o.P.X-start.X)*Forward.X+(o.P.Y-start.Y)*Forward.Y;}
 public float Speed(Piece unit){
  float slow=unit.T.Speed,least=Progress(unit),most=least;
  foreach(var m in Members){
   if(m.Formation!=this||!m.Deployed||m.Path.Count==0||m.Target!=null||m.TrafficBlocked>2.5f)continue;
   slow=Math.Min(slow,m.T.Speed);float progress=Progress(m);least=Math.Min(least,progress);most=Math.Max(most,progress);
  }
  float own=Progress(unit),ahead=own-least;
  if(most-own>24)return unit.T.Speed;
  // Gradual braking avoids the old all-stop / all-go pulses. A blocked member
  // leaves pacing after 2.5 s, so the whole group cannot deadlock behind it.
  return slow*Math.Max(.25f,Math.Min(1,1-(ahead-24)/80));
 }
}
public partial class Campaign {
 public const int FogSize=12,FogCols=Width/FogSize,FogRows=Height/FogSize;
 readonly bool[,,] discovered=new bool[2,FogCols,FogRows];
 public int FogRevision {get;private set;}
 void Discover(int side,int x,int y){if(!discovered[side,x,y]){discovered[side,x,y]=true;if(side==0)FogRevision++;}}
 float visionTimer;
 public bool Seen(int side,int x,int y){return x>=0&&y>=0&&x<FogCols&&y<FogRows&&discovered[side,x,y];}
 public bool Explored(int side,PointF p){return Seen(side,(int)(p.X/FogSize),(int)(p.Y/FogSize));}
 void Reveal(){
  foreach(var o in Units){if(!o.Deployed)continue;float r=Sight(o);
   for(int y=Math.Max(0,(int)((o.P.Y-r)/FogSize));y<=Math.Min(FogRows-1,(int)((o.P.Y+r)/FogSize));y++)
    for(int x=Math.Max(0,(int)((o.P.X-r)/FogSize));x<=Math.Min(FogCols-1,(int)((o.P.X+r)/FogSize));x++){
     if(discovered[o.Side,x,y])continue;float dx=(x+.5f)*FogSize-o.P.X,dy=(y+.5f)*FogSize-o.P.Y;
     if(dx*dx+dy*dy<=r*r)Discover(o.Side,x,y);
    }
  }
 }
 // Match nearby destinations to the current layout instead of forcing units
 // through one another to put a particular class in the front row.
 public int MarchTo(Piece[] troops,PointF p,bool advance,bool together){
  if(troops.Length==0)return 0;
  var center=new PointF(troops.Average(o=>o.P.X),troops.Average(o=>o.P.Y));
  float d=Math.Max(1,Dist(center,p)),fx=(p.X-center.X)/d,fy=(p.Y-center.Y)/d;
  var sorted=troops.OrderBy(o=>o.Id).ToArray();
  int cols=(int)Math.Ceiling(Math.Sqrt(sorted.Length)),rows=(sorted.Length+cols-1)/cols;var group=new March{Forward=new PointF(fx,fy)};var assigned=new List<PointF>();
  var slots=new PointF[sorted.Length];
  for(int i=0;i<slots.Length;i++){
   float depth=((rows-1)*.5f-i/cols)*32,across=(i%cols-(Math.Min(cols,sorted.Length-i/cols*cols)-1)*.5f)*32;
   slots[i]=new PointF(p.X+fx*depth-fy*across,p.Y+fy*depth+fx*across);
  }
  var match=MatchSlots(sorted,slots);
  for(int i=0;i<sorted.Length;i++){
   var o=sorted[i];var goal=slots[match[i]];
   for(int attempt=0;attempt<(sorted.Length==1?1:17);attempt++){
    float radius=attempt==0?0:attempt>8?42:23;double a=attempt*Math.PI/4;var slot=new PointF(goal.X+radius*(float)Math.Cos(a),goal.Y+radius*(float)Math.Sin(a));
    if(assigned.Any(s=>Dist(s,slot)<19)||!Move(o,slot,advance))continue;
    assigned.Add(slot);if(together&&sorted.Length>1){o.Formation=group;group.Members.Add(o);group.Starts[o]=o.P;}break;
   }
  }return assigned.Count;
 }
 public bool SetRally(Piece src,PointF dest){
  if(src==null||!src.Deployed||src.Side!=0||src.T.Key=="refinery"||!HasGate(src.T)||!ParkingAllowed(src.Side,dest))return false;
  float pad=Types.Where(t=>!t.Building&&t.Producer==src.T.Key).Max(t=>t.Clearance);
  if(Blocked(dest,pad,null))return false;
  src.Rally=dest;Radio=src.T.Name+"的集结点已设置。";return true;
 }
 void RallyNewUnit(Piece source,Piece unit){
  var gate=Gate(Footprint(source.T,source.P));var yard=new PointF(source.P.X,gate.Bottom+(source.T.Key=="barracks"?76:48));
  foreach(var center in source.Rally.HasValue?new[]{source.Rally.Value,yard}:new[]{yard}){
   for(int i=0;i<65;i++){int col=i%7;int lateral=(col+1)/2*(col%2==1?-1:1);
    var slot=new PointF(center.X+lateral*40,center.Y+(i/7)*32);
    if(Units.Any(o=>o!=unit&&o.Deployed&&!o.T.Building&&(Dist(o.P,slot)<28||o.Path.Count>0&&Dist(o.Path.Last(),slot)<28)))continue;
    if(Move(unit,slot,false))return;
   }
  }
 }
 PointF Avoid(Piece o,PointF next){
  float step=Dist(o.P,next);if(step<.001f)return next;
  var near=Units.Where(u=>u!=o&&u.Deployed&&!u.T.Building&&Dist(u.P,o.P)<33).ToArray();
  if(near.Length==0){
   if(Clear(o.P,next,Grid(Clearance(o)).Barriers))return next;
   if(!Route(o,o.Path.Last()))o.Path.Clear();return o.P;
  }
  double heading=Math.Atan2(next.Y-o.P.Y,next.X-o.P.X);
  foreach(double angle in new[]{0,.65,-.65,1.12,-1.12,1.52,-1.52}){
   var q=new PointF(o.P.X+(float)Math.Cos(heading+angle)*step,o.P.Y+(float)Math.Sin(heading+angle)*step);
   if(!Clear(o.P,q,Grid(Clearance(o)).Barriers)||q.X<Clearance(o)||q.Y<99+Clearance(o)||q.X>Width-Clearance(o)||q.Y>Height-Clearance(o))continue;
   if(near.All(u=>Dist(u.P,q)>=Math.Min((Clearance(o)+Clearance(u))*.75f,Dist(u.P,o.P))-.001f))return q;
  }return o.P;
 }
}
}
