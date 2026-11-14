using System;
using System.Collections.Generic;
using System.Drawing;

namespace Greyhaven {
public partial class Campaign {
 // Route links span one 15-unit navigation step. Index obstacles in 60-unit
 // buckets so a link tests only nearby rectangles, with the same exact slab
 // intersection as the full scan. Static and traffic grids own separate indexes.
 sealed class NavigationBarriers {
  internal readonly Dictionary<int,List<RectangleF>> Buckets=new Dictionary<int,List<RectangleF>>();
 }
 bool BarrierLinkClear(NavigationGrid grid,int from,int to,PointF a,PointF b){
  int columns=(N+3)/4;
  if(grid.LocalBarriers==null){
   var index=new NavigationBarriers();
   foreach(var r in grid.Barriers){
    int left=Math.Max(0,NavX(r.Left)/4),right=Math.Min(columns-1,NavX(r.Right)/4);
    int top=Math.Max(0,NavY(r.Top)/4),bottom=Math.Min((M+3)/4-1,NavY(r.Bottom)/4);
    for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++){
     int key=y*columns+x;List<RectangleF> bucket;
     if(!index.Buckets.TryGetValue(key,out bucket))index.Buckets[key]=bucket=new List<RectangleF>();bucket.Add(r);
    }
   }grid.LocalBarriers=index;
  }
  int ax=from%N/4,ay=from/N/4,bx=to%N/4,by=to/N/4;
  for(int y=Math.Min(ay,by);y<=Math.Max(ay,by);y++)for(int x=Math.Min(ax,bx);x<=Math.Max(ax,bx);x++){
   List<RectangleF> bucket;if(!grid.LocalBarriers.Buckets.TryGetValue(y*columns+x,out bucket))continue;
   foreach(var r in bucket){float lo=0,hi=1;if(Slab(a.X,b.X-a.X,r.Left,r.Right,ref lo,ref hi)&&Slab(a.Y,b.Y-a.Y,r.Top,r.Bottom,ref lo,ref hi))return false;}
  }return true;
 }
}
}
