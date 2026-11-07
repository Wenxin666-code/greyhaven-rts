using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
namespace Greyhaven {
// Standalone authored map definition. The existing training world is unchanged.
// Preview rendering and connectivity checks consume these SAME cells and lots.
public sealed class RiverbendMap {
 public const int Columns=220,Rows=150;
 public enum Surface { Meadow,Soil,Rock,Marsh,Water,Road,Bridge,Square,Ramp }
 public sealed class Cell {public Surface Ground;public float Height;public bool Tree;public int Variation;}
 public sealed class House {
  public string Kind,Name;public Rectangle Lot;public int Capacity;
  public Point Door {get{return new Point(Lot.X+Lot.Width/2,Lot.Bottom);}}
 }
 public sealed class Landmark {public string Name;public PointF Position;public int Style;}
 public readonly Cell[] Cells=new Cell[Columns*Rows];
 public readonly List<House> Houses=new List<House>();
 public readonly List<Rectangle> Bridges=new List<Rectangle>();
 public readonly List<Landmark> Labels=new List<Landmark>();
 public readonly List<Point> Ore=new List<Point>();
 public readonly Point WestStart=new Point(23,114),EastStart=new Point(202,31),SouthStart=new Point(199,136);
 public Cell At(int x,int y){return x<0||y<0||x>=Columns||y>=Rows?null:Cells[y*Columns+x];}
 public static float RiverCenter(float y){return 111+(float)Math.Sin(y/19)*7+(float)Math.Sin(y/41)*5;}
 static float RiverWidth(float y){return y<112?5.4f:5.4f+(y-112)*.24f;}
 static double Ellipse(int x,int y,float cx,float cy,float rx,float ry){return Math.Pow((x-cx)/rx,2)+Math.Pow((y-cy)/ry,2);}
 static int Hash(int x,int y){unchecked{uint q=(uint)(x*73856093^y*19349663^71423);q^=q>>13;q*=1274126177;return (int)(q&0x7fffffff);}}
 public RiverbendMap(){
  for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++){
   int n=Hash(x,y);var c=new Cell{Ground=Surface.Meadow,Variation=n};Cells[y*Columns+x]=c;
   float bank=Math.Abs(x-RiverCenter(y))-RiverWidth(y);
   bool lake=Ellipse(x,y,32,24,21,13)<1,delta=y>130&&x>80&&x<155&&Ellipse(x,y,119,151,43,20)<1;
   if(bank<0||lake||delta){c.Ground=Surface.Water;continue;}
   if(bank<3.5||Ellipse(x,y,32,24,24,16)<1)c.Ground=Surface.Soil;
   if(Ellipse(x,y,78,129,20,11)<1||Ellipse(x,y,153,122,16,13)<1)c.Ground=Surface.Marsh;
   if(Ellipse(x,y,62,31,20,13)<1){c.Height=2;c.Ground=Surface.Rock;}
   if(Ellipse(x,y,175,75,24,20)<1){c.Height=2;c.Ground=Surface.Rock;}
   if(Ellipse(x,y,188,13,19,9)<1){c.Height=3;c.Ground=Surface.Rock;}
   if(c.Height>0&&Math.Sin(x*.21)+Math.Cos(y*.24)>.05)c.Ground=Surface.Meadow;
   bool forest=Ellipse(x,y,54,65,24,14)<1||Ellipse(x,y,174,115,26,13)<1||Ellipse(x,y,153,23,19,14)<1||Ellipse(x,y,38,133,22,9)<1;
   c.Tree=forest&&n%7<3&&c.Height==0;
   if(c.Ground==Surface.Meadow&&n%59==0)c.Ground=Surface.Soil;
  }
  // Broad junctions and approaches; 3 distinct crossings, no one-cell choke.
  Road(24,103,65,6);Road(30,75,150,5);Road(72,31,5,96);Road(72,119,122,5);
  Road(72,31,122,5);Road(188,31,5,93);Road(145,31,5,93);Road(30,77,5,32);
  Road(138,72,43,5);Road(78,104,5,20);Road(169,77,5,45);
  foreach(int y in new[]{31,75,119}){
   int left=(int)Math.Floor(Enumerable.Range(y,y==75?5:5).Min(v=>RiverCenter(v)-RiverWidth(v)))-3;
   int right=(int)Math.Ceiling(Enumerable.Range(y,5).Max(v=>RiverCenter(v)+RiverWidth(v)))+4;
   var bridge=new Rectangle(left,y,right-left,5);Bridges.Add(bridge);
   Paint(bridge,Surface.Bridge,0);
  }
  // Ramps are actual traversable height gradients, not cliffs painted as roads.
  for(int y=29;y<35;y++)for(int x=68;x<83;x++){var c=At(x,y);c.Ground=Surface.Ramp;c.Height=Math.Max(0,Math.Min(2,(81-x)/5f));c.Tree=false;}
  for(int y=72;y<77;y++)for(int x=148;x<162;x++){var c=At(x,y);c.Ground=Surface.Ramp;c.Height=Math.Max(0,Math.Min(2,(x-150)/5f));c.Tree=false;}
  Square(new Rectangle(20,96,27,24));Road(24,103,65,6);Road(30,77,5,32);
  Square(new Rectangle(180,18,29,30));Road(188,31,5,110);
  Square(new Rectangle(181,122,33,26));Road(188,119,5,24);
  Town(77,63,"西岸老街",true);Town(128,83,"东岸社区",false);
  Town(128,18,"北桥驿站",false);Town(88,107,"南岸村落",false);
  AddHouse("municipal","河湾镇公所",94,67,3,3,8);
  AddHouse("shop","桥头商店",123,71,3,2,6);
  AddHouse("residence","高地民居",165,69,2,2,4);
  AddHouse("residence","高地民居",172,69,2,2,4);
  // Resource yards stay outside the roads, houses and protected exit rings.
  foreach(var p in new[]{new Point(13,114),new Point(49,112),new Point(74,40),new Point(207,23),new Point(210,45),new Point(161,88),new Point(89,91),new Point(151,109),new Point(208,135),new Point(181,142)}){
   for(int yy=p.Y;yy<p.Y+5;yy++)for(int xx=p.X;xx<p.X+8;xx++){var c=At(xx,yy);if(c!=null&&c.Ground!=Surface.Water&&c.Ground!=Surface.Road&&c.Ground!=Surface.Bridge&&c.Ground!=Surface.Ramp){c.Tree=false;Ore.Add(new Point(xx,yy));}}
  }
  // Every road crossing a cliff gets a graded approach, including secondary
  // roads; no road texture is allowed to end in an invisible vertical wall.
  var heights=Cells.Select(c=>c.Height).ToArray();
  for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++){
   var c=At(x,y);if(c.Ground!=Surface.Road&&c.Ground!=Surface.Ramp)continue;
   float sum=0;int count=0;for(int yy=Math.Max(0,y-6);yy<=Math.Min(Rows-1,y+6);yy++)for(int xx=Math.Max(0,x-6);xx<=Math.Min(Columns-1,x+6);xx++){sum+=heights[yy*Columns+xx];count++;}
   c.Height=sum/count;if(c.Height>.01f&&c.Height<1.99f)c.Ground=Surface.Ramp;
  }
  foreach(var b in Bridges)for(int y=b.Top;y<b.Bottom;y++)for(int x=b.Left;x<b.Right;x++)At(x,y).Height=x==b.Left||x==b.Right-1?.2f:.4f;
  Label("我方 · 西南",23,114,1);Label("敌方一 · 东北",202,31,2);Label("敌方二 · 东南",199,136,6);
  Label("西岸老街",85,65,0);Label("东岸社区",135,88,0);Label("北桥驿站",136,20,0);Label("南岸村落",95,112,0);
  Label("采石高地",62,28,3);Label("东部台地",176,80,3);Label("镜湖",31,22,4);Label("河口湿地",117,143,4);
  Label("北桥",117,33,5);Label("中央桥",109,77,5);Label("南桥",109,121,5);
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
 public bool Walkable(int x,int y){var c=At(x,y);return c!=null&&c.Ground!=Surface.Water&&!c.Tree&&!Houses.Any(h=>h.Lot.Contains(x,y));}
 public bool CanStep(Point a,Point b){return Walkable(b.X,b.Y)&&Walkable(a.X,a.Y)&&Math.Abs(At(a.X,a.Y).Height-At(b.X,b.Y).Height)<=.26f;}
 public bool Connected(Point a,Point b){
  if(!Walkable(a.X,a.Y)||!Walkable(b.X,b.Y))return false;
  var seen=new bool[Cells.Length];var q=new Queue<Point>();q.Enqueue(a);seen[a.Y*Columns+a.X]=true;
  while(q.Count>0){var p=q.Dequeue();if(p==b)return true;foreach(var d in new[]{new Point(1,0),new Point(-1,0),new Point(0,1),new Point(0,-1)}){var n=new Point(p.X+d.X,p.Y+d.Y);if(n.X<0||n.Y<0||n.X>=Columns||n.Y>=Rows||seen[n.Y*Columns+n.X]||!CanStep(p,n))continue;seen[n.Y*Columns+n.X]=true;q.Enqueue(n);}}
  return false;
 }
 public void Export(string path){
  var s=new StringBuilder("{\n  \"id\":\"riverbend-v01\",\"status\":\"playable\",\"columns\":220,\"rows\":150,\"cellWorldSize\":24,\n  \"surfaceLegend\":[\"meadow\",\"soil\",\"rock\",\"marsh\",\"water\",\"road\",\"bridge\",\"square\",\"ramp\"],\n  \"cells\":[\n");
  for(int i=0;i<Cells.Length;i++){var c=Cells[i];if(i>0)s.Append(',');if(i%Columns==0)s.Append('\n');s.Append('[').Append((int)c.Ground).Append(',').Append(c.Height.ToString("0.000",CultureInfo.InvariantCulture)).Append(',').Append(c.Tree?1:0).Append(']');}
  s.Append("\n],\"houses\":[\n");for(int i=0;i<Houses.Count;i++){var h=Houses[i];if(i>0)s.Append(",\n");s.Append("{\"kind\":\"").Append(h.Kind).Append("\",\"lot\":[").Append(h.Lot.X).Append(',').Append(h.Lot.Y).Append(',').Append(h.Lot.Width).Append(',').Append(h.Lot.Height).Append("],\"capacity\":").Append(h.Capacity).Append(",\"initialOwner\":null,\"engineerCapture\":true,\"evacuateOnDestruction\":true}");}
  s.Append("\n],\"bridges\":[");for(int i=0;i<Bridges.Count;i++){if(i>0)s.Append(',');var b=Bridges[i];s.Append('[').Append(b.X).Append(',').Append(b.Y).Append(',').Append(b.Width).Append(',').Append(b.Height).Append(']');}
  s.Append("],\"starts\":[[23,114],[202,31],[199,136]],\"ore\":[");for(int i=0;i<Ore.Count;i++){if(i>0)s.Append(',');s.Append('[').Append(Ore[i].X).Append(',').Append(Ore[i].Y).Append(']');}s.Append("]\n}\n");File.WriteAllText(path,s.ToString(),new UTF8Encoding(false));
 }
}
}
