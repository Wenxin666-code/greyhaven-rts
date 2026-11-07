using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 internal void CaptureVehicleSpacing(string folder){
  Directory.CreateDirectory(folder);var t=Campaign.Info("tank");
  using(var board=new Bitmap(1500,650))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,"历史过大模型与当前占地 · 相邻车心一格",25,16,27,pale,true);
   for(int mode=0;mode<2;mode++){
    float left=mode*750;Txt(g,mode==0?"历史过大模型":"当前模型（恢复步坦比例）",left+35,65,24,gold,true);
    Func<float,float,PointF> project=(x,y)=>new PointF(left+365+Axis*2.8f*(x-y),300+Rise*2.8f*(x+y));
    for(int i=-3;i<=3;i++){var a=project(i*24,-72);var b=project(i*24,72);Line(g,a.X,a.Y,b.X,b.Y,Color.FromArgb(55,dim));a=project(-72,i*24);b=project(72,i*24);Line(g,a.X,a.Y,b.X,b.Y,Color.FromArgb(55,dim));}
    for(int row=0;row<3;row++)for(int col=0;col<3;col++){
     var p=project((col-1)*24,(row-1)*24);float raw=t.Size*2.8f/.76f;
     var rect=mode==0?new RectangleF(p.X-raw/2,p.Y-raw*188/320,raw,raw):VehicleRect(t,p,t.Size*2.8f);
     DrawStamp(g,"tank",TankCell(0,56),rect,paintSwatches[0]);
    }
    Txt(g,"地格 24 · 图示极限留距，正常编队间距更宽",left+62,559,17,dim);
   }
   board.Save(Path.Combine(folder,"坦克占地校准.png"),ImageFormat.Png);
  }
  // Film actual simulation/drawing, including a turn. Model view sizes stay
  // large; only battlefield/thumbnail projection uses the calibrated footprint.
  var frames=Path.Combine(folder,"..",".cache","vehicle-spacing");Directory.CreateDirectory(frames);
  ClientSize=new Size(W,H);briefing=false;world=new Campaign(true);world.Units.RemoveAll(u=>u.Side==0);selection.Clear();
  var tanks=new System.Collections.Generic.List<Piece>();
  for(int i=0;i<6;i++){var u=world.Spawn("tank",0,606-i%3*32,337+i/3*32);u.Turn=u.Dir=4;tanks.Add(u);selection.Add(u);world.Move(u,new PointF(766+i/3*32,480-i%3*32),false);}
  CenterAt(new PointF(730,403));zoom=2.7f;
  using(var frame=new Bitmap(W,H))using(var g=Graphics.FromImage(frame))for(int i=0;i<120;i++){
   for(int j=0;j<5;j++)world.Tick(1f/60);OnPaint(new PaintEventArgs(g,ClientRectangle));
   using(var crop=frame.Clone(new Rectangle(380,180,560,410),frame.PixelFormat))crop.Save(Path.Combine(frames,i.ToString("D3")+".png"),ImageFormat.Png);
  }
  for(int n=0;n<1200;n++)world.Tick(1f/60);
  if(tanks.Exists(u=>u.Path.Count>0)){
   foreach(var u in tanks)Console.WriteLine("Recorded tank {0}: {1} -> {2}, path {3}, blocked {4}",u.Id,u.P,u.Goal,u.Path.Count,u.TrafficBlocked);
   throw new Exception("recorded convoy stuck");
  }
  Console.WriteLine("Vehicle occupancy comparison and six-tank movement/turn capture passed.");
 }
 internal void CheckLoadedMinerArt(){
  if(painted["miner-full"].Size!=painted["miner"].Size||painted["miner-full"].Size!=painted["miner-full-paint"].Size)throw new Exception("loaded miner layers");
  var unit=world.Spawn("miner",0,180,650);var p=new PointF(300,300);int payloadHit=0;
  for(int d=0;d<16;d++){
   unit.Dir=(d+2)%16;unit.Cargo=180;if(LiveArtKey(unit)!="miner-full")throw new Exception("loaded miner art state");var src=SourceCell(unit.T,0,d);var rect=VehicleRect(unit.T,p,MobileCell*.76f);
   using(var first=new Bitmap(MobileCell,MobileCell))using(var second=new Bitmap(MobileCell,MobileCell)){
    using(var g=Graphics.FromImage(first))DrawLayeredSource(g,"miner-full",src,new RectangleF(0,0,MobileCell,MobileCell),paintSwatches[0]);
    using(var g=Graphics.FromImage(second))DrawLayeredSource(g,"miner-full",src,new RectangleF(0,0,MobileCell,MobileCell),paintSwatches[4]);
    for(int y=0;y<MobileCell;y+=4)for(int x=0;x<MobileCell;x+=4){
     var a=first.GetPixel(x,y);var b=second.GetPixel(x,y);var mask=painted["miner-full-paint"].GetPixel(src.X+x,src.Y+y);
     if(a.A!=b.A||(mask.A==0&&a!=b))throw new Exception("loaded ore faction isolation");
     var loaded=painted["miner-full"].GetPixel(src.X+x,src.Y+y);var empty=painted["miner"].GetPixel(src.X+x,src.Y+y);
     if(loaded.A>200&&mask.A==0&&Math.Abs(loaded.R-empty.R)+Math.Abs(loaded.G-empty.G)+Math.Abs(loaded.B-empty.B)>80){
      var point=new PointF(rect.X+(x+.1f)/MobileCell*rect.Width,rect.Y+(y+.1f)/MobileCell*rect.Height);
      if(!MobileHit(unit,p,MobileCell*.76f,point))throw new Exception("payload cannot be selected");payloadHit++;
     }
    }
   }
  }
  if(payloadHit==0)throw new Exception("loaded miner does not add visible payload");
  unit.Cargo=0;if(LiveArtKey(unit)!="miner")throw new Exception("empty miner art state");
  world.Units.Remove(unit);Console.WriteLine("Loaded ore: neutral payload, 16 headings, alpha and payload selection passed ("+payloadHit+" samples).");
 }
 internal void ProfileTankPaint(){
  ClientSize=new Size(1920,1080);StartMission(true,true);selection.Clear();
  for(int n=0;n<24;n++)world.Spawn("tank",0,600+n%6*34,600+n/6*40);
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   for(int mode=0;mode<2;mode++){
    if(mode==1)world.Units.RemoveAll(u=>u.T.Key=="tank");camera=new PointF(650,650);zoom=1.05f;
    var times=new System.Collections.Generic.List<double>();
    for(int n=0;n<90;n++){Pan(n%30<15?4:-4,2);var timer=System.Diagnostics.Stopwatch.StartNew();OnPaint(new PaintEventArgs(g,ClientRectangle));if(n>=15)times.Add(timer.Elapsed.TotalMilliseconds);}
    times.Sort();Console.WriteLine("Tank isolation {0}: median {1:F2}ms, p95 {2:F2}ms; stamps {3}, {4:F2} MiB",mode==0?"24 tanks":"no tanks",times[times.Count/2],times[(int)(times.Count*.95)],stamps.Count,stampBytes/1048576.0);
   }
  }Restart();
 }
 internal void CheckTankPaint(){
  var sheet=painted["tank"];if(sheet.Size!=painted["tank-paint"].Size||sheet.Size!=painted["tank-wreck"].Size)throw new Exception("tank layer dimensions");
  int pixelChanges=0;
  for(int frame=0;frame<64;frame++)using(var a=new Bitmap(TankCellSize,TankCellSize))using(var b=new Bitmap(TankCellSize,TankCellSize)){
   var src=TankCell(0,frame);var box=new RectangleF(0,0,TankCellSize,TankCellSize);
   using(var g=Graphics.FromImage(a))DrawLayeredSource(g,"tank",src,box,Color.FromArgb(40,100,190));
   using(var g=Graphics.FromImage(b))DrawLayeredSource(g,"tank",src,box,Color.FromArgb(65,180,70));
   int ink=0,change=0,neutral=0;
   for(int y=2;y<TankCellSize-2;y+=3)for(int x=2;x<TankCellSize-2;x+=3){
    var ca=a.GetPixel(x,y);var cb=b.GetPixel(x,y);var mask=painted["tank-paint"].GetPixel(src.X+x,src.Y+y);
    if(ca.A!=cb.A)throw new Exception("paint alters silhouette");
    if(ca.A<128)continue;ink++;
    if(mask.A==0){neutral++;if(ca!=cb)throw new Exception("neutral material tinted");}
    if(Math.Abs(ca.R-cb.R)+Math.Abs(ca.G-cb.G)+Math.Abs(ca.B-cb.B)>40)change++;
   }
   if(ink<200||change<ink*.08||neutral<ink*.3)throw new Exception("tank needs distinct paint and neutral surfaces: "+frame+" "+change+"/"+ink);
   pixelChanges+=change;
  }
  SetUnitColor(7,Color.FromArgb(117,52,183));if(UnitColor(7)!=Color.FromArgb(117,52,183))throw new Exception("arbitrary faction RGB");
  // Cached color must not carry into another palette choice or mutate the source.
  drawingClient=true;scale=1;zoom=1;ox=oy=0;
  using(var a=new Bitmap(150,150))using(var b=new Bitmap(150,150))using(var ga=Graphics.FromImage(a))using(var gb=Graphics.FromImage(b)){
   var src=TankCell(0,56);var box=new RectangleF(0,0,150,150);DrawStamp(ga,"tank",src,box,paintSwatches[2]);DrawStamp(gb,"tank",src,box,paintSwatches[4]);int changed=0;
   for(int y=0;y<150;y+=2)for(int x=0;x<150;x+=2)if(a.GetPixel(x,y)!=b.GetPixel(x,y))changed++;
   if(changed<100)throw new Exception("cached faction colors alias");
  }
  drawingClient=false;ClearStampCache();
  Console.WriteLine("64 layered tank headings: custom faction RGB, neutral material isolation, alpha and color cache passed ("+pixelChanges+" changed samples).");
 }
 internal void CaptureTankReview(string folder){
  ClientSize=new Size(1920,1080);OpenModelReview();modelSpin=false;modelAngle=56;
  using(var probe=new Bitmap(1920,1080))using(var g=Graphics.FromImage(probe)){
   OnPaint(new PaintEventArgs(g,ClientRectangle));
   Action<string> click=id=>{var r=TestControlBox(id);OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,(int)(ox+(r.X+r.Width/2)*scale),(int)(oy+(r.Y+r.Height/2)*scale),0));OnPaint(new PaintEventArgs(g,ClientRectangle));};
   var previous=UnitColor(0);click("绿");if(modelColor.ToArgb()!=paintSwatches[2].ToArgb())throw new Exception("model palette click");
   click("应用到己方势力");if(UnitColor(0).ToArgb()!=modelColor.ToArgb())throw new Exception("apply unit palette");
   click("蓝");SetUnitColor(0,previous);
   foreach(var key in roster){
    click(Campaign.Info(key).Name);if(roster[modelUnit]!=key)throw new Exception("model selection "+key);
    probe.Save(Path.Combine(folder,"模型预览-"+key+".png"),ImageFormat.Png);
   }
   click("切换满载");if(!modelLoaded)throw new Exception("loaded mining preview");
   click("建筑模型");if(!modelBuildings||modelSpin)throw new Exception("building preview mode");
   foreach(var key in buildingRoster){click(Campaign.Info(key).Name);if(buildingRoster[modelUnit]!=key)throw new Exception("building preview selection");}
   click("紫");click("应用到己方势力");if(UnitColor(0)!=paintSwatches[4])throw new Exception("building palette application");
   click("车辆工厂");probe.Save(Path.Combine(folder,"建筑模型预览.png"),ImageFormat.Png);
   click("蓝");SetUnitColor(0,previous);click("部队模型");click("主战坦克");modelSpin=false;
  }
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"坦克模型预览.png"),ImageFormat.Png);}
  using(var board=new Bitmap(1440,900))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,"主战坦克 / 同一模型，运行时涂装",32,20,28,pale,true);
   Txt(g,"下面八种颜色来自同一套灰度涂装图层；金属、履带、玻璃保持原色。",33,62,17,dim);
   for(int i=0;i<8;i++){
    int x=20+i%4*355,y=108+i/4*361;var crop=TankCell(0,i<4?56:24);DrawStamp(g,"tank",crop,new RectangleF(x,y,340,340),paintSwatches[i]);
    Txt(g,new[]{"蓝","红","绿","金","紫","青","橙","白"}[i],x+155,y+322,20,pale,true);
   }
   board.Save(Path.Combine(folder,"坦克多色涂装.png"),ImageFormat.Png);
  }
  using(var board=new Bitmap(1440,990))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,"全单位模型 / 同材质、同光照、动态势力涂装",32,20,28,pale,true);
   Txt(g,"正式游戏素材 · 下方小样为默认镜头比例",33,62,17,dim);
   for(int i=0;i<roster.Length;i++){
    var t=Campaign.Info(roster[i]);int x=20+i%4*355,y=98+i/4*425;var source=SourceCell(t,0,14);
    Txt(g,t.Name,x+15,y+15,22,gold,true);
    for(int side=0;side<2;side++){
     var size=t.Foot?220:235;var left=x+side*160-23;
     DrawStamp(g,t.Key,source,new RectangleF(left,y+70,size,size),paintSwatches[side==0?0:4]);
     var p=new PointF(x+75+side*162,y+368);float actual=t.Size*DefaultZoom*1920/1472;
     DrawStamp(g,t.Key,source,t.Foot?SoldierBox(p,actual):VehicleRect(t,p,actual),paintSwatches[side==0?0:4]);
    }
   }
   board.Save(Path.Combine(folder,"全单位模型.png"),ImageFormat.Png);
  }
  // The review sheets use the embedded runtime sprites, with front and rear
  // views and true 1080p default-camera thumbnails. Never substitute concept art.
  foreach(bool infantry in new[]{false,true})using(var board=new Bitmap(1600,infantry?1000:1250))using(var g=Graphics.FromImage(board)){
   g.Clear(C("#202c32"));Txt(g,infantry?"步兵模型 · 轻装 / 重装 / 作业装具":"载具模型 · 楔形 / 开放炮架 / 四履带 / 铰接矿斗",32,20,27,pale,true);
   Txt(g,"当前游戏实际素材 · 蓝与紫来自运行时涂装 · 最下方为默认镜头尺寸",33,65,17,dim);
   var keys=infantry?new[]{"squad","at","engineer"}:new[]{"ifv","arty","mcv","miner"};
   for(int i=0;i<keys.Length;i++){
    var t=Campaign.Info(keys[i]);int x=infantry?i*530:i%2*800,y=infantry?110:110+i/2*550;
    Txt(g,t.Name,x+30,y+5,23,gold,true);
    int large=infantry?530:650;var key=t.Key=="miner"?"miner-full":t.Key;
    DrawStamp(g,key,SourceCell(t,0,14),new RectangleF(x+(infantry?-20:40),y+(infantry?0:-35),large,large),paintSwatches[0]);
    DrawStamp(g,key,SourceCell(t,0,6),new RectangleF(x+(infantry?10:520),y+(infantry?470:220),infantry?275:245,infantry?275:245),paintSwatches[4]);
    float actual=t.Size*DefaultZoom*1920/1472;
    var small=new PointF(x+(infantry?370:440),y+(infantry?710:450));
    DrawStamp(g,key,SourceCell(t,0,14),t.Foot?SoldierBox(small,actual):VehicleRect(t,small,actual),paintSwatches[0]);
    Txt(g,"默认镜头",small.X-32,small.Y+24,13,dim);
   }
   board.Save(Path.Combine(folder,infantry?"步兵模型-v04.png":"载具模型-v04.png"),ImageFormat.Png);
  }
  ModelKey(Keys.Right);if(modelAngle!=57||modelSpin)throw new Exception("model viewer stepping");ModelKey(Keys.Space);if(!modelSpin)throw new Exception("model viewer spin");ModelKey(Keys.Escape);if(modelReview)throw new Exception("model viewer close");
  Restart();
 }
}
}
