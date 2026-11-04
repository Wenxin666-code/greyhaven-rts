using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 public const int Tile=24;
 public static RectangleF Footprint(TypeInfo t,PointF pointer){
  int w=(t.Key=="factory"?4:t.Key=="hq"||t.Key=="barracks"?3:2)*Tile;
  int h=(t.Key=="hq"||t.Key=="factory"?3:2)*Tile;
  return new RectangleF((float)Math.Round((pointer.X-w*.5f)/Tile)*Tile,(float)Math.Round((pointer.Y-h*.5f)/Tile)*Tile,w,h);
 }
 public static PointF Middle(RectangleF r){return new PointF(r.Left+r.Width/2,r.Top+r.Height/2);}
 static RectangleF Inflated(RectangleF r,float pad){r.Inflate(pad,pad);return r;}
 public static readonly RectangleF[] Roads={new RectangleF(0,330,1200,63),new RectangleF(735,98,61,577)};
 public static bool HasGate(TypeInfo t){return t.Key=="barracks"||t.Key=="factory"||t.Key=="hq";}
 public static RectangleF Gate(RectangleF body){return new RectangleF(body.Left,body.Bottom,body.Width,Tile);}
 public static readonly RectangleF[] YardWalls={new RectangleF(842,214,74,8),new RectangleF(962,214,77,8),new RectangleF(1094,214,70,8)};
 public bool CanPlace(TypeInfo t,PointF p,Piece skip){
  var r=Footprint(t,p);var mid=Middle(r);var reserve=r;if(HasGate(t))reserve.Height+=Tile;
  if(reserve.Left<0||reserve.Right>1200||reserve.Top<99||reserve.Bottom>669||Terrain.Concat(Roads).Concat(YardWalls).Any(b=>b.IntersectsWith(reserve)))return false;
  if(Units.Any(o=>o!=skip&&o.Alive&&o.T.Building&&(reserve.IntersectsWith(Footprint(o.T,o.P))||HasGate(o.T)&&r.IntersectsWith(Gate(Footprint(o.T,o.P))))))return false;
  if(Units.Any(o=>o!=skip&&o.Alive&&!o.T.Building&&o.Carrier==null&&Inflated(r,Clearance(o)).Contains(o.P)))return false;
  return !Sites.Any(s=>Inflated(r,28).Contains(s.P))&&(skip!=null||Live(0).Any(o=>o.T.Key=="hq"&&Dist(o.P,mid)<265));
 }
 static float Clearance(Piece o){return o.T.Foot?5:11;}
 IEnumerable<RectangleF> Obstacles(){
  foreach(var r in Terrain)yield return r;
  foreach(var r in YardWalls)yield return r;
  foreach(var o in Units)if(o.Alive&&o.T.Building)yield return Footprint(o.T,o.P);
 }
 bool Blocked(PointF p,float pad,Piece skip){
  return p.X<pad||p.Y<99+pad||p.X>1200-pad||p.Y>669-pad||Obstacles().Any(r=>Inflated(r,pad).Contains(p));
 }
 // Intersect the entire swept segment with the expanded obstacle, including corners.
 static bool Slab(float a,float delta,float lo,float hi,ref float entry,ref float exit){
  if(Math.Abs(delta)<.00001f)return a>=lo&&a<=hi;
  float p=(lo-a)/delta,q=(hi-a)/delta;entry=Math.Max(entry,Math.Min(p,q));exit=Math.Min(exit,Math.Max(p,q));return entry<=exit;
 }
 static bool Clear(PointF a,PointF b,RectangleF[] blocks){
  foreach(var r in blocks){float lo=0,hi=1;if(Slab(a.X,b.X-a.X,r.Left,r.Right,ref lo,ref hi)&&Slab(a.Y,b.Y-a.Y,r.Top,r.Bottom,ref lo,ref hi))return false;}return true;
 }
 const int N=80,M=45,Step=15;
 static PointF Center(int i){return new PointF((i%N+.5f)*Step,(i/N+.5f)*Step);}
 static int Estimate(int a,int b){int dx=Math.Abs(a%N-b%N),dy=Math.Abs(a/N-b/N);return 10*Math.Max(dx,dy)+4*Math.Min(dx,dy);}
 int Anchor(PointF p,bool[] open,RectangleF[] blocks){
  int cx=(int)p.X/Step,cy=(int)p.Y/Step,best=-1;float d=50;
  for(int y=Math.Max(0,cy-2);y<=Math.Min(M-1,cy+2);y++)for(int x=Math.Max(0,cx-2);x<=Math.Min(N-1,cx+2);x++){
   int i=y*N+x;float gap=Dist(p,Center(i));if(open[i]&&gap<d&&Clear(p,Center(i),blocks)){best=i;d=gap;}
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
 public bool Route(Piece o,PointF dest){
  float pad=Clearance(o);if(Blocked(dest,pad,null))return false;
  var grid=Grid(pad);var blocks=grid.Barriers;var route=new Queue<PointF>();
  if(!Clear(o.P,dest,blocks)){
   if(grid.Open==null){grid.Open=new bool[N*M];for(int i=0;i<grid.Open.Length;i++){var c=Center(i);grid.Open[i]=c.X>=pad&&c.X<=1200-pad&&c.Y>=99+pad&&c.Y<=669-pad&&!blocks.Any(r=>r.Contains(c));}}
   bool[] open=grid.Open;
   int start=Anchor(o.P,open,blocks),end=Anchor(dest,open,blocks);if(start<0||end<0)return false;
   var previous=new int[N*M];var cost=Enumerable.Repeat(int.MaxValue,N*M).ToArray();var frontier=new SortedSet<long>();cost[start]=0;frontier.Add((long)Estimate(start,end)*4096+start);
   while(frontier.Count!=0){
    long key=frontier.Min;frontier.Remove(key);int cur=(int)(key%4096);if(cur==end)break;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
     int col=cur%N+x,row=cur/N+y,n=row*N+col;if(col<0||col>=N||row<0||row>=M||!open[n])continue;
     int next=cost[cur]+(x*y==0?10:14);if(next>=cost[n])continue;
     if(x*y!=0&&(!open[cur+x]||!open[cur+y*N]))continue;
     if(!Clear(Center(cur),Center(n),blocks))continue;
     int h=Estimate(n,end);if(cost[n]!=int.MaxValue)frontier.Remove((long)(cost[n]+h)*4096+n);
     cost[n]=next;previous[n]=cur;frontier.Add((long)(next+h)*4096+n);
    }
   }
   if(cost[end]==int.MaxValue)return false;
   var points=new List<PointF>();for(int i=end;;i=previous[i]){points.Add(Center(i));if(i==start)break;}
   points.Reverse();points.Add(dest);PointF from=o.P;
   for(int i=0;i<points.Count;){int far=i;while(far+1<points.Count&&Clear(from,points[far+1],blocks))far++;from=points[far];route.Enqueue(from);i=far+1;}
  }else route.Enqueue(dest);
  o.Path=route;o.RouteVersion=topology;return true;
 }
}
}
