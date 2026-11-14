using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
namespace Greyhaven {partial class CommandWindow {
 const int AmbientCell=128,AmbientFrames=96;
 static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
 void EnsureAmbient(string kind){
  string key="ambient/"+kind;if(painted.ContainsKey(key))return;
  var sheet=new Bitmap(AmbientCell*8,AmbientCell*12,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(sheet)){
   g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   for(int frame=0;frame<AmbientFrames;frame++){
    var state=g.Save();g.SetClip(new Rectangle(frame%8*AmbientCell,frame/8*AmbientCell,AmbientCell,AmbientCell));g.TranslateTransform(frame%8*AmbientCell,frame/8*AmbientCell);
    float t=frame/(float)AmbientFrames;
    if(kind=="weld"){
     float glow=(float)Math.Pow(Math.Max(0,Math.Sin(t*Math.PI*6)),6);
     for(int i=0;i<12;i++){float age=(t*2+i/12f)%1,a=i*2.399f,d=age*39,x=64+(float)Math.Cos(a)*d,y=109+(float)Math.Sin(a)*d*.38f+age*age*12;using(var pen=new Pen(Color.FromArgb((int)(190*(1-age)*glow),235,212,139),1))g.DrawLine(pen,x,y,x-(float)Math.Cos(a)*3,y-2);}
     Cloud(g,64,109,3+glow*5,t,Color.FromArgb((int)(210*glow),192,225,237),7);
    }else{
     bool dust=kind=="mill",steam=kind=="steam",diesel=kind=="diesel";int count=dust?8:11;
     for(int i=0;i<count;i++){
      float age=(t*2+i/(float)count)%1,seed=Noise(i*97+21),rise=(dust?31:87)*age,drift=(dust?36:24)*age+(float)Math.Sin(age*4+i)*age*5;
      float x=64+drift+(seed-.5f)*(dust?20:7)*age,y=113-rise,r=(dust?3:2)+(dust?15:steam?12:diesel?7:13)*age;
      float fade=(float)Math.Sin(Math.PI*age)*(dust?.50f:steam?.54f:diesel?.32f:.74f);int alpha=(int)(105*fade);
      Color shade=dust?Color.FromArgb(alpha,159,145,111):diesel?Color.FromArgb(alpha,112,117,112):Color.FromArgb(alpha,182,190,187);
      Cloud(g,x+1,y+1,r,t,shade,i*17+3);Cloud(g,x-1,y-1,r*.78f,t,Color.FromArgb((int)(alpha*.72f),dust?188:210,dust?174:216,dust?138:211),i*17+9);
      if(!diesel)Cloud(g,x-r*.20f,y-r*.20f,r*.41f,t,Color.FromArgb((int)(alpha*.45f),dust?204:229,dust?190:232,dust?156:226),i*17+13);
     }
    }
    g.Restore(state);
   }
  }
  painted.Add(key,sheet);
 }
 void Ambient(Graphics g,string kind,PointF anchor,float size,float time,float age=10){
  var box=new RectangleF(anchor.X-size*.5f,anchor.Y-size*113/AmbientCell,size,size);if(!g.IsVisible(box))return;
  EnsureAmbient(kind);int frame=(int)(Math.Max(0,time)*24)%AmbientFrames;
  var state=g.Save();if(age<2&&kind!="weld"){float rise=Smooth(age/2)*size;g.IntersectClip(new RectangleF(box.Left,anchor.Y-rise,box.Width,rise+size*.12f));}
  DrawStamp(g,"ambient/"+kind,new Rectangle(frame%8*AmbientCell,frame/8*AmbientCell,AmbientCell,AmbientCell),box);g.Restore(state);
 }
 static PointF SocketPoint(RectangleF box,PointF source){return new PointF(box.X+box.Width*source.X/FrameSize,box.Y+box.Height*source.Y/FrameSize);}
 void FacilityFx(Graphics g,string key,RectangleF box,float time,bool operating,bool working,float age=10){
  if(!operating)return;BuildingV08Spec spec;if(!BuildingV08.Specs.TryGetValue(key,out spec))return;
  float z=box.Width*LotSpan/(FrameSize*(spec.Width+spec.Depth)*Campaign.Tile*Axis);
  foreach(var group in spec.Sockets){int i=0;foreach(var socket in group.Value){var p=SocketPoint(box,socket);float clock=time+i++*.71f;
   if(group.Key=="smoke")Ambient(g,"chimney",p,52*z,clock,age);
   else if(group.Key=="steam"&&(key!="factory"||working))Ambient(g,"steam",p,26*z,clock*.7f,age);
   else if(group.Key=="dust"&&working)Ambient(g,"mill",p,31*z,clock,age);
   else if(group.Key=="weld"&&working)Ambient(g,"weld",p,27*z,clock);
   else if(group.Key=="signal"){
    float pulse=(float)Math.Pow(.5+.5*Math.Sin(clock*2.6),12);if(pulse>.08f){int alpha=(int)(210*pulse);using(var pen=new Pen(Color.FromArgb(alpha,246,201,112),Math.Max(.6f,z*.65f))){g.DrawLine(pen,p.X-3*z,p.Y,p.X+3*z,p.Y);g.DrawLine(pen,p.X,p.Y-3*z,p.X,p.Y+3*z);} }
   }
  }}
 }
 Campaign facilityWorld;float facilityAt=-1;readonly bool[] facilityPower=new bool[Campaign.MaxFactions];
 bool FacilityOperating(Piece o){
  if(o.Side<0||o.T.Key=="power"||Campaign.IsRelayStation(o))return true;
  if(facilityWorld!=world||facilityAt!=world.VisualTime){facilityWorld=world;facilityAt=world.VisualTime;for(int i=0;i<Campaign.MaxFactions;i++)facilityPower[i]=world.Power(i)>=0;}
  return o.Side<Campaign.MaxFactions&&facilityPower[o.Side];
 }
 class FacilityClock {internal float Phase,Last,Completed,Ramp;internal bool On;}
 Campaign clockWorld;readonly Dictionary<int,FacilityClock> facilityClocks=new Dictionary<int,FacilityClock>();
 float FacilityPhase(Piece o){
  if(clockWorld!=world){clockWorld=world;facilityClocks.Clear();}
  FacilityClock c;float now=world.VisualTime;bool on=FacilityOperating(o);
  if(!facilityClocks.TryGetValue(o.Id,out c)||c.Completed!=o.CompletedAt){if(facilityClocks.Count>2048)facilityClocks.Clear();float age=o.CompletedAt>=0?Math.Max(0,now-o.CompletedAt):10;facilityClocks[o.Id]=c=new FacilityClock{Phase=o.CompletedAt>=0?Math.Max(0,age-.5f):now+o.Id*.371f,Last=now,Completed=o.CompletedAt,On=on,Ramp=age};return c.Phase;}
  float dt=Math.Max(0,now-c.Last);if(dt>0){if(on){if(!c.On)c.Ramp=0;float before=Math.Min(1,c.Ramp);c.Ramp+=dt;c.Phase+=dt*(before+Math.Min(1,c.Ramp))*.5f;}c.Last=now;c.On=on;}return c.Phase;
 }
 void DrawFacilityEffects(Graphics g,Piece o,PointF p){
  if(!o.Alive||o.Ready<1||o.Packing>0)return;
  if(o.T.Building){if(o.T.Civilian)return;bool working=o.Queue.Count>0&&!o.ProductionPaused&&o.Side>=0&&world.Funds[o.Side]>0||o.Door>0||o.T.Key=="refinery"&&(o.LastReceipt>0&&world.Time-o.ReceiptAt<5||world.Units.Exists(u=>u.Alive&&u.Dock==o&&u.Unload>0));FacilityFx(g,o.T.Key,BuildingRect(Campaign.Footprint(o.T,o.P),p,zoom),FacilityPhase(o),FacilityOperating(o),working,o.CompletedAt>=0?Math.Max(0,world.VisualTime-o.CompletedAt):10);return;}
  if(o.T.Foot)return;
  if(o.T.Key=="tank"||o.T.Key=="ifv"||o.T.Key=="mcv"||o.T.Key=="miner"||o.T.Key=="arty"){
   // Engine haze follows the rear of the actual oriented chassis.
   float angle=((o.Turn+14)%16)*(float)Math.PI/8,localX=-.48f,localZ=-1.5f,width=o.T.Key=="mcv"?8.35f:o.T.Key=="tank"?9.25f:o.T.Key=="miner"?8.1f:o.T.Key=="arty"?9.2f:7.5f;
   float x=localX*(float)Math.Cos(angle)+localZ*(float)Math.Sin(angle),z=localZ*(float)Math.Cos(angle)-localX*(float)Math.Sin(angle);var body=VehicleRect(o.T,p,o.T.Size*zoom);var exhaust=new PointF(body.X+body.Width*(.5f+x/width),body.Y+body.Height*(188f/320+(z*.5f-.7f*.8660254f)/width));
   if(world.Time-o.MovedAt<.15f)Ambient(g,"diesel",exhaust,17*zoom,world.VisualTime+o.Id*.37f);
  }
 }
 static float BaseProgress(Piece o){return o.Packing>0?o.Packing/Campaign.BaseDeploySeconds:o.Ready;}
 PointF DeploymentPoint(Piece o,PointF p){if(!o.BaseTransition)return p;float q=BaseProgress(o),weight=1-Smooth(q/.24f);var a=ScreenOf(o.DeployOrigin);var b=ScreenOf(o.P);return new PointF(p.X+(a.X-b.X)*weight,p.Y+(a.Y-b.Y)*weight);}
}}
