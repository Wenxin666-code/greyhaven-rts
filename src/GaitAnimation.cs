using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Greyhaven {partial class CommandWindow {
 class FootMotion {internal float Start=-10,LastMoving=-10,Amount;internal int Frame;}
 Campaign gaitWorld;readonly Dictionary<int,FootMotion> footMotion=new Dictionary<int,FootMotion>();
 static RectangleF GaitBox(PointF p,float size){float w=size*.54f*3/2.7f;return new RectangleF(p.X-w/2,p.Y-w*.91f,w,w);}
 readonly LinkedList<string> gaitRasterOrder=new LinkedList<string>();readonly Dictionary<string,LinkedListNode<string>> gaitRasterNodes=new Dictionary<string,LinkedListNode<string>>();
 string GaitRaster(string id,Func<Bitmap> create){
  LinkedListNode<string> node;if(gaitRasterNodes.TryGetValue(id,out node)){gaitRasterOrder.Remove(node);gaitRasterOrder.AddLast(node);return id;}
  Bitmap result=create();while(gaitRasterOrder.Count>=128){string old=gaitRasterOrder.First.Value;gaitRasterOrder.RemoveFirst();gaitRasterNodes.Remove(old);painted[old].Dispose();painted.Remove(old);}
  painted[id]=result;gaitRasterNodes[id]=gaitRasterOrder.AddLast(id);return id;
 }
 void DrawGaitBlend(Graphics g,Piece o,PointF p,float size,int view,int frame,float amount){
  var full=GaitBox(p,size);var crop=GaitV09.Crop(o.T.Key,view);var color=UnitColor(o.Side);int level=Math.Max(0,Math.Min(8,(int)Math.Round(amount*8))),width=Math.Max(1,(int)Math.Round(full.Width*PixelScale));
  string suffix=o.T.Key+"/"+view+"/"+color.ToArgb()+"/"+width,walk="gait/"+o.T.Key+"/"+view+"/"+frame;
  string stand=GaitRaster("gait-raster/stand/"+suffix,()=>{var b=new Bitmap(width,width,PixelFormat.Format32bppPArgb);using(var target=Graphics.FromImage(b)){float w=width*2.7f/3;DrawLayeredSource(target,o.T.Key,SourceCell(o.T,0,view*2),new RectangleF((width-w)/2,(width-w)*.91f,w,w),color);}return b;});
  string step=GaitRaster("gait-raster/walk/"+suffix+"/"+frame,()=>{var b=new Bitmap(width,width,PixelFormat.Format32bppPArgb);using(var target=Graphics.FromImage(b))DrawLayeredSource(target,walk,new Rectangle(Point.Empty,crop.Size),new RectangleF(width*crop.X/(float)GaitV09.Cell,width*crop.Y/(float)GaitV09.Cell,width*crop.Width/(float)GaitV09.Cell,width*crop.Height/(float)GaitV09.Cell),color);return b;});
  string blended=GaitRaster("gait-raster/blend/"+suffix+"/"+frame+"/"+level,()=>{
   var a=painted[stand];var b=painted[step];var result=new Bitmap(width,width,PixelFormat.Format32bppPArgb);var rect=new Rectangle(0,0,width,width);var ab=a.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);var bb=b.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);var rb=result.LockBits(rect,ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);var x=new byte[width*4];var y=new byte[width*4];
   try{for(int row=0;row<width;row++){Marshal.Copy(IntPtr.Add(ab.Scan0,row*ab.Stride),x,0,x.Length);Marshal.Copy(IntPtr.Add(bb.Scan0,row*bb.Stride),y,0,y.Length);for(int i=0;i<x.Length;i++)x[i]=(byte)((x[i]*(8-level)+y[i]*level+4)/8);Marshal.Copy(x,0,IntPtr.Add(rb.Scan0,row*rb.Stride),x.Length);}}finally{a.UnlockBits(ab);b.UnlockBits(bb);result.UnlockBits(rb);}return result;
  });DrawStamp(g,blended,new Rectangle(0,0,width,width),full);
 }
 void DrawFootGait(Graphics g,Piece o,PointF p,float size){
  if(gaitWorld!=world){gaitWorld=world;footMotion.Clear();}
  FootMotion motion;if(!footMotion.TryGetValue(o.Id,out motion)){if(footMotion.Count>2048)footMotion.Clear();footMotion[o.Id]=motion=new FootMotion();}
  float now=world.Time;bool moving=now-o.MovedAt<.065f;
  if(moving){if(now-motion.LastMoving>.12f)motion.Start=now;motion.LastMoving=now;motion.Frame=(int)(o.Travel/24f*GaitV09.Frames)%GaitV09.Frames;}
  float amount=moving?Smooth((now-motion.Start)/.12f):1-Smooth((now-motion.LastMoving-.065f)/.12f);motion.Amount=amount;
  int dir=(o.Dir+14)%16;
  if(amount<=.001f){
   if(now-o.CheerAt<1.6f){var state=g.Save();g.TranslateTransform(p.X,p.Y);g.RotateTransform((float)Math.Sin((now-o.CheerAt)*17)*7);g.TranslateTransform(-p.X,-p.Y);DrawStamp(g,o.T.Key,SourceCell(o.T,0,dir),SoldierBox(p,size),UnitColor(o.Side));g.Restore(state);}
   else DrawStamp(g,o.T.Key,SourceCell(o.T,0,dir),SoldierBox(p,size),UnitColor(o.Side));return;
  }
  if(amount<.999f){DrawGaitBlend(g,o,p,size,dir/2,motion.Frame,amount);return;}
  int view=dir/2;var crop=GaitV09.Crop(o.T.Key,view);var full=GaitBox(p,size);var box=new RectangleF(full.X+full.Width*crop.X/GaitV09.Cell,full.Y+full.Height*crop.Y/GaitV09.Cell,full.Width*crop.Width/GaitV09.Cell,full.Height*crop.Height/GaitV09.Cell);string key="gait/"+o.T.Key+"/"+view+"/"+motion.Frame;DrawStamp(g,key,new Rectangle(Point.Empty,crop.Size),box,UnitColor(o.Side));
 }
}}
