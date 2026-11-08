using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public sealed partial class RiverbendMap {
 public bool IsTraining {get;private set;}
 bool coastReady;
 readonly Dictionary<long,Cell> extension=new Dictionary<long,Cell>();
 readonly HashSet<long> protectedGround=new HashSet<long>();
 static long CellKey(int x,int y){return ((long)x<<32)|(uint)y;}
 public Rectangle TerrainBounds {get{return Rectangle.FromLTRB(-HeightCells/2,-WidthCells/2,WidthCells+HeightCells/2,HeightCells+WidthCells/2);}}
 public bool WithinRadar(int x,int y){return new BattlefieldBounds(WidthCells,HeightCells).Contains(new PointF(x+.5f,y+.5f));}
 public static RiverbendMap Training(IEnumerable<PointF> minerals){return new RiverbendMap(false,true,minerals);}
 void GenerateTraining(IEnumerable<PointF> minerals){
  for(int y=0;y<HeightCells;y++)for(int x=0;x<WidthCells;x++)Cells[y*WidthCells+x]=BaseExtension(x,y);
  foreach(var r in Campaign.Roads)Road((int)(r.X/Campaign.Tile),(int)(r.Y/Campaign.Tile),Math.Max(1,(int)(r.Width/Campaign.Tile)),Math.Max(1,(int)(r.Height/Campaign.Tile)));
  if(minerals!=null)foreach(var p in minerals)Ore.Add(new Point((int)(p.X/Campaign.Tile),(int)(p.Y/Campaign.Tile)));
 }
 Cell BaseExtension(int x,int y){
  int n=Hash(x+317,y+781);var c=new Cell{Ground=Surface.Meadow,Variation=n};
  if(!IsTraining){
   bool grove=Math.Sin(x*.071)+Math.Cos(y*.083)+Math.Sin((x+y)*.041)>1.85;
   c.Tree=grove&&n%13<3;
  }
  return c;
 }
 void Protect(Rectangle area){for(int y=area.Top;y<area.Bottom;y++)for(int x=area.Left;x<area.Right;x++)protectedGround.Add(CellKey(x,y));}
 void PrepareCoastline(){
  foreach(var start in Starts)Protect(new Rectangle(start.X-15,start.Y-13,31,28));
  foreach(var house in Houses)Protect(Rectangle.Inflate(house.Lot,2,2));
  foreach(var ore in Ore)Protect(new Rectangle(ore.X-1,ore.Y-1,3,3));
  for(int y=0;y<HeightCells;y++)for(int x=0;x<WidthCells;x++){
   var c=Cells[y*WidthCells+x];if(c.Ground==Surface.Road||c.Ground==Surface.Square||c.Ground==Surface.Ramp)Protect(new Rectangle(x,y,1,1));
  }
  for(int y=0;y<HeightCells;y++)for(int x=0;x<WidthCells;x++)ApplyCoast(x,y,Cells[y*WidthCells+x]);
  coastReady=true;
 }
 float CoastWave(float x,float y){return (float)(.55*Math.Sin(x*.39+y*.21)+.3*Math.Sin(x*.83-y*.57));}
 static float EllipseDistance(float x,float y,float cx,float cy,float rx,float ry){float dx=(x-cx)/rx,dy=(y-cy)/ry;return (float)(1-Math.Sqrt(dx*dx+dy*dy))*Math.Min(rx,ry);}
 public float CoastDistance(float x,float y){
  float span=WidthCells+HeightCells,u=(x-y+HeightCells)/span,v=(x+y)/span,d;
  if(IsIsland){
   float island=EllipseDistance(x,y,44,40,41,33)+CoastWave(x,y)*.65f;
   float rim=-EllipseDistance(x,y,44,40,56,49);
   float shore=.30f+.070f*(float)Math.Sin(u*Math.PI*2+1)+.025f*(float)Math.Sin(u*18+.3);
   float mainland=Math.Min((v-shore)*span*.7f,rim);
   d=Math.Max(island,mainland);
   // Broad coastal bays break the continental outline without enclosing bases.
   if(u<.27f)d=Math.Min(d,-EllipseDistance(u*span,v*span,.04f*span,.79f*span,.13f*span,.12f*span));
   if(protectedGround.Contains(CellKey((int)Math.Floor(x),(int)Math.Floor(y)))&&rim>0)d=Math.Max(d,3);
   float river=Math.Abs(x-(232+11*(float)Math.Sin(y/27f)))-(4.5f+Math.Max(0,y-193)*.18f);
   if(y>12)d=Math.Min(d,river);
   d=Math.Min(d,-EllipseDistance(x,y,171,158,24,14));
   d=Math.Min(d,-EllipseDistance(x,y,286,65,18,12));
   d=Math.Min(d,-EllipseDistance(x,y,132,202,18,11));
  }else{
   float west=.045f+.023f*(float)Math.Sin(v*8+.3)+.012f*(float)Math.Sin(v*23);
   float south=1.035f-.06f*(float)Math.Sin(u*5+.6)-.02f*(float)Math.Sin(u*17);
   d=Math.Min((u-west)*span*.7f,(south-v)*span*.7f)+CoastWave(x,y);
   if(protectedGround.Contains(CellKey((int)Math.Floor(x),(int)Math.Floor(y))))d=Math.Max(d,3);
   if(!IsTraining){
    d=Math.Min(d,Math.Abs(x-RiverCenter(y))-RiverWidth(y));
    d=Math.Min(d,-EllipseDistance(x,y,32,24,21,13));
   }
  }
  return d;
 }
 // Classification is per whole 24-world-unit cell. No cell contains a
 // cosmetic land/water split: rendering, placement and navigation read Ground.
 void ApplyCoast(int x,int y,Cell c){
  if(c.Ground==Surface.Bridge)return;
  float distance=CoastDistance(x+.5f,y+.5f);
  if(distance<0){c.Ground=Surface.Water;c.Height=0;c.Tree=false;return;}
  if(c.Ground==Surface.Water)c.Ground=Surface.Meadow;
  if(distance<2.2f&&c.Ground!=Surface.Road&&c.Ground!=Surface.Square&&c.Ground!=Surface.Ramp){
   c.Ground=Surface.Sand;c.Height=0;c.Tree=false;
  }
 }
 Cell ExtensionAt(int x,int y){
  if(!coastReady||!WithinRadar(x,y))return null;
  Cell c;long key=CellKey(x,y);if(extension.TryGetValue(key,out c))return c;
  c=BaseExtension(x,y);ApplyCoast(x,y,c);extension.Add(key,c);return c;
 }
}
public partial class Campaign {
 RiverbendMap trainingTerrain;
 public RiverbendMap Terrain {get{return River??(trainingTerrain??(trainingTerrain=RiverbendMap.Training(CreateFields().Select(p=>p.P))));}}
 public bool WaterAt(PointF p){var c=Terrain.At((int)Math.Floor(p.X/Tile),(int)Math.Floor(p.Y/Tile));return c!=null&&c.Ground==RiverbendMap.Surface.Water;}
}
}
