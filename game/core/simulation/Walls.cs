using System;
using System.Collections.Generic;
using System.Drawing;

namespace Greyhaven {
public partial class Campaign {
 // Matches the wall atlas order (+Y, +X, -Y, -X).
 static readonly Point[] WallDirections={new Point(0,1),new Point(1,0),new Point(0,-1),new Point(-1,0)};
 const int WallConnectionRange=5;
 readonly Dictionary<Point,Piece> wallCells=new Dictionary<Point,Piece>();
 int wallTopology=-1;

 static Point WallCell(PointF point)=>new Point((int)Math.Floor(point.X/Tile),(int)Math.Floor(point.Y/Tile));
 void RefreshWallCells(){
  if(wallTopology!=topology){
   wallCells.Clear();
   foreach(var wall in Units)
    if(wall.Alive&&wall.T.IsWall)wallCells[WallCell(wall.P)]=wall;
   wallTopology=topology;
  }
 }
 Piece WallAt(Point cell){
  RefreshWallCells();
  return wallCells.TryGetValue(cell,out var wallAtCell)?wallAtCell:null;
 }
 Piece WallAt(PointF point)=>WallAt(WallCell(point));
 public int WallConnections(Piece wall){
  var cell=WallCell(wall.P);int mask=0;
  for(int i=0;i<WallDirections.Length;i++){
   var direction=WallDirections[i];
   var neighbor=WallAt(new Point(cell.X+direction.X,cell.Y+direction.Y));
   if(neighbor?.Side==wall.Side&&Math.Abs(GroundHeight(neighbor.P)-GroundHeight(wall.P))<=.05f)mask|=1<<i;
  }
  return mask;
 }

 IReadOnlyList<RectangleF> WallExtensions(RectangleF foundation,int side,float height){
  List<RectangleF> extensions=null;
  var origin=WallCell(Middle(foundation));
  foreach(var direction in WallDirections){
   Piece anchor=null;int distance=0;
   for(int step=1;step<=WallConnectionRange;step++){
    anchor=WallAt(new Point(origin.X+direction.X*step,origin.Y+direction.Y*step));
    if(anchor!=null){distance=step;break;}
   }
   if(anchor?.Side!=side||distance<2||Math.Abs(GroundHeight(anchor.P)-height)>.05f)continue;
   var gap=new List<RectangleF>(distance-1);
   for(int step=1;step<distance;step++){
    var cell=new RectangleF(foundation.X+direction.X*step*Tile,foundation.Y+direction.Y*step*Tile,Tile,Tile);
    if(LocalPlacementBlock(cell,null,side,height)!=PlacementBlock.None)break;
    gap.Add(cell);
   }
   if(gap.Count!=distance-1)continue;
   extensions??=new List<RectangleF>();
   extensions.AddRange(gap);
  }
  return extensions==null?Array.Empty<RectangleF>():extensions;
 }

 public bool CanAttackTarget(Piece unit,Piece target){
  var weapon=ReferenceBalance.Weapon(unit);
  return target!=null&&weapon!=null&&(!target.T.IsWall||weapon.DamagesWalls);
 }

 // Walk each cell crossed by the projectile, in travel order.
 Piece FirstWall(PointF from,PointF to,out PointF impact){
  impact=to;
  RefreshWallCells();
  if(wallCells.Count==0)return null;
  var start=WallCell(from);var end=WallCell(to);
  float dx=to.X-from.X,dy=to.Y-from.Y;
  int x=start.X,y=start.Y,sx=Math.Sign(dx),sy=Math.Sign(dy);
  float nextX=sx==0?float.PositiveInfinity:((x+(sx>0?1:0))*Tile-from.X)/dx;
  float nextY=sy==0?float.PositiveInfinity:((y+(sy>0?1:0))*Tile-from.Y)/dy;
  float intervalX=sx==0?float.PositiveInfinity:Tile/Math.Abs(dx);
  float intervalY=sy==0?float.PositiveInfinity:Tile/Math.Abs(dy);
  while(x!=end.X||y!=end.Y){
   float entryX=x==end.X?float.PositiveInfinity:nextX,entryY=y==end.Y?float.PositiveInfinity:nextY;
   if(entryX<=entryY){x+=sx;nextX+=intervalX;}
   if(entryY<=entryX){y+=sy;nextY+=intervalY;}
   var wall=WallAt(new Point(x,y));
   if(wall==null)continue;
   float entry=Math.Clamp(Math.Min(entryX,entryY),0,1);
   impact=new PointF(from.X+dx*entry,from.Y+dy*entry);
   return wall;
  }
  return null;
 }
}
}
