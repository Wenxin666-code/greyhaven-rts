using System;
using System.Collections.Generic;
using System.Drawing;
namespace Greyhaven {
public partial class Campaign {
 // Terrain is immutable during a match. Building topology can change often,
 // but it need not rerun terrain tests over the whole enlarged map each time.
 sealed class TerrainNav {public bool[] Open;public ushort[] Links;}
 readonly Dictionary<float,TerrainNav> terrainNavigation=new Dictionary<float,TerrainNav>();
 TerrainNav TerrainNavigation(float pad){
  TerrainNav result;if(terrainNavigation.TryGetValue(pad,out result))return result;
  result=new TerrainNav{Open=new bool[N*M],Links=new ushort[N*M]};
  for(int i=0;i<result.Open.Length;i++){var p=Center(i);result.Open[i]=Battlefield.ContainsFootprint(p,pad)&&TerrainOpen(p,pad);}
  terrainNavigation.Add(pad,result);return result;
 }
 internal void PrepareBattleNavigation(){
  // Pay the immutable terrain scan once while entering a mission, rather than
  // on the first order for each chassis type during play. Building/traffic
  // barriers remain dynamic and use the normal invalidation path.
  foreach(var type in Types)if(!type.Building)TerrainNavigation(type.Clearance);
 }
 // Traffic grids live only for a synchronous route call. Reuse their large
 // arrays and stamp link entries, leaving persistent building grids untouched.
 bool[] temporaryNavigationOpen;ushort[] temporaryNavigationLinks;int[] temporaryNavigationStamps;int temporaryNavigationEpoch;
 bool[] TemporaryNavigationOpen(bool[] source){
  if(temporaryNavigationOpen==null||temporaryNavigationOpen.Length!=source.Length)temporaryNavigationOpen=new bool[source.Length];
  Array.Copy(source,temporaryNavigationOpen,source.Length);return temporaryNavigationOpen;
 }
 int BeginTemporaryNavigation(){
  if(temporaryNavigationLinks==null){temporaryNavigationLinks=new ushort[N*M];temporaryNavigationStamps=new int[N*M];}
  if(temporaryNavigationEpoch==int.MaxValue){Array.Clear(temporaryNavigationStamps,0,temporaryNavigationStamps.Length);temporaryNavigationEpoch=0;}
  return ++temporaryNavigationEpoch;
 }
 static int NavDirection(int x,int y){return y<0?x+1:y==0?(x<0?3:4):x+6;}
 bool NavigationLink(NavigationGrid grid,int from,int to,float pad){
  bool transient=grid.LinkStamp>0;if(!transient&&grid.Links==null)grid.Links=new ushort[N*M];var links=transient?temporaryNavigationLinks:grid.Links;
  int dx=to%N-from%N,dy=to/N-from/N,direction=NavDirection(dx,dy);int known=1<<direction,value=known<<8;
  ushort entry=transient&&temporaryNavigationStamps[from]!=grid.LinkStamp?(ushort)0:links[from];if((entry&known)!=0)return (entry&value)!=0;
  var a=Center(from);var b=Center(to);bool valid=true;
  {
   var terrain=grid.Terrain??(grid.Terrain=TerrainNavigation(pad));int edge=terrain.Links[from];
   if((edge&known)==0){bool open=TerrainLine(a,b,pad);edge|=known;if(open)edge|=value;terrain.Links[from]=(ushort)edge;}
   valid=(edge&value)!=0;
  }
  if(valid)valid=BarrierLinkClear(grid,from,to,a,b);
  entry|=(ushort)known;if(valid)entry|=(ushort)value;links[from]=entry;if(transient)temporaryNavigationStamps[from]=grid.LinkStamp;return valid;
 }
 sealed class NavHeap {
  long[] items=new long[2048];int count;
  public int Count {get{return count;}}public void Clear(){count=0;}
  public void Add(long value){if(count==items.Length)Array.Resize(ref items,items.Length*2);int i=count++;while(i>0){int parent=(i-1)/2;if(items[parent]<=value)break;items[i]=items[parent];i=parent;}items[i]=value;}
  public long Pop(){long top=items[0],value=items[--count];if(count==0)return top;int i=0;while(i*2+1<count){int child=i*2+1;if(child+1<count&&items[child+1]<items[child])child++;if(items[child]>=value)break;items[i]=items[child];i=child;}items[i]=value;return top;}
 }
 bool SmallGoalEnclosed(NavigationGrid grid,int start,int end,float pad){
  // Reject tiny enclosed destinations before a long-distance A* scans the
  // whole continent. Hitting the limit means unknown, never unreachable.
  var reached=new HashSet<int>{end};var queue=new Queue<int>();queue.Enqueue(end);
  while(queue.Count>0&&reached.Count<64){int current=queue.Dequeue();if(current==start)return false;
   for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++){
    if(dx==0&&dy==0)continue;int x=current%N+dx,y=current/N+dy,next=y*N+x;
    if(x<0||x>=N||y<0||y>=M||!grid.Open[next]||reached.Contains(next))continue;
    if(dx*dy!=0&&(!grid.Open[current+dx]||!grid.Open[current+dy*N]))continue;
    if(NavigationLink(grid,current,next,pad)){reached.Add(next);queue.Enqueue(next);}
   }
  }return queue.Count==0&&!reached.Contains(start);
 }
 bool IslandMovePreview(Piece unit,PointF point,NavigationGrid grid,float pad,bool tracks){
  OpenGrid(grid,pad);int start=Anchor(unit.P,grid.Open,grid.Barriers,tracks,pad),end=Anchor(point,grid.Open,grid.Barriers,tracks,pad);if(start<0||end<0)return false;
  if(grid.Reachability==null)grid.Reachability=new Dictionary<long,bool>();long key=(long)start*(N*M)+end;bool reachable;
  if(grid.Reachability.TryGetValue(key,out reachable))return reachable;
  // A detached probe uses the exact route solver without writing orders or
  // flooding every connected cell on the large continent during a hover.
  var probe=new Piece{T=unit.T,Side=unit.Side,Hp=unit.Hp,P=unit.P};reachable=PlanRoute(probe,point,null);
  if(grid.Reachability.Count>=256)grid.Reachability.Clear();grid.Reachability[key]=reachable;return reachable;
 }
 // Keep path cost as the primary A* key. Among equal-cost candidates,
 // prefer the point nearer the destination instead of a fixed map index.
 // This avoids flooding large equal-f-score rectangles on open terrain.
 long NavigationPriority(int cost,int heuristic,int index){
  long stride=10L*(N+M)+1;return (((long)cost+heuristic)*stride+heuristic)*((long)N*M)+index;
 }
 readonly NavHeap navigationHeap=new NavHeap();int[] navigationCosts,navigationPrevious,navigationStamp;int navigationEpoch;
 void BeginNavigationSearch(){
  if(navigationCosts==null){navigationCosts=new int[N*M];navigationPrevious=new int[N*M];navigationStamp=new int[N*M];}
  if(navigationEpoch==int.MaxValue){Array.Clear(navigationStamp,0,navigationStamp.Length);navigationEpoch=0;}navigationEpoch++;navigationHeap.Clear();
 }
 int NavigationCost(int index){return navigationStamp[index]==navigationEpoch?navigationCosts[index]:int.MaxValue;}
 void SetNavigationCost(int index,int value,int previous){navigationStamp[index]=navigationEpoch;navigationCosts[index]=value;navigationPrevious[index]=previous;}
 readonly float[] nextAiDecision=new float[MaxFactions];
 void TickStrategicAis(){
  // At most one faction plans in an update; construction and tactical
  // decisions stay responsive without synchronizing five long planning bursts.
  for(int side=1;side<FactionCount;side++)if(Time>=nextAiDecision[side]){nextAiDecision[side]=Time+MatchTempo.DecisionInterval;EnemyEconomy(side);EnemyHousing(side);break;}
 }
}
}
