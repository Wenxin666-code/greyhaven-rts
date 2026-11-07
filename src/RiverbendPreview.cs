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
 bool riverBattleRender,riverViewBattle;
 RectangleF mapViewport {get{return riverBattleRender?new RectangleF(0,MapTop,MW,MH):new RectangleF(0,55,W-212,H-100);}}
 const int RiverMargin=144;
 RectangleF riverDrawArea {get{var r=mapViewport;r.Inflate(RiverMargin,RiverMargin);return r;}}
 PointF RiverScreen(float x,float y,float height=0){return new PointF(mapViewport.Width*.5f+(x-y-mapCenter.X+mapCenter.Y)*Campaign.Tile*Axis*mapZoom,mapViewport.Top+mapViewport.Height*.5f+(x+y-mapCenter.X-mapCenter.Y)*Campaign.Tile*Rise*mapZoom-height*15*mapZoom);}
 PointF RiverWorld(PointF p){float a=(p.X-mapViewport.Width*.5f)/(Campaign.Tile*Axis*mapZoom),b=(p.Y-mapViewport.Top-mapViewport.Height*.5f)/(Campaign.Tile*Rise*mapZoom);return new PointF(mapCenter.X+(a+b)/2,mapCenter.Y+(b-a)/2);}
 void RiverFit(){mapCenter=new PointF(RiverbendMap.Columns*.5f,RiverbendMap.Rows*.5f);mapZoom=Math.Min((mapViewport.Width-92)/((RiverbendMap.Columns+RiverbendMap.Rows)*Campaign.Tile*Axis),(mapViewport.Height-60)/((RiverbendMap.Columns+RiverbendMap.Rows)*Campaign.Tile*Rise));}
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
  EnsureRiverArt();mapReview=true;briefing=true;missionPicker=false;modelReview=false;rulesOpen=false;mapDragging=false;hits.Clear();RiverFit();riverViewZoom=-1;hudDirty=true;Invalidate();
 }
 void CloseMapReview(){mapReview=false;mapDragging=false;Capture=false;missionPicker=true;hits.Clear();hudDirty=true;Invalidate();}
 void RiverKey(Keys key){
  if(key==Keys.Escape){CloseMapReview();return;}if(key==Keys.Home)RiverFit();
  if(key==Keys.L)mapLabels=!mapLabels;
  float a=key==Keys.Left?-90:key==Keys.Right?90:0,b=key==Keys.Up?-90:key==Keys.Down?90:0;
  if(a!=0||b!=0)RiverPan(a,b);hudDirty=true;Invalidate();
 }
 void RiverPan(float dx,float dy){var p=RiverWorld(new PointF(mapViewport.Width/2+dx,mapViewport.Top+mapViewport.Height/2+dy));mapCenter=new PointF(Math.Max(0,Math.Min(RiverbendMap.Columns,p.X)),Math.Max(0,Math.Min(RiverbendMap.Rows,p.Y)));hudDirty=true;}
 void RiverWheel(PointF p,int delta){if(!mapViewport.Contains(p))return;var a=RiverWorld(p);mapZoom=Math.Max(.12f,Math.Min(3.1f,mapZoom*(float)Math.Pow(1.18,delta/120f)));var b=RiverWorld(p);mapCenter=new PointF(mapCenter.X+a.X-b.X,mapCenter.Y+a.Y-b.Y);hudDirty=true;Invalidate();}
 PointF[] RiverQuad(float x,float y,float w,float h,float z){return new[]{RiverScreen(x,y,z),RiverScreen(x+w,y,z),RiverScreen(x+w,y+h,z),RiverScreen(x,y+h,z)};}
 static void RiverPoly(Graphics g,PointF[] p,Color c){using(var b=new SolidBrush(c))g.FillPolygon(b,p);}
 void RiverFace(Graphics g,PointF a,PointF b,float depth,Color c){if(depth<=.1f)return;RiverPoly(g,new[]{a,b,new PointF(b.X,b.Y+depth),new PointF(a.X,a.Y+depth)},c);if(mapZoom>.40f){using(var pen=new Pen(Color.FromArgb(85,27,35,29),Math.Max(.4f,mapZoom*.65f)))for(float d=4*mapZoom;d<depth;d+=6*mapZoom)g.DrawLine(pen,a.X,a.Y+d,b.X,b.Y+d);}}
 void DrawRiverCell(Graphics g,int x,int y){
  var c=riverMap.At(x,y);var p=RiverQuad(x,y,1,1,c.Height);float tile=Campaign.Tile*Axis*mapZoom;
  if(p[0].X+tile<riverDrawArea.Left||p[0].X-tile>riverDrawArea.Right||p[0].Y+tile+c.Height*15*mapZoom<riverDrawArea.Top||p[0].Y>riverDrawArea.Bottom+60)return;
  bool wet=c.Ground==RiverbendMap.Surface.Water,road=c.Ground==RiverbendMap.Surface.Road||c.Ground==RiverbendMap.Surface.Bridge||c.Ground==RiverbendMap.Surface.Ramp;
  Color color=wet?C("#315963"):road?C("#626864"):c.Ground==RiverbendMap.Surface.Square?C("#97998a"):c.Ground==RiverbendMap.Surface.Soil?C("#a29b77"):c.Ground==RiverbendMap.Surface.Rock?C("#858775"):c.Ground==RiverbendMap.Surface.Marsh?C("#617966"):C("#78875b");
  if(wet){bool bank=new[]{riverMap.At(x-1,y),riverMap.At(x+1,y),riverMap.At(x,y-1),riverMap.At(x,y+1)}.Any(q=>q!=null&&q.Ground!=RiverbendMap.Surface.Water&&q.Ground!=RiverbendMap.Surface.Bridge);color=bank?C("#638a83"):C("#355e68");}
  var east=riverMap.At(x+1,y);var south=riverMap.At(x,y+1);
  RiverFace(g,p[1],p[2],(c.Height-(east==null?0:east.Height))*15*mapZoom,C("#626653"));
  RiverFace(g,p[2],p[3],(c.Height-(south==null?0:south.Height))*15*mapZoom,C("#777965"));
  RiverPoly(g,p,color);
  if(mapZoom>=.38f){
   int texture=wet?2:road?3:c.Ground==RiverbendMap.Surface.Square?1:c.Ground==RiverbendMap.Surface.Meadow||c.Ground==RiverbendMap.Surface.Marsh?0:c.Ground==RiverbendMap.Surface.Rock?5:4;
   g.FillPolygon(riverTextures[texture],p);
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
  if(p.X<riverDrawArea.Left-w||p.X>riverDrawArea.Right+w||p.Y<riverDrawArea.Top-5||p.Y>riverDrawArea.Bottom+h)return;
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
  var origin=RiverScreen(0,0);foreach(var t in riverTextures){t.ResetTransform();t.TranslateTransform(origin.X,origin.Y);t.ScaleTransform(.32f*mapZoom,.22f*mapZoom);}
  g.SmoothingMode=SmoothingMode.None;
  // Only inverse-projected visible cells are visited when zoomed in.
  var corners=new[]{RiverWorld(new PointF(riverDrawArea.Left,riverDrawArea.Top-80)),RiverWorld(new PointF(riverDrawArea.Right,riverDrawArea.Top-80)),RiverWorld(new PointF(riverDrawArea.Left,riverDrawArea.Bottom+120)),RiverWorld(new PointF(riverDrawArea.Right,riverDrawArea.Bottom+120))};
  int x0=Math.Max(0,(int)Math.Floor(corners.Min(p=>p.X))-10),x1=Math.Min(RiverbendMap.Columns-1,(int)Math.Ceiling(corners.Max(p=>p.X))+10),y0=Math.Max(0,(int)Math.Floor(corners.Min(p=>p.Y))-10),y1=Math.Min(RiverbendMap.Rows-1,(int)Math.Ceiling(corners.Max(p=>p.Y))+10);
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
   for(int x=x0;x<=x1;x++){int y=depth-x;if(y>=y0&&y<=y1&&riverMap.At(x,y).Tree)DrawRiverTree(g,x,y);}
   if(!riverBattleRender)foreach(var house in riverMap.Houses)if(house.Lot.Right+house.Lot.Bottom==depth)DrawRiverHouse(g,house);
  }
  if(mapLabels&&!riverBattleRender){
   foreach(var label in riverMap.Labels){var p=RiverScreen(label.Position.X,label.Position.Y);if(!riverDrawArea.Contains(p)||mapZoom>.6f&&label.Style==0)continue;
    var color=label.Style==1?C("#7db6d2"):label.Style==2?C("#db9c87"):label.Style==6?C("#95bd76"):label.Style==4?C("#93bbc1"):gold;
    Rect(g,p.X-42,p.Y-10,84,23,Color.FromArgb(215,20,32,33));Txt(g,label.Name,p.X-35,p.Y-7,12,color,true);
   }
  }
  // Scale witnesses, explicitly staged in the preview, never combat entities.
  if(mapZoom>.65f&&!riverBattleRender){
   var p=RiverScreen(92,76);DrawLive(g,Campaign.Info("engineer"),0,14,p,Campaign.Info("engineer").Size*mapZoom,1);
   p=RiverScreen(94,77);DrawLive(g,Campaign.Info("squad"),0,14,p,Campaign.Info("squad").Size*mapZoom,1);
   p=RiverScreen(97,77);DrawLive(g,Campaign.Info("tank"),0,14,p,Campaign.Info("tank").Size*mapZoom,1);
  }
  g.Restore(saved);
 }
 void DrawMapReview(Graphics g){
  DrawRiverCached(g);Metal(g,0,0,W,55);Emblem(g,28,25,11);Txt(g,"河湾镇",53,10,25,pale,true);Txt(g,"新关卡地形预览  /  220 × 150 格",174,19,14,gold,true);
  Button(g,W-194,9,177,36,"返回关卡选择",CloseMapReview);
  Metal(g,W-212,55,212,H-100);float x=W-197;
  Txt(g,"战区总览",x,76,22,pale,true);Txt(g,"面积约为旧地图 2.6 倍",x,111,13,dim);
  Button(g,x,150,180,38,"全图   [Home]",()=>{RiverFit();hudDirty=true;});
  Button(g,x,202,180,38,"老街与中央桥",()=>RiverFocus(96,75,1.55f));
  Button(g,x,254,180,38,"东部高地与坡道",()=>RiverFocus(165,76,.80f));
  Button(g,x,306,180,38,"南桥与河口湿地",()=>RiverFocus(111,123,1.15f));
  Button(g,x,358,180,38,mapLabels?"地点标注：开 [L]":"地点标注：关 [L]",()=>{mapLabels=!mapLabels;hudDirty=true;});
  Txt(g,"地形图例",x,430,18,gold,true);
  var names=new[]{"河流 / 湖泊：不可通行","三座桥梁：跨河通道","陡崖：阻挡 · 坡道通行","林带：需绕行","中立房屋：28 栋","金色矿区：10 处"};
  for(int i=0;i<names.Length;i++)Txt(g,names[i],x,467+i*27,13,dim);
  Txt(g,"房屋驻防规则",x,650,17,gold,true);Txt(g,"工兵占领后换色",x,680,13,dim);Txt(g,"驻防 4 / 6 / 8 名步兵",x,704,13,dim);Txt(g,"毁楼后从出口撤出",x,728,13,dim);
  Metal(g,0,H-45,W,45);Txt(g,"滚轮缩放  ·  左键或中键拖动  ·  方向键平移  ·  Esc 返回",18,H-31,14,pale);
  Txt(g,"选关预览 · 正式对局限制战术缩放",W-345,H-31,13,gold,true);
 }
 void DrawRiverCached(Graphics g){
  var anchor=RiverScreen(riverViewCenter.X,riverViewCenter.Y);float dx=anchor.X-mapViewport.Width*.5f,dy=anchor.Y-mapViewport.Top-mapViewport.Height*.5f;
  var size=new Size((int)Math.Ceiling(riverDrawArea.Width*scale),(int)Math.Ceiling(riverDrawArea.Height*scale));
  if(riverView==null||riverView.Size!=size||riverViewScale!=scale||Math.Abs(dx)>=RiverMargin||Math.Abs(dy)>=RiverMargin||riverViewZoom!=mapZoom||riverViewLabels!=mapLabels||riverViewBattle!=riverBattleRender){
   if(riverView==null||riverView.Size!=size){if(riverView!=null)riverView.Dispose();riverView=new Bitmap(size.Width,size.Height,PixelFormat.Format32bppPArgb);}
   using(var target=Graphics.FromImage(riverView)){
    target.ScaleTransform(scale,scale);target.TranslateTransform(RiverMargin,RiverMargin-mapViewport.Top);target.InterpolationMode=InterpolationMode.Bilinear;
    bool prior=drawingClient;drawingClient=false;try{DrawRiverMap(target);}finally{drawingClient=prior;}
   }
   riverViewScale=scale;riverViewCenter=mapCenter;riverViewZoom=mapZoom;riverViewLabels=mapLabels;riverViewBattle=riverBattleRender;dx=dy=0;
  }
  var saved=g.Save();g.IntersectClip(mapViewport);g.ResetTransform();g.DrawImageUnscaled(riverView,(int)Math.Round(ox+(riverDrawArea.X+dx)*scale),(int)Math.Round(oy+(riverDrawArea.Y+dy)*scale));g.Restore(saved);
 }
 void StartRiverMission(){
  Restart();world=new Campaign(false,false,true);OpenMapReview();riverMap=world.River;mapReview=false;briefing=false;paused=false;selection.Clear();selection.Add(world.Units[0]);CenterAt(world.Home(0));zoom=DefaultZoom;edgeArmed=true;hits.Clear();hudDirty=true;
 }
 void DrawRiverBattleGround(Graphics g){
  riverBattleRender=true;riverMap=world.River;mapCenter=new PointF(camera.X/Campaign.Tile,camera.Y/Campaign.Tile);mapZoom=zoom;
  try{DrawRiverCached(g);}finally{riverBattleRender=false;}
 }
}
}
