using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Greyhaven {partial class CommandWindow {
 internal const int CloudCellV11=192,CloudFramesV11=96;
 readonly Queue<int> cloudFrameOrder=new Queue<int>();
 Bitmap EmbeddedBlast(string name){using(var stream=typeof(CommandWindow).Assembly.GetManifestResourceStream("Greyhaven.Art."+name+".png")){if(stream==null)return null;using(var source=new Bitmap(stream)){var copy=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(copy)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(source,0,0);}return copy;}}}
 void EnsureBlastArt(){
  if(painted.ContainsKey("cloud-v11"))return;bool bake=Environment.GetEnvironmentVariable("GREYHAVEN_REBAKE_V11")=="1";
  var cloud=bake?null:EmbeddedBlast("blast-cloud-v11");if(cloud==null){using(var source=EmbeddedBlast("blast-cloud-source-v11")){if(source==null)throw new InvalidDataException("Missing authored blast source");cloud=ResampleBlastCloud(source);}}
  painted.Add("cloud-v11",cloud);var body=bake?null:EmbeddedBlast("debris-base-v11");var mask=bake?null:EmbeddedBlast("debris-paint-v11");
  if(body==null||mask==null){if(body!=null)body.Dispose();if(mask!=null)mask.Dispose();BuildDebrisAtlas(out body,out mask);}painted.Add("debris-v11",body);painted.Add("debris-v11-paint",mask);
 }
 static byte[] BlastPixels(Bitmap image){var rect=new Rectangle(Point.Empty,image.Size);var data=image.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);var raw=new byte[image.Width*image.Height*4];try{for(int y=0;y<image.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),raw,y*image.Width*4,image.Width*4);}finally{image.UnlockBits(data);}return raw;}
 static Bitmap ResampleBlastCloud(Bitmap source){
  var frames=new List<byte[]>();int sw=source.Width/6,sh=source.Height/4;
  for(int i=0;i<24;i++)using(var cell=new Bitmap(CloudCellV11,CloudCellV11,PixelFormat.Format32bppPArgb)){
   using(var g=Graphics.FromImage(cell))using(var attr=new ImageAttributes()){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;attr.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(source,new[]{new PointF(0,0),new PointF(CloudCellV11,0),new PointF(0,CloudCellV11)},new Rectangle(i%6*sw,i/6*sh,sw,sh),GraphicsUnit.Pixel,attr);}frames.Add(BlastPixels(cell));
  }
  var atlas=new Bitmap(CloudCellV11*8,CloudCellV11*12,PixelFormat.Format32bppPArgb);var row=new byte[CloudCellV11*4];var bits=atlas.LockBits(new Rectangle(Point.Empty,atlas.Size),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
  try{for(int frame=0;frame<CloudFramesV11;frame++){
   float time=frame/(float)(CloudFramesV11-1),phase=time*23;int a=(int)phase,b=Math.Min(23,a+1);float q=phase-a,fade=time>.90f?1-Smooth((time-.90f)/.10f):1;
   for(int y=0;y<CloudCellV11;y++){for(int x=0;x<row.Length;x++){int p=y*row.Length+x;row[x]=(byte)Math.Max(0,Math.Min(255,(frames[a][p]*(1-q)+frames[b][p]*q)*fade));}Marshal.Copy(row,0,IntPtr.Add(bits.Scan0,(frame/8*CloudCellV11+y)*bits.Stride+frame%8*CloudCellV11*4),row.Length);}
  }}finally{atlas.UnlockBits(bits);}return atlas;
 }
 void DrawBlastCloud(Graphics g,PointF ground,float size,float progress){
  if(progress<0||progress>=1)return;EnsureBlastArt();var box=new RectangleF(ground.X-size*.5f,ground.Y-size*238/256,size,size);if(!g.IsVisible(box))return;int frame=Math.Min(95,(int)(progress*95));string key="cloud-frame-v11/"+frame;
  if(!painted.ContainsKey(key)){while(cloudFrameOrder.Count>=32){int old=cloudFrameOrder.Dequeue();string retired="cloud-frame-v11/"+old;painted[retired].Dispose();painted.Remove(retired);}painted.Add(key,painted["cloud-v11"].Clone(new Rectangle(frame%8*CloudCellV11,frame/8*CloudCellV11,CloudCellV11,CloudCellV11),PixelFormat.Format32bppPArgb));cloudFrameOrder.Enqueue(frame);}
  DrawStamp(g,key,new Rectangle(0,0,CloudCellV11,CloudCellV11),box);
 }
 sealed class FragmentFace {internal int[] Indices;internal bool Paint;internal Color Material;internal FragmentFace(int[] v,Color c,bool p=false){Indices=v;Material=c;Paint=p;}}
 sealed class FragmentMesh {internal List<BlastVector> Points=new List<BlastVector>();internal List<FragmentFace> Faces=new List<FragmentFace>();}
 sealed class FragmentPolygon {internal PointF[] Points;internal float Depth,Light;internal Color Color;internal bool Paint;}
 static BlastVector RotateFragment(BlastVector p,float yaw,float flip){
  float z=(float)Math.Sin(flip)*.53f,c=(float)Math.Cos(z),s=(float)Math.Sin(z),x=p.X*c-p.Y*s,y=p.X*s+p.Y*c;p=new BlastVector(x,y,p.Z);c=(float)Math.Cos(flip);s=(float)Math.Sin(flip);y=p.Y*c-p.Z*s;float zz=p.Y*s+p.Z*c;p=new BlastVector(p.X,y,zz);yaw+=(float)Math.Sin(flip)*.38f;c=(float)Math.Cos(yaw);s=(float)Math.Sin(yaw);return new BlastVector(p.X*c+p.Z*s,p.Y,p.Z*c-p.X*s);
 }
 static BlastVector Cross(BlastVector a,BlastVector b){return new BlastVector(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);}
 static void FragmentBox(FragmentMesh mesh,float x,float y,float z,float w,float h,float d,Color color,bool paint){
  int at=mesh.Points.Count;foreach(var p in new[]{new BlastVector(-w,-h,-d),new BlastVector(w,-h,-d),new BlastVector(w,h,-d),new BlastVector(-w,h,-d),new BlastVector(-w,-h,d),new BlastVector(w,-h,d),new BlastVector(w,h,d),new BlastVector(-w,h,d)})mesh.Points.Add(new BlastVector(x+p.X*.5f,y+p.Y*.5f,z+p.Z*.5f));
  int[][] faces={new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,4,7,3},new[]{1,2,6,5},new[]{3,7,6,2},new[]{0,1,5,4}};for(int i=0;i<faces.Length;i++){var f=(int[])faces[i].Clone();for(int j=0;j<f.Length;j++)f[j]+=at;mesh.Faces.Add(new FragmentFace(f,color,paint&&(i==1||i==4)));}
 }
 static FragmentMesh MakeFragmentMesh(int kind){
  var mesh=new FragmentMesh();var steel=Color.FromArgb(118,130,131);var concrete=Color.FromArgb(153,145,126);
  if(kind==0){
   float[][] shape={new[]{-.47f,-.26f},new[]{-.20f,-.34f},new[]{.46f,-.22f},new[]{.35f,.34f},new[]{-.10f,.29f},new[]{-.44f,.12f}};
   for(int side=0;side<2;side++)for(int i=0;i<6;i++)mesh.Points.Add(new BlastVector(shape[i][0],shape[i][1],(side==0?-.035f:.035f)+(i==3?.10f:0)));
   mesh.Faces.Add(new FragmentFace(new[]{5,4,3,2,1,0},steel));mesh.Faces.Add(new FragmentFace(new[]{6,7,8,9,10,11},Color.FromArgb(220,223,217),true));for(int i=0;i<6;i++)mesh.Faces.Add(new FragmentFace(new[]{i,(i+1)%6,(i+1)%6+6,i+6},Color.FromArgb(175,178,169)));
  }else if(kind==1){
   FragmentBox(mesh,0,0,0,.64f,.63f,.58f,concrete,false);for(int i=0;i<mesh.Points.Count;i++){var p=mesh.Points[i];p.X*=.72f+BlastNoise(i+37)*.38f;p.Y*=.75f+BlastNoise(i+91)*.35f;p.Z*=.75f+BlastNoise(i+52)*.35f;mesh.Points[i]=p;}
  }else if(kind==2){FragmentBox(mesh,-.10f,0,0,.13f,.93f,.14f,steel,false);FragmentBox(mesh,.09f,.35f,0,.48f,.13f,.17f,steel,false);FragmentBox(mesh,-.19f,-.32f,.07f,.28f,.12f,.20f,Color.FromArgb(84,93,92),false);}
  else{FragmentBox(mesh,0,0,0,.69f,.52f,.29f,Color.FromArgb(193,199,192),true);for(int i=0;i<5;i++)FragmentBox(mesh,-.24f+i*.12f,0,.165f,.045f,.36f,.035f,Color.FromArgb(49,60,60),false);FragmentBox(mesh,.26f,.19f,.07f,.14f,.12f,.20f,steel,false);}
  return mesh;
 }
 static void BuildDebrisAtlas(out Bitmap body,out Bitmap mask){
  body=new Bitmap(2048,2048,PixelFormat.Format32bppPArgb);mask=new Bitmap(2048,2048,PixelFormat.Format32bppPArgb);
  using(var bg=Graphics.FromImage(body))using(var pg=Graphics.FromImage(mask)){
   bg.SmoothingMode=pg.SmoothingMode=SmoothingMode.AntiAlias;pg.CompositingMode=CompositingMode.SourceCopy;
   for(int kind=0;kind<4;kind++){var mesh=MakeFragmentMesh(kind);for(int yaw=0;yaw<8;yaw++)for(int pose=0;pose<32;pose++){
    int frame=(kind*8+yaw)*32+pose;var transformed=new BlastVector[mesh.Points.Count];for(int i=0;i<transformed.Length;i++)transformed[i]=RotateFragment(mesh.Points[i],yaw*(float)Math.PI/4,pose*(float)Math.PI/16);
    var polygons=new List<FragmentPolygon>();foreach(var face in mesh.Faces){BlastVector a=transformed[face.Indices[0]],b=transformed[face.Indices[1]],c=transformed[face.Indices[2]],n=Cross(b-a,c-a);float length=(float)Math.Sqrt(n.X*n.X+n.Y*n.Y+n.Z*n.Z);if(length<.0001f)continue;n=n*(1/length);if(n.X+n.Z+n.Y*.816f<=0)continue;var points=new PointF[face.Indices.Length];float depth=0;for(int i=0;i<points.Length;i++){var p=transformed[face.Indices[i]];points[i]=new PointF(32+(p.X-p.Z)*25,32+(p.X+p.Z)*12.5f-p.Y*30.6f);depth+=p.X+p.Z+p.Y*.816f;}float lit=.48f+Math.Max(0,-n.X*.45f+n.Y*.72f+n.Z*.53f)*.57f;polygons.Add(new FragmentPolygon{Points=points,Depth=depth/points.Length,Light=lit,Color=face.Material,Paint=face.Paint});}
    polygons.Sort((a,b)=>a.Depth.CompareTo(b.Depth));var bs=bg.Save();var ps=pg.Save();bg.SetClip(new Rectangle(frame%32*64,frame/32*64,64,64));pg.SetClip(new Rectangle(frame%32*64,frame/32*64,64,64));bg.TranslateTransform(frame%32*64,frame/32*64);pg.TranslateTransform(frame%32*64,frame/32*64);
    foreach(var p in polygons){var color=Color.FromArgb(Math.Min(255,(int)(p.Color.R*p.Light)),Math.Min(255,(int)(p.Color.G*p.Light)),Math.Min(255,(int)(p.Color.B*p.Light)));using(var brush=new SolidBrush(color))bg.FillPolygon(brush,p.Points);using(var paint=new SolidBrush(p.Paint?color:Color.Transparent))pg.FillPolygon(paint,p.Points);using(var outline=new Pen(Color.FromArgb(158,29,36,34),.65f))bg.DrawPolygon(outline,p.Points);using(var erase=new Pen(Color.Transparent,.75f))pg.DrawPolygon(erase,p.Points);}
    bg.Restore(bs);pg.Restore(ps);
   }}
  }
 }
}}
