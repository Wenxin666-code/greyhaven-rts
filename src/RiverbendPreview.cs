using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 bool mapReview,mapDragging,mapLabels=true;PointF mapDragPoint,mapCenter;
 float mapZoom;RiverbendMap riverMap;TextureBrush[] riverTextures;
 Bitmap riverView;PointF riverViewCenter;float riverViewZoom,riverViewScale;bool riverViewLabels;
 bool riverBattleRender,riverViewBattle;RiverbendMap riverViewMap;
 int riverBattleWidth;
 RectangleF mapViewport {get{return riverBattleRender?new RectangleF(0,MapTop,riverBattleWidth,MH):new RectangleF(0,55,W-212,H-100);}}
 // Keep the same world projection when the cinematic reveals the command panel area.
 float RiverProjectionX {get{return (riverBattleRender?MW:mapViewport.Width)*.5f;}}
 const int RiverMargin=144;
 RectangleF riverDrawArea {get{if(riverTileDrawArea.HasValue)return riverTileDrawArea.Value;var r=mapViewport;r.Inflate(RiverMargin,RiverMargin);return r;}}
 PointF RiverScreen(float x,float y,float height=0){return new PointF(RiverProjectionX+(x-y-mapCenter.X+mapCenter.Y)*Campaign.Tile*Axis*mapZoom,mapViewport.Top+mapViewport.Height*.5f+(x+y-mapCenter.X-mapCenter.Y)*Campaign.Tile*Rise*mapZoom-height*15*mapZoom);}
 PointF RiverWorld(PointF p){float a=(p.X-RiverProjectionX)/(Campaign.Tile*Axis*mapZoom),b=(p.Y-mapViewport.Top-mapViewport.Height*.5f)/(Campaign.Tile*Rise*mapZoom);return new PointF(mapCenter.X+(a+b)/2,mapCenter.Y+(b-a)/2);}
 void RiverFit(){mapCenter=new PointF(riverMap.WidthCells*.5f,riverMap.HeightCells*.5f);mapZoom=Math.Min((mapViewport.Width-92)/((riverMap.WidthCells+riverMap.HeightCells)*Campaign.Tile*Axis),(mapViewport.Height-60)/((riverMap.WidthCells+riverMap.HeightCells)*Campaign.Tile*Rise));}
 void RiverFocus(float x,float y,float k){mapCenter=new PointF(x,y);mapZoom=k;hudDirty=true;Invalidate();}
 void EnsureRiverArt(){
  if(riverMap==null)riverMap=new RiverbendMap();
  foreach(var key in new[]{"residence","shop","municipal"})if(!painted.ContainsKey(key)){painted[key]=Raw("iso-"+key);painted[key+"-paint"]=Raw("iso-"+key+"-paint");}
  if(riverTextures==null)using(var material=Raw("materials")){
   riverTextures=new TextureBrush[6];int w=material.Width/2,h=material.Height/2;
   for(int i=0;i<4;i++)using(var tile=material.Clone(new Rectangle(i%2*w+2,i/2*h+2,w-4,h-4),PixelFormat.Format32bppPArgb))riverTextures[i]=new TextureBrush(tile,WrapMode.TileFlipXY);
   for(int i=4;i<6;i++)using(var tile=new Bitmap(128,128))using(var surface=Graphics.FromImage(tile)){
    var random=new Random(413+i);surface.Clear(i==4?C("#aaa183"):C("#8f917e"));
    for(int n=0;n<1900;n++){int v=random.Next(30,100);using(var dot=new SolidBrush(Color.FromArgb(v,n%2==0?Color.FromArgb(44,46,37):Color.FromArgb(219,210,175))))surface.FillRectangle(dot,random.Next(128),random.Next(128),random.Next(1,4),random.Next(1,3));}
    if(i==5)for(int n=0;n<36;n++){int x=random.Next(128),y=random.Next(128);using(var pen=new Pen(Color.FromArgb(80,49,55,47)))surface.DrawLine(pen,x,y,x+random.Next(4,17),y+random.Next(-4,4));}
    riverTextures[i]=new TextureBrush(tile,WrapMode.TileFlipXY);
   }
  }
 }
 void OpenMapReview(){
  bool chapterOne=!riverMissionSelected;
  if(riverMap==null||riverMap.IsChapterOne!=chapterOne||riverMap.IsIsland!=islandMissionSelected||riverMap.IsRelay!=relayMissionSelected){
   riverMap=chapterOne?RiverbendMap.ChapterOne():relayMissionSelected?RiverbendMap.Relay():new RiverbendMap(islandMissionSelected);riverViewZoom=-1;
  }
  EnsureRiverArt();mapReview=true;briefing=true;missionPicker=false;modelReview=false;rulesOpen=false;mapDragging=false;hits.Clear();RiverFit();riverViewZoom=-1;hudDirty=true;Invalidate();
 }
 void CloseMapReview(){mapReview=false;mapDragging=false;Capture=false;missionPicker=true;hits.Clear();hudDirty=true;Invalidate();}
 void RiverKey(Keys key){
  if(key==Keys.Escape){CloseMapReview();return;}if(key==Keys.Home)RiverFit();
  if(key==Keys.L)mapLabels=!mapLabels;
  float a=key==Keys.Left?-90:key==Keys.Right?90:0,b=key==Keys.Up?-90:key==Keys.Down?90:0;
  if(a!=0||b!=0)RiverPan(a,b);hudDirty=true;Invalidate();
 }
 void RiverPan(float dx,float dy){var p=RiverWorld(new PointF(mapViewport.Width/2+dx,mapViewport.Top+mapViewport.Height/2+dy));mapCenter=new BattlefieldBounds(riverMap.WidthCells,riverMap.HeightCells).ClampCenter(p,mapViewport.Width/(2*Campaign.Tile*Axis*mapZoom),mapViewport.Height/(2*Campaign.Tile*Rise*mapZoom));hudDirty=true;}
 void RiverWheel(PointF p,int delta){if(!mapViewport.Contains(p))return;var a=RiverWorld(p);mapZoom=Math.Max(.12f,Math.Min(3.1f,mapZoom*(float)Math.Pow(1.18,delta/120f)));var b=RiverWorld(p);mapCenter=new PointF(mapCenter.X+a.X-b.X,mapCenter.Y+a.Y-b.Y);hudDirty=true;Invalidate();}
 PointF[] RiverQuad(float x,float y,float w,float h,float z){return new[]{RiverScreen(x,y,z),RiverScreen(x+w,y,z),RiverScreen(x+w,y+h,z),RiverScreen(x,y+h,z)};}
 static void RiverPoly(Graphics g,PointF[] p,Color c){using(var b=new SolidBrush(c))g.FillPolygon(b,p);}
 void RiverFace(Graphics g,PointF a,PointF b,float depth,Color c){if(depth<=.1f)return;RiverPoly(g,new[]{a,b,new PointF(b.X,b.Y+depth),new PointF(a.X,a.Y+depth)},c);if(mapZoom>.40f){using(var pen=new Pen(Color.FromArgb(85,27,35,29),Math.Max(.4f,mapZoom*.65f)))for(float d=4*mapZoom;d<depth;d+=6*mapZoom)g.DrawLine(pen,a.X,a.Y+d,b.X,b.Y+d);}}
 void DrawRiverCell(Graphics g,int x,int y){
  var c=riverMap.At(x,y);if(c==null)return;var p=RiverQuad(x,y,1,1,c.Height);float tile=Campaign.Tile*Axis*mapZoom;
  if(p[0].X+tile<riverDrawArea.Left||p[0].X-tile>riverDrawArea.Right||p[0].Y+tile+c.Height*15*mapZoom<riverDrawArea.Top||p[0].Y>riverDrawArea.Bottom+60)return;
  bool wet=c.Ground==RiverbendMap.Surface.Water,road=c.Ground==RiverbendMap.Surface.Road||c.Ground==RiverbendMap.Surface.Bridge||c.Ground==RiverbendMap.Surface.Ramp;
  Color color=wet?C("#315963"):road?C("#626864"):c.Ground==RiverbendMap.Surface.Square?C("#97998a"):c.Ground==RiverbendMap.Surface.Sand?C("#b6ad8b"):c.Ground==RiverbendMap.Surface.Soil?C("#a29b77"):c.Ground==RiverbendMap.Surface.Rock?C("#858775"):c.Ground==RiverbendMap.Surface.Marsh?C("#617966"):C("#78875b");
  if(wet)color=C("#355e68");
  var east=riverMap.At(x+1,y);var south=riverMap.At(x,y+1);
  RiverFace(g,p[1],p[2],(c.Height-(east==null?0:east.Height))*15*mapZoom,C("#626653"));
  RiverFace(g,p[2],p[3],(c.Height-(south==null?0:south.Height))*15*mapZoom,C("#777965"));
  RiverPoly(g,p,color);
  if(mapZoom>=.38f){
   int texture=wet?2:road?3:c.Ground==RiverbendMap.Surface.Square?1:c.Ground==RiverbendMap.Surface.Meadow||c.Ground==RiverbendMap.Surface.Marsh?0:c.Ground==RiverbendMap.Surface.Rock?5:4;
   g.FillPolygon(riverTextures[texture],p);
   if(c.Ground==RiverbendMap.Surface.Sand)RiverPoly(g,p,Color.FromArgb(65,182,173,139));
   if(wet)RiverPoly(g,p,Color.FromArgb(color.R>80?105:35,color));
   if(c.Ground==RiverbendMap.Surface.Marsh)RiverPoly(g,p,Color.FromArgb(64,39,77,58));
  }
  if(!road&&!wet){int shade=c.Variation%9;RiverPoly(g,p,Color.FromArgb(shade+2,shade<4?Color.Black:Color.White));}
  if(road&&mapZoom>.40f){
   bool alongX=riverMap.At(x+1,y)!=null&&(riverMap.At(x+1,y).Ground==c.Ground||riverMap.At(x+1,y).Ground==RiverbendMap.Surface.Bridge);
   bool edgeY=riverMap.At(x,y+1)==null||!(riverMap.At(x,y+1).Ground==RiverbendMap.Surface.Road||riverMap.At(x,y+1).Ground==RiverbendMap.Surface.Bridge||riverMap.At(x,y+1).Ground==RiverbendMap.Surface.Ramp);
   bool edgeX=riverMap.At(x+1,y)==null||!(riverMap.At(x+1,y).Ground==RiverbendMap.Surface.Road||riverMap.At(x+1,y).Ground==RiverbendMap.Surface.Bridge||riverMap.At(x+1,y).Ground==RiverbendMap.Surface.Ramp);
   using(var pen=new Pen(C("#b1ad91"),Math.Max(.4f,mapZoom))){if(edgeY)g.DrawLine(pen,p[3],p[2]);if(edgeX)g.DrawLine(pen,p[1],p[2]);}
   if((alongX&&y%5==2&&x%3!=0)||(!alongX&&x%5==2&&y%3!=0)){var a=RiverScreen(x+.25f,y+.5f,c.Height);var b=RiverScreen(x+.75f,y+.5f,c.Height);using(var pen=new Pen(Color.FromArgb(150,219,211,178),mapZoom*.75f))g.DrawLine(pen,a,b);}
  }
  if(wet&&mapZoom>.35f&&c.Variation%5==0){PointF a=RiverScreen(x+.2f,y+.4f),b=RiverScreen(x+.75f,y+.4f);using(var pen=new Pen(Color.FromArgb(75,148,187,176),Math.Max(.5f,mapZoom*.6f)))g.DrawLine(pen,a,b);}
  if(c.Ground==RiverbendMap.Surface.Marsh&&mapZoom>.55f&&c.Variation%3==0){var a=RiverScreen(x+.5f,y+.5f);using(var pen=new Pen(Color.FromArgb(150,64,78,42),mapZoom))for(int j=0;j<3;j++)g.DrawLine(pen,a.X+j*mapZoom,a.Y,a.X+(j-1)*mapZoom,a.Y-5*mapZoom);}
 }
 void DrawRiverTree(Graphics g,int x,int y){
  var c=riverMap.At(x,y);var p=RiverScreen(x+.5f,y+.5f,c.Height);float z=mapZoom,h=(20+c.Variation%13)*z,w=(9+c.Variation%5)*z;
  if(p.X+2*w<riverDrawArea.Left||p.X-w>riverDrawArea.Right||p.Y+10*z<riverDrawArea.Top||p.Y-h>riverDrawArea.Bottom)return;
  RiverPoly(g,new[]{new PointF(p.X-w*.6f,p.Y),new PointF(p.X+w*.4f,p.Y-2*z),new PointF(p.X+w*2,p.Y+8*z),new PointF(p.X+w*.8f,p.Y+10*z)},Color.FromArgb(60,15,25,22));
  Rect(g,p.X-z,p.Y-h*.46f,2*z,h*.46f,C("#665e42"));
  for(int i=0;i<3;i++){float top=p.Y-h+i*h*.20f,wide=w*(.68f+i*.17f);var points=new[]{new PointF(p.X,top),new PointF(p.X+wide*.46f,top+h*.25f),new PointF(p.X+wide,top+h*.42f),new PointF(p.X+wide*.35f,top+h*.47f),new PointF(p.X-wide,top+h*.36f),new PointF(p.X-wide*.4f,top+h*.18f)};RiverPoly(g,points,i==0?C("#697f54"):i==1?C("#506c49"):C("#3e5b43"));}
 }
 void DrawRiverHouse(Graphics g,RiverbendMap.House house){
  var lot=house.Lot;var c=riverMap.At(lot.X,lot.Y);var p=RiverScreen(lot.X+lot.Width*.5f,lot.Y+lot.Height*.5f,c.Height);
  var box=BuildingRect(new RectangleF(0,0,lot.Width*Campaign.Tile,lot.Height*Campaign.Tile),p,mapZoom);
  if(!box.IntersectsWith(riverDrawArea))return;
  DrawGroundShadow(g,house.Kind,new Rectangle(0,0,320,320),box,false,8);
  DrawStamp(g,house.Kind,new Rectangle(0,0,320,320),box,UnitColor(-1));
  if(mapZoom>.75f){var door=RiverScreen(house.Door.X+.5f,house.Door.Y+.5f,c.Height);var quad=RiverQuad(house.Door.X,house.Door.Y,1,1,c.Height);using(var pen=new Pen(Color.FromArgb(100,211,198,144),Math.Max(.6f,mapZoom*.65f)))g.DrawPolygon(pen,quad);}
 }
 void DrawRiverMap(Graphics g){
  var saved=g.Save();g.IntersectClip(riverDrawArea);Rect(g,riverDrawArea.Left,riverDrawArea.Top,riverDrawArea.Width,riverDrawArea.Height,C("#152629"));
  var origin=RiverScreen(0,0);
  var sea=new RectangleF(origin.X-riverMap.HeightCells*Campaign.Tile*Axis*mapZoom,origin.Y,(riverMap.WidthCells+riverMap.HeightCells)*Campaign.Tile*Axis*mapZoom,(riverMap.WidthCells+riverMap.HeightCells)*Campaign.Tile*Rise*mapZoom);
  sea.Intersect(riverDrawArea);if(sea.Width>0&&sea.Height>0){DrawSea(g,sea,origin,mapZoom);g.IntersectClip(sea);}
  foreach(var t in riverTextures){t.ResetTransform();t.TranslateTransform(origin.X,origin.Y);t.ScaleTransform(.32f*mapZoom,.22f*mapZoom);}
  g.SmoothingMode=SmoothingMode.None;
  // Only inverse-projected visible cells are visited when zoomed in.
  // Account for the maximum tree/shadow overhang and height-three cliffs in
  // projected pixels. Ten extra rows on all sides multiplied tiny cache-tile
  // redraws into thousands of irrelevant cells.
  float marginX=riverBattleRender?30*mapZoom+2:0,marginTop=riverBattleRender?12*mapZoom+2:80,marginBottom=riverBattleRender?80*mapZoom+2:120;int extra=riverBattleRender?1:10;
  var corners=new[]{RiverWorld(new PointF(riverDrawArea.Left-marginX,riverDrawArea.Top-marginTop)),RiverWorld(new PointF(riverDrawArea.Right+marginX,riverDrawArea.Top-marginTop)),RiverWorld(new PointF(riverDrawArea.Left-marginX,riverDrawArea.Bottom+marginBottom)),RiverWorld(new PointF(riverDrawArea.Right+marginX,riverDrawArea.Bottom+marginBottom))};
  var domain=riverMap.TerrainBounds;int x0=Math.Max(domain.Left,(int)Math.Floor(corners.Min(p=>p.X))-extra),x1=Math.Min(domain.Right-1,(int)Math.Ceiling(corners.Max(p=>p.X))+extra),y0=Math.Max(domain.Top,(int)Math.Floor(corners.Min(p=>p.Y))-extra),y1=Math.Min(domain.Bottom-1,(int)Math.Ceiling(corners.Max(p=>p.Y))+extra);
  for(int depth=x0+y0;depth<=x1+y1;depth++)for(int x=x0;x<=x1;x++){int y=depth-x;if(y>=y0&&y<=y1)DrawRiverCell(g,x,y);}
  g.SmoothingMode=SmoothingMode.AntiAlias;
  foreach(var bridge in riverMap.Bridges){
   foreach(float y in new[]{bridge.Top+.15f,bridge.Bottom-.15f}){
    PointF a=RiverScreen(bridge.Left,y,.4f),b=RiverScreen(bridge.Right,y,.4f);using(var pen=new Pen(C("#c2bda4"),Math.Max(1,2*mapZoom)))g.DrawLine(pen,a.X,a.Y-2*mapZoom,b.X,b.Y-2*mapZoom);
    if(mapZoom>.30f)for(int x=bridge.Left;x<=bridge.Right;x+=2){var p=RiverScreen(x,y,.4f);Line(g,p.X,p.Y,p.X,p.Y-5*mapZoom,C("#4b5550"),Math.Max(.6f,mapZoom));}
   }
  }
  if(!riverBattleRender)foreach(var ore in riverMap.Ore){var p=RiverScreen(ore.X+.5f,ore.Y+.5f,riverMap.At(ore.X,ore.Y).Height);if(!riverDrawArea.Contains(p))continue;float z=mapZoom*5;RiverPoly(g,new[]{new PointF(p.X-z,p.Y),new PointF(p.X-z*.4f,p.Y-z*1.4f),new PointF(p.X+z*.5f,p.Y-z*.8f),new PointF(p.X+z,p.Y),new PointF(p.X,p.Y+z*.4f)},C("#b89d53"));Line(g,p.X-z*.4f,p.Y-z*1.4f,p.X,p.Y,C("#e1c878"),Math.Max(.4f,mapZoom));}
  for(int depth=x0+y0;depth<=x1+y1;depth++){
   for(int x=x0;x<=x1;x++){int y=depth-x;if(y>=y0&&y<=y1&&riverMap.At(x,y)!=null&&riverMap.At(x,y).Tree)DrawRiverTree(g,x,y);}
   if(!riverBattleRender)foreach(var house in riverMap.Houses)if(house.Lot.Right+house.Lot.Bottom==depth)DrawRiverHouse(g,house);
  }
  if(riverMap.IsChapterOne&&!riverBattleRender){
   var office=RiverbendMap.HarborOffice;
   var p=RiverScreen(office.X,office.Y);
   if(HarborSprites.Office.At(p,mapZoom).IntersectsWith(riverDrawArea))DrawHarborSprite(g,HarborSprites.Office,p,mapZoom,1,HarborCoat);
  }
  if(mapLabels&&!riverBattleRender){
   foreach(var label in riverMap.Labels){var p=RiverScreen(label.Position.X,label.Position.Y);if(!riverDrawArea.Contains(p)||mapZoom>.6f&&label.Style==0)continue;
    var color=label.Style>=7?UnitColor(label.Style-6):label.Style==1?C("#7db6d2"):label.Style==2?C("#db9c87"):label.Style==6?C("#95bd76"):label.Style==4?C("#93bbc1"):gold;
    Rect(g,p.X-42,p.Y-10,84,23,Color.FromArgb(215,20,32,33));Txt(g,label.Name,p.X-35,p.Y-7,12,color,true);
   }
  }
  if(riverMap.IsRelay&&!riverBattleRender){
   var station=RiverbendMap.RelayCell(53,21);var point=RiverScreen(station.X+.5f,station.Y+.5f,2);
   var box=BuildingRect(new RectangleF(0,0,3*Campaign.Tile,3*Campaign.Tile),point,mapZoom);
   if(box.IntersectsWith(riverDrawArea)){DrawGroundShadow(g,"relay",B08Source(),box,false,8);DrawStamp(g,"relay",B08Source(),box,UnitColor(1));}
  }
  // Scale witnesses, explicitly staged in the preview, never combat entities.
  if(mapZoom>.65f&&!riverBattleRender&&!riverMap.IsRelay&&riverMap.Bridges.Count>0){
   var crossing=riverMap.Bridges[0];
   float x=riverMap.IsIsland?92:crossing.Left-8,y=riverMap.IsIsland?50:crossing.Top+2;
   var p=RiverScreen(x,y);DrawLive(g,Campaign.Info("engineer"),0,14,p,Campaign.Info("engineer").Size*mapZoom,1);
   p=RiverScreen(x+2,y+1);DrawLive(g,Campaign.Info("squad"),0,14,p,Campaign.Info("squad").Size*mapZoom,1);
   p=RiverScreen(x+5,y+1);DrawLive(g,Campaign.Info("tank"),0,14,p,Campaign.Info("tank").Size*mapZoom,1);
  }
  g.Restore(saved);
 }
 void DrawMapReview(Graphics g){
  DrawRiverCached(g);Metal(g,0,0,W,55);Emblem(g,28,25,11);Txt(g,riverMap.Title,53,10,25,pale,true);Txt(g,"关卡地形预览  /  "+riverMap.WidthCells+" × "+riverMap.HeightCells+" 格",270,19,14,gold,true);
  Button(g,W-194,9,177,36,"返回关卡选择",CloseMapReview);
  Metal(g,W-212,55,212,H-100);float x=W-197;
  Txt(g,"战区总览",x,76,22,pale,true);
  if(riverMap.IsChapterOne)DrawHarborMapLegend(g,x);
  else{
  Txt(g,riverMap.IsRelay?"山地争夺 · 通信恢复行动":riverMap.IsIsland?"六方交战 · 单一堤桥出入口":"三方交战 · 突破河湾大桥",x,111,13,dim);
  Button(g,x,150,180,38,"全图   [Home]",()=>{RiverFit();hudDirty=true;});
  Button(g,x,202,180,38,riverMap.IsRelay?"西南前进基地":riverMap.IsIsland?"资源岛与唯一堤桥":"河湾大桥",()=>{if(riverMap.IsRelay)FocusRelay(15,82);else if(riverMap.IsIsland)RiverFocus(64,42,.55f);else FocusRiverBridge();});
  Button(g,x,254,180,38,riverMap.IsRelay?"北岭中继站":riverMap.IsIsland?"东部高地与坡道":"西岸出发点",()=>{if(riverMap.IsRelay)FocusRelay(53,21);else if(riverMap.IsIsland)FocusIsland(285,150,.80f);else RiverFocus(riverMap.WestStart.X,riverMap.WestStart.Y,1.15f);});
  Button(g,x,306,180,38,riverMap.IsRelay?"东侧补给基地":riverMap.IsIsland?"中央河道与居民区":"东岸封锁区",()=>{if(riverMap.IsRelay)FocusRelay(84,42);else if(riverMap.IsIsland)FocusIsland(211,117,.65f);else RiverFocus((riverMap.EastStart.X+riverMap.SouthStart.X)*.5f,(riverMap.EastStart.Y+riverMap.SouthStart.Y)*.5f,.9f);});
  Button(g,x,358,180,38,mapLabels?"地点标注：开 [L]":"地点标注：关 [L]",()=>{mapLabels=!mapLabels;hudDirty=true;});
  Txt(g,"地形图例",x,430,18,gold,true);
  var names=riverMap.IsRelay?new[]{"湖水 / 溪流：不可通行","主公路 / 西侧林道 / 东路","山岭 / 坡道 / 林带 / 矿田","中立房屋："+riverMap.Houses.Count+" 栋","敌方基地与北岭守军","六片有限矿区 · 每格 2400"}:riverMap.IsIsland?new[]{"海域 / 河湖：不可通行","西北资源岛：唯一堤桥","高地 / 坡道 / 林带 / 湿地","中立房屋："+riverMap.Houses.Count+" 栋","五个独立敌方阵营","岛内三片矿区 · 大陆矿田"}:new[]{"河流 / 湖泊：不可通行","河湾大桥：唯一跨河通道","陡崖：阻挡 · 坡道通行","东岸两方各自为战","中立房屋："+riverMap.Houses.Count+" 栋","有限矿区 · 每格 2400"};
  for(int i=0;i<names.Length;i++)Txt(g,names[i],x,467+i*27,13,dim);
  Txt(g,"房屋驻防规则",x,650,17,gold,true);Txt(g,"工兵占领后换色",x,680,13,dim);Txt(g,"驻防 4 / 6 / 8 名步兵",x,704,13,dim);Txt(g,"毁楼后从出口撤出",x,728,13,dim);
  }
  Metal(g,0,H-45,W,45);Txt(g,"滚轮缩放  ·  左键／右键／中键拖动  ·  方向键平移  ·  Esc 返回",18,H-31,14,pale);
  Txt(g,"选关预览 · 正式对局限制战术缩放",W-345,H-31,13,gold,true);
 }
 void DrawHarborMapLegend(Graphics g,float x){
  Txt(g,"旧港滩头 · 战役序章",x,111,14,dim);
  Button(g,x,150,180,38,"全图   [Home]",()=>{RiverFit();hudDirty=true;});
  Button(g,x,202,180,38,"卸载坡道与展开区",()=>RiverFocus(19,40,1.45f));
  Button(g,x,254,180,38,"港务中心与泊位",()=>RiverFocus(46,21,1.5f));
  Button(g,x,306,180,38,"东行公路与货运闸",()=>RiverFocus(63,31,1.35f));
  Button(g,x,358,180,38,mapLabels?"地点标注：开 [L]":"地点标注：关 [L]",()=>{mapLabels=!mapLabels;hudDirty=true;});
  Txt(g,"地形图例",x,430,18,gold,true);
  Txt(g,"港湾水域 · 陆军不可通行",x,467,13,dim);
  Txt(g,"铺装泊位 · 地面可通行",x,494,13,dim);
  Txt(g,"滩头近矿 · 基地补给",x,521,13,dim);
  Txt(g,"货运闸 · 通往河湾镇",x,548,13,dim);
 }
 void DrawRiverCached(Graphics g){
  var anchor=RiverScreen(riverViewCenter.X,riverViewCenter.Y);float dx=anchor.X-RiverProjectionX,dy=anchor.Y-mapViewport.Top-mapViewport.Height*.5f;
  var size=new Size((int)Math.Ceiling(riverDrawArea.Width*scale),(int)Math.Ceiling(riverDrawArea.Height*scale));
  if(riverViewMap!=riverMap||riverView==null||riverView.Size!=size||riverViewScale!=scale||Math.Abs(dx)>=RiverMargin||Math.Abs(dy)>=RiverMargin||riverViewZoom!=mapZoom||riverViewLabels!=mapLabels||riverViewBattle!=riverBattleRender){
   if(riverView==null||riverView.Size!=size){if(riverView!=null)riverView.Dispose();riverView=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);}
   using(var target=Graphics.FromImage(riverView)){
    target.ScaleTransform(scale,scale);target.TranslateTransform(RiverMargin,RiverMargin-mapViewport.Top);target.InterpolationMode=InterpolationMode.Bilinear;
    bool prior=drawingClient;drawingClient=false;try{if(riverBattleRender)DrawRiverGroundTiles(target);else DrawRiverMap(target);}finally{drawingClient=prior;}
   }
   riverViewMap=riverMap;riverViewScale=scale;riverViewCenter=mapCenter;riverViewZoom=mapZoom;riverViewLabels=mapLabels;riverViewBattle=riverBattleRender;dx=dy=0;
  }
  var saved=g.Save();g.IntersectClip(mapViewport);g.ResetTransform();g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(riverView,(int)Math.Round(ox+(riverDrawArea.X+dx)*scale),(int)Math.Round(oy+(riverDrawArea.Y+dy)*scale));g.Restore(saved);
  if(riverBattleRender)WarmRiverGroundTiles();
 }
 void FocusRelay(float u,float v){var p=RiverbendMap.RelayGrid(u,v);RiverFocus(p.X,p.Y,1.1f);}
 void FocusIsland(int x,int y,float zoom){var p=RiverbendMap.IslandCell(x,y);RiverFocus(p.X,p.Y,zoom);}
 void FocusRiverBridge(){var bridge=riverMap.Bridges[0];RiverFocus(bridge.X+bridge.Width*.5f,bridge.Y+bridge.Height*.5f,1.15f);}
 void StartRelayMission(){StartTerrainMission(false,true);}
 void StartRiverMission(){StartTerrainMission(false);BeginRiverbendOpening();}
 void StartIslandMission(){StartTerrainMission(true);}
 void StartTerrainMission(bool island,bool relay=false){
  Restart();world=new Campaign(false,false,true,island,relay);world.PrepareBattleNavigation();riverMap=world.River;EnsureRiverArt();riverViewZoom=-1;ClearRiverGroundCache();
  mapReview=false;briefing=false;paused=false;selection.Clear();selection.Add(world.Units.First(o=>o.Alive&&o.Side==0&&o.T.Key=="mcv"));CenterAt(world.Home(0));zoom=DefaultZoom;if(relay)PrepareRelayArt();edgeArmed=true;hits.Clear();hudDirty=true;
 }
 void DrawRiverBattleGround(Graphics g,int viewWidth=MW){
  riverBattleWidth=viewWidth;riverBattleRender=true;riverMap=world.Terrain;EnsureRiverArt();mapCenter=new PointF(camera.X/Campaign.Tile,camera.Y/Campaign.Tile);mapZoom=zoom;
  try{DrawRiverCached(g);}finally{riverBattleRender=false;}
 }
}
}
