using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Greyhaven {
// The embedded indexed stream keeps the native build independent of loose files.
// Decode only frames currently needed by the viewport; never allocate full atlases.
internal sealed class IndexedSpritePack {
 readonly string resource;readonly Dictionary<string,Tuple<long,int>> index=new Dictionary<string,Tuple<long,int>>();readonly long start;
 Stream Open(){return typeof(IndexedSpritePack).Assembly.GetManifestResourceStream("Greyhaven.Art."+resource);}
 internal IndexedSpritePack(string name){resource=name;using(var stream=Open())using(var reader=new BinaryReader(stream)){
  if(System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8))!="GHBLD008")throw new InvalidDataException(resource);
  int count=reader.ReadInt32();if(count<1||count>20000)throw new InvalidDataException(resource);
  for(int i=0;i<count;i++){int length=reader.ReadUInt16();string key=System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length));long offset=reader.ReadInt64();int bytes=reader.ReadInt32();if(offset<0||bytes<0)throw new InvalidDataException(key);index.Add(key,Tuple.Create(offset,bytes));}
  start=stream.Position;foreach(var item in index.Values)if(start+item.Item1+item.Item2>stream.Length)throw new InvalidDataException("Truncated "+resource);
 }}
 internal bool Has(string name){return index.ContainsKey(name);}
 internal Bitmap Read(string name){var item=index[name];using(var stream=Open()){
  stream.Position=start+item.Item1;var data=new byte[item.Item2];int read=0;while(read<data.Length){int got=stream.Read(data,read,data.Length-read);if(got==0)throw new EndOfStreamException(name);read+=got;}
  using(var memory=new MemoryStream(data,false))using(var png=new Bitmap(memory)){var result=new Bitmap(png.Width,png.Height,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(result)){g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.DrawImageUnscaled(png,0,0);}return result;}
 }}
}
// Stable indexed format; V10 structural transitions overlay unchanged V09 idle art.
internal static partial class BuildingV08 {
 internal const int Frames=72,PartialFrames=48,IdleFrames=96,IdleFps=24,DoorFrames=16,DeployFrames=96;
 static readonly IndexedSpritePack buildings=new IndexedSpritePack("buildings-v09.pak"),structures=new IndexedSpritePack("structures-v10.pak"),walking=new IndexedSpritePack("gait-v09.pak");
 internal static bool Has(string name){return structures.Has(name)||buildings.Has(name)||name.StartsWith("gait/",StringComparison.Ordinal)&&walking.Has(name);}
 internal static Bitmap Read(string name){return name.StartsWith("gait/",StringComparison.Ordinal)?walking.Read(name):structures.Has(name)?structures.Read(name):buildings.Read(name);}
 internal static bool IsBuilding(string key){return Specs.ContainsKey(key);}
 internal static bool Contribution(string key){return IsBuilding(key)||key=="debris-v11"||key.StartsWith("b08/",StringComparison.Ordinal)||key.StartsWith("gait/",StringComparison.Ordinal);}
}
partial class CommandWindow {
 const long BuildingFrameBudget=64L*1024*1024;
 long buildingFrameBytes;
 readonly LinkedList<string> buildingFrames=new LinkedList<string>();
 readonly Dictionary<string,LinkedListNode<string>> buildingFrameNodes=new Dictionary<string,LinkedListNode<string>>();
 string EnsureB08(string key){
  LinkedListNode<string> node;
  if(buildingFrameNodes.TryGetValue(key,out node)){buildingFrames.Remove(node);buildingFrames.AddLast(node);return key;}
  Bitmap body=BuildingV08.Read(key),paint=null;
  try{paint=BuildingV08.Read(key+"-paint");}catch{body.Dispose();throw;}
  long bytes=4L*(body.Width*body.Height+paint.Width*paint.Height);
  while(buildingFrameBytes+bytes>BuildingFrameBudget&&buildingFrames.Count>0){string old=buildingFrames.First.Value;buildingFrames.RemoveFirst();buildingFrameNodes.Remove(old);foreach(var name in new[]{old,old+"-paint"}){var image=painted[name];buildingFrameBytes-=4L*image.Width*image.Height;painted.Remove(name);image.Dispose();}}
  painted.Add(key,body);painted.Add(key+"-paint",paint);buildingFrameBytes+=bytes;buildingFrameNodes.Add(key,buildingFrames.AddLast(key));return key;
 }
 string B08Frame(string key,string action,int frame){return EnsureB08("b08/"+key+"/"+action+"/"+frame);}
 static Rectangle B08Source(){return new Rectangle(0,0,FrameSize,FrameSize);}
 static RectangleF B08CropBox(RectangleF box,Rectangle crop){return new RectangleF(box.X+box.Width*crop.X/FrameSize,box.Y+box.Height*crop.Y/FrameSize,box.Width*crop.Width/FrameSize,box.Height*crop.Height/FrameSize);}
 void DrawB08Moving(Graphics g,string key,string kind,int frame,Rectangle crop,RectangleF box,Color color){
  var target=B08CropBox(box,crop);if(!g.IsVisible(target))return;
  var name="b08/"+key+"/"+kind+"/"+frame;DrawStamp(g,name,new Rectangle(0,0,crop.Width,crop.Height),target,color);
 }
 void DrawB08Idle(Graphics g,string key,RectangleF box,Color color,float time,float door,int damage=0){
  if(!g.IsVisible(box))return;var spec=BuildingV08.Specs[key];
  DrawStamp(g,"b08/"+key+(damage>0?"/damage-"+damage+"-background":"/background"),B08Source(),box,color);
  DrawB08Moving(g,key,"idle",((int)(Math.Max(0,time)*BuildingV08.IdleFps))%BuildingV08.IdleFrames,spec.Idle,box,color);
  if(key=="factory")DrawB08Moving(g,key,damage>0?"damage-door-"+damage:"door",Math.Max(0,Math.Min(BuildingV08.DoorFrames-1,(int)Math.Round(door*(BuildingV08.DoorFrames-1)))),spec.Door,box,color);
 }
 void DrawB08Field(Graphics g,Piece o,PointF p,float size){
  var box=BuildingRect(Campaign.Footprint(o.T,o.P),p,size/o.T.Size);if(!g.IsVisible(box))return;
  if(!o.Alive||o.Ready<1||o.Packing>0){DrawBuildingState(g,o,p,size);return;}
  float open=Math.Min(1,o.Door*3);if(o.Queue.Count>0)open=Math.Max(open,Math.Max(0,1-(o.Queue.Peek().Seconds-o.Work)/.6f));open=open*open*(3-2*open);
  DrawB08Idle(g,o.T.Key,box,UnitColor(o.Side),FacilityPhase(o),open,BuildingDamageLevel(o));
 }
 // Exact premultiplied contribution replacement: B - P + tint(P).
 // Geometry alpha is copied once. Neutral surfaces and worn edges stay intact.
 static Bitmap BuildingTint(Bitmap body,Bitmap paint,Rectangle source,Color color){
  var result=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppPArgb);
  var a=body.LockBits(source,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);BitmapData b=null,c=null;
  try{
   b=paint.LockBits(source,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);c=result.LockBits(new Rectangle(Point.Empty,result.Size),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
   int width=source.Width*4;var row=new byte[width];var mask=new byte[width];int[] rgb={color.B,color.G,color.R};
   for(int y=0;y<source.Height;y++){
    Marshal.Copy(IntPtr.Add(a.Scan0,y*a.Stride),row,0,width);Marshal.Copy(IntPtr.Add(b.Scan0,y*b.Stride),mask,0,width);
    for(int x=0;x<width;x+=4)for(int channel=0;channel<3;channel++)row[x+channel]=(byte)Math.Max(0,Math.Min(row[x+3],row[x+channel]-(mask[x+channel]*(255-rgb[channel])+127)/255));
    Marshal.Copy(row,0,IntPtr.Add(c.Scan0,y*c.Stride),width);
   }
  }finally{body.UnlockBits(a);if(b!=null)paint.UnlockBits(b);if(c!=null)result.UnlockBits(c);}
  return result;
 }
}
}
