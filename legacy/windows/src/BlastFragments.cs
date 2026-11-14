using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Collections.Generic;
namespace Greyhaven {
internal struct BlastVector {
 internal float X,Y,Z;
 internal BlastVector(float x,float y,float z){X=x;Y=y;Z=z;}
 public static BlastVector operator +(BlastVector a,BlastVector b){return new BlastVector(a.X+b.X,a.Y+b.Y,a.Z+b.Z);}
 public static BlastVector operator -(BlastVector a,BlastVector b){return new BlastVector(a.X-b.X,a.Y-b.Y,a.Z-b.Z);}
 public static BlastVector operator *(BlastVector a,float f){return new BlastVector(a.X*f,a.Y*f,a.Z*f);}
}
internal sealed class BlastShard {
 internal BlastVector Origin,Velocity;internal float Gravity,Drag,Impact,BounceTime,Restitution,Life,Release,Size,Spin;internal int Kind,Yaw,Pose;internal bool Smoking;
 internal BlastVector Flight(float t){float travel=(1-(float)Math.Exp(-Drag*t))/Drag;return new BlastVector(Origin.X+Velocity.X*travel,Origin.Y+Velocity.Y*t-.5f*Gravity*t*t,Origin.Z+Velocity.Z*travel);}
 internal BlastVector Sample(float age){
  float t=Math.Max(0,age-Release);if(t<=Impact)return Flight(t);
  var hit=Flight(Impact);float after=t-Impact,vertical=(Gravity*Impact-Velocity.Y)*Restitution,retained=(float)Math.Exp(-Drag*Impact)*.48f,travel=(1-(float)Math.Exp(-Drag*after))/Drag;
  return new BlastVector(hit.X+Velocity.X*retained*travel,Math.Max(0,vertical*after-.5f*Gravity*after*after),hit.Z+Velocity.Z*retained*travel);
 }
 internal float Opacity(float age){float t=age-Release;if(t<0||t>Life)return 0;return Math.Min(1,Math.Max(0,(Life-t)/.16f));}
}
partial class CommandWindow {
 internal const int DebrisCell=64,DebrisPoses=32,DebrisYaws=8,DebrisKinds=4;
 readonly Dictionary<string,BlastShard[]> blastProfiles=new Dictionary<string,BlastShard[]>();readonly Queue<string> blastProfileOrder=new Queue<string>();
 readonly Dictionary<Tuple<int,int>,Bitmap> debrisColorFrames=new Dictionary<Tuple<int,int>,Bitmap>();readonly Queue<Tuple<int,int>> debrisColorOrder=new Queue<Tuple<int,int>>();
 static float BlastNoise(int seed){unchecked{uint v=(uint)seed+0x9e3779b9;v=(v^(v>>16))*0x7feb352d;v=(v^(v>>15))*0x846ca68b;v^=v>>16;return (v&0xffffff)/16777215f;}}
 static int BlastSeed(string key){unchecked{int s=17;foreach(char c in key)s=s*31+c;return s;}}
 static float BlastHeight(string key){switch(key){case "power":return 2.45f;case "hq":return 2.56f;case "factory":return 2.36f;case "radar":return 2.23f;case "satellite":return 2.50f;case "post":return .97f;case "municipal":return 3.11f;case "shop":return 2.55f;case "barracks":return 1.84f;default:return 2.12f;}}
 BlastShard[] BlastProfile(string key,int seed,float ready){
  string cacheKey=key+"/"+seed+"/"+(int)Math.Round(ready*1000);BlastShard[] profile;if(blastProfiles.TryGetValue(cacheKey,out profile))return profile;
  var spec=BuildingV08.Specs[key];int count=Math.Max(8,(int)((9+(spec.Width+spec.Depth)*.6f)*Math.Max(.55f,ready)));profile=new BlastShard[count];int salt=BlastSeed(key)^seed*371;
  for(int i=0;i<count;i++){
   int s=salt+i*97;float a=i*2.399963f+(BlastNoise(s+1)-.5f)*.75f,weight=BlastNoise(s+2),speed=4.0f+BlastNoise(s+3)*3.1f;
   int kind=i%DebrisKinds;float height=(.27f+BlastNoise(s+4)*BlastHeight(key)*.56f)*Math.Max(.22f,ready),vertical=2.0f+BlastNoise(s+5)*1.65f,gravity=15.5f+weight*3.0f;
   var p=new BlastShard{Kind=kind,Yaw=((int)Math.Round(a*4/Math.PI)+16)%8,Pose=(int)(BlastNoise(s+6)*32)%32,Origin=new BlastVector((BlastNoise(s+7)-.5f)*spec.Width*.66f,height,(BlastNoise(s+8)-.5f)*spec.Depth*.66f),Velocity=new BlastVector((float)Math.Cos(a)*speed,vertical,(float)Math.Sin(a)*speed),Gravity=gravity,Drag=1.55f+weight*.75f,Restitution=kind==1?.12f:.22f,Release=BlastNoise(s+9)*.075f,Size=(kind==2?.36f:kind==0?.37f:.25f)*( .83f+BlastNoise(s+10)*.42f),Spin=(25+BlastNoise(s+11)*35)*(i%2==0?1:-1),Smoking=i%7==0};
   p.Impact=(vertical+(float)Math.Sqrt(vertical*vertical+2*gravity*height))/gravity;p.BounceTime=2*(gravity*p.Impact-vertical)*p.Restitution/gravity;p.Life=p.Impact+p.BounceTime+.16f;profile[i]=p;
  }
  while(blastProfiles.Count>=128&&blastProfileOrder.Count>0)blastProfiles.Remove(blastProfileOrder.Dequeue());blastProfiles.Add(cacheKey,profile);blastProfileOrder.Enqueue(cacheKey);return profile;
 }
 static PointF ProjectShard(PointF ground,BlastVector p,float z){return new PointF(ground.X+(p.X-p.Z)*Campaign.Tile*Axis*z,ground.Y+((p.X+p.Z)*Rise-p.Y*.8660254f)*Campaign.Tile*z);}
 static float BlastScale(string key,RectangleF box){var spec=BuildingV08.Specs[key];return box.Width*LotSpan/(FrameSize*(spec.Width+spec.Depth)*Campaign.Tile*Axis);}
 Bitmap DebrisColorFrame(int frame,Color color){
  var id=Tuple.Create(frame,color.ToArgb());Bitmap image;if(debrisColorFrames.TryGetValue(id,out image))return image;
  EnsureBlastArt();var rect=new Rectangle(frame%32*64,frame/32*64,64,64);image=BuildingTint(painted["debris-v11"],painted["debris-v11-paint"],rect,color);
  while(debrisColorFrames.Count>=1280&&debrisColorOrder.Count>0){var old=debrisColorOrder.Dequeue();painted.Remove(DebrisFrameKey(old.Item1,old.Item2));debrisColorFrames[old].Dispose();debrisColorFrames.Remove(old);}debrisColorFrames.Add(id,image);painted.Add(DebrisFrameKey(frame,color.ToArgb()),image);debrisColorOrder.Enqueue(id);return image;
 }
 static string DebrisFrameKey(int frame,int color){return "shard-frame-v11/"+frame+"/"+color;}
 void DrawShard(Graphics g,BlastShard shard,PointF point,float z,float age,Color color){
  float t=age-shard.Release,alpha=shard.Opacity(age);if(alpha<=0)return;int pose=((shard.Pose+(int)(t*shard.Spin))%32+32)%32,frame=(shard.Kind*8+shard.Yaw)*32+pose;
  float size=shard.Size*Campaign.Tile*z*2.1f;var box=new RectangleF(point.X-size*.5f,point.Y-size*.5f,size,size);if(!g.IsVisible(box))return;
  var image=DebrisColorFrame(frame,color);if(alpha>=.995f){DrawStamp(g,DebrisFrameKey(frame,color.ToArgb()),new Rectangle(0,0,64,64),box);return;}
  using(var attr=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=alpha;attr.SetColorMatrix(matrix);g.DrawImage(image,new[]{new PointF(box.Left,box.Top),new PointF(box.Right,box.Top),new PointF(box.Left,box.Bottom)},new Rectangle(0,0,64,64),GraphicsUnit.Pixel,attr);}
 }
 void DrawBlastDebris(Graphics g,string key,RectangleF box,float age,int seed,float ready,Color color,bool rear){
  if(age<0||age>1.45f)return;float z=BlastScale(key,box);var ground=SocketPoint(box,new PointF(160,220));var profile=BlastProfile(key,seed,ready);
  foreach(var shard in profile){if(shard.Opacity(age)<=0)continue;var position=shard.Sample(age);if((position.X+position.Z<0)!=rear)continue;var point=ProjectShard(ground,position,z);
   if(shard.Smoking&&age>shard.Release+.07f)for(int j=1;j<=3;j++){
    float past=age-j*.060f;if(past<shard.Release)continue;var at=shard.Sample(past);at.Y+=j*.032f;var trail=ProjectShard(ground,at,z);DrawBlastCloud(g,trail,(10+j*2)*z,.70f+j*.060f);
   }
   DrawShard(g,shard,point,z,age,color);
   float impact=age-shard.Release-shard.Impact;if(impact>=0&&impact<.25f){var at=shard.Flight(shard.Impact);at.Y=0;var p=ProjectShard(ground,at,z);Effect(g,shard.Kind==1?"dust":"spark",p,(shard.Kind==1?12:8)*z,impact/.25f);}
  }
 }
 void DrawBlastEmbers(Graphics g,string key,RectangleF box,float age,int seed){
  if(age<0||age>1.0f)return;float z=BlastScale(key,box);var ground=SocketPoint(box,new PointF(160,220));int salt=BlastSeed(key)^seed;
  for(int i=0;i<14;i++){
   float delay=BlastNoise(salt+i*9)*.10f,t=age-delay;if(t<0)continue;float a=i*2.399f+BlastNoise(salt+i),speed=5+BlastNoise(salt+i*17)*5,travel=(1-(float)Math.Exp(-3*t))/3,height=Math.Max(0,.8f+(2.6f+BlastNoise(salt+i*3)*2)*t-9*t*t),fade=Math.Max(0,1-t/(.6f+BlastNoise(salt+i*5)*.30f));if(fade<=0)continue;
   var pos=new BlastVector((float)Math.Cos(a)*speed*travel,height,(float)Math.Sin(a)*speed*travel);PointF p=ProjectShard(ground,pos,z),before=ProjectShard(ground,new BlastVector(pos.X-(float)Math.Cos(a)*.14f,pos.Y+.025f,pos.Z-(float)Math.Sin(a)*.14f),z);
   using(var pen=new Pen(Color.FromArgb((int)(190*fade),255,158,47),Math.Max(.65f,z*.48f)))g.DrawLine(pen,before,p);using(var pen=new Pen(Color.FromArgb((int)(245*fade),255,222,134),Math.Max(.7f,z*.65f)))g.DrawLine(pen,p.X,p.Y,p.X+.7f*z,p.Y-.4f*z);
  }
 }
 void DrawBlastGroundShadows(Graphics g,Piece o){
  float age=o.Dead-TerminalBlastAt;if(o.Alive||!o.T.Building||age<0||age>1.45f||!world.Visible(o))return;var ground=ScreenOf(o.P);foreach(var shard in BlastProfile(o.T.Key,o.Id,StructuralReadiness(o))){float alpha=shard.Opacity(age);if(alpha<=0)continue;var p=shard.Sample(age);float height=p.Y;p.Y=0;var at=ProjectShard(ground,p,zoom);float w=shard.Size*Campaign.Tile*zoom*.9f;using(var brush=new SolidBrush(Color.FromArgb((int)(alpha*34/(1+height)),24,27,25)))g.FillPolygon(brush,new[]{new PointF(at.X-w,at.Y),new PointF(at.X,at.Y-w*.40f),new PointF(at.X+w,at.Y),new PointF(at.X,at.Y+w*.40f)});}
 }
 void ReleaseBlastFragments(){foreach(var item in debrisColorFrames){painted.Remove(DebrisFrameKey(item.Key.Item1,item.Key.Item2));item.Value.Dispose();}debrisColorFrames.Clear();debrisColorOrder.Clear();blastProfiles.Clear();blastProfileOrder.Clear();}
}
}
