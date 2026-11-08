using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Linq;
namespace Greyhaven {
partial class CommandWindow {
 Bitmap projectedRadar;Campaign radarWorld;int radarRevision=-1;
 PointF RadarPoint(PointF p){float span=world.MapWidth+world.MapHeight;return new PointF((p.X-p.Y+world.MapHeight)*148/span,(p.X+p.Y)*100/span);}
 PointF RadarWorldPoint(PointF p){float span=world.MapWidth+world.MapHeight,u=p.X*span/148-world.MapHeight,v=p.Y*span/100;return new PointF((u+v)*.5f,(v-u)*.5f);}
 void RefreshProjectedRadar(){
  if(projectedRadar==null)projectedRadar=new Bitmap(148,100,PixelFormat.Format32bppArgb);
  if(radarWorld==world&&radarRevision==world.FogRevision)return;
  var pixels=new byte[148*100*4];
  for(int y=0;y<100;y++)for(int x=0;x<148;x++){
   var p=RadarWorldPoint(new PointF(x+.5f,y+.5f));var c=Color.FromArgb(16,27,32);
   if(p.X>=0&&p.Y>=0&&p.X<world.MapWidth&&p.Y<world.MapHeight&&world.Explored(0,p)){
    if(world.River==null)c=radarGround.GetPixel(Math.Min(radarGround.Width-1,(int)(p.X/world.MapWidth*radarGround.Width)),Math.Min(radarGround.Height-1,(int)(p.Y/world.MapHeight*radarGround.Height)));
    else{var tile=world.River.At((int)(p.X/Campaign.Tile),(int)(p.Y/Campaign.Tile));c=tile.Ground==RiverbendMap.Surface.Water?C("#365f6b"):tile.Tree?C("#405a3d"):tile.Ground==RiverbendMap.Surface.Road||tile.Ground==RiverbendMap.Surface.Bridge||tile.Ground==RiverbendMap.Surface.Ramp?C("#96998a"):tile.Height>1?C("#969c76"):C("#758b61");}
   }
   int i=(y*148+x)*4;pixels[i]=c.B;pixels[i+1]=c.G;pixels[i+2]=c.R;pixels[i+3]=255;
  }
  var bits=projectedRadar.LockBits(new Rectangle(0,0,148,100),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{Marshal.Copy(pixels,0,bits.Scan0,pixels.Length);}finally{projectedRadar.UnlockBits(bits);}radarWorld=world;radarRevision=world.FogRevision;
 }
 void DrawRadar(Graphics g,float x){
  if(!RadarOnline()){Txt(g,"雷达离线",x+32,119,15,dim,true);Txt(g,world.Live(0).Any(o=>o.T.Key=="radar")?"恢复供电后启用":"建造雷达站后启用",x+13,148,10,dim);return;}
  RefreshProjectedRadar();var state=g.Save();g.IntersectClip(new RectangleF(x,88,148,100));g.DrawImage(projectedRadar,new RectangleF(x,88,148,100));
  foreach(var ore in world.Ore)if(ore.Stock>0&&world.Explored(0,ore.P)){var p=RadarPoint(ore.P);Rect(g,x+p.X,88+p.Y,1.5f,1.5f,gold);}
  foreach(var o in world.Units.Where(o=>o.Alive&&o.Carrier==null&&world.Visible(o))){var p=RadarPoint(o.P);float size=o.T.Building?2.5f:1.8f;Rect(g,x+p.X-size/2,88+p.Y-size/2,size,size,Team(o.Side));}
  if(world.LastEvent.HasValue&&world.Time-world.EventTime<5){var p=RadarPoint(world.LastEvent.Value);using(var pen=new Pen(gold,1))g.DrawRectangle(pen,x+p.X-3,88+p.Y-3,6,6);}
  var center=RadarPoint(camera);float span=world.MapWidth+world.MapHeight;
  float width=MW/zoom/Axis*148/span,height=MH/zoom/Rise*100/span;
  using(var pen=new Pen(pale,1))g.DrawRectangle(pen,x+center.X-width/2,88+center.Y-height/2,width,height);
  g.Restore(state);
  hits.Add(new HitBox{Id="radar-map",Box=new RectangleF(x,88,148,100),Run=()=>RadarAction(RadarWorldPoint(new PointF(mouse.X-x,mouse.Y-88)),false),Right=()=>RadarAction(RadarWorldPoint(new PointF(mouse.X-x,mouse.Y-88)),true)});
 }
}
}
