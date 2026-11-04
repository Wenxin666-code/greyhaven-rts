using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Greyhaven;
class WindowChecks {
 static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Field(CommandWindow f,string n){return typeof(CommandWindow).GetField(n,flags).GetValue(f);}
 static void Frame(CommandWindow f,string output){using(var b=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));if(output!=null)b.Save(output,System.Drawing.Imaging.ImageFormat.Png);}}
 static void Press(CommandWindow f,int x,int y,MouseButtons button=MouseButtons.Left){float size=(float)Field(f,"scale");int xx=(int)(x*size+(float)Field(f,"ox")),yy=(int)(y*size+(float)Field(f,"oy"));
  var e=new MouseEventArgs(button,1,xx,yy,0);foreach(var n in new[]{"OnMouseDown","OnMouseUp"})typeof(CommandWindow).GetMethod(n,flags).Invoke(f,new object[]{e});Frame(f,null);}
 static void MapClick(CommandWindow f,int x,int y,MouseButtons button=MouseButtons.Left){
  var p=(PointF)typeof(CommandWindow).GetMethod("ScreenOf",flags).Invoke(f,new object[]{new PointF(x,y)});Press(f,(int)p.X,(int)p.Y,button);
 }
 [STAThread]static int Main(string[] args){try{Run(args);return 0;}catch(Exception e){Console.Error.WriteLine(e.Message);Console.Error.WriteLine(e.StackTrace);return 1;}}
 static void Run(string[] args){
  using(var f=new CommandWindow()){if(args.Length==1&&args[0]=="--window"){f.ProfileWindow();return;}if(args.Length==1&&args[0]=="--profile"){f.ProfileBattle();return;}if(args.Length==2&&args[0]=="--mine"){f.RecordOreTrip(args[1]);return;}if(args.Length==2&&args[0]=="--drive"){f.RecordStreetTurn(args[1]);return;}if(args.Length>0)f.CheckWarehouseView(args[0]);f.CheckEdgeScroll();f.CheckFocusAndMining();if(args.Length>0)f.CheckScreenTravel(args[0]);f.CheckPlates(args.Length==0?null:Path.Combine(Path.GetDirectoryName(args[0]),"角度校准.png"));Frame(f,args.Length==0?null:Path.Combine(Path.GetDirectoryName(args[0]),"主界面.png"));Press(f,190,641);if(!(bool)Field(f,"rulesOpen"))throw new Exception("rules entry");
   Press(f,157,290);if(!(bool)Field(f,"unitRoster"))throw new Exception("roster entry");
   if(args.Length>0)Frame(f,Path.Combine(Path.GetDirectoryName(args[0]),"单位图鉴.png"));
   Press(f,158,700);if((bool)Field(f,"rulesOpen"))throw new Exception("rules return");Press(f,220,571);
   if(!(bool)Field(f,"missionPicker")||!(bool)Field(f,"briefing"))throw new Exception("start must open mission selection");
   Press(f,190,348);Press(f,865,551);Frame(f,args.Length==0?null:Path.Combine(Path.GetDirectoryName(args[0]),"关卡选择.png"));
   Press(f,1194,651);var standard=(Campaign)Field(f,"world");
   if(standard.Tutorial||standard.Assisted)throw new Exception("standard campaign");
   typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.Escape)});Frame(f,null);Press(f,860,575);
   Press(f,701,550);Press(f,1194,651);var easy=(Campaign)Field(f,"world");
   if(easy.Tutorial||!easy.Assisted)throw new Exception("assisted campaign");
   typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.Escape)});Frame(f,null);Press(f,860,575);
   Press(f,140,694);if((bool)Field(f,"missionPicker"))throw new Exception("back to title");
   Press(f,220,571);Press(f,180,255);Press(f,1194,651);if((bool)Field(f,"briefing"))throw new Exception("tutorial launch");
   var screen=new PointF(647,405);var inv=typeof(CommandWindow).GetMethod("WorldOf",flags);
   var before=(PointF)inv.Invoke(f,new object[]{screen});float ui=(float)Field(f,"scale");
   typeof(CommandWindow).GetMethod("OnMouseWheel",flags).Invoke(f,new object[]{new MouseEventArgs(MouseButtons.None,0,(int)(screen.X*ui+(float)Field(f,"ox")),(int)(screen.Y*ui+(float)Field(f,"oy")),120)});
   var after=(PointF)inv.Invoke(f,new object[]{screen});if(Campaign.Dist(before,after)>2||(float)Field(f,"zoom")<=1.7f)throw new Exception("cursor anchored zoom");Frame(f,null);
   Press(f,1276,298);Press(f,1170,346);if(((Campaign)Field(f,"world")).LastOrder!=null)throw new Exception("locked card accepted");
   Press(f,1150,298);
   var w=(Campaign)Field(f,"world");Press(f,399,738);if(!w.Landing)throw new Exception("deploy button");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   Press(f,1170,346);MapClick(f,263,548);if(!w.Units.Any(o=>o.Side==0&&o.T.Key=="power"))throw new Exception("construction click");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   if(w.LessonKey!="refinery")throw new Exception("missing refinery stage");
   Press(f,767,90);MapClick(f,66,450);
   for(int i=0;i<100;i++)w.Tick(.1f);Frame(f,null);
   if(w.LessonKey!="ore")throw new Exception("ore stage "+w.LessonKey);
   Press(f,767,90);var miner=w.Live(0).First(o=>o.T.Key=="miner");
   if(!((System.Collections.Generic.List<Piece>)Field(f,"selection")).Contains(miner))throw new Exception("focus truck");
   for(int i=0;i<400&&w.LessonKey=="ore";i++)w.Tick(.1f);
   if(w.LessonKey!="receipt"||w.OreReceipts[0]!=0)throw new Exception("deposit lesson before sale");
   for(int i=0;i<400&&w.LessonKey=="receipt";i++)w.Tick(.1f);
   if(w.LessonKey!="barracks"||w.OreReceipts[0]<=0)throw new Exception("first receipt");
   typeof(CommandWindow).GetMethod("CenterAt",flags).Invoke(f,new object[]{new PointF(255,465)});Frame(f,null);
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);Press(f,1350,346);MapClick(f,123,610);for(int i=0;i<65;i++)w.Tick(.1f);
   Press(f,1170,404);MapClick(f,339,548);for(int i=0;i<110;i++)w.Tick(.1f);
   Press(f,1276,298);Press(f,1170,346);if(!w.Live(0).Any(o=>o.Queue.Count>0))throw new Exception("training click");
   Press(f,1170,346);Press(f,1400,298);
   if(!(bool)Field(f,"queueOpen"))throw new Exception("queue tab");
   var source=w.Live(0).Single(o=>o.Queue.Count>0);if(source.Queue.Count!=2)throw new Exception("second order");
   Frame(f,args.Length==0?null:args[0]);int funds=w.Funds[0];Press(f,1400,429);if(source.Queue.Count!=1||w.Funds[0]!=funds+85)throw new Exception("cancel waiting slot");
   if(w.LessonKey!="squad"||!w.Tutorial)throw new Exception("default tutorial step");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   if(w.LessonKey!="west")throw new Exception("delivery milestone");Press(f,767,90);MapClick(f,172,214);
   for(int i=0;i<400;i++)w.Tick(.1f);Frame(f,null);if(w.LessonKey!="ifv")throw new Exception("capture plus prebuilt factory");
   Press(f,767,90);Press(f,1350,404);for(int i=0;i<95;i++)w.Tick(.1f);Frame(f,null);
   if(args.Length>0){
    w.Upgrade();w.Train(Campaign.Info("tank"),0,null);for(int i=0;i<140;i++)w.Tick(.1f);
    Press(f,1276,298);typeof(CommandWindow).GetMethod("CenterAt",flags).Invoke(f,new object[]{new PointF(300,495)});
    typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.G)});
    var armor=w.Live(0).FirstOrDefault(u=>u.T.Key=="tank");if(armor!=null){
     var picked=(System.Collections.Generic.List<Piece>)Field(f,"selection");picked.Clear();picked.Add(armor);
    }
    var factory=w.Live(0).First(u=>u.T.Key=="factory");w.Train(Campaign.Info("tank"),0,factory);w.Train(Campaign.Info("ifv"),0,factory);for(int t=0;t<28;t++)w.Tick(.1f);
    Frame(f,Path.Combine(Path.GetDirectoryName(args[0]),"等距战场.png"));while(factory.Queue.Count>0)w.CancelOrder(factory,0);
   }
   if(w.LessonKey!="port")throw new Exception("ifv training");Press(f,767,90);
   typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.A)});MapClick(f,1330,292);
   for(int i=0;i<1100;i++)w.Tick(.1f);Frame(f,null);if(!w.Won)throw new Exception("tutorial victory through interface: "+w.LessonKey+" "+w.Hold+" "+w.Radio+" / "+String.Join(" ",w.Live(0).Where(u=>u.T.Foot).Select(u=>u.P+" to "+u.Goal)));
   Press(f,688,574);w=(Campaign)Field(f,"world");if(w.Tutorial||!w.Assisted)throw new Exception("handoff to assisted mission");var trooper=w.Live(0).First(o=>o.T.Foot);var nearby=w.Spawn("squad",1,351,421);for(int i=0;i<3;i++)w.Tick(.1f);Frame(f,null);
   MapClick(f,(int)trooper.P.X,(int)trooper.P.Y);var units=(System.Collections.Generic.List<Piece>)Field(f,"selection");
   if(!units.Contains(trooper))throw new Exception("classic select");
   MapClick(f,(int)trooper.P.X,(int)trooper.P.Y);if(units.Count!=0)throw new Exception("second click should deselect");
   MapClick(f,(int)trooper.P.X,(int)trooper.P.Y);if(!units.Contains(trooper))throw new Exception("third click selects again");
   MapClick(f,395,470);if(!units.Contains(trooper)||trooper.Path.Count==0)throw new Exception("classic ground movement");
   MapClick(f,(int)nearby.P.X,(int)nearby.P.Y);if(trooper.Target!=nearby)throw new Exception("classic direct targeting");
   MapClick(f,395,470,MouseButtons.Right);if(units.Count!=0||trooper.Target!=nearby)throw new Exception("right deselect must not issue a move");
   MapClick(f,(int)trooper.P.X,(int)trooper.P.Y);
   var targetScreen=(PointF)typeof(CommandWindow).GetMethod("ScreenOf",flags).Invoke(f,new object[]{new PointF(390,459)});
   typeof(CommandWindow).GetField("mouse",flags).SetValue(f,targetScreen);
   typeof(CommandWindow).GetMethod("ContextOrder",flags).Invoke(f,new object[]{Keys.Control|Keys.Shift});
   if(!trooper.Advancing||trooper.Path.Count==0||trooper.FirePoint.HasValue)throw new Exception("classic attack move");
   typeof(CommandWindow).GetMethod("ContextOrder",flags).Invoke(f,new object[]{Keys.Control});
   if(!trooper.FirePoint.HasValue||trooper.Target!=null)throw new Exception("ground suppression");
   var aim=trooper.FirePoint.Value;bool fired=false;for(int i=0;i<150;i++){w.Tick(.1f);if(w.Shots.Any(hit=>Campaign.Dist(hit.B,aim)<1))fired=true;}
   if(!fired)throw new Exception("no shot at designated ground");
   typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.S)});
   if(trooper.FirePoint.HasValue||trooper.Path.Count>0)throw new Exception("stop ground bombardment");
   Console.WriteLine("Native UI mission entry, deploy, placement, production and render passed.");}
 }
}

