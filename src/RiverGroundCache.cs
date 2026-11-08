using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
namespace Greyhaven {
partial class CommandWindow {
 const int RiverTileSize=256,RiverTileGutter=3;
 const long RiverTileBudget=64L*1024*1024;
 sealed class RiverGroundTile {public Bitmap Image;public int Used;}
 readonly Dictionary<Point,RiverGroundTile> riverGroundTiles=new Dictionary<Point,RiverGroundTile>();
 RiverbendMap riverGroundMap;float riverGroundZoom,riverGroundScale;long riverGroundBytes;int riverGroundFrame;
 RectangleF? riverTileDrawArea;
 void ClearRiverGroundCache(){foreach(var tile in riverGroundTiles.Values)tile.Image.Dispose();riverGroundTiles.Clear();riverGroundBytes=0;riverGroundMap=null;}
 void EnsureRiverGroundCache(){
  if(riverGroundMap==riverMap&&riverGroundZoom==mapZoom&&riverGroundScale==scale)return;
  ClearRiverGroundCache();riverGroundMap=riverMap;riverGroundZoom=mapZoom;riverGroundScale=scale;
 }
 Rectangle RiverTileRange(RectangleF area){
  var origin=RiverScreen(0,0);int left=(int)Math.Floor((area.Left-origin.X)*scale/RiverTileSize),top=(int)Math.Floor((area.Top-origin.Y)*scale/RiverTileSize);
  int right=(int)Math.Ceiling((area.Right-origin.X)*scale/RiverTileSize),bottom=(int)Math.Ceiling((area.Bottom-origin.Y)*scale/RiverTileSize);
  return Rectangle.FromLTRB(left,top,right,bottom);
 }
 Bitmap GetRiverGroundTile(Point key){
  RiverGroundTile found;if(riverGroundTiles.TryGetValue(key,out found)){found.Used=riverGroundFrame;return found.Image;}
  int edge=RiverTileSize+RiverTileGutter*2;long bytes=4L*edge*edge;
  while(riverGroundBytes+bytes>RiverTileBudget){
   var old=riverGroundTiles.OrderBy(p=>p.Value.Used).FirstOrDefault();if(old.Value==null)break;
   old.Value.Image.Dispose();riverGroundTiles.Remove(old.Key);riverGroundBytes-=bytes;
  }
  var origin=RiverScreen(0,0);var area=new RectangleF(origin.X+(key.X*RiverTileSize-RiverTileGutter)/scale,origin.Y+(key.Y*RiverTileSize-RiverTileGutter)/scale,edge/scale,edge/scale);
  var image=new Bitmap(edge,edge,PixelFormat.Format32bppPArgb);var previousArea=riverTileDrawArea;bool previousClient=drawingClient;
  try{using(var g=Graphics.FromImage(image)){
   g.ScaleTransform(scale,scale);g.TranslateTransform(-area.Left,-area.Top);g.InterpolationMode=InterpolationMode.Bilinear;
   riverTileDrawArea=area;drawingClient=false;DrawRiverMap(g);
  }}catch{image.Dispose();throw;}finally{riverTileDrawArea=previousArea;drawingClient=previousClient;}
  riverGroundTiles.Add(key,new RiverGroundTile{Image=image,Used=riverGroundFrame});riverGroundBytes+=bytes;return image;
 }
 // Tiles are fixed to projected world pixels. Camera movement changes only the
 // composite offset; distant terrain is neither rerendered nor held as a giant bitmap.
 void DrawRiverGroundTiles(Graphics g){
  EnsureRiverGroundCache();riverGroundFrame++;var area=riverDrawArea;var range=RiverTileRange(area);var origin=RiverScreen(0,0);
  int left=(int)Math.Round((origin.X-area.Left)*scale),top=(int)Math.Round((origin.Y-area.Top)*scale);
  var state=g.Save();g.ResetTransform();g.CompositingMode=CompositingMode.SourceCopy;
  try{for(int y=range.Top;y<range.Bottom;y++)for(int x=range.Left;x<range.Right;x++){
   var image=GetRiverGroundTile(new Point(x,y));g.DrawImageUnscaled(image,left+x*RiverTileSize-RiverTileGutter,top+y*RiverTileSize-RiverTileGutter);
  }}finally{g.Restore(state);}
 }
 void WarmRiverGroundTiles(){
  EnsureRiverGroundCache();var area=riverDrawArea;area.Inflate(RiverTileSize/scale,RiverTileSize/scale);var range=RiverTileRange(area);
  var center=RiverWorld(new PointF(mapViewport.Width*.5f,mapViewport.Top+mapViewport.Height*.5f));
  float cx=(center.X-center.Y)*Campaign.Tile*Axis*mapZoom*scale/RiverTileSize,cy=(center.X+center.Y)*Campaign.Tile*Rise*mapZoom*scale/RiverTileSize;
  Point next=Point.Empty;float best=float.MaxValue;bool pending=false;
  for(int y=range.Top;y<range.Bottom;y++)for(int x=range.Left;x<range.Right;x++){
   var key=new Point(x,y);if(riverGroundTiles.ContainsKey(key))continue;float dx=x+.5f-cx,dy=y+.5f-cy,d=dx*dx+dy*dy;
   if(d<best){next=key;best=d;pending=true;}
  }
  // At most one small tile per ordinary frame, before the next viewport boundary.
  if(pending)GetRiverGroundTile(next);
 }
}
}
