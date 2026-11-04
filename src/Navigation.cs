using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 public const int Tile=24, Width=1680, Height=960;
 public static RectangleF Footprint(TypeInfo t,PointF pointer){
  int w=(t.Key=="factory"?4:t.Key=="hq"||t.Key=="barracks"||t.Key=="refinery"?3:2)*Tile;
  int h=(t.Key=="hq"||t.Key=="factory"?3:2)*Tile;
  return new RectangleF((float)Math.Round((pointer.X-w*.5f)/Tile)*Tile,(float)Math.Round((pointer.Y-h*.5f)/Tile)*Tile,w,h);
 }
 public static PointF Middle(RectangleF r){return new PointF(r.Left+r.Width/2,r.Top+r.Height/2);}
 static RectangleF Inflated(RectangleF r,float pad){r.Inflate(pad,pad);return r;}
 public static readonly RectangleF[] Roads={new RectangleF(0,330,Width,63),new RectangleF(735,98,61,Height-98),new RectangleF(735,743,755,50),new RectangleF(1435,295,55,498)};
 public static bool HasGate(TypeInfo t){return t.Key=="barracks"||t.Key=="factory"||t.Key=="hq"||t.Key=="refinery";}
 public static RectangleF Gate(RectangleF body){return new RectangleF(body.Left,body.Bottom,body.Width,Tile);}
 public static readonly RectangleF[] YardWalls={new RectangleF(842,214,74,8),new RectangleF(962,214,77,8),new RectangleF(1094,214,70,8)};
 public bool CanPlace(TypeInfo t,PointF p,Piece skip){
  var r=Footprint(t,p);var mid=Middle(r);var reserve=ReservedLot(t,r);
  if(skip==null&&!(Explored(0,new PointF(r.Left,r.Top))&&Explored(0,new PointF(r.Right,r.Top))&&Explored(0,new PointF(r.Left,r.Bottom))&&Explored(0,new PointF(r.Right,r.Bottom))))return false;
  if(reserve.Left<0||reserve.Right>Width||reserve.Top<99||reserve.Bottom>Height||Roads.Concat(YardWalls).Any(b=>b.IntersectsWith(reserve)))return false;
  if(Ore.Any(patch=>patch.Stock>0&&Inflated(reserve,12).Contains(patch.P)))return false;
  if(Sites.Any(site=>site.Body.IntersectsWith(reserve)))return false;
  if(Units.Any(o=>o!=skip&&o.Alive&&o.T.Building&&(reserve.IntersectsWith(ReservedLot(o.T,Footprint(o.T,o.P))))))return false;
  if(Units.Any(o=>o!=skip&&o.Alive&&!o.T.Building&&o.Carrier==null&&Inflated(t.Key=="barracks"?reserve:r,Clearance(o)).Contains(o.P)))return false;
  if(t.Key=="barracks"&&Units.Any(o=>o.Alive&&o.Path.Count>0&&reserve.Contains(o.Path.Last())))return false;
  return !Sites.Any(s=>Inflated(r,28).Contains(s.P))&&(skip!=null||Live(0).Any(o=>o.T.Key=="hq"&&Dist(o.P,mid)<265));
 }
 static float Clearance(Piece o){return o.T.Foot?5:11;}
 IEnumerable<RectangleF> Obstacles(){
  foreach(var site in Sites)yield return site.Body;
  foreach(var r in YardWalls)yield return r;
  foreach(var o in Units)if(o.Alive&&o.T.Building)yield return Footprint(o.T,o.P);
 }
 bool Blocked(PointF p,float pad,Piece skip){
  return p.X<pad||p.Y<99+pad||p.X>Width-pad||p.Y>Height-pad||Obstacles().Any(r=>Inflated(r,pad).Contains(p));
 }
 // Intersect the entire swept segment with the expanded obstacle, including corners.
 static bool Slab(float a,float delta,float lo,float hi,ref float entry,ref float exit){
  if(Math.Abs(delta)<.00001f)return a>=lo&&a<=hi;
  float p=(lo-a)/delta,q=(hi-a)/delta;entry=Math.Max(entry,Math.Min(p,q));exit=Math.Min(exit,Math.Max(p,q));return entry<=exit;
 }
 static bool Clear(PointF a,PointF b,RectangleF[] blocks){
  foreach(var r in blocks){float lo=0,hi=1;if(Slab(a.X,b.X-a.X,r.Left,r.Right,ref lo,ref hi)&&Slab(a.Y,b.Y-a.Y,r.Top,r.Bottom,ref lo,ref hi))return false;}return true;
 }
 const int Step=15,N=Width/Step,M=Height/Step;
 static PointF Center(int i){return new PointF((i%N+.5f)*Step,(i/N+.5f)*Step);}
 static int Estimate(int a,int b){int dx=Math.Abs(a%N-b%N),dy=Math.Abs(a/N-b/N);return 10*Math.Max(dx,dy)+4*Math.Min(dx,dy);}
 int Anchor(PointF p,bool[] open,RectangleF[] blocks,bool tracks){
  int cx=(int)p.X/Step,cy=(int)p.Y/Step,best=-1;float d=50;
  for(int y=Math.Max(0,cy-2);y<=Math.Min(M-1,cy+2);y++)for(int x=Math.Max(0,cx-2);x<=Math.Min(N-1,cx+2);x++){
   int i=y*N+x;float gap=Dist(p,Center(i));if(open[i]&&gap<d&&CanLink(tracks,p,Center(i),blocks)){best=i;d=gap;}
  }return best;
 }
 int topology;
 sealed class NavigationGrid {public RectangleF[] Barriers;public bool[] Open;}
 readonly Dictionary<float,NavigationGrid> grids=new Dictionary<float,NavigationGrid>();
 void Changed(){topology++;grids.Clear();}
 NavigationGrid Grid(float pad){
  NavigationGrid grid;if(grids.TryGetValue(pad,out grid))return grid;
  grid=new NavigationGrid{Barriers=Obstacles().Select(r=>Inflated(r,pad)).ToArray()};grids[pad]=grid;return grid;
 }
 // A tracked chassis may translate only along map axes or their diagonals.
 // Two legs suffice in open space; test both bend orders against swept obstacles.
 static bool TrackLegs(PointF a,PointF b,RectangleF[] blocks,Queue<PointF> result){
  float dx=b.X-a.X,dy=b.Y-a.Y,d=Math.Min(Math.Abs(dx),Math.Abs(dy));
  var diag=new PointF(d*Math.Sign(dx),d*Math.Sign(dy));
  var first=new PointF(a.X+diag.X,a.Y+diag.Y);var last=new PointF(b.X-diag.X,b.Y-diag.Y);
  // On a shared street, keep the long leg parallel to its kerbs.
  bool followStreet=Roads.Any(r=>r.Contains(a)&&r.Contains(b)&&
   (r.Width>r.Height?Math.Abs(dx)>=Math.Abs(dy):Math.Abs(dy)>=Math.Abs(dx)));
  foreach(var bend in followStreet?new[]{last,first}:new[]{first,last}){
   if(!Clear(a,bend,blocks)||!Clear(bend,b,blocks))continue;
   if(result!=null){if(Dist(a,bend)>.001f&&Dist(bend,b)>.001f)result.Enqueue(bend);result.Enqueue(b);}
   return true;
  }return false;
 }
 // Preserve the lane offset through the intersection. Only prefer the corner
 // when both endpoints are on different streets and meet at an open junction.
 static bool StreetCorner(PointF a,PointF b,RectangleF[] blocks,Queue<PointF> route){
  foreach(var from in Roads){
   if(!from.Contains(a)||from.Contains(b))continue;
   foreach(var to in Roads){
    if(!to.Contains(b)||to.Contains(a)||(from.Width>from.Height)==(to.Width>to.Height))continue;
    var corner=from.Width>from.Height?new PointF(b.X,a.Y):new PointF(a.X,b.Y);
    if(!from.Contains(corner)||!to.Contains(corner))continue;
    if(Clear(a,corner,blocks)&&Clear(corner,b,blocks)){route.Enqueue(corner);route.Enqueue(b);return true;}
   }
  }return false;
 }
 static bool CanLink(bool tracks,PointF a,PointF b,RectangleF[] blocks){return tracks?TrackLegs(a,b,blocks,null):Clear(a,b,blocks);}
 public bool Route(Piece o,PointF dest){return PlanRoute(o,dest,null);}
 bool PlanRoute(Piece o,PointF dest,RectangleF[] temporary){
  bool tracks=!o.T.Foot&&!o.T.Building;
  float pad=Clearance(o);if(Blocked(dest,pad,null))return false;
  var grid=Grid(pad);if(temporary!=null)grid=new NavigationGrid{Barriers=grid.Barriers.Concat(temporary).ToArray()};var blocks=grid.Barriers;var route=new Queue<PointF>();
  if(tracks&&StreetCorner(o.P,dest,blocks,route)){o.Path=route;o.RouteVersion=topology;return true;}
  if(!CanLink(tracks,o.P,dest,blocks)){
   if(grid.Open==null){grid.Open=new bool[N*M];for(int i=0;i<grid.Open.Length;i++){var c=Center(i);grid.Open[i]=c.X>=pad&&c.X<=Width-pad&&c.Y>=99+pad&&c.Y<=Height-pad&&!blocks.Any(r=>r.Contains(c));}}
   bool[] open=grid.Open;
   int start=Anchor(o.P,open,blocks,tracks),end=Anchor(dest,open,blocks,tracks);if(start<0||end<0)return false;
   var previous=new int[N*M];var cost=Enumerable.Repeat(int.MaxValue,N*M).ToArray();var frontier=new SortedSet<long>();cost[start]=0;frontier.Add((long)Estimate(start,end)*(N*M)+start);
   while(frontier.Count!=0){
    long key=frontier.Min;frontier.Remove(key);int cur=(int)(key%(N*M));if(cur==end)break;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
     int col=cur%N+x,row=cur/N+y,n=row*N+col;if(col<0||col>=N||row<0||row>=M||!open[n])continue;
     int next=cost[cur]+(x*y==0?10:14);if(next>=cost[n])continue;
     if(x*y!=0&&(!open[cur+x]||!open[cur+y*N]))continue;
     if(!Clear(Center(cur),Center(n),blocks))continue;
     int h=Estimate(n,end);if(cost[n]!=int.MaxValue)frontier.Remove((long)(cost[n]+h)*(N*M)+n);
     cost[n]=next;previous[n]=cur;frontier.Add((long)(next+h)*(N*M)+n);
    }
   }
   if(cost[end]==int.MaxValue)return false;
   var points=new List<PointF>();for(int i=end;;i=previous[i]){points.Add(Center(i));if(i==start)break;}
   points.Reverse();points.Add(dest);PointF from=o.P;
   for(int i=0;i<points.Count;){int far=i;while(far+1<points.Count&&CanLink(tracks,from,points[far+1],blocks))far++;if(tracks){if(!TrackLegs(from,points[far],blocks,route))return false;}else route.Enqueue(points[far]);from=points[far];i=far+1;}
  }else if(tracks)TrackLegs(o.P,dest,blocks,route);else route.Enqueue(dest);
  o.Path=route;o.RouteVersion=topology;return true;
 }
}
}
