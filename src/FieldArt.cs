using System;
using System.Drawing;
using System.Collections.Generic;
namespace Greyhaven {
partial class CommandWindow {
 readonly Dictionary<string,Bitmap> painted=new Dictionary<string,Bitmap>();
 const int FrameSize=320,LotSpan=224,GroundX=160,GroundY=220;
 const int MobileCell=512,FootCell=512;
 static RectangleF BuildingRect(RectangleF footprint,PointF ground,float magnification){
  float k=(footprint.Width+footprint.Height)*Axis*magnification/LotSpan;
  return new RectangleF(ground.X-GroundX*k,ground.Y-GroundY*k,FrameSize*k,FrameSize*k);
 }
 static RectangleF VehicleRect(TypeInfo type,PointF ground,float size){
  size=VehicleCanvas(type,size/type.Size);return new RectangleF(ground.X-size/2,ground.Y-size*188f/FrameSize,size,size);
 }
 static Rectangle SourceCell(TypeInfo t,int row,int dir){
  if(t.Building)return new Rectangle(0,0,FrameSize,FrameSize);
  if(t.Foot)return new Rectangle(dir/2*FootCell,0,FootCell,FootCell);
  if(t.Key=="tank")return TankCell(row,dir*4);
  return new Rectangle(dir%8*MobileCell,dir/8*MobileCell,MobileCell,MobileCell);
 }
 static int TankFrame(Piece o){return ((int)Math.Round(o.Turn*4)+56)%64;}
 const int TankCellSize=512;
 // Geometry/alpha is shared by every faction. Color is an independent layer.
 static Rectangle TankCell(int side,int angle){return new Rectangle(angle%8*TankCellSize,angle/8*TankCellSize,TankCellSize,TankCellSize);}
 // Reuse the final physical-size sprite while the camera scale is unchanged.
 bool drawingClient;
 float stampScale,stampZoom;long stampBytes;
 readonly Dictionary<Tuple<string,Rectangle,Size>,Bitmap> stamps=new Dictionary<Tuple<string,Rectangle,Size>,Bitmap>();
 readonly Queue<Tuple<string,Rectangle,Size>> stampOrder=new Queue<Tuple<string,Rectangle,Size>>();
 void ClearStampCache(){foreach(var img in stamps.Values)img.Dispose();stamps.Clear();stampOrder.Clear();stampBytes=0;}
 void DrawStamp(Graphics g,string key,Rectangle source,RectangleF box,Color? faction=null){
  if(!drawingClient){DrawLayeredSource(g,key,source,box,faction);return;}
  // Reject offscreen stamps before asking GDI+ about the complex clip region.
  if(box.Right<0||box.Left>W||box.Bottom<0||box.Top>H)return;
  if(!g.IsVisible(box))return;
  if(stampScale!=scale||stampZoom!=zoom){ClearStampCache();stampScale=scale;stampZoom=zoom;}
  var size=new Size(Math.Max(1,(int)Math.Round(box.Width*scale)),Math.Max(1,(int)Math.Round(box.Height*scale)));
  var id=Tuple.Create(faction.HasValue?key+"/"+faction.Value.ToArgb():key,source,size);Bitmap stamp;
  if(!stamps.TryGetValue(id,out stamp)){
   long bytes=4L*size.Width*size.Height;
   const long budget=64L*1024*1024;
   if(bytes>budget){DrawLayeredSource(g,key,source,box,faction);return;}
   // Animation sequences must not permanently disable the cache after filling
   // it once. Retire old frames under the same fixed memory budget.
   while(stampBytes+bytes>budget&&stampOrder.Count>0){var old=stampOrder.Dequeue();Bitmap retired;if(stamps.TryGetValue(old,out retired)){stampBytes-=4L*retired.Width*retired.Height;stamps.Remove(old);retired.Dispose();}}
   stamp=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
   using(var target=Graphics.FromImage(stamp)){
    target.InterpolationMode=faction.HasValue||key.EndsWith("-wreck")?System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic:System.Drawing.Drawing2D.InterpolationMode.Bilinear;
    DrawLayeredSource(target,key,source,new Rectangle(Point.Empty,size),faction);
   }
   stamps.Add(id,stamp);stampOrder.Enqueue(id);stampBytes+=bytes;
  }
  var state=g.Save();g.ResetTransform();g.DrawImageUnscaled(stamp,(int)Math.Round(ox+box.X*scale),(int)Math.Round(oy+box.Y*scale));g.Restore(state);
 }
 void PaintFrame(Graphics g,TypeInfo t,int side,int dir,RectangleF r){
  var crop=SourceCell(t,side,dir);
  DrawStamp(g,t.Key,crop,r,UnitColor(side));
 }
 readonly Dictionary<string,Rectangle> cardCrops=new Dictionary<string,Rectangle>();
 void DrawCardArt(Graphics g,TypeInfo t,RectangleF slot){
  var sheet=painted[t.Key];Rectangle source;
  if(!cardCrops.TryGetValue(t.Key,out source)){
   var r=SourceCell(t,0,14);int l=r.Right,top=r.Bottom,right=r.Left,bottom=r.Top;
   for(int y=r.Top;y<r.Bottom;y+=2)for(int x=r.Left;x<r.Right;x+=2)if(sheet.GetPixel(x,y).A>64){
    l=Math.Min(l,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
   }
   source=Rectangle.Intersect(r,Rectangle.FromLTRB(l-3,top-3,right+4,bottom+4));cardCrops[t.Key]=source;
  }
  float k=Math.Min(slot.Width/source.Width,slot.Height/source.Height),w=source.Width*k,h=source.Height*k;
  DrawStamp(g,t.Key,source,new RectangleF(slot.X+(slot.Width-w)/2,slot.Y+(slot.Height-h)/2,w,h),UnitColor(0));
 }
 void BuildingArt(Graphics g,string key,int side,RectangleF box,float built,bool wreck=false){
  var source=new Rectangle(0,wreck?2*FrameSize:0,FrameSize,FrameSize);
  if(built<1&&!wreck&&BuildingV08.IsBuilding(key)){key=B08Frame(key,"build",Math.Max(0,Math.Min(BuildFrames-1,(int)(built*(BuildFrames-1)))));source=B08Source();}
  DrawStamp(g,key,source,box,wreck?(Color?)null:key=="harbormaster"?Color.FromArgb(140,151,143):UnitColor(side));
 }
 static RectangleF SoldierBox(PointF p,float size){
  float w=size*.54f;return new RectangleF(p.X-w/2,p.Y-w*.91f,w,w);
 }
 static PointF SoldierFeet(Piece o,PointF p,float zoom,float time){
  // Step feedback follows travelled distance; a blocked order must not bob.
  // Foot placement is animated in the articulated gait mesh.
  return p;
 }
 // Project the same soldier onto the ground as it falls. Six cached poses per view.
 const int FallCell=144,FallPoses=6,FootViews=8;
 static Bitmap FallenSheet(Bitmap standing){
  var result=new Bitmap(FootViews*FallCell,FallPoses*FallCell,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(result)){
   for(int d=0;d<FootViews;d++)for(int i=0;i<FallPoses;i++){
    float fall=i/(float)(FallPoses-1);int row=i;var state=g.Save();
    g.TranslateTransform(d*FallCell+72,row*FallCell+82);g.ScaleTransform(1,1-.55f*fall);
    g.RotateTransform((d<FootViews/2?1:-1)*83*fall);
    g.DrawImage(standing,new RectangleF(-30,-60*.91f,60,60),new RectangleF(d*FootCell,0,FootCell,FootCell),GraphicsUnit.Pixel);
    g.Restore(state);
   }
  }return result;
 }
 void DrawLive(Graphics g,TypeInfo t,int side,int dir,PointF p,float s,float built){
  if(t.Foot){PaintFrame(g,t,side,dir,SoldierBox(p,s));
  }else if(t.Building)BuildingArt(g,t.Key,side,BuildingRect(Campaign.Footprint(t,PointF.Empty),p,s/t.Size),built);
  else PaintFrame(g,t,side,dir,VehicleRect(t,p,s));
 }
 void DrawFieldSprite(Graphics g,Piece o,PointF p,float size){
  if(o.T.Building&&BuildingV08.IsBuilding(o.T.Key)){DrawB08Field(g,o,p,size);return;}
  p=RecoilPoint(o,p);
  int dir=(o.Dir+14)%16;
  if(o.T.Building&&(!o.Alive||o.Ready<1)){DrawBuildingState(g,o,p,size);return;}
  if(o.T.Key=="tank"){
   int frame=TankFrame(o);
   DrawStamp(g,o.Alive?"tank":"tank-wreck",TankCell(0,frame),VehicleRect(o.T,p,size),o.Alive?(Color?)UnitColor(o.Side):null);return;
  }
  if(!o.Alive){
   if(o.T.Building)BuildingArt(g,o.T.Key,o.Side,BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom),1,true);
   else if(!o.T.Foot)DrawStamp(g,o.T.Key+"-wreck",SourceCell(o.T,0,dir),VehicleRect(o.T,p,size));
   else{
    int pose=Math.Min(FallPoses-1,(int)(o.Dead*14)),row=pose;
    float k=size*.54f/60;
    DrawStamp(g,"fallen-"+o.T.Key,new Rectangle(dir/2*FallCell,row*FallCell,FallCell,FallCell),new RectangleF(p.X-72*k,p.Y-82*k,FallCell*k,FallCell*k),UnitColor(o.Side));
   }
   return;
  }
  if(world.RepairVehicle(o)){DrawStamp(g,"ifv-repair",SourceCell(o.T,o.Side,dir),VehicleRect(o.T,p,size),UnitColor(o.Side));return;}
  if(o.T.Key=="miner"&&o.Cargo>10){
   DrawStamp(g,"miner-full",SourceCell(o.T,o.Side,dir),VehicleRect(o.T,p,size),UnitColor(o.Side));return;
  }
  if(o.T.Foot){DrawFootGait(g,o,p,size);return;}
  DrawLive(g,o.T,o.Side,dir,p,size,o.Ready);

 }
 string LiveArtKey(Piece o){return world.RepairVehicle(o)?"ifv-repair":o.T.Key=="miner"&&o.Cargo>10?"miner-full":o.T.Key;}
 bool MobileHit(Piece o,PointF p,float size,PointF screen){
  int dir=(o.Dir+14)%16;
  if(o.T.Foot){
   if(CombatPose(o)){string pose=EnsureB08(CombatFrame(o));var crop=CombatCrop(pose);return PixelHit(pose,new Rectangle(Point.Empty,crop.Size),FootCropBox(GaitBox(p,size),crop),screen);}
   if(o.Stance>0){string key=EnsureB08("stance/"+o.T.Key+"/"+dir/2+"/"+(int)Math.Round(o.Stance*32));return PixelHit(key,new Rectangle(0,0,384,384),GaitBox(p,size),screen);}
   var feet=SoldierFeet(o,p,zoom,world.Time);var box=SoldierBox(feet,size);
   if(Math.Abs(p.X-screen.X)<size*.09f&&Math.Abs(p.Y-screen.Y)<size*.07f)return true;
   return PixelHit(o.T.Key,SourceCell(o.T,o.Side,dir),box,screen);
  }
  var source=o.T.Key=="tank"?TankCell(o.Side,TankFrame(o)):SourceCell(o.T,o.Side,dir);
  return PixelHit(LiveArtKey(o),source,VehicleRect(o.T,p,size),screen);
 }
 bool PixelHit(string key,Rectangle src,RectangleF box,PointF screen){
  if(!box.Contains(screen))return false;int x=(int)((screen.X-box.X)*src.Width/box.Width),y=(int)((screen.Y-box.Y)*src.Height/box.Height);
  return painted[key].GetPixel(src.X+Math.Min(src.Width-1,x),src.Y+Math.Min(src.Height-1,y)).A>45;
 }
 bool InkHit(string key,int row,RectangleF box,PointF screen){
  row=0; // Every faction shares the same neutral building geometry.
  if(!box.Contains(screen))return false;
  int x=(int)((screen.X-box.X)*FrameSize/box.Width),y=(int)((screen.Y-box.Y)*FrameSize/box.Height);
  return painted[key].GetPixel(Math.Min(FrameSize-1,x),row*FrameSize+Math.Min(FrameSize-1,y)).A>40;
 }
}
}
