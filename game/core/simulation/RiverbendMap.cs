using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
namespace Greyhaven {
// Authored map core plus full radar-domain terrain; all coast cells are whole tiles.
// Preview rendering and connectivity checks consume these SAME cells and lots.
public sealed partial class RiverbendMap {
 public const int Columns=92,Rows=68;
 public readonly int WidthCells,HeightCells;public readonly bool IsIsland;public readonly Point[] Starts;
 public string Title {get{return IsBasin?"盆地岔口":IsRelay?"雾脊中继站":IsChapterOne?"断线之夜":IsTraining?"野外训练":IsIsland?"孤岛要塞":"河湾镇";}}
 public enum Surface { Meadow,Soil,Rock,Marsh,Water,Road,Bridge,Square,Ramp,Sand }
 public sealed class Cell {public Surface Ground;public float Height;public bool Tree,Railroad,Cliff;public int Variation;}
 public sealed class House {
  public string Kind,Name;public Rectangle Lot;public int Capacity;
  public Point Door {get{return new Point(Lot.X+Lot.Width/2,Lot.Bottom);}}
 }
 public sealed class Landmark {public string Name;public PointF Position;public int Style;}
 public readonly Cell[] Cells;
 public readonly List<House> Houses=new List<House>();
 public readonly List<Rectangle> Bridges=new List<Rectangle>();
 public readonly List<Landmark> Labels=new List<Landmark>();
 public readonly List<Point> Ore=new List<Point>();
 public readonly Point WestStart=new Point(24,34),EastStart=new Point(73,18),SouthStart=new Point(74,52);
 internal bool IsRiverbendChapter {get{return !IsTraining&&!IsIsland&&!IsRelay&&!IsBasin;}}
 internal const int RiverRoadTop=31,RiverRoadWidth=7,RiverExitX=119;
 internal static readonly Point RiverJunction=new Point(63,34),RiverBridgeHouse=new Point(36,28);
 internal static readonly Rectangle RiverImpound=new Rectangle(57,25,5,5);
 internal static readonly Point RiverExitSign=new Point(78,38);
 public Cell At(int x,int y){return x<0||y<0||x>=WidthCells||y>=HeightCells?ExtensionAt(x,y):Cells[y*WidthCells+x];}
 public static float RiverCenter(float y){return 45+2.5f*(float)Math.Sin((y-11)/11)+1.1f*(float)Math.Sin(y/5.2);}
 static float RiverWidth(float y){return 4.8f+Math.Max(0,y-48)*.12f;}
 static float RiverbendWaterDistance(float x,float y){
  return Math.Min(Math.Abs(x-RiverCenter(y))-RiverWidth(y),-EllipseDistance(x,y,19,12,8,5));
 }
 static double Ellipse(int x,int y,float cx,float cy,float rx,float ry){return Math.Pow((x-cx)/rx,2)+Math.Pow((y-cy)/ry,2);}
 static int Hash(int x,int y){unchecked{uint q=(uint)(x*73856093^y*19349663^71423);q^=q>>13;q*=1274126177;return (int)(q&0x7fffffff);}}
 public RiverbendMap(bool island=false):this(island,false,null){}
 RiverbendMap(bool island,bool training,IEnumerable<PointF> minerals,bool relay=false,bool chapterOne=false,bool basin=false){
  IsRelay=relay;IsIsland=island;IsTraining=training||chapterOne;IsChapterOne=chapterOne;
  IsBasin=basin;
  WidthCells=basin?BasinWidth:chapterOne?ChapterOneWidth:relay?240:training?Campaign.Width/Campaign.Tile:island?IslandColumns:Columns;
  HeightCells=basin?BasinHeight:chapterOne?ChapterOneHeight:relay?180:training?Campaign.Height/Campaign.Tile:island?IslandRows:Rows;
  Cells=new Cell[WidthCells*HeightCells];
  Starts=basin?BasinStarts():chapterOne?new[]{LandingBase,new Point(54,29)}:relay?new[]{RelayCell(15,82),RelayCell(84,42)}:training?new[]{new Point(6,21),new Point((int)(Campaign.EnemyHome.X/Campaign.Tile),(int)(Campaign.EnemyHome.Y/Campaign.Tile))}:island?new[]{new Point(36,30),IslandCell(320,31),IslandCell(325,104),IslandCell(316,209),IslandCell(191,213),IslandCell(163,105)}:new[]{WestStart,EastStart,SouthStart};
  if(basin){GenerateBasin();return;}
  if(chapterOne){GenerateChapterOne();return;}
  if(relay){GenerateRelay();return;}
  if(training){GenerateTraining(minerals);PrepareCoastline();return;}
  if(island){
   GenerateIsland();PrepareCoastline();
   // The shoreline is final only after generation. Its water cells cannot host ore.
   Ore.RemoveAll(point=>!Walkable(point.X,point.Y));
   return;
  }
  GenerateRiverbend();
  PrepareCoastline();
 }
 void GenerateRiverbend(){
  for(int y=0;y<HeightCells;y++)for(int x=0;x<WidthCells;x++){
   int n=Hash(x,y);var c=new Cell{Ground=Surface.Meadow,Variation=n};Cells[y*WidthCells+x]=c;
   float bank=RiverbendWaterDistance(x+.5f,y+.5f);
   if(bank<0){c.Ground=Surface.Water;continue;}
   if(bank<4)c.Ground=y>45?Surface.Marsh:Surface.Soil;
   if(Ellipse(x,y,31,13,9,7)<1){c.Height=1.2f;c.Ground=Surface.Rock;}
   bool grove=Ellipse(x,y,8,23,8,12)<1||Ellipse(x,y,32,53,11,7)<1||
    Ellipse(x,y,58,12,8,7)<1||Ellipse(x,y,85,35,9,7)<1;
   c.Tree=grove&&n%8<3&&c.Height==0;
   if(c.Ground==Surface.Meadow&&n%59==0)c.Ground=Surface.Soil;
  }
  foreach(var start in Starts){
   Paint(new Rectangle(start.X-10,start.Y-9,23,20),Surface.Meadow,0);
   Square(new Rectangle(start.X-4,start.Y-3,9,8));
  }

  const int bridgeTop=RiverRoadTop,bridgeWidth=RiverRoadWidth;
  var crossingRows=Enumerable.Range(bridgeTop,bridgeWidth).Select(y=>y+.5f);
  int left=(int)Math.Floor(crossingRows.Min(y=>RiverCenter(y)-RiverWidth(y)))-2;
  int right=(int)Math.Ceiling(crossingRows.Max(y=>RiverCenter(y)+RiverWidth(y)))+2;
  var bridge=Rectangle.FromLTRB(left,bridgeTop,right,bridgeTop+bridgeWidth);
  Road(0,bridgeTop,Columns,bridgeWidth);
  Protect(new Rectangle(Columns,bridgeTop,RiverExitX-Columns+5,bridgeWidth));
  Square(new Rectangle(19,31,10,8));
  Square(RiverImpound);
  Square(new Rectangle(63,40,10,7));
  Road(61,18,5,36);Road(62,18,10,5);Road(62,49,11,5);
  Bridges.Add(bridge);Paint(bridge,Surface.Bridge,0);
  for(int y=15;y<=22;y++)Paint(new Rectangle(29,y,4,1),Surface.Ramp,Math.Max(0,1.2f-(y-15)*.2f));

  AddHouse("shop","西岸杂货铺",28,24,3,2,6);
  AddHouse("residence","桥管值班室",RiverBridgeHouse.X,RiverBridgeHouse.Y,2,2,4);
  AddHouse("residence","西岸民居二",29,42,2,2,4);
  AddHouse("residence","西岸民居三",35,43,2,2,4);
  AddHouse("municipal","河湾镇公所",54,22,3,3,8);
  AddHouse("shop","东桥头货栈",55,41,3,2,6);
  AddHouse("residence","东岸民居一",68,26,2,2,4);
  AddHouse("residence","东岸民居二",73,41,2,2,4);
  AddHouse("shop","白石卸货场",68,43,3,2,6);

  var fields=new[]{new Point(15,42),new Point(26,49),new Point(80,20),
   new Point(68,5),new Point(81,54),new Point(66,62)};
  foreach(var site in fields){
   for(int y=site.Y;y<site.Y+4;y++)for(int x=site.X;x<site.X+6;x++){
    At(x,y).Tree=false;Ore.Add(new Point(x,y));
   }
  }
  Label("我方 · 西岸",WestStart.X,WestStart.Y,1);
  Label("北线驻防军",EastStart.X,EastStart.Y,2);
  Label("白石警卫队",SouthStart.X,SouthStart.Y,6);
  Label("河湾大桥",bridge.X+bridge.Width*.5f,bridge.Y+bridge.Height*.5f,5);
  Label("旧码头来路",-10,34,0);Label("桥管值班室",RiverBridgeHouse.X+1,RiverBridgeHouse.Y,0);Label("北线扣车坪",59,27,2);
  Label("白石卸货场",70,43,6);Label("往矿业盆地",80,34,0);
  Label("采石台地",31,12,3);Label("河滩",45,56,4);
 }
 void Label(string name,float x,float y,int style){Labels.Add(new Landmark{Name=name,Position=new PointF(x,y),Style=style});}
 void Paint(Rectangle r,Surface ground,float? height){for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++){var c=At(x,y);if(c==null)continue;c.Ground=ground;c.Tree=false;if(height.HasValue)c.Height=height.Value;}}
 void Road(int x,int y,int w,int h){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++){var c=At(xx,yy);if(c!=null&&c.Ground!=Surface.Water){c.Ground=Surface.Road;c.Tree=false;}}}
 void Square(Rectangle r){Paint(r,Surface.Square,0);}
 void Town(int x,int y,string name,bool square){
  Square(new Rectangle(x-1,y-1,18,11));Road(x-1,y+4,18,3);
  for(int row=0;row<2;row++)for(int col=0;col<3;col++)AddHouse(col==1&&square?"shop":"residence",name+(row*3+col+1),x+col*6,y+row*8,col==1&&square?3:2,2,col==1&&square?6:4);
 }
 void AddHouse(string key,string name,int x,int y,int w,int h,int slots){
  var lot=new Rectangle(x,y,w,h);float level=At(x,y).Height;
  for(int yy=y-1;yy<=y+h;yy++)for(int xx=x-1;xx<=x+w;xx++){var c=At(xx,yy);c.Tree=false;if(c.Ground!=Surface.Road){c.Ground=Surface.Square;c.Height=level;}}
  Houses.Add(new House{Kind=key,Name=name,Lot=lot,Capacity=slots});
 }
 public bool Walkable(int x,int y){var c=At(x,y);return c!=null&&c.Ground!=Surface.Water&&!c.Tree&&!c.Cliff&&!Houses.Any(h=>h.Lot.Contains(x,y));}
 public bool CanStep(Point a,Point b){return Walkable(b.X,b.Y)&&Walkable(a.X,a.Y)&&Math.Abs(At(a.X,a.Y).Height-At(b.X,b.Y).Height)<=.26f;}
 public bool Connected(Point a,Point b)=>Connected(a,b,null);
 internal bool Connected(Point a,Point b,Predicate<Point> blocked){
  if(!Walkable(a.X,a.Y)||!Walkable(b.X,b.Y)||blocked?.Invoke(a)==true||blocked?.Invoke(b)==true)return false;
  var seen=new HashSet<long>();var q=new Queue<Point>();q.Enqueue(a);seen.Add(CellKey(a.X,a.Y));
  while(q.Count>0){var p=q.Dequeue();if(p==b)return true;foreach(var d in new[]{new Point(1,0),new Point(-1,0),new Point(0,1),new Point(0,-1)}){var n=new Point(p.X+d.X,p.Y+d.Y);long key=CellKey(n.X,n.Y);if(seen.Contains(key)||blocked?.Invoke(n)==true||!CanStep(p,n))continue;seen.Add(key);q.Enqueue(n);}}
  return false;
 }
 public void Export(string path){
  if(IsRelay){ExportRelay(path);return;}if(IsIsland){ExportIsland(path);return;}
  var s=new StringBuilder("{\n  \"id\":\"riverbend-v02\",\"status\":\"playable\",\"columns\":");
  s.Append(WidthCells).Append(",\"rows\":").Append(HeightCells).Append(",\"cellWorldSize\":").Append(Campaign.Tile)
   .Append(",\n  \"surfaceLegend\":[\"meadow\",\"soil\",\"rock\",\"marsh\",\"water\",\"road\",\"bridge\",\"square\",\"ramp\",\"sand\"],\n  \"cells\":[\n");
  for(int i=0;i<Cells.Length;i++){var c=Cells[i];if(i>0)s.Append(',');if(i%WidthCells==0)s.Append('\n');s.Append('[').Append((int)c.Ground).Append(',').Append(c.Height.ToString("0.000",CultureInfo.InvariantCulture)).Append(',').Append(c.Tree?1:0).Append(']');}
  s.Append("\n],\"houses\":[\n");for(int i=0;i<Houses.Count;i++){var h=Houses[i];if(i>0)s.Append(",\n");s.Append("{\"kind\":\"").Append(h.Kind).Append("\",\"lot\":[").Append(h.Lot.X).Append(',').Append(h.Lot.Y).Append(',').Append(h.Lot.Width).Append(',').Append(h.Lot.Height).Append("],\"capacity\":").Append(h.Capacity).Append(",\"initialOwner\":null,\"engineerCapture\":true,\"evacuateOnDestruction\":true}");}
  s.Append("\n],\"bridges\":[");for(int i=0;i<Bridges.Count;i++){if(i>0)s.Append(',');var b=Bridges[i];s.Append('[').Append(b.X).Append(',').Append(b.Y).Append(',').Append(b.Width).Append(',').Append(b.Height).Append(']');}
  s.Append("],\"starts\":[");for(int i=0;i<Starts.Length;i++){if(i>0)s.Append(',');s.Append('[').Append(Starts[i].X).Append(',').Append(Starts[i].Y).Append(']');}
  s.Append("],\"ore\":[");for(int i=0;i<Ore.Count;i++){if(i>0)s.Append(',');s.Append('[').Append(Ore[i].X).Append(',').Append(Ore[i].Y).Append(']');}s.Append(']');AppendRadarTerrain(s);s.Append("\n}\n");File.WriteAllText(path,s.ToString(),new UTF8Encoding(false));
 }
}
}
