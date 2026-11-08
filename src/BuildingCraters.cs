using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
namespace Greyhaven {partial class CommandWindow {
 sealed class CraterMark {internal PointF Position;internal int Seed;}
 Campaign craterWorld;readonly Dictionary<int,CraterMark> buildingCraters=new Dictionary<int,CraterMark>();readonly Queue<int> craterOrder=new Queue<int>();
 string CraterImage(int variant){
  string key="crater/v10/"+variant;if(painted.ContainsKey(key))return key;var image=new Bitmap(128,64,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(image)){
   g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;var points=new PointF[18];
   for(int i=0;i<points.Length;i++){float a=i*(float)Math.PI*2/points.Length,r=.84f+Noise(i*47+variant*29)*.16f;points[i]=new PointF(64+(float)Math.Cos(a)*49*r,34+(float)Math.Sin(a)*21*r);}
   using(var brush=new SolidBrush(Color.FromArgb(42,44,38,28)))g.FillPolygon(brush,points);
   for(int i=0;i<points.Length;i++){points[i].X=64+(points[i].X-64)*.86f;points[i].Y=33+(points[i].Y-34)*.74f;}
   using(var brush=new SolidBrush(Color.FromArgb(104,51,45,35)))g.FillPolygon(brush,points);
   for(int i=0;i<points.Length;i++){points[i].X=65+(points[i].X-64)*.66f;points[i].Y=31+(points[i].Y-33)*.55f;}
   using(var brush=new SolidBrush(Color.FromArgb(64,32,31,27)))g.FillPolygon(brush,points);
   using(var light=new Pen(Color.FromArgb(98,161,146,113),1.2f))for(int i=9;i<17;i+=2){float a=i*(float)Math.PI/9;float x=64+(float)Math.Cos(a)*39,y=33+(float)Math.Sin(a)*14;g.DrawLine(light,x,y,x+5,y-1);}
   for(int i=0;i<17;i++){float a=i*2.399f,r=30+Noise(i*31+variant)*20,x=64+(float)Math.Cos(a)*r,y=34+(float)Math.Sin(a)*r*.43f;using(var brush=new SolidBrush(Color.FromArgb(70,105,97,76)))g.FillRectangle(brush,x,y,1+Noise(i*17)*2,1);}
  }painted.Add(key,image);return key;
 }
 void DrawCrater(Graphics g,PointF point,float z,int seed){
  float width=(34+Noise(seed)*9)*z;var rect=new RectangleF(point.X-width*.5f,point.Y-width*.25f,width,width*.5f);if(!g.IsVisible(rect))return;DrawStamp(g,CraterImage((seed&0x7fffffff)%5),new Rectangle(0,0,128,64),rect);
 }
 void DrawBuildingScars(Graphics g){
  if(craterWorld!=world){craterWorld=world;buildingCraters.Clear();craterOrder.Clear();}
  foreach(var flying in world.Units)DrawBlastGroundShadows(g,flying);
  foreach(var o in world.Units)if(!o.Alive&&o.T.Building&&o.Dead>=TerminalWreckAt&&world.Visible(o)&&!buildingCraters.ContainsKey(o.Id)){
   while(buildingCraters.Count>=256&&craterOrder.Count>0){buildingCraters.Remove(craterOrder.Dequeue());}
   buildingCraters.Add(o.Id,new CraterMark{Position=o.P,Seed=o.Id});craterOrder.Enqueue(o.Id);
  }
  foreach(var mark in buildingCraters.Values)if(world.Explored(0,mark.Position))DrawCrater(g,ScreenOf(mark.Position),zoom,mark.Seed);
 }
 void PreviewCrater(Graphics g,string key,RectangleF box){
  var spec=BuildingV08.Specs[key];float z=box.Width*LotSpan/(FrameSize*(spec.Width+spec.Depth)*Campaign.Tile*Axis);DrawCrater(g,SocketPoint(box,new PointF(160,220)),z,key.Length*37);
 }
}}
