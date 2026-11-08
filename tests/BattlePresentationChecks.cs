using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 internal void CaptureTrafficReview(string folder){
  ClientSize=new Size(W,H);briefing=false;paused=false;modelReview=false;mouse=new PointF(-100,-100);showRanges=false;showGrid=true;
  foreach(bool hauling in new[]{false,true}){
   world=new Campaign(true);world.Units.Clear();world.Ore.Clear();world.Spawn("mcv",0,60,450);world.Spawn("mcv",1,2300,1400);world.Landing=true;selection.Clear();
   string name=hauling?"矿车动态绕障":"紧凑编队行驶";var frames=Path.Combine(folder,"..",".cache",hauling?"haul-review":"compact-review");Directory.CreateDirectory(frames);
   Piece truck=null;Piece plant=null;
   if(hauling){
    plant=world.Spawn("refinery",0,1100,1000);plant.StarterDelivered=true;truck=world.Spawn("miner",0,500,1100);truck.Cargo=180;selection.Add(truck);world.MineOrder(truck,null,plant);world.Tick(.1f);
    // Add the obstacle after dispatch, on the longest existing route segment.
    var points=new[]{truck.P}.Concat(truck.Path).ToArray();int i=Enumerable.Range(0,points.Length-1).OrderByDescending(n=>Campaign.Dist(points[n],points[n+1])).First();
    world.Spawn("mcv",1,(points[i].X+points[i+1].X)/2,(points[i].Y+points[i+1].Y)/2);
   }else{
    var tanks=Enumerable.Range(0,9).Select(i=>world.Spawn("tank",0,800+i%3*32,1100+i/3*32)).ToArray();selection.AddRange(tanks);world.MarchTo(tanks,new PointF(1200,1100),false,true);
   }
   CenterAt(new PointF(hauling?850:1020,1100));zoom=hauling?1.75f:2.25f;hudDirty=true;
   using(var frame=new Bitmap(W,H))using(var g=Graphics.FromImage(frame))for(int i=0;i<216;i++){
    for(int n=0;n<5;n++)world.Tick(1f/60);OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(frames,i.ToString("D3")+".png"),ImageFormat.Png);
   }
   for(int n=0;n<1800;n++)world.Tick(1f/60);
   using(var frame=new Bitmap(W,H))using(var g=Graphics.FromImage(frame)){OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,name+".png"),ImageFormat.Png);}
   if(hauling&&plant.LastReceipt!=180)throw new Exception("recorded detour did not deliver cargo");
   if(!hauling&&selection.Any(u=>Campaign.Dist(u.P,u.Goal)>.2f))throw new Exception("recorded compact formation not assembled");
  }
  Console.WriteLine("Recorded adjacent-cell formation and dynamically blocked haul with verified actual delivery.");
 }
 internal void CheckBattlePresentation(string folder){
  CheckBuildingPaint(folder);
  foreach(float windowScale in new[]{.85f,1.3043478f,1.7391304f})foreach(float z in new[]{.62f,1.05f,2.4f,3.1f}){
   drawingClient=true;scale=windowScale;ox=oy=0;zoom=z;
   using(var pixels=new Bitmap(600,600))using(var g=Graphics.FromImage(pixels)){
    g.ScaleTransform(scale,scale);PixelRing(g,new PointF(150,150),12*z,5*z,Color.Lime);
    var middle=DevicePoint(new PointF(150,150));int rowPixels=0;
    for(int x=0;x<600;x++)if(pixels.GetPixel(x,middle.Y).A>0)rowPixels++;
    if(rowPixels!=2)throw new Exception("Selection line must be one physical pixel at each side: "+rowPixels);
    for(int y=0;y<600;y++)for(int x=0;x<600;x+=2){int alpha=pixels.GetPixel(x,y).A;if(alpha!=0&&alpha!=255)throw new Exception("selection anti-alias fringe");}
   }
  }
  if(HealthColor(.51f)!=Color.FromArgb(69,219,71)||HealthColor(.5f)!=Color.FromArgb(242,203,46)||HealthColor(.25f)!=Color.FromArgb(240,54,42))throw new Exception("health thresholds");
  world=new Campaign(true);world.Units.Clear();selection.Clear();
  foreach(var key in new[]{"squad","tank","miner","factory","refinery","radar"}){
   var u=world.Spawn(key,0,600,600);u.Turn=0;u.Dir=0;
   foreach(float z in new[]{.62f,1.05f,2.4f,3.1f}){
    zoom=z;scale=1.3043478f;ox=oy=0;drawingClient=true;var position=new PointF(220,350);
    var opaque=DeviceBox(VisibleUnitBox(u,position,u.T.Size*zoom));var bar=HealthBox(u,opaque);
    if(bar.Bottom>opaque.Top-3||bar.Width>108||bar.Height>5)throw new Exception("bar must remain above opaque head/roof "+key);
    using(var pixels=new Bitmap(900,900))using(var g=Graphics.FromImage(pixels)){
     g.ScaleTransform(scale,scale);DrawUnitHealth(g,u,position,u.T.Size*zoom);
     for(int x=bar.Left;x<bar.Right;x++)if(pixels.GetPixel(x,bar.Top)!=Color.FromArgb(8,12,9))throw new Exception("bar top border missing");
     if(pixels.GetPixel(bar.Left,bar.Top-1).A!=0||pixels.GetPixel(bar.Left+1,bar.Top+1)==Color.FromArgb(8,12,9))throw new Exception("bar border thickness");
    }
   }
  }
  drawingClient=false;
  CheckGroundScale(folder);
  if(folder!=null){CaptureBattlePresentation(folder);CaptureGroundShadowReview(folder);}
  Console.WriteLine("12 zoom/window combinations: one-device-pixel selection, hard health borders, 50/25% colours, above-roof/head bars passed.");
  Restart();
 }
 internal void CheckGroundScale(string folder){
  var at=new PointF(220,380);
  foreach(float z in new[]{.62f,1.05f,2.4f}){
   foreach(var t in Campaign.Types.Where(t=>t.Building)){
    var lot=Campaign.Footprint(t,PointF.Empty);var box=BuildingRect(lot,at,z);
    // The baked slab's left/front/right vertices must meet the logical ground
    // corners, including rectangular (not only square) foundations.
    float a=224f*t.FoundationWidth/(t.FoundationWidth+t.FoundationHeight),b=224-a;
    var contacts=new[]{new PointF(48,220+(b-a)/4),new PointF(160+(a-b)/2,276),new PointF(272,220+(a-b)/4)};
    var expected=new[]{new PointF(at.X-(lot.Width+lot.Height)*Axis*z/2,at.Y+(lot.Height-lot.Width)*Rise*z/2),new PointF(at.X+(lot.Width-lot.Height)*Axis*z/2,at.Y+(lot.Width+lot.Height)*Rise*z/2),new PointF(at.X+(lot.Width+lot.Height)*Axis*z/2,at.Y+(lot.Width-lot.Height)*Rise*z/2)};
    for(int i=0;i<3;i++){var actual=new PointF(box.X+contacts[i].X*box.Width/320,box.Y+contacts[i].Y*box.Height/320);if(Campaign.Dist(actual,expected[i])>.02f)throw new Exception("building slab versus cells: "+t.Key);}
   }
  }
  zoom=DefaultZoom*1920/W;drawingClient=false;float tileWidth=2*Campaign.Tile*Axis*zoom;
  var foot=world.Spawn("squad",0,800,800);float height=VisibleUnitBox(foot,at,foot.T.Size*zoom).Height;
  if(height/tileWidth<.40f||height/tileWidth>.60f)throw new Exception("infantry height must stay proportional to cell width: "+height/tileWidth);
  if(Campaign.Width/Campaign.Tile!=140||Campaign.Height/Campaign.Tile!=90)throw new Exception("expanded cell count");
  if(folder==null)return;
  using(var board=new Bitmap(1800,1030))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,"统一地格比例 · 建筑 / 车辆 / 单兵",30,20,29,pale,true);
   Txt(g,"所有格子同尺寸，所有对象同倍率；使用游戏实际素材与占地。当前默认镜头 1.55；对象相对尺度保持。",30,67,18,dim);
   int index=0;
   foreach(string key in new[]{"hq","power","barracks","factory","radar","refinery","post","tank","ifv","miner","squad","at"}){
    int row=index/4,col=index++%4;float left=col*450,top=120+row*290;var t=Campaign.Info(key);var center=new PointF(left+225,top+170);
    Func<float,float,PointF> project=(x,y)=>new PointF(center.X+(x-y)*Axis*zoom,center.Y+(x+y)*Rise*zoom);
    float w=(t.Building?t.FoundationWidth:1)*Campaign.Tile,h=(t.Building?t.FoundationHeight:1)*Campaign.Tile;
    float x0=-w/2-Campaign.Tile,y0=-h/2-Campaign.Tile,x1=w/2+Campaign.Tile,y1=h/2+Campaign.Tile;
    using(var pen=new Pen(Color.FromArgb(65,dim),1)){
     for(float x=x0;x<=x1;x+=Campaign.Tile)g.DrawLine(pen,project(x,y0),project(x,y1));
     for(float y=y0;y<=y1;y+=Campaign.Tile)g.DrawLine(pen,project(x0,y),project(x1,y));
    }
    var u=world.Spawn(key,0,800,800);u.Turn=u.Dir=0;
    using(var pen=new Pen(gold,1))g.DrawPolygon(pen,new[]{project(-w/2,-h/2),project(w/2,-h/2),project(w/2,h/2),project(-w/2,h/2)});
    DrawFieldSprite(g,u,center,t.Size*zoom);
    Txt(g,t.Name,left+28,top+218,20,pale,true);
    string detail=t.Building?"地基 "+t.FoundationWidth+" × "+t.FoundationHeight+" 格":t.Foot?"单兵；金框为一格参照":"底盘长 "+UnitGroundMetric.LengthCells(key).ToString("0.00")+" 格（不含炮管）";
    Txt(g,detail,left+28,top+249,16,dim);
   }
   board.Save(Path.Combine(folder,"统一地格比例.png"),ImageFormat.Png);
  }
  using(var board=new Bitmap(1200,900))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,"实际地图扩建 · 140 × 90 格",30,20,27,pale,true);
   Txt(g,"旧范围 105 × 60 格；黄色虚线为旧边界，扩建面积 +100%。俯视总览，不代表游戏镜头。",30,65,17,dim);
   const float k=.32f;const int x=45,y=130;
   g.DrawImage(ground,new RectangleF(x,y,Campaign.Width*k,Campaign.Height*k));
   using(var pen=new Pen(gold,2)){pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;g.DrawRectangle(pen,x,y,2520*k,1440*k);}
   foreach(var patch in world.Ore)Rect(g,x+patch.P.X*k-3,y+patch.P.Y*k-3,6,6,gold);
   Rect(g,x+152*k-5,y+514*k-5,10,10,teal);Txt(g,"己方基地",x+152*k+10,y+514*k-5,17,pale,true);
   Rect(g,x+Campaign.EnemyHome.X*k-5,y+Campaign.EnemyHome.Y*k-5,10,10,red);Txt(g,"敌方基地",x+Campaign.EnemyHome.X*k-70,y+Campaign.EnemyHome.Y*k+15,17,pale,true);
   board.Save(Path.Combine(folder,"地图扩建范围.png"),ImageFormat.Png);
  }
  Console.WriteLine("Seven building slabs align with cell corners at three zooms; infantry/cell scale and expanded map checked.");
  CaptureCompactBuildingScale(folder);
  CaptureInfantryRefinement(folder);
 }
 internal void CaptureInfantryRefinement(string folder){
  ClientSize=new Size(1920,1080);StartMission(true,true);mouse=new PointF(-100,-100);
  if(zoom!=DefaultZoom)throw new Exception("mission must reset default camera");
  using(var shot=new Bitmap(1920,1080))using(var g=Graphics.FromImage(shot)){OnPaint(new PaintEventArgs(g,ClientRectangle));shot.Save(Path.Combine(folder,"新默认镜头开局.png"),ImageFormat.Png);}
  drawingClient=false;
  using(var board=new Bitmap(1600,930))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,"步兵细化 · 八方向与多势力配色",26,18,27,pale,true);
   Txt(g,"同一模型：胸背、肩臂与护具染色；肤色、武器、黄盔和工具保持独立。下排为 1080p 新默认镜头尺寸。",26,62,17,dim);
   int row=0;foreach(var key in new[]{"squad","at","engineer"}){
    var t=Campaign.Info(key);float top=100+row++*265;Txt(g,t.Name,25,top,21,gold,true);
    for(int i=0;i<8;i++){
     var src=SourceCell(t,0,i*2);float x=i*195+10;
     DrawStamp(g,key,src,new RectangleF(x,top+12,215,215),paintSwatches[i]);
     float actual=t.Size*DefaultZoom*1920/W;
     DrawStamp(g,key,src,SoldierBox(new PointF(x+145,top+252),actual),paintSwatches[i]);
    }
   }
   board.Save(Path.Combine(folder,"步兵八方向与多势力.png"),ImageFormat.Png);
  }
  Restart();
 }
 internal void CaptureCompactBuildingScale(string folder){
  if(folder==null)return;
  string[] keys={"hq","factory","barracks","refinery","power","radar","post"};
  string history=Path.GetFullPath(Path.Combine(folder,"../art/archive/building-scale-before-20261106"));
  if(!keys.All(key=>File.Exists(Path.Combine(history,"iso-"+key+".png")))){
   Console.WriteLine("Historical comparison export omitted: optional local snapshots absent; current building footprint checks completed.");return;
  }
  drawingClient=false;zoom=1.5f;
  using(var board=new Bitmap(1960,860))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,"建筑与部队 · 同倍率前后对比",24,16,27,pale,true);
   Txt(g,"上排：修改前    下排：紧凑地基与房体    每栋旁边均为相同尺寸的步枪兵和坦克；镜头、单位没有缩放变化。",24,59,18,dim);
   for(int row=0;row<2;row++)for(int col=0;col<keys.Length;col++){
    var key=keys[col];var t=Campaign.Info(key);int w=row==0?(key=="factory"?4:key=="hq"||key=="barracks"||key=="refinery"?3:2):t.FoundationWidth;
    int d=row==0?(key=="factory"||key=="hq"?3:2):t.FoundationHeight;
    float left=col*280,top=100+row*370;var p=new PointF(left+140,top+200);
    Func<float,float,PointF> project=(x,y)=>new PointF(p.X+(x-y)*Axis*zoom,p.Y+(x+y)*Rise*zoom);
    float halfW=w*Campaign.Tile*.5f,halfD=d*Campaign.Tile*.5f;
    using(var pen=new Pen(Color.FromArgb(65,dim),1)){
     for(float x=-halfW;x<=halfW;x+=Campaign.Tile)g.DrawLine(pen,project(x,-halfD),project(x,halfD));
     for(float y=-halfD;y<=halfD;y+=Campaign.Tile)g.DrawLine(pen,project(-halfW,y),project(halfW,y));
    }
    var lot=new RectangleF(0,0,w*Campaign.Tile,d*Campaign.Tile);var box=BuildingRect(lot,p,zoom);
    if(row==0){using(var old=Image.FromFile(Path.Combine(history,"iso-"+key+".png")))g.DrawImage(old,box,new RectangleF(0,0,320,320),GraphicsUnit.Pixel);}
    else {var u=world.Spawn(key,0,800,800);DrawFieldSprite(g,u,p,t.Size*zoom);}
    var soldier=world.Spawn("squad",0,800,800);var tank=world.Spawn("tank",0,800,800);tank.Dir=0;tank.Turn=0;
    DrawFieldSprite(g,soldier,new PointF(left+85,top+270),soldier.T.Size*zoom);
    DrawFieldSprite(g,tank,new PointF(left+170,top+270),tank.T.Size*zoom);
    Txt(g,t.Name+"  "+w+"×"+d+" 地面格",left+20,top+290,17,pale,true);
    Txt(g,"步兵位置折算 "+(w*2)+"×"+(d*2),left+20,top+319,15,dim);
   }
   board.Save(Path.Combine(folder,"建筑部队比例前后对比.png"),ImageFormat.Png);
  }
 }
 internal void CaptureGroundShadowReview(string folder){
  ClientSize=new Size(1920,1080);briefing=false;paused=false;modelReview=false;
  world=new Campaign(false,true);world.Units.Clear();world.Ore.Clear();selection.Clear();world.Landing=true;
  world.Spawn("mcv",1,3000,1800);
  world.Spawn("barracks",0,520,490);world.Spawn("factory",0,650,490);
  world.Spawn("power",0,790,490);var plant=world.Spawn("refinery",0,940,490);plant.StarterDelivered=true;
  for(int i=0;i<8;i++){
   var u=world.Spawn(i%3==0?"squad":i%3==1?"at":"engineer",0,540+i*36,650);u.Dir=i*2;
  }
  world.Tick(.01f);CenterAt(new PointF(725,565));zoom=1.85f;mouse=new PointF(-100,-100);showGrid=false;showRanges=false;hudDirty=true;
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"士兵与建筑落地阴影.png"),ImageFormat.Png);
  }
 }
 internal void CaptureBattlePresentation(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);briefing=false;paused=false;modelReview=false;
  world=new Campaign(true);world.Units.Clear();world.Spawn("squad",1,2300,1200);world.Landing=true;selection.Clear();
  var factory=world.Spawn("factory",0,520,520);factory.Hp=factory.T.Hp*.85f;selection.Add(factory);
  var plant=world.Spawn("refinery",0,710,520);plant.StarterDelivered=true;plant.Hp=plant.T.Hp*.45f;selection.Add(plant);
  var power=world.Spawn("power",0,880,520);power.Hp=power.T.Hp*.2f;selection.Add(power);
  for(int i=0;i<3;i++){var u=world.Spawn(i==0?"squad":i==1?"at":"engineer",0,455+i*24,690);u.Hp*=i==0?.85f:i==1?.45f:.2f;selection.Add(u);}
  var tank=world.Spawn("tank",0,600,710);tank.Hp*=.85f;tank.Turn=tank.Dir=0;selection.Add(tank);
  var ifv=world.Spawn("ifv",0,650,710);ifv.Hp*=.45f;selection.Add(ifv);
  var arty=world.Spawn("arty",0,700,710);arty.Hp*=.20f;selection.Add(arty);
  var miner=world.Spawn("miner",0,755,710);world.Halt(miner);miner.Cargo=180;selection.Add(miner);
  var mcv=world.Spawn("mcv",0,810,710);selection.Add(mcv);
  world.Tick(.01f);mouse=new PointF(-100,-100);showRanges=false;showGrid=false;
  foreach(float z in new[]{.62f,1.05f,2.4f}){
   CenterAt(new PointF(655,625));zoom=z;hudDirty=true;
   using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
    OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"比例与像素标记-"+z.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png"),ImageFormat.Png);
   }
  }
  // A readable comparison strip, using runtime sprites and indicators at three
  // actual zoom levels. The unit scale is not enlarged to fill the panel.
  using(var board=new Bitmap(1500,670))using(var g=Graphics.FromImage(board)){
   drawingClient=false;g.Clear(C("#202c32"));Txt(g,"步坦比例与像素标记 · 同一套实际游戏绘制",24,18,25,pale,true);
   int col=0;foreach(float z in new[]{.62f,1.05f,2.4f}){
    zoom=z*1920/1472f;float left=col++*500;
    Txt(g,z.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" × 镜头",left+25,72,20,gold,true);
    var foot=world.Live(0).First(u=>u.T.Key=="squad");var p=new PointF(left+130,310);float sz=foot.T.Size*zoom;
    SelectionPixels(g,foot,p,sz);DrawFieldSprite(g,foot,p,sz);DrawUnitHealth(g,foot,p,sz);Txt(g,"步枪兵",left+105,340,15,dim);
    p=new PointF(left+310,310);sz=tank.T.Size*zoom;SelectionPixels(g,tank,p,sz);DrawFieldSprite(g,tank,p,sz);DrawUnitHealth(g,tank,p,sz);Txt(g,"主战坦克",left+280,340,15,dim);
    p=new PointF(left+245,600);sz=power.T.Size*zoom;DrawFieldSprite(g,power,p,sz);DrawUnitHealth(g,power,p,sz);
   }
   board.Save(Path.Combine(folder,"步坦比例与像素血条.png"),ImageFormat.Png);
  }
 }
}
}
