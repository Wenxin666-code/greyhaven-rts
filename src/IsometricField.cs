using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;

namespace Greyhaven {
partial class CommandWindow {
 float zoom=1.7f;PointF camera=new PointF(255,465),panPoint;bool panning,sweep,syncMarch=true;
 const float Axis=.87f, Rise=.435f;
 PointF ScreenOf(PointF w){return new PointF(MW*.5f+zoom*Axis*(w.X-camera.X-w.Y+camera.Y),MapTop+MH*.5f+zoom*Rise*(w.X-camera.X+w.Y-camera.Y));}
 PointF WorldOf(PointF s){float a=(s.X-MW*.5f)/(Axis*zoom),b=(s.Y-MapTop-MH*.5f)/(Rise*zoom);return new PointF(camera.X+(b+a)/2,camera.Y+(b-a)/2);}
 void CenterAt(PointF p){camera=new PointF(Math.Max(0,Math.Min(Campaign.Width,p.X)),Math.Max(0,Math.Min(Campaign.Height,p.Y)));}
 void Pan(float x,float y){CenterAt(WorldOf(new PointF(MW/2+x,MapTop+MH/2+y)));}
 bool pointerInside;float edgeDwell;
 void ResetEdgeScroll(){pointerInside=false;edgeDwell=0;}
 protected override void OnMouseLeave(EventArgs e){base.OnMouseLeave(e);ResetEdgeScroll();}
 static float EdgeSpeed(float position,float extent){
  const float band=22;
  if(position<band)return -(1-position/band);
  return position>extent-band?(position-extent+band)/band:0;
 }
 void EdgeScroll(float dt){
  if(!pointerInside||briefing||paused||world.Won||world.Lost||drag||panning||
     mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>=MapTop+MH){edgeDwell=0;return;}
  float x=EdgeSpeed(mouse.X,MW),y=EdgeSpeed(mouse.Y-MapTop,MH);
  if((x==0&&y==0)||hits.Any(h=>h.Box.Contains(mouse))){edgeDwell=0;return;}
  edgeDwell+=dt;if(edgeDwell<=.12f)return;
  float ramp=Math.Min(1,(edgeDwell-.12f)/.16f);
  float speed=660*dt*ramp/Math.Max(1,(float)Math.Sqrt(x*x+y*y));
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
 // Cache only immutable terrain, at physical display resolution. The extra
 // border lets small pans reuse the same pixels without rerasterizing the map.
 const int TerrainMargin=144;
 Bitmap terrainView,radarGround;
 PointF terrainCamera;float terrainZoom,terrainScale;
 const float TerrainDensity=2;
 Bitmap projectedGround;
 Bitmap ProjectTerrain(){
  var map=new Bitmap((int)Math.Ceiling((Campaign.Width+Campaign.Height)*Axis*TerrainDensity)+2,
   (int)Math.Ceiling((Campaign.Width+Campaign.Height)*Rise*TerrainDensity)+2,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(map))
  using(var m=new Matrix(Axis*TerrainDensity,Rise*TerrainDensity,-Axis*TerrainDensity,Rise*TerrainDensity,Campaign.Height*Axis*TerrainDensity,0)){
   g.Clear(C("#132122"));g.Transform=m;g.InterpolationMode=InterpolationMode.Bilinear;g.DrawImage(ground,0,0,ground.Width,ground.Height);
  }
  return map;
 }
 void DrawGroundView(Graphics g){
  int w=(int)Math.Ceiling(MW*scale)+2*TerrainMargin,h=(int)Math.Ceiling(MH*scale)+2*TerrainMargin;
  var anchor=ScreenOf(terrainCamera);
  int dx=(int)Math.Round((anchor.X-MW*.5f)*scale),dy=(int)Math.Round((anchor.Y-MapTop-MH*.5f)*scale);
  if(terrainView==null||terrainView.Width!=w||terrainView.Height!=h||zoom!=terrainZoom||scale!=terrainScale||Math.Abs(dx)>=TerrainMargin||Math.Abs(dy)>=TerrainMargin){
   if(terrainView==null||terrainView.Width!=w||terrainView.Height!=h){
    if(terrainView!=null)terrainView.Dispose();terrainView=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
   }
   terrainCamera=camera;terrainScale=scale;terrainZoom=zoom;dx=dy=0;
   using(var bg=Graphics.FromImage(terrainView)){
    bg.Clear(C("#132122"));bg.InterpolationMode=InterpolationMode.Bilinear;
    var origin=ScreenOf(new PointF(0,Campaign.Height));
    var top=ScreenOf(PointF.Empty);
    float k=zoom*scale/TerrainDensity;
    var dst=new RectangleF(TerrainMargin+origin.X*scale,TerrainMargin+(top.Y-MapTop)*scale,projectedGround.Width*k,projectedGround.Height*k);
    var draw=RectangleF.Intersect(dst,new RectangleF(0,0,w,h));
    if(!draw.IsEmpty)bg.DrawImage(projectedGround,draw,new RectangleF((draw.X-dst.X)/k,(draw.Y-dst.Y)/k,draw.Width/k,draw.Height/k),GraphicsUnit.Pixel);
   }
  }
  var state=g.Save();g.ResetTransform();
  g.DrawImageUnscaled(terrainView,(int)Math.Round(ox)+dx-TerrainMargin,(int)Math.Round(oy+MapTop*scale)+dy-TerrainMargin);g.Restore(state);
 }
 void DrawBattle(Graphics g){
  var view=g.Save();g.SetClip(new Rectangle(0,MapTop,MW,MH));
  DrawGroundView(g);var land=g.Save();GroundSpace(g);DrawTerrainPlan(g);
  if(world.Hold>0){float bx=world.Sites[2].P.X-175*(1-world.Hold/world.HoldTime);Rect(g,bx,41,85,23,ink);Rect(g,bx+5,44,18,16,pale);for(int i=0;i<3;i++)Rect(g,bx+26+i*16,44,13,16,dim);}
  var mode=g.SmoothingMode;g.SmoothingMode=SmoothingMode.None;Shroud(g);g.SmoothingMode=mode;g.Restore(land);
  DrawOre(g);
  if(selection.Count==1){
   var u=selection[0];
   if(u.Deployed&&u.T.Hit>0){
    var p=ScreenOf(u.P);float r=u.T.LockRange*zoom*(float)Math.Sqrt(2);
    using(var ring=new Pen(Color.FromArgb(118,teal),1)){
     ring.DashStyle=DashStyle.Dash;g.DrawEllipse(ring,p.X-Axis*r,p.Y-Rise*r,2*Axis*r,2*Rise*r);
    }
   }
  }
  foreach(var site in world.Sites){
   var p=ScreenOf(site.P);float rx=Axis*zoom*1.414214f*Greyhaven.Site.CaptureRadius,ry=rx*Rise/Axis;
   using(var pen=new Pen(Color.FromArgb(163,SiteColor(site)),1.3f)){pen.DashStyle=DashStyle.Dash;g.DrawEllipse(pen,p.X-rx,p.Y-ry,2*rx,2*ry);}
  }
  var objectives=new Queue<Site>(world.Sites.Where(site=>world.Explored(0,Campaign.Middle(site.Body))).OrderBy(SiteDepth));
  foreach(var o in world.Units.Where(a=>a.Carrier==null&&world.Visible(a)).OrderBy(a=>a.P.X+a.P.Y)){
   while(objectives.Count>0&&SiteDepth(objectives.Peek())<=o.P.X+o.P.Y)DrawDepot(g,objectives.Dequeue());
   var p=ScreenOf(o.P);float s=o.T.Size*zoom;
   if(p.X< -s||p.X>MW+s||p.Y<MapTop-s||p.Y>MapTop+MH+s)continue;
   if(selection.Contains(o)){using(var pen=new Pen(teal,2.2f))g.DrawEllipse(pen,p.X-s*.32f,p.Y-s*.13f,s*.64f,s*.31f);}
   DrawFieldSprite(g,o,p,s);
   float age=world.Time-o.ReceiptAt;
   if(o.Side==0&&o.Alive&&o.LastReceipt>0&&age<2.3f)Txt(g,"+"+o.LastReceipt,p.X-14,p.Y-s*.33f-age*8,15,gold,true);
   if(o.Alive){float y=p.Y+s*.10f;Line(g,p.X-5,y,p.X+5,y,Team(o.Side),3);
    if(selection.Contains(o)||o.Hp<o.T.Hp||o.Ready<1){Rect(g,p.X-20,y+4,40,4,ink);Rect(g,p.X-20,y+4,40*(o.Ready<1?o.Ready:o.Hp/o.T.Hp),4,o.Ready<1?gold:teal);}
   }
  }
  while(objectives.Count>0)DrawDepot(g,objectives.Dequeue());
  foreach(var site in world.Sites){
   var p=ScreenOf(site.P);float y=p.Y+Greyhaven.Site.CaptureRadius*Rise*1.414214f*zoom+3;
   if(p.X< -60||p.X>MW+60||y<MapTop-25||y>MapTop+MH)continue;
   var c=SiteColor(site);Rect(g,p.X-37,y,74,22,Color.FromArgb(208,ink));Rect(g,p.X-37,y,3,22,c);
   Txt(g,site.Name,p.X-29,y+1,14,pale,true);
   if(world.Explored(0,site.P))Line(g,p.X-31,y+20,p.X-31+62*Math.Abs(site.Capture)/5,y+20,site.Capture>=0?Team(0):Team(1),2);
  }
  foreach(var shot in world.Shots){if(!world.Explored(0,shot.A)||!world.Explored(0,shot.B))continue;var a=ScreenOf(shot.A);var b=ScreenOf(shot.B);Line(g,a.X,a.Y-5,b.X,b.Y-5,shot.Shell?gold:Team(shot.Side),2);}
  if(!briefing&&!paused&&!drag&&!panning&&place==null&&!sweep&&selection.Any(u=>u.Deployed&&u.T.Hit>0)&&mouse.X>=0&&mouse.X<MW&&mouse.Y>=MapTop&&mouse.Y<MapTop+MH){
   var under=HitScreen(mouse);
   if(under!=null&&under.Side==1){var q=ScreenOf(under.P);
    using(var pen=new Pen(red,2))g.DrawEllipse(pen,q.X-19,q.Y-11,38,22);
    Txt(g,"集火 "+under.T.Name+" #"+under.Id,mouse.X+12,mouse.Y-23,13,red,true);}
  }
  if(markedPoint.HasValue&&world.Time-markTime<2.5f){
   var enemy=markedEnemy;if(enemy==null||enemy.Alive&&world.Visible(enemy)){
    var q=ScreenOf(enemy==null?markedPoint.Value:enemy.P);var c=markHostile?red:teal;
    using(var pen=new Pen(c,2)){g.DrawEllipse(pen,q.X-12,q.Y-7,24,14);g.DrawLine(pen,q.X-17,q.Y,q.X-8,q.Y);g.DrawLine(pen,q.X+8,q.Y,q.X+17,q.Y);}
    Txt(g,enemy==null?(markHostile?"进攻点":"移动点"):enemy.T.Name+" #"+enemy.Id,q.X+15,q.Y-14,13,c,true);
   }}
  if(selection.Count==1&&selection[0].Path.Count>0){var u=selection[0];using(var pen=new Pen(teal)){pen.DashStyle=DashStyle.Dash;g.DrawLines(pen,new[]{u.P}.Concat(u.Path).Select(ScreenOf).ToArray());}}
  if(selection.Count==1&&selection[0].Rally.HasValue){var a=ScreenOf(selection[0].P);var b=ScreenOf(selection[0].Rally.Value);
   using(var pen=new Pen(teal,1.5f)){pen.DashStyle=DashStyle.Dot;g.DrawLine(pen,a,b);g.DrawEllipse(pen,b.X-8,b.Y-5,16,10);}}
  if(place!=null&&mouse.X<MW&&mouse.Y>=MapTop&&mouse.Y<MapTop+MH){
   var w=WorldOf(mouse);var r=Campaign.Footprint(place,w);var p=ScreenOf(Campaign.Middle(r));DrawLive(g,place,0,14,p,place.Size*zoom,1);
   var state=g.Save();GroundSpace(g);DrawTiles(g,r,world.CanPlace(place,w,null)?teal:red,true);if(Campaign.HasGate(place))DrawTiles(g,Campaign.Gate(r),gold,false);if(place.Key=="barracks")DrawTiles(g,Campaign.Muster(r),teal,true);g.Restore(state);
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
   var q=ScreenOf(basePos);if(site.Body.Contains(location)||InkHit(SiteKey(site),site.Owner==1?1:0,BuildingRect(site.Body,q,zoom),screen))return site.P;
  }return location;
 }
 float SiteDepth(Site s){var p=Campaign.Middle(s.Body);return p.X+p.Y;}
 Color SiteColor(Site s){return s.Owner>=0&&world.Explored(0,s.P)?Team(s.Owner):gold;}
 string SiteKey(Site site){return site==world.Sites[0]?"depot":site==world.Sites[1]?"freight":"harbormaster";}
 void DrawDepot(Graphics g,Site site){
  var box=BuildingRect(site.Body,ScreenOf(Campaign.Middle(site.Body)),zoom);
  BuildingArt(g,SiteKey(site),site.Owner==1?1:0,box,1);
 }
 void DrawRadar(Graphics g,float x){
  var state=g.Save();g.SmoothingMode=SmoothingMode.None;g.SetClip(new RectangleF(x,93,354,132));g.TranslateTransform(x,93);g.ScaleTransform(354f/Campaign.Width,132f/Campaign.Height);
  g.DrawImage(radarGround,0,0,Campaign.Width,Campaign.Height);Shroud(g);
  foreach(var patch in world.Ore)if(patch.Stock>0&&world.Explored(0,patch.P))Rect(g,patch.P.X-5,patch.P.Y-5,14,14,gold);
  bool radar=world.Power(0)>=0&&world.Live(0).Any(o=>o.T.Key=="radar");
  foreach(var o in world.Units.Where(a=>a.Alive&&a.Carrier==null&&(a.Side==0||radar&&world.Visible(a))))Rect(g,o.P.X-6,o.P.Y-6,o.T.Building?26:16,o.T.Building?26:16,Team(o.Side));
  foreach(var site in world.Sites)using(var pen=new Pen(gold,5))g.DrawRectangle(pen,site.P.X-15,site.P.Y-15,30,30);
  if(world.LastEvent.HasValue&&world.Time-world.EventTime<4){
   var p=world.LastEvent.Value;float r=17+(world.Time-world.EventTime)%1*32;
   using(var pen=new Pen(red,6))g.DrawEllipse(pen,p.X-r,p.Y-r,r*2,r*2);
  }
  using(var pen=new Pen(pale,6))g.DrawPolygon(pen,new[]{new PointF(0,MapTop),new PointF(MW,MapTop),new PointF(MW,MapTop+MH),new PointF(0,MapTop+MH)}.Select(WorldOf).ToArray());g.Restore(state);
  hits.Add(new HitBox{Box=new RectangleF(x,93,354,132),Run=()=>CenterAt(new PointF((mouse.X-x)*Campaign.Width/354,(mouse.Y-93)*Campaign.Height/132))});
 }
 Piece HitScreen(PointF screen){
  var w=WorldOf(screen);return world.Units.Where(o=>o.Alive&&o.Carrier==null&&world.Visible(o)).OrderByDescending(o=>o.P.X+o.P.Y).FirstOrDefault(o=>{
   var p=ScreenOf(o.P);float size=o.T.Size*zoom;if(o.T.Building){var lot=Campaign.Footprint(o.T,o.P);return lot.Contains(w)||InkHit(o.T.Key,o.Side,BuildingRect(lot,p,zoom),screen);}
   return MobileHit(o,p,size,screen);
  });
 }
 // Bake the apron using the map's own concrete brush in world coordinates.
 // The driveway terminates in the nearest road; neither the yard nor its shadow is a sprite backdrop.
 void DrawDepotYard(Graphics g,Site site,TextureBrush concrete){
  var lot=site.Body;var p=site.P;float left=lot.Left-7,top=lot.Top-7,right=p.X+36,bottom=Math.Max(lot.Bottom+8,p.Y+30);
  var road=Campaign.Roads[0];float roadEdge=p.Y<road.Top?road.Top:road.Bottom;
  float laneX=p.X+4,laneW=29;
  var drive=RectangleF.FromLTRB(laneX-laneW/2,Math.Min(roadEdge-2,p.Y),laneX+laneW/2,Math.Max(roadEdge+2,p.Y));
  using(var shoulder=new Pen(Color.FromArgb(71,105,95,72),4))g.DrawRectangle(shoulder,drive.X,drive.Y,drive.Width,drive.Height);
  g.FillRectangle(concrete,drive);
  float trackEnd=p.Y<road.Top?bottom:top;
  for(int side=-1;side<=1;side+=2)Line(g,laneX+side*8,trackEnd,laneX+side*8,roadEdge,Color.FromArgb(36,58,55,48),2);
  var yard=new[]{new PointF(left+5,top),new PointF(right-11,top),new PointF(right,top+11),new PointF(right,bottom-9),
   new PointF(right-9,bottom),new PointF(left+9,bottom),new PointF(left,bottom-9),new PointF(left,top+5)};
  for(int border=6;border>=2;border-=2)using(var gravel=new Pen(Color.FromArgb(37,121,111,90),border))g.DrawPolygon(gravel,yard);
  // A worn gravel shoulder breaks the hard cut between concrete and turf.
  var noise=new Random((int)lot.X);
  for(int n=0;n<yard.Length;n++){
   var a=yard[n];var b=yard[(n+1)%yard.Length];int count=(int)Campaign.Dist(a,b);
   for(int j=0;j<count;j++){
    float t=(float)noise.NextDouble(),gx=a.X+(b.X-a.X)*t+noise.Next(-3,4),gy=a.Y+(b.Y-a.Y)*t+noise.Next(-3,4);
    Rect(g,gx,gy,1.5f,1,Color.FromArgb(75,134,125,103));
   }
  }
  g.FillPolygon(concrete,yard);
  // Keep the entrance flush instead of drawing a curb across the driveway.
  float lip=p.Y<road.Top?bottom:top;g.FillRectangle(concrete,laneX-laneW/2,lip-2,laneW,4);
  using(var joint=new Pen(Color.FromArgb(56,66,65,55),.7f)){
   for(float x=lot.Right+7;x<right-7;x+=22)g.DrawLine(joint,x,top+6,x,bottom-6);
   g.DrawLine(joint,left+5,lot.Bottom+4,right-5,lot.Bottom+4);
  }
  float drainY=lot.Bottom+12;
  Rect(g,lot.Left+7,drainY,lot.Width-14,2.8f,C("#67655c"));
  for(float x=lot.Left+8;x<lot.Right-7;x+=3)Line(g,x,drainY+.3f,x,drainY+2,C("#403f3a"),.85f);
  for(int i=0;i<5;i++)Line(g,lot.Left+4+i*8,lot.Bottom+7,lot.Left+7+i*8,lot.Bottom+10,C("#b9a16a"),1.4f);
  // A cast shadow and a tighter contact shadow use the same footprint as navigation.
  for(int n=4;n>=1;n--){
   var shadow=lot;shadow.Inflate(n*.6f,n*.6f);shadow.Offset(n*1.05f,n*.45f);
   Rect(g,shadow.X,shadow.Y,shadow.Width,shadow.Height,Color.FromArgb(16,29,30,27));
  }
 }
 Bitmap MakeGround(){
  using(var materials=Raw("materials")){
   var bmp=new Bitmap(Campaign.Width,Campaign.Height,PixelFormat.Format32bppPArgb);
   using(var g=Graphics.FromImage(bmp)){
    g.SmoothingMode=SmoothingMode.AntiAlias;int w=materials.Width/2,h=materials.Height/2;
    var tiles=new TextureBrush[4];
    try{for(int i=0;i<4;i++){using(var t=materials.Clone(new Rectangle(i%2*w+2,i/2*h+2,w-4,h-4),PixelFormat.Format32bppPArgb)){
     tiles[i]=new TextureBrush(t,WrapMode.TileFlipXY);tiles[i].ScaleTransform(.38f,.38f);}}
     g.FillRectangle(tiles[0],0,0,bmp.Width,bmp.Height);g.FillRectangle(tiles[2],0,0,bmp.Width,101);
     foreach(var pad in new[]{new Rectangle(25,402,668,288),new Rectangle(800,100,880,610),new Rectangle(1230,710,290,210)})g.FillRectangle(tiles[1],pad);
     using(var edge=new Pen(C("#bbb4a0"),8)){g.DrawLine(edge,0,100,bmp.Width,100);}
     foreach(var r in Campaign.Roads){using(var p=new Pen(C("#a09981"),5))g.DrawRectangle(p,r.X,r.Y,r.Width,r.Height);g.FillRectangle(tiles[3],r);}
     foreach(var r in Campaign.Roads){bool horiz=r.Width>r.Height;using(var pen=new Pen(C("#ccc3a6"),2)){pen.DashPattern=new[]{7f,10f};if(horiz)g.DrawLine(pen,r.Left,r.Top+r.Height/2,r.Right,r.Top+r.Height/2);else g.DrawLine(pen,r.Left+r.Width/2,r.Top,r.Left+r.Width/2,r.Bottom);}}
     foreach(var pad in new[]{new Rectangle(26,402,664,284),new Rectangle(810,110,850,585),new Rectangle(1230,715,285,201)}){
      using(var pen=new Pen(Color.FromArgb(100,pale),2))g.DrawRectangle(pen,pad);
      for(int i=pad.Left+20;i<pad.Right-14;i+=34)Line(g,i,pad.Bottom-29,i,pad.Bottom-8,Color.FromArgb(128,pale),2);
     }
     foreach(var site in world.Sites.Take(2))DrawDepotYard(g,site,tiles[1]);
     foreach(int y in new[]{316,400,727,803})for(int i=0;i<7;i++)Rect(g,737+i*8,y,4,8,pale);
     for(int x=80;x<bmp.Width;x+=144){using(var tire=new Pen(C("#454946"),4))g.DrawEllipse(tire,x,91,13,9);}
     for(int i=12;i<bmp.Width;i+=48){Rect(g,i,97,7,5,ink);Rect(g,i,97,6,2,gold);}
     foreach(var s in world.Sites.Skip(2)){for(int i=0;i<7;i++)Line(g,s.P.X-42+i*13,s.P.Y+32,s.P.X-36+i*13,s.P.Y+20,gold,3);}
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
