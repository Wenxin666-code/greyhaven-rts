using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Greyhaven {
partial class CommandWindow {
 internal const float DefaultZoom=1.05f;
 float zoom=DefaultZoom;PointF camera=new PointF(255,465),panPoint;bool panning,syncMarch=true;
 const float Axis=.87f, Rise=.435f;
 PointF ScreenOf(PointF w){return new PointF(MW*.5f+zoom*Axis*(w.X-camera.X-w.Y+camera.Y),MapTop+MH*.5f+zoom*Rise*(w.X-camera.X+w.Y-camera.Y));}
 PointF WorldOf(PointF s){float a=(s.X-MW*.5f)/(Axis*zoom),b=(s.Y-MapTop-MH*.5f)/(Rise*zoom);return new PointF(camera.X+(b+a)/2,camera.Y+(b-a)/2);}
 void CenterAt(PointF p){camera=new PointF(Math.Max(0,Math.Min(Campaign.Width,p.X)),Math.Max(0,Math.Min(Campaign.Height,p.Y)));}
 void Pan(float x,float y){followUnit=null;CenterAt(WorldOf(new PointF(MW/2+x,MapTop+MH/2+y)));}
 bool edgeArmed;float edgeDwell;
 void ResetEdgeScroll(){edgeArmed=false;edgeDwell=0;}
 // WM_MOUSEMOVE stops at the window border. Poll the desktop cursor while this game owns focus.
 void PollEdgeScroll(float dt){
  var screen=Cursor.Position;
  EdgeAtClient(dt,PointToClient(screen),screen,Screen.FromHandle(Handle).Bounds);
 }
 void EdgeAtClient(float dt,Point client,Point screen,Rectangle display){
  var pos=new PointF((client.X-ox)/Math.Max(.01f,scale),(client.Y-oy)/Math.Max(.01f,scale));
  int outerX=client.X<0?-1:client.X>=ClientSize.Width?1:0;
  int outerY=client.Y<0?-1:client.Y>=ClientSize.Height?1:0;
  if(outerX==0&&outerY==0){
   outerX=screen.X<=display.Left+2?-1:screen.X>=display.Right-3?1:0;
   outerY=screen.Y<=display.Top+2?-1:screen.Y>=display.Bottom-3?1:0;
  }
  EdgeScroll(dt,pos,outerX,outerY);
 }
 static float EdgeSpeed(float position,float extent){
  const float band=30;
  if(position<band)return -Math.Min(1,1-position/band);
  return position>extent-band?Math.Min(1,(position-extent+band)/band):0;
 }
 void EdgeScroll(float dt,PointF pos,int outerX,int outerY){
  if(!edgeArmed||briefing||paused||world.Won||world.Lost||drag||panning){edgeDwell=0;return;}
  bool outside=outerX!=0||outerY!=0;
  if(!outside&&(pos.X<0||pos.X>=MW||pos.Y<MapTop||pos.Y>=MapTop+MH||hits.Any(h=>h.Box.Contains(pos)))){edgeDwell=0;return;}
  float x=outside?outerX:EdgeSpeed(pos.X,MW),y=outside?outerY:EdgeSpeed(pos.Y-MapTop,MH);
  if(x==0&&y==0){edgeDwell=0;return;}
  edgeDwell+=dt;
  float ramp=Math.Max(0,Math.Min(1,(edgeDwell-.035f)/.11f));
  float speed=740*dt*ramp/Math.Max(1,(float)Math.Sqrt(x*x+y*y));
  Pan(x*speed,y*speed);
 }
 protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);var p=ToScreenPoint(e);if(briefing||paused||p.X<0||p.X>=MW||p.Y<MapTop||p.Y>=MapTop+MH)return;
  var before=WorldOf(p);zoom=Math.Max(.62f,Math.Min(3.1f,zoom*(float)Math.Pow(1.18,e.Delta/120.0)));var after=WorldOf(p);CenterAt(new PointF(camera.X+before.X-after.X,camera.Y+before.Y-after.Y));Invalidate();}
 void GroundSpace(Graphics g){var origin=ScreenOf(PointF.Empty);using(var m=new Matrix(Axis*zoom,Rise*zoom,-Axis*zoom,Rise*zoom,origin.X,origin.Y))g.MultiplyTransform(m);}
 Campaign fogWorld;int fogRevision;RectangleF[] fogRuns;
 void Shroud(Graphics g){
  if(fogWorld!=world||fogRevision!=world.FogRevision){
   var runs=new List<RectangleF>();var previous=new Dictionary<int,int>();
   for(int y=0;y<Campaign.FogRows;y++){
    var next=new Dictionary<int,int>();
    for(int x=0;x<Campaign.FogCols;){
     if(world.Seen(0,x,y)){x++;continue;}int start=x++;while(x<Campaign.FogCols&&!world.Seen(0,x,y))x++;
     int key=start*(Campaign.FogCols+1)+x,index;
     if(previous.TryGetValue(key,out index)){var r=runs[index];r.Height+=Campaign.FogSize;runs[index]=r;}
     else{index=runs.Count;runs.Add(new RectangleF(start*Campaign.FogSize,y*Campaign.FogSize,(x-start)*Campaign.FogSize+.45f,Campaign.FogSize+.45f));}
     next[key]=index;
    }
    previous=next;
   }
   fogRuns=runs.ToArray();fogWorld=world;fogRevision=world.FogRevision;
  }
  if(fogRuns.Length>0)using(var brush=new SolidBrush(C("#152323")))g.FillRectangles(brush,fogRuns);
 }
 void DrawBattle(Graphics g){
  var view=g.Save();g.SetClip(new Rectangle(0,MapTop,MW,MH));
  DrawGroundView(g);var land=g.Save();GroundSpace(g);DrawTerrainPlan(g);
  var mode=g.SmoothingMode;g.SmoothingMode=SmoothingMode.None;Shroud(g);g.SmoothingMode=mode;g.Restore(land);
  DrawOre(g);
  if(showRanges&&selection.Count==1){
   var u=selection[0];
   if(u.Deployed&&u.T.Hit>0){
    var p=ScreenOf(u.P);var anchor=ScreenOf(u.GuardHome??u.P);float r=Campaign.ScanRange(u)*zoom*(float)Math.Sqrt(2);
    using(var ring=new Pen(Color.FromArgb(118,teal),1)){
     ring.DashStyle=DashStyle.Dash;g.DrawEllipse(ring,anchor.X-Axis*r,anchor.Y-Rise*r,2*Axis*r,2*Rise*r);
    }
    float reach=u.T.Range*zoom*(float)Math.Sqrt(2);
    using(var ring=new Pen(Color.FromArgb(145,gold),1))g.DrawEllipse(ring,p.X-Axis*reach,p.Y-Rise*reach,2*Axis*reach,2*Rise*reach);
    Txt(g,"射程",p.X+Axis*reach+3,p.Y-16,10,gold);
    if(Campaign.ScanRange(u)>u.T.Range)Txt(g,u.GuardHome.HasValue?"警戒":"搜索",anchor.X+Axis*r+3,anchor.Y+2,10,teal);
   }
  }
  bool drawHarbor=world.Explored(0,Campaign.Middle(world.Harbor.Body));
  foreach(var o in world.Units.Where(a=>a.Carrier==null&&world.Visible(a)).OrderBy(a=>a.P.X+a.P.Y)){
   if(drawHarbor&&SiteDepth(world.Harbor)<=o.P.X+o.P.Y){DrawHarbor(g,world.Harbor);drawHarbor=false;}
   var p=ScreenOf(o.P);float s=o.T.Size*zoom;
   if(p.X< -s||p.X>MW+s||p.Y<MapTop-s||p.Y>MapTop+MH+s)continue;
   if(selection.Contains(o)){float ring=o.T.Foot?s*.54f:s;using(var pen=new Pen(teal,2.2f))g.DrawEllipse(pen,p.X-ring*.32f,p.Y-ring*.13f,ring*.64f,ring*.31f);}
   DrawFieldSprite(g,o,p,s);
   DrawTargetFlash(g,o,p,s);
   float age=world.Time-o.ReceiptAt;
   if(o.Side==0&&o.Alive&&o.LastReceipt>0&&age<2.3f)Txt(g,"+"+o.LastReceipt,p.X-14,p.Y-s*.33f-age*8,15,gold,true);
   if(o.Alive){float y=p.Y+s*.10f;Line(g,p.X-5,y,p.X+5,y,Team(o.Side),3);
    if(selection.Contains(o)||o.Hp<o.T.Hp||o.Ready<1){Rect(g,p.X-20,y+4,40,4,ink);Rect(g,p.X-20,y+4,40*(o.Ready<1?o.Ready:o.Hp/o.T.Hp),4,o.Ready<1?gold:teal);}
   }
  }
  if(drawHarbor)DrawHarbor(g,world.Harbor);
  foreach(var site in world.Sites){
   var p=ScreenOf(site.P);float y=p.Y+7;
   if(p.X< -60||p.X>MW+60||y<MapTop-25||y>MapTop+MH)continue;
   var c=dim;Rect(g,p.X-37,y,74,22,Color.FromArgb(208,ink));Rect(g,p.X-37,y,3,22,c);
   Txt(g,site.Name,p.X-29,y+1,14,pale,true);
  }
  foreach(var shot in world.Shots){if(!world.Explored(0,shot.A)||!world.Explored(0,shot.B))continue;var a=ScreenOf(shot.A);var b=ScreenOf(shot.B);Line(g,a.X,a.Y-5,b.X,b.Y-5,shot.Shell?gold:Team(shot.Side),2);}
  DrawPreselection(g);DrawPlannedPaths(g);
  if(markedPoint.HasValue&&world.Time-markTime<2.5f){
   var enemy=markedEnemy;if(enemy==null||enemy.Alive&&world.Visible(enemy)){
    var q=ScreenOf(enemy==null?markedPoint.Value:enemy.P);var c=markHostile?red:teal;
    using(var pen=new Pen(c,2)){g.DrawEllipse(pen,q.X-12,q.Y-7,24,14);g.DrawLine(pen,q.X-17,q.Y,q.X-8,q.Y);g.DrawLine(pen,q.X+8,q.Y,q.X+17,q.Y);}
    Txt(g,enemy==null?(markHostile?"进攻点":"移动点"):enemy.T.Name+" #"+enemy.Id,q.X+15,q.Y-14,13,c,true);
   }}
  foreach(var u in selection.Where(u=>u.Deployed&&u.Path.Count>0)){
   using(var pen=new Pen(Color.FromArgb(selection.Count==1?230:155,teal),1)){pen.DashStyle=DashStyle.Dash;g.DrawLines(pen,new[]{u.P}.Concat(u.Path).Select(ScreenOf).ToArray());}
   var finish=ScreenOf(u.Path.Last());using(var pen=new Pen(teal,1))g.DrawEllipse(pen,finish.X-3,finish.Y-2,6,4);
  }
  if(selection.Count==1&&selection[0].Rally.HasValue){var a=ScreenOf(selection[0].P);var b=ScreenOf(selection[0].Rally.Value);
   using(var pen=new Pen(teal,1.5f)){pen.DashStyle=DashStyle.Dot;g.DrawLine(pen,a,b);g.DrawEllipse(pen,b.X-8,b.Y-5,16,10);}}
  if(place!=null&&mouse.X<MW&&mouse.Y>=MapTop&&mouse.Y<MapTop+MH){
   var w=WorldOf(mouse);var r=Campaign.Footprint(place,w);var p=ScreenOf(Campaign.Middle(r));DrawLive(g,place,0,14,p,place.Size*zoom,1);
   var state=g.Save();GroundSpace(g);DrawTiles(g,r,world.CanPlace(place,w,null)?teal:red,true);if(Campaign.HasGate(place))DrawTiles(g,place.Key=="refinery"?Campaign.UnloadingLane(r):Campaign.Gate(r),gold,place.Key=="refinery");if(place.Key=="barracks")DrawTiles(g,Campaign.Muster(r),teal,true);g.Restore(state);
  }
  if(showGrid){
   foreach(var o in world.Units.Where(u=>u.Alive&&u.T.Building&&world.Visible(u)))CornerPins(g,Campaign.Footprint(o.T,o.P),Team(o.Side));
   foreach(var site in world.Sites.Where(s=>world.Explored(0,Campaign.Middle(s.Body))))CornerPins(g,site.Body,gold);
  }
  if(drag&&dragged){var r=DragRect();Rect(g,r.X,r.Y,r.Width,r.Height,Color.FromArgb(38,teal));using(var pen=new Pen(teal))g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}
  DrawObjectiveMarker(g);g.Restore(view);
 }
 void CornerPins(Graphics g,RectangleF r,Color color){
  var corners=new[]{new PointF(r.Left,r.Top),new PointF(r.Right,r.Top),new PointF(r.Right,r.Bottom),new PointF(r.Left,r.Bottom)}.Select(ScreenOf).ToArray();
  using(var pen=new Pen(color,2)){for(int i=0;i<4;i++){var p=corners[i];foreach(int d in new[]{1,3}){var n=corners[(i+d)%4];g.DrawLine(pen,p,new PointF(p.X+(n.X-p.X)*.13f,p.Y+(n.Y-p.Y)*.13f));}}}
 }
 PointF ApproachSite(PointF screen,PointF location){
  foreach(var site in world.Sites){
   var basePos=Campaign.Middle(site.Body);if(!world.Explored(0,basePos))continue;
   var q=ScreenOf(basePos);if(site.Body.Contains(location)||InkHit("harbormaster",0,BuildingRect(site.Body,q,zoom),screen))return site.P;
  }return location;
 }
 float SiteDepth(Site s){var p=Campaign.Middle(s.Body);return p.X+p.Y;}

 void DrawHarbor(Graphics g,Site site){
  var box=BuildingRect(site.Body,ScreenOf(Campaign.Middle(site.Body)),zoom);
  BuildingArt(g,"harbormaster",0,box,1);
 }
 bool RadarOnline(){return world.Power(0)>=0&&world.Live(0).Any(o=>o.T.Key=="radar");}
 void DrawRadar(Graphics g,float x){
  if(!RadarOnline()){Txt(g,"雷达离线",x+32,119,15,dim,true);Txt(g,world.Live(0).Any(o=>o.T.Key=="radar")?"恢复供电后启用":"建造雷达站后启用",x+13,148,10,dim);return;}
  var state=g.Save();g.SmoothingMode=SmoothingMode.None;g.SetClip(new RectangleF(x,88,148,100));g.TranslateTransform(x,88);g.ScaleTransform(148f/Campaign.Width,100f/Campaign.Height);
  g.DrawImage(radarGround,0,0,Campaign.Width,Campaign.Height);Shroud(g);
  foreach(var patch in world.Ore)if(patch.Stock>0&&world.Explored(0,patch.P))Rect(g,patch.P.X-5,patch.P.Y-5,14,14,gold);

  foreach(var o in world.Units.Where(a=>a.Alive&&a.Carrier==null&&world.Visible(a)))Rect(g,o.P.X-6,o.P.Y-6,o.T.Building?26:16,o.T.Building?26:16,Team(o.Side));
  foreach(var site in world.Sites.Where(s=>world.Explored(0,Campaign.Middle(s.Body))))using(var pen=new Pen(gold,5))g.DrawRectangle(pen,site.P.X-15,site.P.Y-15,30,30);
  if(world.LastEvent.HasValue&&world.Time-world.EventTime<4){
   var p=world.LastEvent.Value;float r=17+(world.Time-world.EventTime)%1*32;
   using(var pen=new Pen(red,6))g.DrawEllipse(pen,p.X-r,p.Y-r,r*2,r*2);
  }
  using(var pen=new Pen(pale,6))g.DrawPolygon(pen,new[]{new PointF(0,MapTop),new PointF(MW,MapTop),new PointF(MW,MapTop+MH),new PointF(0,MapTop+MH)}.Select(WorldOf).ToArray());g.Restore(state);
  hits.Add(new HitBox{Id="radar-map",Box=new RectangleF(x,88,148,100),Run=()=>CenterAt(new PointF((mouse.X-x)*Campaign.Width/148,(mouse.Y-88)*Campaign.Height/100))});
 }
 Piece HitScreen(PointF screen){return PickUnit(screen);}
 Bitmap MakeGround(){
  using(var materials=Raw("materials")){
   var bmp=new Bitmap(Campaign.Width,Campaign.Height,PixelFormat.Format32bppPArgb);
   using(var g=Graphics.FromImage(bmp)){
    g.SmoothingMode=SmoothingMode.AntiAlias;int w=materials.Width/2,h=materials.Height/2;
    var tiles=new TextureBrush[4];
    try{for(int i=0;i<4;i++){using(var t=materials.Clone(new Rectangle(i%2*w+2,i/2*h+2,w-4,h-4),PixelFormat.Format32bppPArgb)){
     tiles[i]=new TextureBrush(t,WrapMode.TileFlipXY);tiles[i].ScaleTransform(.38f,.38f);}}
     g.FillRectangle(tiles[0],0,0,bmp.Width,bmp.Height);g.FillRectangle(tiles[2],0,0,bmp.Width,101);
     foreach(var pad in new[]{new Rectangle(25,402,668,288),new Rectangle(800,100,880,610),new Rectangle(1230,710,290,210),
      new Rectangle(1740,410,755,300),new Rectangle(2328,804,176,398),new Rectangle(825,835,565,350),new Rectangle(225,1090,435,282)})g.FillRectangle(tiles[1],pad);
     using(var edge=new Pen(C("#bbb4a0"),8)){g.DrawLine(edge,0,100,bmp.Width,100);}
     foreach(var r in Campaign.Roads){using(var p=new Pen(C("#a09981"),5))g.DrawRectangle(p,r.X,r.Y,r.Width,r.Height);g.FillRectangle(tiles[3],r);}
     foreach(var r in Campaign.Roads){bool horiz=r.Width>r.Height;using(var pen=new Pen(C("#ccc3a6"),2)){pen.DashPattern=new[]{7f,10f};if(horiz)g.DrawLine(pen,r.Left,r.Top+r.Height/2,r.Right,r.Top+r.Height/2);else g.DrawLine(pen,r.Left+r.Width/2,r.Top,r.Left+r.Width/2,r.Bottom);}}
     foreach(var pad in new[]{new Rectangle(26,402,664,284),new Rectangle(810,110,850,585),new Rectangle(1230,715,285,201),
      new Rectangle(1745,415,745,290),new Rectangle(2333,809,166,388),new Rectangle(830,840,555,340),new Rectangle(230,1095,425,272)}){
      using(var pen=new Pen(Color.FromArgb(100,pale),2))g.DrawRectangle(pen,pad);
      for(int i=pad.Left+20;i<pad.Right-14;i+=34)Line(g,i,pad.Bottom-29,i,pad.Bottom-8,Color.FromArgb(128,pale),2);
     }
     foreach(int y in new[]{316,400,727,803})for(int i=0;i<7;i++)Rect(g,737+i*8,y,4,8,pale);
     for(int x=80;x<bmp.Width;x+=144){using(var tire=new Pen(C("#454946"),4))g.DrawEllipse(tire,x,91,13,9);}
     for(int i=12;i<bmp.Width;i+=48){Rect(g,i,97,7,5,ink);Rect(g,i,97,6,2,gold);}
     foreach(var s in world.Sites){for(int i=0;i<7;i++)Line(g,s.P.X-42+i*13,s.P.Y+32,s.P.X-36+i*13,s.P.Y+20,gold,3);}
    }finally{foreach(var t in tiles)if(t!=null)t.Dispose();}
   }return bmp;
  }
 }
 static Bitmap Raw(string name){using(var stream=typeof(CommandWindow).Assembly.GetManifestResourceStream("Greyhaven.Art."+name+".png"))using(var src=new Bitmap(stream)){
  var bmp=new Bitmap(src.Width,src.Height,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(bmp)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(src,0,0);}return bmp;
 }}
}
}
