using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
namespace Greyhaven {
public sealed partial class RiverbendMap {
 // Keep the island and its one bridge intact. Compress mainland distances,
 // never unit footprints, road lane widths, building lots or base clearings.
 static int IslandX(int x){return x<=112?x:112+(int)Math.Round((x-112)*188/248.0);}
 static int IslandY(int y){return y<=94?y:94+(int)Math.Round((y-94)*106/146.0);}
 static Point IslandCell(int x,int y){return new Point(IslandX(x),IslandY(y));}
 static float IslandAuthX(float x){return x<=112?x:112+(x-112)*248/188;}
 static float IslandAuthY(float y){return y<=94?y:94+(y-94)*146/106;}
 void IslandTown(int x,int y,string name,bool paved){Town(IslandX(x),IslandY(y),name,paved);}
 void IslandLabel(string name,int x,int y,int style){Label(name,IslandX(x),IslandY(y),style);}
 public readonly Rectangle IslandGate=new Rectangle(80,48,36,6);
 void GenerateIsland(){
  for(int y=0;y<HeightCells;y++)for(int x=0;x<WidthCells;x++){
   int n=Hash(x+941,y+613);var c=new Cell{Ground=Surface.Meadow,Variation=n};Cells[y*WidthCells+x]=c;
   int u=(int)IslandAuthX(x),v=(int)IslandAuthY(y);
   bool sea=u<112&&v<94;
   bool island=Ellipse(u,v,44,40,41,33)<1+.028*Math.Sin(v*.43);
   float channel=Math.Abs(u-(232+11*(float)Math.Sin(v/27f)));
   bool river=v>12&&channel<4.5f+(v>193?(v-193)*.18f:0);
   bool lake=Ellipse(u,v,171,158,24,14)<1||Ellipse(u,v,286,65,18,12)<1||Ellipse(u,v,132,202,18,11)<1;
   if(sea&&!island||!sea&&(river||lake)){c.Ground=Surface.Water;continue;}
   if(sea){if(Ellipse(u,v,44,40,38,30)>.9)c.Ground=Surface.Soil;continue;}
   if(channel<10||Ellipse(u,v,203,185,30,19)<1)c.Ground=Surface.Marsh;
   if(Ellipse(u,v,160,52,27,19)<1||Ellipse(u,v,284,151,30,21)<1){c.Height=2;c.Ground=n%4==0?Surface.Rock:Surface.Meadow;}
   if(Ellipse(u,v,107,143,17,23)<1){c.Height=1;c.Ground=Surface.Rock;}
   bool forest=Ellipse(u,v,143,112,23,17)<1||Ellipse(u,v,274,25,26,12)<1||Ellipse(u,v,301,179,27,15)<1||Ellipse(u,v,77,151,28,18)<1||Ellipse(u,v,198,82,16,18)<1;
   c.Tree=forest&&n%11<4&&c.Height==0;
   if(c.Ground==Surface.Meadow&&n%43==0)c.Ground=Surface.Soil;
  }
  IslandRoad(112,48,83,6);IslandRoad(186,48,6,171);IslandRoad(160,108,174,6);
  IslandRoad(249,33,85,6);IslandRoad(316,33,6,184);IslandRoad(185,207,144,6);
  IslandRoad(186,33,66,6);IslandRoad(160,105,6,12);IslandRoad(143,110,6,53);
  foreach(int y in new[]{33,108,207}){
   int cx=(int)(232+11*Math.Sin((y+3)/27f));var b=new Rectangle(IslandX(cx-19),IslandY(y),IslandX(cx+20)-IslandX(cx-19),6);Bridges.Add(b);Paint(b,Surface.Bridge,0);
  }
  // The only traversable island/mainland connection. No sea cells elsewhere
  // are overwritten by roads, houses, resource clearings or spawn yards.
  Bridges.Insert(0,IslandGate);Paint(IslandGate,Surface.Bridge,0);
  IslandRoad(34,31,6,23);IslandRoad(35,48,46,6);
  foreach(var start in Starts)Square(new Rectangle(start.X-12,start.Y-10,27,23));
  IslandRamp(132,48,true,0,2);IslandRamp(162,66,false,2,0);
  IslandRamp(252,147,true,0,2);IslandRamp(283,169,false,2,0);
  IslandRamp(87,140,true,0,1);
  IslandTown(121,119,"西岭村",true);IslandTown(194,40,"北部驿站",false);
  IslandTown(259,112,"东岸街区",true);IslandTown(263,197,"南部小镇",false);
  IslandTown(173,181,"湿地聚落",false);
  AddHouse("municipal","大陆行政中心",IslandX(193),IslandY(112),3,3,8);
  AddHouse("shop","堤桥补给站",IslandX(119),IslandY(56),3,2,6);
  AddHouse("residence","岛上旧屋",IslandX(62),IslandY(33),2,2,4);
  IslandOre(14,42,10,7);IslandOre(46,22,10,7);IslandOre(55,56,9,6);
  for(int i=1;i<Starts.Length;i++){
   var s=Starts[i];IslandOre(s.X-10,s.Y+7,9,6);IslandOre(s.X+9,s.Y-7,8,6);
  }
  foreach(var p in new[]{new Point(119,83),new Point(174,60),new Point(209,142),new Point(267,157),new Point(154,216),new Point(304,94)})IslandOre(IslandX(p.X),IslandY(p.Y),9,6);
  for(int i=0;i<Starts.Length;i++)Label(i==0?"我方 · 西北资源岛":"敌方 "+i,Starts[i].X,Starts[i].Y,i==0?1:i+6);
  IslandLabel("唯一陆路 · 堤桥",98,51,5);IslandLabel("北部高地",159,49,3);IslandLabel("东部台地",285,150,3);
  IslandLabel("中央河道",234,136,4);IslandLabel("镜泊湖",286,65,4);IslandLabel("南部湿地",206,185,4);
  IslandLabel("岛内矿区",22,47,0);IslandLabel("大陆行政中心",196,115,0);
 }
 void IslandRoad(int x,int y,int w,int h){
  w=w<=6?w:IslandX(x+w)-IslandX(x);h=h<=6?h:IslandY(y+h)-IslandY(y);x=IslandX(x);y=IslandY(y);
  for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++){var c=At(xx,yy);if(c==null||c.Ground==Surface.Water)continue;c.Ground=Surface.Road;c.Height=0;c.Tree=false;}
 }
 void IslandRamp(int x,int y,bool horizontal,float from,float to){
  x=IslandX(x);y=IslandY(y);const int length=14;
  for(int i=0;i<length;i++)for(int j=0;j<6;j++){var c=At(x+(horizontal?i:j),y+(horizontal?j:i));if(c==null||c.Ground==Surface.Water)continue;c.Ground=Surface.Ramp;c.Tree=false;c.Height=from+(to-from)*i/(length-1);}
 }
 void IslandOre(int x,int y,int w,int h){
  for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++){
   var c=At(xx,yy);if(c==null||c.Ground==Surface.Water||c.Ground==Surface.Bridge||c.Ground==Surface.Road||c.Ground==Surface.Ramp||Houses.Any(a=>a.Lot.Contains(xx,yy)))continue;
   if(Starts.Any(s=>Math.Abs(s.X-xx)<6&&Math.Abs(s.Y-yy)<5))continue;c.Tree=false;Ore.Add(new Point(xx,yy));
  }
 }
 void ExportIsland(string path){
  var s=new StringBuilder("{\"id\":\"island-fortress-v02\",\"status\":\"playable\",\"columns\":"+WidthCells+",\"rows\":"+HeightCells+",\"cellWorldSize\":24,\"mutuallyHostileFactions\":6,\"singleIslandGate\":[80,48,36,6],\"starts\":[");
  for(int i=0;i<Starts.Length;i++){if(i>0)s.Append(',');s.Append('[').Append(Starts[i].X).Append(',').Append(Starts[i].Y).Append(']');}
  s.Append("],\"cells\":[");for(int i=0;i<Cells.Length;i++){if(i>0)s.Append(',');var c=Cells[i];s.Append('[').Append((int)c.Ground).Append(',').Append(c.Height.ToString("0.000",CultureInfo.InvariantCulture)).Append(',').Append(c.Tree?1:0).Append(']');}
  s.Append("],\"ore\":[");for(int i=0;i<Ore.Count;i++){if(i>0)s.Append(',');s.Append('[').Append(Ore[i].X).Append(',').Append(Ore[i].Y).Append(']');}
  s.Append("],\"houses\":[");for(int i=0;i<Houses.Count;i++){if(i>0)s.Append(',');var h=Houses[i];s.Append("{\"kind\":\"").Append(h.Kind).Append("\",\"lot\":[").Append(h.Lot.X).Append(',').Append(h.Lot.Y).Append(',').Append(h.Lot.Width).Append(',').Append(h.Lot.Height).Append("],\"initialOwner\":null}");}
  s.Append(']');AppendRadarTerrain(s);s.Append('}');File.WriteAllText(path,s.ToString(),new UTF8Encoding(false));
 }
}
}
