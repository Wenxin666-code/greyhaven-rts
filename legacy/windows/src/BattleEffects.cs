using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace Greyhaven {
partial class CommandWindow {
 const int EffectCell=96,EffectFrames=24;
 static float Clamp01(float value){return Math.Max(0,Math.Min(1,value));}
 static float Noise(int seed){return ((seed*1103515245+12345)&0x7fffffff)/2147483647f;}
 static void Cloud(Graphics g,float x,float y,float radius,float phase,Color color,int seed){
  var points=new PointF[12];
  for(int i=0;i<points.Length;i++){float a=i*(float)Math.PI/6,r=radius*(.81f+Noise(seed+i*17)*.24f);points[i]=new PointF(x+(float)Math.Cos(a)*r,y+(float)Math.Sin(a)*r*.84f);}
  using(var brush=new SolidBrush(color))g.FillPolygon(brush,points);
 }
 void EnsureEffect(string type){
  string key="fx-"+type;if(painted.ContainsKey(key))return;
  var sheet=new Bitmap(EffectCell*8,EffectCell*3,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(sheet)){
   g.SmoothingMode=SmoothingMode.AntiAlias;
   for(int i=0;i<EffectFrames;i++){
    float t=i/(float)(EffectFrames-1);var save=g.Save();g.SetClip(new Rectangle(i%8*EffectCell,i/8*EffectCell,EffectCell,EffectCell));g.TranslateTransform(i%8*EffectCell,i/8*EffectCell);
    if(type=="blast"||type=="fire"){
     for(int j=0;j<8;j++){
      float a=j*.79f+Noise(j*29),spread=(6+22*t)*(1-t*.4f),x=48+(float)Math.Cos(a)*spread,y=55+(float)Math.Sin(a)*spread*.60f-t*18;
      float r=(7+9*(float)Math.Sin(t*Math.PI))*(1-t*.75f),fade=Clamp01((1-t)*1.8f);
      Cloud(g,x,y,r,t,Color.FromArgb((int)(170*fade),92+(int)(72*(1-t)),62,37),j*23);
      if(t<.65f){Cloud(g,x-1,y-2,r*.66f,t,Color.FromArgb((int)(230*fade),249,135+(int)(60*(1-t)),44),j*23);Cloud(g,x-1,y-3,r*.32f,t,Color.FromArgb((int)(240*fade),255,233,165),j*23);}
     }
    }else if(type=="smoke"||type=="dust"){
     for(int j=0;j<7;j++){
      float k=Noise(j*47+11),x=48+(k-.5f)*(12+38*t)+t*9,y=type=="dust"?61+(Noise(j*67)-.5f)*15-t*7:73-t*35-j*2;
      float r=type=="dust"?5+t*15:6+t*12,fade=(float)Math.Sin(t*Math.PI);
      var c=type=="dust"?Color.FromArgb((int)(55*fade),143,131,107):Color.FromArgb((int)(65*fade),73,75,69);
      Cloud(g,x,y,r,t,c,j*31);
     }
    }else{
     // Short angular chips / sparks. No thick rings or selection-like outlines.
     for(int j=0;j<11;j++){
      float a=j*2.399f,dist=(4+31*t)*(0.45f+Noise(j*43)*.55f),x=48+(float)Math.Cos(a)*dist,y=54+(float)Math.Sin(a)*dist*.7f+t*t*9;
      var c=type=="weld"?Color.FromArgb((int)(255*(1-t)),193,226,238):Color.FromArgb((int)(255*(1-t)),250,205,117);
      using(var pen=new Pen(c,1.4f))g.DrawLine(pen,x,y,x-(float)Math.Cos(a)*(5*(1-t)+1),y-(float)Math.Sin(a)*3*(1-t));
     }
     if(t<.28f)Cloud(g,48,54,6*(1-t/.28f),t,Color.FromArgb(240,255,243,200),13);
    }
    g.Restore(save);
   }
  }
  painted[key]=sheet;
 }
 void Effect(Graphics g,string type,PointF anchor,float size,float progress){
  if(progress<0||progress>=1)return;var box=new RectangleF(anchor.X-size*.5f,anchor.Y-size*.58f,size,size);
  if(!g.IsVisible(box))return;EnsureEffect(type);int frame=Math.Min(EffectFrames-1,(int)(progress*EffectFrames));
  DrawStamp(g,"fx-"+type,new Rectangle(frame%8*EffectCell,frame/8*EffectCell,EffectCell,EffectCell),box);
 }
 PointF Muzzle(Shot s){
  if(s.Garrison){var window=ScreenOf(s.A);window.Y-=18*zoom;return window;}
  if(s.Weapon=="tank")return TankMuzzle(s);
  var t=Campaign.Info(s.Weapon);var p=ScreenOf(s.A);float x=0,y=0,z=0,width=1,anchor=188f/320;
  switch(s.Weapon){
   case "squad":x=.13f;y=1.277f;z=.89f;width=2.7f;anchor=.91f;break;
   case "sniper":x=.13f;y=1.277f;z=1.089f;width=2.7f;anchor=.91f;break;
   case "at":x=s.Rocket?.26f:.13f;y=s.Rocket?1.54f:1.277f;z=s.Rocket?.73f:.89f;width=2.7f;anchor=.91f;break;
   case "ifv":x=-.27f;y=2.27f;z=1.755f;width=7.5f;break;
   case "arty":y=3.16f;z=3.36f;width=9.2f;break;
   case "post":{
    var r=BuildingRect(Campaign.Footprint(t,s.A),p,zoom);var muzzle=BuildingV08.Specs["post"].Muzzle;
    return new PointF(r.X+r.Width*muzzle.X/FrameSize,r.Y+r.Height*muzzle.Y/FrameSize);
   }
  }
  // Use precisely the same view quantization as the body sprite.
  float heading=((int)Math.Round(s.Heading)+14)%16;
  if(t.Foot){
   y-=.34f*s.Stance;z+=.05f*s.Stance;
   float angle,py,pz;
   if(s.Weapon=="at"&&s.Rocket){angle=.12f*(1-s.Aim);py=1.54f-.34f*s.Stance;pz=.19f+.05f*s.Stance;}
   else{angle=.43f-.455f*s.Aim;py=1.25f-.34f*s.Stance;pz=.20f+.05f*s.Stance;}
   float dy=y-py,dz=z-pz,co=(float)Math.Cos(angle),si=(float)Math.Sin(angle);
   y=py+dy*co-dz*si+(s.Weapon=="at"&&s.Rocket?.02f:.29f)*s.Aim;
   z=pz+dy*si+dz*co-.02f*s.Aim;
  }
  if(t.Foot)heading=(int)heading/2*2;
  float a=heading*(float)Math.PI/8,c=(float)Math.Cos(a),sn=(float)Math.Sin(a),rx=x*c+z*sn,rz=z*c-x*sn;
  var box=t.Foot?SoldierBox(p,t.Size*zoom):VehicleRect(t,p,t.Size*zoom);
  return new PointF(box.X+box.Width*(.5f+rx/width),box.Y+box.Height*(anchor+(rz*.5f-y*.8660254f)/width));
 }
 void MuzzleFlash(Graphics g,Shot s,float age){
  bool cannon=s.Weapon=="tank"||s.Weapon=="arty",rocket=s.Rocket;
  float duration=cannon?.10f:rocket?.14f:.045f;
  if(!cannon&&!rocket){if(age>.14f)return;age%=.055f;}
  if(age>duration)return;var p=Muzzle(s);var target=ScreenOf(s.B);target.Y-=s.ImpactLift*zoom;
  float dx=target.X-p.X,dy=target.Y-p.Y,len=(float)Math.Sqrt(dx*dx+dy*dy);if(len<1)return;dx/=len;dy/=len;
  float size=(cannon?13:rocket?8:4.5f)*zoom*(1-age/duration*.7f),wide=size*.38f;
  var points=new[]{new PointF(p.X-dx*size*.15f,p.Y-dy*size*.15f),new PointF(p.X-dy*wide,p.Y+dx*wide),new PointF(p.X+dx*size*.45f-dy*wide*.45f,p.Y+dy*size*.45f+dx*wide*.45f),new PointF(p.X+dx*size,p.Y+dy*size),new PointF(p.X+dx*size*.4f+dy*wide,p.Y+dy*size*.4f-dx*wide),new PointF(p.X+dy*wide*.45f,p.Y-dx*wide*.45f)};
  using(var b=new SolidBrush(Color.FromArgb(220,247,173,73)))g.FillPolygon(b,points);
  using(var pen=new Pen(Color.FromArgb(250,255,239,185),Math.Max(.7f,zoom*1.5f)))g.DrawLine(pen,p.X,p.Y,p.X+dx*size*.70f,p.Y+dy*size*.70f);
 }
 void ShotImpact(Graphics g,Shot s){
  if(!world.Explored(0,s.B))return;float age=world.VisualTime-s.Born;var p=ScreenOf(s.B);p.Y-=s.ImpactLift*zoom;
  bool explosive=s.Rocket||s.Weapon=="tank"||s.Weapon=="arty";
  if(explosive){Effect(g,"blast",p,(s.Weapon=="arty"?48:s.Weapon=="tank"?35:29)*zoom,age/.75f);Effect(g,"smoke",p,38*zoom,(age-.12f)/1.0f);}
  else Effect(g,s.HitFoot?"dust":"spark",p,(s.HitFoot?12:17)*zoom,age/.30f);
 }
 void DrawShotTrails(Graphics g){
  foreach(var s in world.Shots){
   float age=world.VisualTime-s.Born;
   if(age>.10f||!world.Explored(0,s.A)||!world.Explored(0,s.B))continue;
   var a=Muzzle(s);var b=ScreenOf(s.B);b.Y-=s.ImpactLift*zoom;
   // Impact and damage are simultaneous in this game. These are brief tracer
   // afterimages, not delayed cosmetic projectiles arriving after a target dies.
   float alpha=(1-age/.10f)*175;
   using(var pen=new Pen(Color.FromArgb((int)alpha,231,210,153),Math.Max(.7f,zoom*(s.Weapon=="squad"?.45f:.8f))))g.DrawLine(pen,a,b);
  }
 }
 PointF BuildingDamagePoint(Piece o,PointF p){
  var box=BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom);var ink=SpriteInk(o.T.Key,new Rectangle(0,0,FrameSize,FrameSize));return new PointF(box.X+(ink.Left+ink.Width*.55f)*box.Width/FrameSize,box.Y+(ink.Top+ink.Height*.35f)*box.Height/FrameSize);
 }
 void WeaponResidue(Graphics g,Shot shot,float age){
  if(age<0||age>.40f)return;var p=Muzzle(shot);
  if((shot.Weapon=="tank"||shot.Weapon=="arty")&&age>.035f)Effect(g,"smoke",p,15*zoom,(age-.035f)/.55f);
  if(shot.Weapon=="squad"||shot.Weapon=="sniper"||shot.Weapon=="at"&&!shot.Rocket||shot.Weapon=="ifv"&&!shot.Rocket){
   float t=age/.4f,a=shot.Heading*(float)Math.PI/8,x=p.X+(float)Math.Cos(a)*(2+9*t)*zoom,y=p.Y+(float)Math.Sin(a)*(2+9*t)*zoom*.5f-7*zoom*(float)Math.Sin(t*Math.PI)+6*zoom*t*t;
   var st=g.Save();g.TranslateTransform(x,y);g.RotateTransform(t*330);Rect(g,-zoom,-zoom*.4f,2*zoom,.8f*zoom,Color.FromArgb((int)(225*(1-t)),194,164,94));g.Restore(st);
  }
 }
 void DrawActorEffects(Graphics g,Piece o,PointF p){
  float now=world.VisualTime;
  foreach(var s in world.Shots){
   if(s.SourceId==o.Id&&world.Explored(0,s.A)){MuzzleFlash(g,s,now-s.Born);WeaponResidue(g,s,now-s.Born);}
   if(s.TargetId==o.Id)ShotImpact(g,s);
  }
  if(o.T.Building&&BuildingV08.IsBuilding(o.T.Key)){StructuralActorEffects(g,o,p);return;}
  if(!o.Alive){
   if(o.T.Foot){Effect(g,"dust",p,14*zoom,o.Dead/.65f);return;}
   float size=(o.T.Building?(o.T.FoundationWidth+o.T.FoundationHeight)*16:37)*zoom;
   var center=o.T.Building?BuildingDamagePoint(o,p):new PointF(p.X,p.Y-size*.23f);
   Effect(g,"blast",center,size,o.Dead/.8f);
   if(o.T.Building){Effect(g,"blast",new PointF(center.X+size*.23f,center.Y+size*.13f),size*.65f,(o.Dead-.14f)/.8f);Effect(g,"dust",p,size*1.15f,(o.Dead-.15f)/1.5f);}
   if(o.Dead>.25f&&o.Dead<4.8f)Effect(g,"smoke",center,size*.8f,((o.Dead-.25f)%1.6f)/1.6f);
   if(o.Dead<.9f)for(int i=0;i<7;i++){
    float a=i*2.399f+o.Id,d=size*.45f*o.Dead*(.6f+Noise(i*7+o.Id)*.4f),x=p.X+(float)Math.Cos(a)*d,y=p.Y+(float)Math.Sin(a)*d*.45f-size*.5f*o.Dead*(1-o.Dead);
    Rect(g,x,y,Math.Max(1,zoom*1.3f),Math.Max(1,zoom),Color.FromArgb((int)(210*(1-o.Dead)),85,81,66));
   }
   return;
  }
  if(!o.T.Foot&&o.Hp<o.T.Hp*.45f){
   float size=(o.T.Building?34:21)*zoom;var smoke=o.T.Building?BuildingDamagePoint(o,p):new PointF(p.X+size*.16f,p.Y-size*.55f);
   Effect(g,"smoke",smoke,size,(now*.65f+o.Id*.13f)%1);
   if(o.Hp<o.T.Hp*.25f)Effect(g,"fire",smoke,size*.58f,(now*1.7f+o.Id*.21f)%1);
  }
  if(o.T.Building&&now-o.CompletedAt<.55f)Effect(g,"dust",p,50*zoom,(now-o.CompletedAt)/.55f);
  if(o.T.Building&&o.Queue.Count>0){var box=BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom);Rect(g,p.X+box.Width*.10f,p.Y-box.Height*.18f,2*zoom,zoom,(int)(now*3)%2==0?gold:teal);}
  if(now-o.RepairAt<.40f&&world.Explored(0,o.RepairPoint)){var q=ScreenOf(o.RepairPoint);q.Y-=4*zoom;Effect(g,"weld",q,15*zoom,(now-o.RepairAt)/.40f);}
  if(o.T.Key=="miner"&&o.Mining&&o.Path.Count==0&&!o.Returning&&!o.Surveying&&o.Patch!=null&&Campaign.Dist(o.P,o.Patch.P)<33)Effect(g,"dust",p,24*zoom,(now*1.2f+o.Id*.23f)%1);
  if(!o.T.Foot&&!o.T.Building&&now-o.MovedAt<.10f)Effect(g,"dust",new PointF(p.X,p.Y+2*zoom),19*zoom,(now*.9f+o.Id*.17f)%1);
 }
}
}
