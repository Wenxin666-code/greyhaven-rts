using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 public const int Tile=24, Width=140*Tile, Height=90*Tile;
 public static readonly PointF EnemyHome=new PointF(Width-570,Height-480);
 public static RectangleF Footprint(TypeInfo t,PointF pointer){
  int w=t.FoundationWidth*Tile;
  int h=t.FoundationHeight*Tile;
  return new RectangleF((float)Math.Round((pointer.X-w*.5f)/Tile)*Tile,(float)Math.Round((pointer.Y-h*.5f)/Tile)*Tile,w,h);
 }
 public static PointF Middle(RectangleF r){return new PointF(r.Left+r.Width/2,r.Top+r.Height/2);}
 static RectangleF Inflated(RectangleF r,float pad){r.Inflate(pad,pad);return r;}
 public static readonly RectangleF[] Roads={
  new RectangleF(0,330,Width,63),new RectangleF(735,98,61,Height-98),
  new RectangleF(735,743,1595,50),new RectangleF(1435,295,55,978),
  new RectangleF(2275,295,55,978),new RectangleF(735,1223,1595,50),
  new RectangleF(200,1007,596,50),
  new RectangleF(3090,295,55,1698),new RectangleF(2275,1273,55,720),
  new RectangleF(1435,1273,55,720),new RectangleF(735,1943,2410,50),
  new RectangleF(2330,743,815,50),new RectangleF(2330,1223,815,50)};
 public static bool HasGate(TypeInfo t){return t.Key=="barracks"||t.Key=="factory"||t.Key=="hq"||t.Key=="refinery";}
 public static RectangleF Gate(RectangleF body){return new RectangleF(body.Left,body.Bottom,body.Width,Tile);}
 public static readonly RectangleF[] YardWalls={new RectangleF(842,214,74,8),new RectangleF(962,214,77,8),new RectangleF(1094,214,70,8)};
 public bool InBuildRange(TypeInfo t,RectangleF r,int side){
  return Live(side).Any(o=>o.T.Building&&!o.T.Civilian&&!o.T.Defense&&!o.T.IsOilDerrick&&!IsRelayStation(o)&&
   Math.Max(Math.Max(Footprint(o.T,o.P).Left-r.Right,r.Left-Footprint(o.T,o.P).Right),Math.Max(Footprint(o.T,o.P).Top-r.Bottom,r.Top-Footprint(o.T,o.P).Bottom))<=BuildAdjacentCells(t)*Tile+.01f);
 }
 public bool PlacementCell(RectangleF cell,Piece skip,int side){return LocalPlacementBlock(cell,skip,side,null)==PlacementBlock.None;}
 public bool CanPlace(TypeInfo t,PointF p,Piece skip,int side=0){return AssessPlacement(t,p,skip,side).Valid;}
 static float Clearance(Piece o){return o.T.Clearance;}
 IEnumerable<RectangleF> Obstacles(Piece skip=null){
  foreach(var site in Sites)yield return site.Body;
  foreach(var r in MapWalls)yield return r;
  foreach(var o in Units)if(o!=skip&&o.Alive&&o.T.Building)yield return Footprint(o.T,o.P);
 }
 bool Blocked(PointF p,float pad,Piece skip){
  return !TerrainOpen(p,pad)||!Battlefield.ContainsFootprint(p,pad)||Obstacles(skip).Any(r=>Inflated(r,pad).Contains(p));
 }
 // Intersect the entire swept segment with the expanded obstacle, including corners.
 static bool Slab(float a,float delta,float lo,float hi,ref float entry,ref float exit){
  if(Math.Abs(delta)<.00001f)return a>=lo&&a<=hi;
  float p=(lo-a)/delta,q=(hi-a)/delta;entry=Math.Max(entry,Math.Min(p,q));exit=Math.Min(exit,Math.Max(p,q));return entry<=exit;
 }
 bool Clear(PointF a,PointF b,RectangleF[] blocks,float pad=0){
  float left=Math.Min(a.X,b.X),right=Math.Max(a.X,b.X),top=Math.Min(a.Y,b.Y),bottom=Math.Max(a.Y,b.Y);
  foreach(var r in blocks){if(r.Right<left||r.Left>right||r.Bottom<top||r.Top>bottom)continue;float lo=0,hi=1;if(Slab(a.X,b.X-a.X,r.Left,r.Right,ref lo,ref hi)&&Slab(a.Y,b.Y-a.Y,r.Top,r.Bottom,ref lo,ref hi))return false;}return TerrainLine(a,b,pad);
 }
 const int Step=15;int N {get{return (MapWidth+MapHeight)/Step;}} int M {get{return N;}}
 int NavX(float x){return (int)Math.Floor((x-FogOriginX)/Step);}
 int NavY(float y){return (int)Math.Floor((y-FogOriginY)/Step);}
 PointF Center(int i){return new PointF(FogOriginX+(i%N+.5f)*Step,FogOriginY+(i/N+.5f)*Step);}
 int Estimate(int a,int b){int dx=Math.Abs(a%N-b%N),dy=Math.Abs(a/N-b/N);return 10*Math.Max(dx,dy)+4*Math.Min(dx,dy);}
 int Anchor(PointF p,bool[] open,RectangleF[] blocks,bool tracks,float pad=0){
  int cx=NavX(p.X),cy=NavY(p.Y),best=-1;float d=50;
  for(int y=Math.Max(0,cy-2);y<=Math.Min(M-1,cy+2);y++)for(int x=Math.Max(0,cx-2);x<=Math.Min(N-1,cx+2);x++){
   int i=y*N+x;float gap=Dist(p,Center(i));if(open[i]&&gap<d&&CanLink(tracks,p,Center(i),blocks,pad)){best=i;d=gap;}
  }return best;
 }
 int topology;
 sealed class NavigationGrid {public RectangleF[] Barriers;public bool[] Open;public int[] Regions;public ushort[] Links;public Dictionary<long,bool> Reachability;public int LinkStamp;public NavigationBarriers LocalBarriers;public TerrainNav Terrain;}
 readonly Dictionary<float,NavigationGrid> grids=new Dictionary<float,NavigationGrid>();
 void Changed(){topology++;grids.Clear();}
 NavigationGrid Grid(float pad){
  NavigationGrid grid;if(grids.TryGetValue(pad,out grid))return grid;
  grid=new NavigationGrid{Barriers=Obstacles().Select(r=>Inflated(r,pad)).ToArray()};grids[pad]=grid;return grid;
 }
 // A tracked chassis may translate only along map axes or their diagonals.
 // Two legs suffice in open space; test both bend orders against swept obstacles.
 bool TrackLegs(PointF a,PointF b,RectangleF[] blocks,Queue<PointF> result,float pad=0){
  float dx=b.X-a.X,dy=b.Y-a.Y,d=Math.Min(Math.Abs(dx),Math.Abs(dy));
  var diag=new PointF(d*Math.Sign(dx),d*Math.Sign(dy));
  var first=new PointF(a.X+diag.X,a.Y+diag.Y);var last=new PointF(b.X-diag.X,b.Y-diag.Y);
  // On a shared street, keep the long leg parallel to its kerbs.
  bool followStreet=MapRoads.Any(r=>r.Contains(a)&&r.Contains(b)&&
   (r.Width>r.Height?Math.Abs(dx)>=Math.Abs(dy):Math.Abs(dy)>=Math.Abs(dx)));
  foreach(var bend in followStreet?new[]{last,first}:new[]{first,last}){
   if(!Clear(a,bend,blocks,pad)||!Clear(bend,b,blocks,pad))continue;
   if(result!=null){if(Dist(a,bend)>.001f&&Dist(bend,b)>.001f)result.Enqueue(bend);result.Enqueue(b);}
   return true;
  }return false;
 }
 // Preserve the lane offset through the intersection. Only prefer the corner
 // when both endpoints are on different streets and meet at an open junction.
 bool StreetCorner(PointF a,PointF b,RectangleF[] blocks,Queue<PointF> route,float pad=0){
  foreach(var from in MapRoads){
   if(!from.Contains(a)||from.Contains(b))continue;
   foreach(var to in MapRoads){
    if(!to.Contains(b)||to.Contains(a)||(from.Width>from.Height)==(to.Width>to.Height))continue;
    var corner=from.Width>from.Height?new PointF(b.X,a.Y):new PointF(a.X,b.Y);
    if(!from.Contains(corner)||!to.Contains(corner))continue;
    if(Clear(a,corner,blocks,pad)&&Clear(corner,b,blocks,pad)){route.Enqueue(corner);route.Enqueue(b);return true;}
   }
  }return false;
 }
 bool CanLink(bool tracks,PointF a,PointF b,RectangleF[] blocks,float pad=0){return tracks?TrackLegs(a,b,blocks,null,pad):Clear(a,b,blocks,pad);}
 // Probe increasingly distant points, then refine the last valid interval.
 // Every accepted segment still passes the exact swept terrain/obstacle check.
 int FarthestRoutePoint(PointF from,List<PointF> points,int first,bool tracks,RectangleF[] blocks,float pad){
  int far=first,stride=1,upper=points.Count-1;
  while(far<upper){int test=Math.Min(upper,far+stride);if(!CanLink(tracks,from,points[test],blocks,pad)){upper=test-1;break;}far=test;stride*=2;}
  while(far<upper){int middle=(far+upper+1)/2;if(CanLink(tracks,from,points[middle],blocks,pad))far=middle;else upper=middle-1;}
  return far;
 }
 public bool Route(Piece o,PointF dest){return PlanRoute(o,dest,null);}
 bool PlanRoute(Piece o,PointF dest,RectangleF[] temporary,Piece ignore=null,float padding=0){
  bool tracks=!o.T.Foot&&!o.T.Building;
  float pad=Math.Max(Clearance(o),padding);if(Blocked(dest,pad,ignore))return false;
  var grid=ignore==null?Grid(pad):new NavigationGrid{Barriers=Obstacles(ignore).Select(r=>Inflated(r,pad)).ToArray()};
  if(temporary!=null){
   // Reuse static terrain and touch only cells covered by current traffic.
   OpenGrid(grid,pad);var open=TemporaryNavigationOpen(grid.Open);
   foreach(var r in temporary)for(int y=Math.Max(0,NavY(r.Top));y<=Math.Min(M-1,NavY(r.Bottom));y++)
    for(int x=Math.Max(0,NavX(r.Left));x<=Math.Min(N-1,NavX(r.Right));x++)if(r.Contains(Center(y*N+x)))open[y*N+x]=false;
   grid=new NavigationGrid{Barriers=grid.Barriers.Concat(temporary).ToArray(),Open=open,LinkStamp=BeginTemporaryNavigation()};
  }
  var blocks=grid.Barriers;var route=new Queue<PointF>();
  if(tracks&&StreetCorner(o.P,dest,blocks,route,pad)){o.Path=route;o.RouteVersion=topology;return true;}
  if(!CanLink(tracks,o.P,dest,blocks,pad)){
   OpenGrid(grid,pad);
   bool[] open=grid.Open;
   int start=Anchor(o.P,open,blocks,tracks,pad),end=Anchor(dest,open,blocks,tracks,pad);if(start<0||end<0)return false;if(SmallGoalEnclosed(grid,start,end,pad))return false;
   BeginNavigationSearch();SetNavigationCost(start,0,start);navigationHeap.Add(NavigationPriority(0,Estimate(start,end),start));
   while(navigationHeap.Count!=0){
    long key=navigationHeap.Pop();int cur=(int)(key%(N*M));if(NavigationCost(cur)==int.MaxValue||key!=NavigationPriority(NavigationCost(cur),Estimate(cur,end),cur))continue;if(cur==end)break;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
     int col=cur%N+x,row=cur/N+y,n=row*N+col;if(col<0||col>=N||row<0||row>=M||!open[n])continue;
     int next=NavigationCost(cur)+(x*y==0?10:14);if(next>=NavigationCost(n))continue;
     if(x*y!=0&&(!open[cur+x]||!open[cur+y*N]))continue;
     if(!NavigationLink(grid,cur,n,pad))continue;
     int h=Estimate(n,end);SetNavigationCost(n,next,cur);navigationHeap.Add(NavigationPriority(next,h,n));
    }
   }
   if(NavigationCost(end)==int.MaxValue)return false;
   var points=new List<PointF>();for(int i=end;;i=navigationPrevious[i]){points.Add(Center(i));if(i==start)break;}
   points.Reverse();points.Add(dest);PointF from=o.P;
   for(int i=0;i<points.Count;){int far=FarthestRoutePoint(from,points,i,tracks,blocks,pad);if(tracks){if(!TrackLegs(from,points[far],blocks,route,pad))return false;}else route.Enqueue(points[far]);from=points[far];i=far+1;}
  }else if(tracks)TrackLegs(o.P,dest,blocks,route,pad);else route.Enqueue(dest);
  o.Path=route;o.RouteVersion=topology;return true;
 }
}
}
