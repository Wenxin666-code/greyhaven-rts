using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace Greyhaven {partial class CommandWindow {
 internal const float TerminalBlastAt=1.28f,TerminalWreckAt=1.43f;
 static int BuildingDamageLevel(Piece o){float health=o.Hp/o.T.Hp;return health>=.75f?0:health>=.50f?1:health>=.25f?2:3;}
 void EnsureHazard(string kind){
  string key="hazard/"+kind;if(painted.ContainsKey(key))return;
  if(Environment.GetEnvironmentVariable("GREYHAVEN_REBAKE_EFFECTS")!="1")using(var resource=typeof(CommandWindow).Assembly.GetManifestResourceStream("Greyhaven.Art.hazard-"+kind+"-v10.png"))if(resource!=null){using(var image=new Bitmap(resource)){var copy=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppPArgb);using(var target=Graphics.FromImage(copy)){target.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;target.DrawImageUnscaled(image,0,0);}painted.Add(key,copy);}return;}
  int frames=kind=="blast"?32:96;var atlas=new Bitmap(128*8,128*(frames/8),PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(atlas)){
   g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   for(int frame=0;frame<frames;frame++){
    var state=g.Save();g.SetClip(new Rectangle(frame%8*128,frame/8*128,128,128));g.TranslateTransform(frame%8*128,frame/8*128);
    float t=frame/96f,a=t*(float)Math.PI*2;
    if(kind=="blast")DrawHeatBlast(g,frame/31f);
    else if(kind=="flame"){
     DrawHeatFlame(g,a);
     for(int j=0;j<8;j++){float age=(t*3+j/8f)%1,x=52+j*3+(float)Math.Sin(j*3.1f+age*4)*age*13,y=104-age*83;using(var pen=new Pen(Color.FromArgb((int)(185*(1-age)),251,179,73),1))g.DrawLine(pen,x,y,x-1,y+3);}
    }else{
     for(int j=0;j<10;j++){
      float age=(t*2+j/10f)%1,x=64+age*20+(Noise(j*39)-.5f)*age*20,y=113-age*87,r=3+age*18,fade=(float)Math.Sin(age*Math.PI);
      Cloud(g,x,y,r,t,Color.FromArgb((int)(93*fade),48,45,40),j*29);Cloud(g,x-2,y-2,r*.66f,t,Color.FromArgb((int)(51*fade),80,75,64),j*31+4);
     }
    }g.Restore(state);
   }
  }painted.Add(key,atlas);
 }
 void DrawHeatFlame(Graphics target,float phase){
  using(var image=new Bitmap(128,128,PixelFormat.Format32bppPArgb)){
   var data=new byte[128*128*4];
   for(int y=22;y<115;y++){
    float h=(113-y)/91f,up=Math.Max(0,h),wind=(float)Math.Sin(phase*2+up*4)*up*6;
    for(int x=21;x<108;x++){
     double field=-1;
     for(int j=0;j<3;j++){
      double center=64+(j-1)*10+wind+Math.Sin(phase*(2+j)+up*(7+j))*up*8,width=(14+j%2*4)*(1-up*.83),dx=(x-center)/width;
      field=Math.Max(field,1-dx*dx-up*.91);
     }
     field+=.16*Math.Sin(x*.41+up*27-phase*6)+.12*Math.Sin(x*.17-up*18+phase*5)-.11;
     if(field<=0)continue;double density=Math.Min(1,field),heat=Math.Min(1,density*.88+(1-up)*.12),alpha=Math.Min(1,density*2.7)*Math.Min(1,(115-y)/5.0);
     int aa=(int)(alpha*(190+density*52)),r=(int)(193+62*Math.Min(1,heat*2)),gg=(int)(48+182*Math.Pow(heat,.83)),b=(int)(13+127*Math.Pow(heat,2.2)),at=(y*128+x)*4;
     data[at]=(byte)(b*aa/255);data[at+1]=(byte)(gg*aa/255);data[at+2]=(byte)(r*aa/255);data[at+3]=(byte)aa;
    }
   }
   var locked=image.LockBits(new Rectangle(0,0,128,128),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);try{Marshal.Copy(data,0,locked.Scan0,data.Length);}finally{image.UnlockBits(locked);}target.DrawImageUnscaled(image,0,0);
  }
 }
 void DrawHeatBlast(Graphics target,float time){
  using(var image=new Bitmap(128,128,PixelFormat.Format32bppPArgb)){
   var data=new byte[128*128*4];double expand=1-Math.Exp(-time*12),fade=Math.Min(1,(1-time)*4.0);
   double[] lx={0,-.33,.32,-.17,.23,.04},ly={0,.08,.04,-.31,-.25,-.50};
   for(int y=7;y<124;y++)for(int x=7;x<121;x++){
    double field=-1;
    for(int i=0;i<6;i++){
     double cx=64+lx[i]*42*expand,cy=84+ly[i]*42*expand-time*9,r=(20+i%3*3)*(.28+.72*expand),dx=(x-cx)/r,dy=(y-cy)/(r*.90);
     field=Math.Max(field,1-dx*dx-dy*dy);
    }
    double grain=.12*Math.Sin(x*.47+y*.29-time*15)+.08*Math.Sin(x*.19-y*.41+time*17);field+=grain;if(field<=0)continue;
    double density=Math.Min(1,field*2.4),heat=Math.Min(1,Math.Max(0,(field*.72+.16)*(1-time*1.12))),ember=Math.Min(1,heat*2),smoke=Math.Min(1,Math.Max(0,(time-.48)*2.2));
    int aa=(int)(density*fade*246),red=(int)((65+190*ember)*(1-smoke)+57*smoke),green=(int)((44+201*Math.Pow(heat,.72))*(1-smoke)+55*smoke),blue=(int)((24+134*Math.Pow(heat,1.8))*(1-smoke)+49*smoke),at=(y*128+x)*4;
    data[at]=(byte)(blue*aa/255);data[at+1]=(byte)(green*aa/255);data[at+2]=(byte)(red*aa/255);data[at+3]=(byte)aa;
   }
   var locked=image.LockBits(new Rectangle(0,0,128,128),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);try{Marshal.Copy(data,0,locked.Scan0,data.Length);}finally{image.UnlockBits(locked);}target.DrawImageUnscaled(image,0,0);
  }
 }
 void LargeBlast(Graphics g,PointF center,float size,float progress){
  if(progress<0||progress>=1)return;var box=new RectangleF(center.X-size*.5f,center.Y-size*84/128,size,size);if(!g.IsVisible(box))return;EnsureHazard("blast");int frame=Math.Min(31,(int)(progress*32));DrawStamp(g,"hazard/blast",new Rectangle(frame%8*128,frame/8*128,128,128),box);
 }
 void Hazard(Graphics g,string kind,PointF anchor,float size,float clock){
  var box=new RectangleF(anchor.X-size*.5f,anchor.Y-size*109/128,size,size);if(!g.IsVisible(box))return;EnsureHazard(kind);int frame=(int)(Math.Max(0,clock)*24)%96;
  DrawStamp(g,"hazard/"+kind,new Rectangle(frame%8*128,frame/8*128,128,128),box);
 }
 void BuildingFireFx(Graphics g,string key,RectangleF box,float time,int damage,float growth=1,float ready=1){
  if(damage<=0)return;PointF[] sites;if(!StructuralV10.FireSites.TryGetValue(key,out sites))return;
  var spec=BuildingV08.Specs[key];float z=box.Width*LotSpan/(FrameSize*(spec.Width+spec.Depth)*Campaign.Tile*Axis);
  if(ready<1){var site=new PointF(160,214);StructuralSite[][] construction;if(StructuralV10.Sites.TryGetValue(key,out construction))foreach(var candidate in construction[0])if(candidate.Time<=ready)site=candidate.Point;sites=new[]{site};}
  int count=Math.Min(sites.Length,damage==1?1:damage==2?2:sites.Length);
  for(int i=0;i<count;i++){var p=SocketPoint(box,sites[i]);float clock=time+i*.681f;Hazard(g,"soot",p,(damage==1?25:damage==2?34:42)*z*growth,clock);if(damage>=2)Hazard(g,"flame",p,(damage==2?18:25)*z*growth,clock);}
 }
 void DrawUprightDamage(Graphics g,string key,RectangleF box,Color color,int level){
  if(level==0){DrawStamp(g,key,B08Source(),box,color);return;}
  DrawStamp(g,"b08/"+key+"/damage-"+level,B08Source(),box,color);
 }
 void DrawFatalBuilding(Graphics g,string key,RectangleF box,Color color,float age,float ready=1){
  if(age<TerminalWreckAt){
   if(ready<1)DrawStamp(g,"b08/"+key+"/build/"+Math.Min(BuildFrames-1,(int)(ready*(BuildFrames-1))),B08Source(),box,color);
   else DrawUprightDamage(g,key,box,color,3);
  }else PreviewCrater(g,key,box);
 }
 void FatalBuildingFx(Graphics g,string key,RectangleF box,float age,float ready=1,int seed=0,Color? faction=null){
  if(age<TerminalBlastAt){BuildingFireFx(g,key,box,age,3,.85f+.25f*Smooth(age/TerminalBlastAt),ready);return;}
  var spec=BuildingV08.Specs[key];float z=box.Width*LotSpan/(FrameSize*(spec.Width+spec.Depth)*Campaign.Tile*Axis),blast=age-TerminalBlastAt;
  var ink=SpriteInk(key,B08Source());var point=new PointF(ink.Left+ink.Width*.5f,ink.Top+ink.Height*.47f);if(ready<1)point.Y+=ink.Height*(1-ready)*.30f;var center=SocketPoint(box,point);
  Color fragmentColor=faction??Color.FromArgb(173,179,170);DrawBlastDebris(g,key,box,blast,seed,ready,fragmentColor,true);
  var origin=SocketPoint(box,new PointF(160,215));float main=blast/.98f,secondary=(blast-.10f)/.78f;
  if(main>=0&&main<1)DrawBlastCloud(g,origin,110*z,(1-(float)Math.Exp(-main*2))/(1-(float)Math.Exp(-2)));
  if(secondary>=0&&secondary<1)DrawBlastCloud(g,new PointF(origin.X+16*z,origin.Y+3*z),68*z,(1-(float)Math.Exp(-secondary*2))/(1-(float)Math.Exp(-2)));
  Effect(g,"dust",SocketPoint(box,new PointF(160,220)),82*z,(blast-.12f)/1.20f);
  if(blast<.065f)Cloud(g,center.X,center.Y,(16+blast*190)*z,blast,Color.FromArgb((int)(230*(1-blast/.065f)),255,244,202),47);
  DrawBlastDebris(g,key,box,blast,seed,ready,fragmentColor,false);DrawBlastEmbers(g,key,box,blast,seed);

  if(blast>.5f&&blast<1.8f)Effect(g,"smoke",SocketPoint(box,new PointF(160,211)),46*z,(blast-.5f)/1.3f);
 }
}}