namespace Greyhaven {
partial class CommandWindow {
 internal void CheckWarehouseView(string screenshot){
  StartMission(true,true);var site=world.Sites[0];
  CenterAt(site.P);zoom=2.4f;world.Spawn("squad",0,site.P.X+12,site.P.Y+14);
  for(int i=0;i<70;i++)world.Tick(.1f);
  if(site.Owner!=0)throw new Exception("warehouse was not captured");
  using(var b=new Bitmap(MW,MapTop+MH))using(var g=Graphics.FromImage(b)){
   scale=1;ox=oy=0;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;DrawBattle(g);
   using(var crop=b.Clone(new Rectangle(MW/2-266,MapTop+MH/2-246,532,410),b.PixelFormat))
    crop.Save(Path.Combine(Path.GetDirectoryName(screenshot),"西岸仓库.png"),System.Drawing.Imaging.ImageFormat.Png);
  }
  Restart();
 }
 internal void CheckEdgeScroll(){
  StartMission(true,true);camera=new PointF(800,500);scale=1;ox=oy=0;var home=camera;
  Action<int,int> point=(x,y)=>OnMouseMove(new MouseEventArgs(MouseButtons.None,0,x,y,0));
  Action advance=()=>{for(int i=0;i<20;i++)EdgeScroll(.02f,mouse,0,0);};
  for(int dx=-1;dx<=1;dx++)for(int dy=-1;dy<=1;dy++){
   camera=home;ResetEdgeScroll();point(dx==0?MW/2:dx<0?1:MW-1,MapTop+(dy==0?MH/2:dy<0?1:MH-1));
   EdgeScroll(.02f,mouse,0,0);if(camera!=home)throw new Exception("passing edge should not pan");advance();
   var screen=ScreenOf(home);float x=MW*.5f-screen.X,y=MapTop+MH*.5f-screen.Y;
   if((dx==0?Math.Abs(x)>.01:Math.Sign(x)!=dx)||(dy==0?Math.Abs(y)>.01:Math.Sign(y)!=dy))throw new Exception("edge direction");
  }
  camera=home;point(MW+6,300);advance();if(camera!=home)throw new Exception("sidebar pan");
  point(0,300);OnMouseLeave(EventArgs.Empty);
  foreach(var direction in new[]{new Point(-1,0),new Point(1,0),new Point(0,-1),new Point(0,1),new Point(1,1)}){
   camera=home;for(int i=0;i<15;i++)EdgeScroll(.02f,new PointF(-50,-50),direction.X,direction.Y);
   var old=ScreenOf(home);if((direction.X!=0&&Math.Sign(MW*.5f-old.X)!=direction.X)||(direction.Y!=0&&Math.Sign(MapTop+MH*.5f-old.Y)!=direction.Y))throw new Exception("pan outside game");
  }
  var display=new Rectangle(-100,0,2500,1500);camera=home;
  for(int i=0;i<16;i++)EdgeAtClient(.02f,new Point(ClientSize.Width+50,300),new Point(ClientSize.Width+130,400),display);
  if(ScreenOf(home).X>=MW*.5f)throw new Exception("right overflow past sidebar did not scroll");
  camera=home;for(int i=0;i<16;i++)EdgeAtClient(.02f,new Point(ClientSize.Width-1,300),new Point(display.Right-1,300),display);
  if(ScreenOf(home).X>=MW*.5f)throw new Exception("physical monitor edge did not scroll");
  camera=home;ResetEdgeScroll();for(int i=0;i<8;i++)EdgeScroll(.02f,mouse,1,0);if(camera!=home)throw new Exception("pan after deactivation");
  point(0,300);paused=true;advance();paused=false;drag=true;advance();drag=false;
  hits.Add(new HitBox{Box=new RectangleF(0,280,22,40)});advance();hits.Clear();if(camera!=home)throw new Exception("pan behind overlay or while dragging");
  camera=new PointF(Campaign.Width,0);point(MW-1,MapTop+MH/2);advance();
  if(camera.X!=Campaign.Width||camera.Y!=0)throw new Exception("world edge clamp");
  Restart();Console.WriteLine("Edge pan: eight directions, dwell, map bounds, sidebar, mouse leave, pause and drag passed.");
 }
 internal void CheckFocusAndMining(){
  StartMission(true,true);var near=world.Live(0).First(u=>u.T.Key=="squad");
  var far=world.Spawn("squad",0,1480,840);var enemy=world.Spawn("squad",1,242,402);
  CenterAt(near.P);selection.Clear();selection.Add(near);
  Action<Keys> tap=k=>{OnKeyDown(new KeyEventArgs(k));OnKeyUp(new KeyEventArgs(k));};
  tap(Keys.T);if(selection.Count!=2||selection.Contains(far)||selection.Contains(enemy))throw new Exception("viewport type select");
  tap(Keys.T);if(selection.Count!=3||!selection.Contains(far)||selection.Contains(enemy))throw new Exception("global type select");
  var saved=camera;float z=zoom;tap(Keys.Control|Keys.F1);zoom=3;CenterAt(far.P);tap(Keys.F1);
  if(camera!=saved||zoom!=z)throw new Exception("camera bookmark");
  for(int slot=0;slot<4;slot++){
   CenterAt(new PointF(190+slot*100,210+slot*90));tap(Keys.Control|(Keys)((int)Keys.F1+slot));
  }
  for(int slot=3;slot>=0;slot--){tap((Keys)((int)Keys.F1+slot));if(camera!=new PointF(190+slot*100,210+slot*90))throw new Exception("independent bookmarks");}
  tap(Keys.H);if(camera!=world.Units[0].P)throw new Exception("home before deploy");
  for(int i=0;i<5;i++)world.Tick(.1f);
  if(!world.LastEvent.HasValue)throw new Exception("incoming attack event");
  tap(Keys.Space);if(camera!=world.LastEvent.Value||paused)throw new Exception("jump to event");
  var truck=world.Spawn("miner",0,80,595);world.Tick(.1f);world.Halt(truck);
  var ore=world.Ore.First();selection.Clear();selection.Add(truck);CenterAt(ore.P);mouse=ScreenOf(ore.P);ContextOrder(Keys.None);
  if(!truck.Mining||truck.Patch!=ore)throw new Exception("click visible ore");
  var hq=world.Units[0];if(!world.Deploy(hq))throw new Exception("deploy during focus test");
  CenterAt(far.P);tap(Keys.H);if(camera!=hq.P)throw new Exception("home after deployment");
  Restart();tap(Keys.Enter);var fresh=camera;tap(Keys.F1);if(camera!=fresh)throw new Exception("old bookmark leaked");
  Restart();Console.WriteLine("Same-type selection, four-slot camera controls, event focus and miner orders passed.");
 }
 internal void RecordOreTrip(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(W,H);StartMission(false,true);
  if(!world.Deploy(world.Units[0]))throw new Exception("deploy");
  for(int n=0;n<60;n++)world.Tick(.1f);
  if(!world.Build(Campaign.Info("power"),new PointF(260,550)))throw new Exception("build power");
  for(int n=0;n<60;n++)world.Tick(.1f);
  if(!world.Build(Campaign.Info("refinery"),new PointF(66,450)))throw new Exception("build refinery");
  for(int n=0;n<92;n++)world.Tick(.1f);
  var plant=world.Live(0).First(u=>u.T.Key=="refinery");
  CenterAt(new PointF(70,534));zoom=1.75f;selection.Clear();
  using(var frame=new Bitmap(W,H))using(var g=Graphics.FromImage(frame)){
   for(int i=0;i<160;i++){
    for(int n=0;n<6;n++)world.Tick(1f/60);
    var state=g.Save();OnPaint(new PaintEventArgs(g,ClientRectangle));g.Restore(state);
    using(var clip=frame.Clone(new Rectangle(305,172,565,408),frame.PixelFormat))
     clip.Save(Path.Combine(folder,i.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
   }
  }
  if(plant.LastReceipt<=0)throw new Exception("filmed route never deposited any ore");
 }
 internal void RecordStreetTurn(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(W,H);briefing=false;world=new Campaign(true);
  var tank=world.Spawn("tank",0,696,361);tank.Turn=4;tank.Dir=4;
  CenterAt(new PointF(755,384));zoom=2.4f;selection.Clear();selection.Add(tank);
  if(!world.Move(tank,new PointF(766,437),false))throw new Exception("street turn blocked");
  using(var frame=new Bitmap(W,H))using(var g=Graphics.FromImage(frame)){
   for(int i=0;i<80;i++){
    for(int n=0;n<4;n++)world.Tick(1f/60);
    var state=g.Save();OnPaint(new PaintEventArgs(g,ClientRectangle));g.Restore(state);
    using(var clip=frame.Clone(new Rectangle(320,208,550,350),frame.PixelFormat))
     clip.Save(Path.Combine(folder,i.ToString("D3")+".png"),System.Drawing.Imaging.ImageFormat.Png);
   }
  }
  if(Campaign.Dist(tank.P,new PointF(766,437))>.02f)throw new Exception("road drive incomplete");
 }
 sealed class PaintTimes:NativeWindow {
  public readonly System.Collections.Generic.List<double> Intervals=new System.Collections.Generic.List<double>();
  readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();double previous;
  protected override void WndProc(ref Message m){
   base.WndProc(ref m);if(m.Msg!=15)return;double now=clock.Elapsed.TotalMilliseconds;
   if(previous!=0)Intervals.Add(now-previous);previous=now;
  }
 }
 internal void ProfileWindow(){
  ClientSize=new Size(1260,706);StartMission(true,true);
  var tank=world.Spawn("tank",0,201,486);CenterAt(new PointF(129,516));
  world.Spawn("factory",0,323,548).Ready=.18f;Show();paused=false;
  var probe=new PaintTimes();probe.AssignHandle(Handle);int run=0;
  var goals=new[]{new PointF(251,492),new PointF(204,479),new PointF(247,510)};
  var results=new System.Collections.Generic.List<double[]>();
  try{
   using(var loop=new ApplicationContext())using(var deadline=new Timer{Interval=2600}){
    world.Move(tank,goals[run],false);
    deadline.Tick+=(sender,args)=>{
     results.Add(probe.Intervals.Skip(2).OrderBy(v=>v).ToArray());probe.Intervals.Clear();
     if(++run==goals.Length){deadline.Stop();loop.ExitThread();}else world.Move(tank,goals[run],false);
    };
    deadline.Start();Application.Run(loop);
   }
   foreach(var times in results){
    if(times.Length<4)throw new Exception("no window paints");
    Console.WriteLine("Live 1260x706: {0:F1} paints/s, p95 interval {1:F1}ms",1000/times.Average(),times[(int)(times.Length*.95)]);
   }
  }finally{probe.ReleaseHandle();Close();}
 }
 internal void ProfileBattle(){
  briefing=false;var tank=world.Spawn("tank",0,590,361);
  foreach(var size in new[]{new Size(1260,706),new Size(1920,1080)}){
   ClientSize=size;
   using(var surface=new Bitmap(size.Width,size.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
   using(var g=Graphics.FromImage(surface)){
    foreach(bool pan in new[]{false,true}){
     camera=new PointF(600,361);world.Move(tank,new PointF(tank.P.X<720?780:540,361),false);
     var samples=new System.Collections.Generic.List<double>();
     for(int i=0;i<50;i++){
      var timer=System.Diagnostics.Stopwatch.StartNew();world.Tick(1f/60);
      if(pan)Pan(2,1);
      var state=g.Save();OnPaint(new PaintEventArgs(g,ClientRectangle));g.Restore(state);g.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
      if(i>=5)samples.Add(timer.Elapsed.TotalMilliseconds);
     }
     samples.Sort();Console.WriteLine("{0}x{1} {2} median {3:F2}ms p95 {4:F2}ms",size.Width,size.Height,pan?"pan":"fixed",samples[22],samples[42]);
    }
   }
  }
 }
 internal void CheckScreenTravel(string shot){
  var measurements=File.ReadAllLines(Path.Combine(Path.GetDirectoryName(shot),"../art/source/aligned/tank-bearing-check.csv")).Skip(1)
   .Select(line=>line.Split(',').Select(s=>double.Parse(s,System.Globalization.CultureInfo.InvariantCulture)).ToArray()).ToArray();
  double worst=0;int headings=0;
  for(int j=0;j<16;j++){
   var sim=new Campaign(true);var tank=sim.Spawn("tank",0,700,730);double a=(j+.31)*Math.PI/8;
   var goal=new PointF(tank.P.X+52*(float)Math.Sin(a),tank.P.Y+52*(float)Math.Cos(a));sim.Move(tank,goal,false);
   for(int i=0;i<220;i++){var old=tank.P;sim.Tick(.025f);if(Campaign.Dist(old,tank.P)<.015f)continue;
    var p=ScreenOf(old);var q=ScreenOf(tank.P);double x=q.X-p.X,y=q.Y-p.Y;
    int frame=TankFrame(tank);var m=measurements[frame];double error=Math.Abs(Math.Atan2(x*m[2]-y*m[1],x*m[1]+y*m[2]))*180/Math.PI;
    worst=Math.Max(worst,error);headings|=1<<(tank.Dir/2);}
   if(Campaign.Dist(tank.P,goal)>.02f)throw new Exception("lost arbitrary destination");
  }
  if(worst>2||headings!=255)throw new Exception("visible chassis / route disagreement "+worst);
  Console.WriteLine("Measured gun axis vs actual screen travel: all eight headings; max "+worst.ToString("0.00")+" degrees.");
 }
 internal void CheckPlates(string path){
  var keys=new[]{"hq","power","barracks","factory","radar","post","depot","freight","harbormaster"};
  using(var b=new Bitmap(1160,835))using(var g=Graphics.FromImage(b)){
   g.Clear(C("#253431"));g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   Txt(g,"灰港登陆  /  地面投影验收",28,18,28,pale,true);
   Txt(g,"青色角标为实际占地的四角；门前台阶与出厂坡道可延伸至预留区域。",29,60,16,dim);
   for(int i=0;i<keys.Length;i++){
    var k=keys[i];var t=Campaign.Info(k);string name=t==null?world.Sites[i-6].Name:t.Name;
    var lot=t==null?new RectangleF(0,0,48,48):Campaign.Footprint(t,PointF.Empty);
    var label=name+"   "+lot.Width/Campaign.Tile+" × "+lot.Height/Campaign.Tile;
    var p=new PointF(128+i%5*226,269+i/5*229);
    float z=180/((lot.Width+lot.Height)*Axis);var target=BuildingRect(lot,p,z);BuildingArt(g,k,0,target,1);
    foreach(var corner in new[]{new PointF(lot.Left,lot.Top),new PointF(lot.Right,lot.Top),new PointF(lot.Right,lot.Bottom),new PointF(lot.Left,lot.Bottom)}){
     float x=corner.X-lot.Left-lot.Width/2,y=corner.Y-lot.Top-lot.Height/2;
     var screen=new PointF(p.X+z*Axis*(x-y),p.Y+z*Rise*(x+y));
     int sx=(int)Math.Round((screen.X-target.X)*FrameSize/target.Width),sy=(int)Math.Round((screen.Y-target.Y)*FrameSize/target.Height);
     if(!Enumerable.Range(-2,5).Any(dx=>Enumerable.Range(-2,5).Any(dy=>painted[k].GetPixel(sx+dx,sy+dy).A>100)))throw new Exception(k+" foundation contact");
     foreach(float sign in new[]{-1f,1f}){var tx=screen.X+(x<0?1:-1)*sign*8;var ty=screen.Y+(sign>0?(x<0?1:-1):(y<0?1:-1))*4;
      if(sign<0)tx=screen.X-(y<0?1:-1)*8;Line(g,screen.X,screen.Y,tx,ty,teal,2);}
    }
    Txt(g,label,p.X-84,p.Y+50,16,pale,true);
   }
   Line(g,24,584,1130,584,dim);Txt(g,"车辆接地方向  /  斜向与正侧面",28,596,20,gold,true);
   int j=0;foreach(var k in new[]{"mcv","ifv","tank","arty"}){
    var t=Campaign.Info(k);var sheet=painted[k];if(k=="tank"?sheet.Width!=8*TankCellSize||sheet.Height!=24*TankCellSize:sheet.Width!=FrameSize*8||sheet.Height!=FrameSize*6)throw new Exception(k+" directional sheet");
    var baseX=40+j++*285;Txt(g,t.Name,baseX,641,16,pale,true);
    for(int n=0;n<2;n++){int dir=n==0?14:4;double yaw=dir*Math.PI/8;
     var pt=new PointF(baseX+50+n*120,741);float dx=(float)Math.Sin(yaw)*60,dy=(float)Math.Cos(yaw)*30;
     Line(g,pt.X-dx,pt.Y-dy,pt.X+dx,pt.Y+dy,teal,1);DrawLive(g,t,0,dir,pt,132,1);}
   }
   Txt(g,"图取自发布程序内嵌资源。车辆行进、施工预览和侧栏图标使用同套图集。",28,796,15,dim);
   if(path!=null)b.Save(path,System.Drawing.Imaging.ImageFormat.Png);
  }
 }
}
}
