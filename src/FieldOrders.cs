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
  float slow=unit.T.Speed;var progressValues=new List<float>();float own=Progress(unit);
  foreach(var m in Members){
   if(m.Formation!=this||!m.Deployed||m.Path.Count==0||m.Target!=null||m.TrafficBlocked>.35f||m.StalledFor>.4f||m.NoProgressFor>.6f||m.FollowingWait>.3f)continue;
   slow=Math.Min(slow,m.T.Speed);if(Campaign.Dist(m.P,unit.P)<180)progressValues.Add(Progress(m));
  }
  if(progressValues.Count==0||unit.StalledFor>.4f)return unit.T.Speed;
  progressValues.Sort();float median=progressValues[progressValues.Count/2],ahead=own-median;
  if(ahead< -24)return unit.T.Speed;
  // Cohesion follows the moving cohort, never its most delayed member.
  return slow*Math.Max(.85f,Math.Min(1,1-(ahead-24)/160));
 }
}
public partial class Campaign {
 public const int FogSize=12,FogCols=(Width+Height)/FogSize+1,FogRows=FogCols;
 readonly bool[,,] discovered;
 public int MapFogCols {get{return (MapWidth+MapHeight)/FogSize+1;}} public int MapFogRows {get{return MapFogCols;}}
 public int FogRevision {get;private set;}
 void Discover(int side,int x,int y){if(!discovered[side,x,y]){discovered[side,x,y]=true;if(side==0)FogRevision++;}}
 float visionTimer;
 public bool Seen(int side,int x,int y){return x>=0&&y>=0&&side>=0&&side<MaxFactions&&x<MapFogCols&&y<MapFogRows&&(satelliteVision[side]||discovered[side,x,y]);}
 public bool Explored(int side,PointF p){return Battlefield.Contains(p)&&Seen(side,FogColumn(p.X),FogRow(p.Y));}
 void Reveal(){
  UpdateSatellites();
  foreach(var o in Units){if(!o.Deployed||o.Side<0)continue;float r=Sight(o);
   for(int y=Math.Max(0,FogRow(o.P.Y-r));y<=Math.Min(MapFogRows-1,FogRow(o.P.Y+r));y++)
    for(int x=Math.Max(0,FogColumn(o.P.X-r));x<=Math.Min(MapFogCols-1,FogColumn(o.P.X+r));x++){
     if(discovered[o.Side,x,y])continue;float dx=FogOriginX+(x+.5f)*FogSize-o.P.X,dy=FogOriginY+(y+.5f)*FogSize-o.P.Y;
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
  var slots=new PointF[sorted.Length];float pitch=sorted.Any(u=>!u.T.Foot)?Tile:Tile/2;
  for(int i=0;i<slots.Length;i++){
   float depth=((rows-1)*.5f-i/cols)*pitch,across=(i%cols-(Math.Min(cols,sorted.Length-i/cols*cols)-1)*.5f)*pitch;
   slots[i]=new PointF(p.X+across,p.Y+depth);
  }
  var match=MatchSlots(sorted,slots);
  for(int i=0;i<sorted.Length;i++){
   var o=sorted[i];var goal=slots[match[i]];
   for(int attempt=0;attempt<(sorted.Length==1?1:17);attempt++){
    float radius=attempt==0?0:attempt>8?42:23;double a=attempt*Math.PI/4;var slot=new PointF(goal.X+radius*(float)Math.Cos(a),goal.Y+radius*(float)Math.Sin(a));
    if(assigned.Any(s=>Dist(s,slot)<pitch*.78f)||!Move(o,slot,advance))continue;
    assigned.Add(slot);if(together&&sorted.Length>1){o.Formation=group;group.Members.Add(o);group.Starts[o]=o.P;}break;
   }
  }return assigned.Count;
 }
 public bool SetRally(Piece src,PointF dest){
  if(!CanRallyTo(src,dest))return false;
  src.Rally=dest;Radio=src.T.Name+"的集结点已设置。";return true;
 }
 void RallyNewUnit(Piece source,Piece unit){
  var gate=Gate(Footprint(source.T,source.P));var yard=new PointF(Campaign.Middle(ExitApron(source.T,Footprint(source.T,source.P))).X,gate.Bottom+Tile);
  foreach(var center in source.Rally.HasValue?new[]{source.Rally.Value,yard}:new[]{yard}){
   for(int i=0;i<65;i++){int col=i%7;int lateral=(col+1)/2*(col%2==1?-1:1);
    var slot=new PointF(center.X+lateral*(unit.T.Foot?Tile/2:Tile),center.Y+(i/7)*(unit.T.Foot?Tile/2:Tile));
    if(Units.Any(o=>o!=unit&&o.Deployed&&!o.T.Building&&(TrafficContact(unit.T,slot,o.T,o.P)||o.Path.Count>0&&TrafficContact(unit.T,slot,o.T,o.Path.Last()))))continue;
    if(Move(unit,slot,false))return;
   }
  }
 }
 PointF Avoid(Piece o,PointF next){
  RecoverFootTraffic(o);
  float step=Dist(o.P,next);if(step<.001f)return next;
  var near=Units.Where(u=>u!=o&&u.Deployed&&!u.T.Building&&Dist(u.P,o.P)<33).ToArray();
  if(near.Length==0){
   if(Clear(o.P,next,Grid(Clearance(o)).Barriers,Clearance(o)))return next;
   if(!Route(o,o.Path.Last()))o.Path.Clear();return o.P;
  }
  double heading=Math.Atan2(next.Y-o.P.Y,next.X-o.P.X);
  foreach(double angle in new[]{0,.65,-.65,1.12,-1.12,1.52,-1.52}){
   var q=new PointF(o.P.X+(float)Math.Cos(heading+angle)*step,o.P.Y+(float)Math.Sin(heading+angle)*step);
   if(!Clear(o.P,q,Grid(Clearance(o)).Barriers,Clearance(o))||!Battlefield.ContainsFootprint(q,Clearance(o)))continue;
   if(near.All(u=>!TrafficContact(o.T,q,u.T,u.P)||Dist(u.P,q)>=Dist(u.P,o.P)-.001f))return q;
  }return o.P;
 }
}
}
