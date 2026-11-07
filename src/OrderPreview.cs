using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
namespace Greyhaven {
public partial class Campaign {
 void OpenGrid(NavigationGrid grid,float pad){
  if(grid.Open!=null)return;
  grid.Open=new bool[N*M];
  for(int i=0;i<grid.Open.Length;i++){var c=Center(i);grid.Open[i]=c.X>=pad&&c.X<=MapWidth-pad&&c.Y>=(River==null?99:0)+pad&&c.Y<=MapHeight-pad&&TerrainOpen(c,pad)&&!grid.Barriers.Any(r=>r.Contains(c));}
 }
 void ConnectGrid(NavigationGrid grid,float pad){
  if(grid.Regions!=null)return;OpenGrid(grid,pad);
  grid.Regions=new int[N*M];int region=0;var queue=new Queue<int>();
  for(int seed=0;seed<grid.Open.Length;seed++){
   if(!grid.Open[seed]||grid.Regions[seed]!=0)continue;grid.Regions[seed]=++region;queue.Enqueue(seed);
   while(queue.Count>0){int cur=queue.Dequeue();
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
     if(x==0&&y==0)continue;int col=cur%N+x,row=cur/N+y,n=row*N+col;
     if(col<0||col>=N||row<0||row>=M||!grid.Open[n]||grid.Regions[n]!=0)continue;
     if(x*y!=0&&(!grid.Open[cur+x]||!grid.Open[cur+y*N]))continue;
     if(!Clear(Center(cur),Center(n),grid.Barriers,pad))continue;
     grid.Regions[n]=region;queue.Enqueue(n);
    }
   }
  }
 }
 // Same static obstacles, eight-way connections and end-point anchors as A*.
 // Hover checks never write paths/orders, and reuse connected regions per footprint.
 public bool CanMoveTo(Piece unit,PointF p){
  if(unit==null||!unit.Deployed||unit.T.Building||!ParkingAllowed(unit.Side,p))return false;
  float pad=Clearance(unit);if(Blocked(p,pad,null))return false;
  var grid=Grid(pad);bool tracks=!unit.T.Foot;
  if(CanLink(tracks,unit.P,p,grid.Barriers,pad))return true;
  ConnectGrid(grid,pad);int a=Anchor(unit.P,grid.Open,grid.Barriers,tracks,pad),b=Anchor(p,grid.Open,grid.Barriers,tracks,pad);
  return a>=0&&b>=0&&grid.Regions[a]==grid.Regions[b];
 }
 public bool CanRallyTo(Piece src,PointF p){
  if(src==null||!src.Deployed||src.Side!=0||src.T.Key=="refinery"||!HasGate(src.T))return false;
  var gate=Gate(Footprint(src.T,src.P));
  return Types.Where(t=>!t.Building&&t.Producer==src.T.Key).All(t=>CanMoveTo(new Piece{T=t,Side=src.Side,Hp=t.Hp,P=new PointF(src.P.X,gate.Bottom+12)},p));
 }
}
}
