using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
namespace Greyhaven {
public sealed partial class RiverbendMap {
 public readonly bool IsRelay;
 public static RiverbendMap Relay(){return new RiverbendMap(false,false,null,true);}
 public static PointF RelayGrid(float u,float v){float a=-180+u*4.2f,b=v*4.2f;return new PointF((a+b)*.5f,(b-a)*.5f);}
 public static Point RelayCell(float u,float v){var p=RelayGrid(u,v);return new Point((int)Math.Floor(p.X),(int)Math.Floor(p.Y));}
 public static PointF RelayWorld(float u,float v){var p=RelayCell(u,v);return new PointF((p.X+.5f)*Campaign.Tile,(p.Y+.5f)*Campaign.Tile);}
 public sealed class RelayRoad {public string Id;public float Width;public PointF[] Points;public float[] Levels;public RelayRoad(string id,float width,PointF[] points,float[] levels){Id=id;Width=width;Points=points.Select(p=>RelayGrid(p.X,p.Y)).ToArray();Levels=levels;}}
 public static readonly RelayRoad[] RelayRoads={
  new RelayRoad("A",8,new[]{new PointF(15,82),new PointF(29,76),new PointF(42,66),new PointF(49,57),new PointF(53,48),new PointF(46,38),new PointF(47,30),new PointF(53,21)},new[]{0f,0,0,.5f,1,1,2,2}),
  new RelayRoad("B",6,new[]{new PointF(15,82),new PointF(12,66),new PointF(13,57),new PointF(20,48),new PointF(27,36),new PointF(28,25),new PointF(37,15),new PointF(45,16),new PointF(53,21)},new[]{0f,0,0,.4f,1,1.2f,1.5f,2,2}),
  new RelayRoad("C",8,new[]{new PointF(42,66),new PointF(54,71),new PointF(63,68),new PointF(70,60),new PointF(76,54),new PointF(84,42)},new[]{0f,0,0,0,1,1}),
  new RelayRoad("R",6,new[]{new PointF(84,42),new PointF(80,32),new PointF(78,24),new PointF(68,18),new PointF(58,23),new PointF(53,21)},new[]{1f,1,1.3f,2,2,2}),
  new RelayRoad("L",6,new[]{new PointF(53,48),new PointF(60,50),new PointF(67,56),new PointF(70,60)},new[]{1f,.5f,0,0})
 };
 static readonly PointF[][] relayRock={
  new[]{new PointF(0,0),new PointF(40,0),new PointF(42,10),new PointF(35,21),new PointF(32,30),new PointF(22,37),new PointF(16,47),new PointF(0,54)},
  new[]{new PointF(43,8),new PointF(61,9),new PointF(69,20),new PointF(63,34),new PointF(43,32),new PointF(38,23)},
  new[]{new PointF(40,29),new PointF(47,33),new PointF(55,36),new PointF(60,42),new PointF(57,52),new PointF(45,50),new PointF(38,42)},
  new[]{new PointF(88,0),new PointF(100,0),new PointF(100,24),new PointF(95,23),new PointF(86,17)}
 };
 static readonly PointF[] relayLake={new PointF(75,73),new PointF(82,74),new PointF(88,82),new PointF(86,91),new PointF(78,96),new PointF(70,94),new PointF(65,87),new PointF(68,79)};
 static readonly PointF[] relayStream=new[]{new PointF(72,43),new PointF(73,50),new PointF(70,60),new PointF(70,69),new PointF(75,76)}.Select(p=>RelayGrid(p.X,p.Y)).ToArray();
 static readonly float[][] relayForests={new[]{17f,43,17,22},new[]{36f,84,10,12},new[]{87f,69,13,13},new[]{59f,9,18,10},new[]{91f,91,13,14}};
 static bool RelayInside(PointF p,PointF[] poly){bool hit=false;for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)if((poly[i].Y>p.Y)!=(poly[j].Y>p.Y)&&p.X<(poly[j].X-poly[i].X)*(p.Y-poly[i].Y)/(poly[j].Y-poly[i].Y)+poly[i].X)hit=!hit;return hit;}
 static float RelaySegment(PointF p,PointF a,PointF b,out float t){float x=b.X-a.X,y=b.Y-a.Y;t=Math.Max(0,Math.Min(1,((p.X-a.X)*x+(p.Y-a.Y)*y)/(x*x+y*y)));return (float)Math.Sqrt(Math.Pow(p.X-a.X-t*x,2)+Math.Pow(p.Y-a.Y-t*y,2));}
 Cell RelayGround(int x,int y){
  var p=new PointF(x+.5f,y+.5f);var uv=new PointF((p.X-p.Y+180)/4.2f,(p.X+p.Y)/4.2f);int hash=Hash(x+493,y+631);
  var c=new Cell{Ground=hash%39==0?Surface.Soil:Surface.Meadow,Variation=hash};
  for(int i=0;i<relayRock.Length;i++)if(RelayInside(uv,relayRock[i])){c.Ground=Surface.Rock;c.Height=i==1?2:i==2?1:3;}
  if(uv.X>75&&uv.Y<62&&uv.Y>25)c.Height=1;
  c.Tree=relayForests.Any(f=>Math.Pow((uv.X-f[0])/f[2],2)+Math.Pow((uv.Y-f[1])/f[3],2)<1)&&hash%11<5;
  bool wet=RelayInside(uv,relayLake);float t;for(int i=1;i<relayStream.Length;i++)if(RelaySegment(p,relayStream[i-1],relayStream[i],out t)<2.3f)wet=true;
  if(wet){c.Ground=Surface.Water;c.Tree=false;c.Height=0;}
  float best=float.MaxValue;foreach(var r in RelayRoads)for(int i=1;i<r.Points.Length;i++){
   float distance=RelaySegment(p,r.Points[i-1],r.Points[i],out t);if(distance>r.Width*.5f+1||distance>=best)continue;
   best=distance;c.Height=r.Levels[i-1]+(r.Levels[i]-r.Levels[i-1])*t;c.Tree=false;
   c.Ground=wet?Surface.Bridge:Math.Abs(r.Levels[i]-r.Levels[i-1])>.001f?Surface.Ramp:Surface.Road;
  }
  return c;
 }
 void RelayFlat(Point center,int w,int h,float level){
  // Flatten buildable ground without turning an entire base clearing into a slab.
  for(int y=center.Y-h/2;y<center.Y-h/2+h;y++)for(int x=center.X-w/2;x<center.X-w/2+w;x++){
   var c=At(x,y);if(c==null)continue;
   bool road=c.Ground==Surface.Road||c.Ground==Surface.Ramp||c.Ground==Surface.Bridge;
   c.Ground=road?Surface.Road:c.Ground==Surface.Rock?Surface.Rock:Surface.Meadow;c.Height=level;c.Tree=false;
  }
 }
 void RelayOre(float u,float v,int width,int depth,float level){
  var p=RelayCell(u,v);RelayFlat(p,width+6,depth+6,level);
  for(int y=p.Y-depth/2;y<p.Y-depth/2+depth;y++)for(int x=p.X-width/2;x<p.X-width/2+width;x++){
   var c=At(x,y);c.Ground=Surface.Soil;c.Tree=false;Ore.Add(new Point(x,y));
  }
 }
 void RelayVillage(float u,float v,int count,float level){
  var p=RelayCell(u,v);RelayFlat(p,26,24,level);
  for(int i=0;i<count;i++){int col=i%2,row=i/2;AddHouse(i==0?"shop":"residence","山地旧屋",p.X+(col==0?-10:7),p.Y-9+row*7,i==0?3:2,2,i==0?6:4);}
 }
 void GenerateRelay(){
  var b=TerrainBounds;for(int y=b.Top;y<b.Bottom;y++)for(int x=b.Left;x<b.Right;x++){
   if(!WithinRadar(x,y))continue;var c=RelayGround(x,y);
   if(x>=0&&y>=0&&x<WidthCells&&y<HeightCells)Cells[y*WidthCells+x]=c;else extension[CellKey(x,y)]=c;
  }
  coastReady=true;
  RelayFlat(Starts[0],30,24,0);RelayFlat(Starts[1],32,26,1);
  RelayFlat(RelayCell(53,21),28,22,2);RelayFlat(RelayCell(53,48),18,14,1);
  RelayVillage(42,66,6,0);RelayVillage(27,36,4,1);RelayVillage(67,56,4,0);
  RelayOre(10,78,7,5,0);RelayOre(20,86,7,5,0);RelayOre(36,46,8,5,0);RelayOre(55,84,8,5,0);RelayOre(79,38,7,5,1);RelayOre(90,46,7,5,1);
  for(int y=b.Top;y<b.Bottom;y++)for(int x=b.Left;x<b.Right;x++){
   var c=At(x,y);if(c==null||c.Ground!=Surface.Meadow&&c.Ground!=Surface.Soil)continue;
   if(new[]{At(x-1,y),At(x+1,y),At(x,y-1),At(x,y+1)}.Any(n=>n!=null&&n.Ground==Surface.Water)){c.Ground=Surface.Sand;c.Tree=false;}
  }
  foreach(var item in new[]{Tuple.Create("我方展开区",15f,82f,1),Tuple.Create("中继控制站",53f,21f,5),Tuple.Create("敌方生产基地",84f,42f,2),Tuple.Create("林区侧路",27f,36f,0),Tuple.Create("正面公路",53f,48f,0),Tuple.Create("东麓截击线",67f,56f,0),Tuple.Create("雾脊湖",77f,85f,4)}){var p=RelayGrid(item.Item2,item.Item3);Label(item.Item1,p.X,p.Y,item.Item4);}
 }
 void ExportRelay(string path){
  var s=new StringBuilder("{\"id\":\"mist-ridge-relay-v01\",\"status\":\"playable\",\"columns\":240,\"rows\":180,\"cellWorldSize\":24,\"starts\":[");
  for(int i=0;i<Starts.Length;i++){if(i>0)s.Append(',');s.Append('[').Append(Starts[i].X).Append(',').Append(Starts[i].Y).Append(']');}s.Append(']');
  var objective=RelayCell(53,21);s.Append(",\"objective\":{\"cell\":[").Append(objective.X).Append(',').Append(objective.Y).Append("],\"footprint\":[3,3],\"hp\":1800,\"initialOwner\":1,\"cumulativeGameSeconds\":360,\"lostOnDestruction\":true},\"oreStockPerCell\":480,\"ore\":[");
  for(int i=0;i<Ore.Count;i++){if(i>0)s.Append(',');s.Append('[').Append(Ore[i].X).Append(',').Append(Ore[i].Y).Append(']');}s.Append("],\"houses\":[");
  for(int i=0;i<Houses.Count;i++){if(i>0)s.Append(',');var h=Houses[i];s.Append("{\"kind\":\"").Append(h.Kind).Append("\",\"lot\":[").Append(h.Lot.X).Append(',').Append(h.Lot.Y).Append(',').Append(h.Lot.Width).Append(',').Append(h.Lot.Height).Append("],\"initialOwner\":null,\"capacity\":").Append(h.Capacity).Append('}');}s.Append(']');
  AppendRadarTerrain(s);s.Append('}');File.WriteAllText(path,s.ToString(),new UTF8Encoding(false));
 }
}
}
