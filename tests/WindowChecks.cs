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
 static void Tap(CommandWindow f,string id){var r=f.TestControlBox(id);Press(f,(int)(r.X+r.Width/2),(int)(r.Y+r.Height/2));}
 static void CardClick(CommandWindow f,int index){var p=f.CardLocation(index);Press(f,p.X,p.Y);}
 [STAThread]static int Main(string[] args){try{Run(args);return 0;}catch(Exception e){Console.Error.WriteLine(e.Message);Console.Error.WriteLine(e.StackTrace);return 1;}}
 static void Run(string[] args){
  bool controlsOnly=args.Length==2&&args[0]=="--controls";if(controlsOnly)args=new[]{args[1]};
  if(args.Length==2&&args[0]=="--river"){using(var f=new CommandWindow())f.CheckRiverPlayable(args[1]);return;}
  if(args.Length==2&&args[0]=="--map"){using(var f=new CommandWindow())f.CheckRiverbend(args[1]);return;}
  if(args.Length==2&&args[0]=="--animation"){using(var f=new CommandWindow()){f.CheckBattleAnimations();f.CaptureAnimationReview(args[1]);f.ProfileAnimationLoad();}return;}
  if(!controlsOnly&&args.Length>0&&args[0].EndsWith(".png"))using(var f=new CommandWindow())f.CheckBattleAnimations();
  if(args.Length==2&&args[0]=="--traffic"){using(var f=new CommandWindow())f.CaptureTrafficReview(args[1]);return;}
  if(args.Length==2&&args[0]=="--presentation"){using(var f=new CommandWindow())f.CheckBattlePresentation(args[1]);return;}
  if(args.Length==2&&args[0]=="--spacing"){using(var f=new CommandWindow())f.CaptureVehicleSpacing(args[1]);return;}
  if(args.Length==2&&args[0]=="--model"){using(var f=new CommandWindow()){f.CaptureTankReview(args[1]);f.CheckInfantry();f.ProfileTankPaint();}return;}
  if(args.Length==1&&args[0]=="--scroll"){using(var f=new CommandWindow())f.ProfileScroll();return;}
  if(args.Length==1&&args[0]=="--scroll-window"){using(var f=new CommandWindow())f.ProfileScrollWindow();return;}
  if(!controlsOnly&&args.Length>0&&args[0]!="--profile"&&args[0]!="--window"&&args[0]!="--mine"&&args[0]!="--drive")using(var f=new CommandWindow())f.CheckTerrainCache();
  if(!controlsOnly&&args.Length>0&&args[0].EndsWith(".png"))using(var f=new CommandWindow())f.CheckCommandFeedback(Path.GetDirectoryName(args[0]));
  if(!controlsOnly&&args.Length>0&&args[0].EndsWith(".png"))using(var f=new CommandWindow())f.CheckBattlePresentation(Path.GetDirectoryName(args[0]));
  using(var f=new CommandWindow()){if(args.Length==1&&args[0]=="--window"){f.ProfileWindow();return;}if(args.Length==1&&args[0]=="--profile"){f.ProfileBattle();return;}if(args.Length==2&&args[0]=="--mine"){f.RecordOreTrip(args[1]);return;}if(args.Length==2&&args[0]=="--drive"){f.RecordStreetTurn(args[1]);return;}f.CheckPointerAndFullscreen(args.Length>0?Path.GetDirectoryName(args[0]):null);f.CheckClassicOrders();f.CheckInfantry();f.CheckRadarGate();f.CheckEdgeScroll();f.CheckFocusAndMining();if(args.Length>0)f.CheckScreenTravel(args[0]);f.CheckPlates(args.Length==0?null:Path.Combine(Path.GetDirectoryName(args[0]),"角度校准.png"));if(args.Length>0)f.CaptureTankReview(Path.GetDirectoryName(args[0]));Frame(f,args.Length==0?null:Path.Combine(Path.GetDirectoryName(args[0]),"主界面.png"));Press(f,190,641);if(!(bool)Field(f,"rulesOpen"))throw new Exception("rules entry");
   Press(f,157,290);if(!(bool)Field(f,"unitRoster"))throw new Exception("roster entry");
   if(args.Length>0){Frame(f,Path.Combine(Path.GetDirectoryName(args[0]),"单位图鉴.png"));Press(f,170,350);Frame(f,Path.Combine(Path.GetDirectoryName(args[0]),"经典指令.png"));}
   Press(f,158,700);if((bool)Field(f,"rulesOpen"))throw new Exception("rules return");Press(f,220,571);
   if(!(bool)Field(f,"missionPicker")||!(bool)Field(f,"briefing"))throw new Exception("start must open mission selection");
   Frame(f,args.Length==0?null:Path.Combine(Path.GetDirectoryName(args[0]),"关卡选择.png"));
   Press(f,220,454);if(!(bool)Field(f,"mapReview"))throw new Exception("new map preview entry");
   typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.Escape)});Frame(f,null);
   if((bool)Field(f,"mapReview")||!(bool)Field(f,"missionPicker"))throw new Exception("map preview return to missions");
   Press(f,140,694);if((bool)Field(f,"missionPicker"))throw new Exception("back to title");
   Press(f,220,571);Press(f,180,255);Press(f,1194,651);if((bool)Field(f,"briefing")||!((Campaign)Field(f,"world")).Tutorial)throw new Exception("only tutorial may launch");
   var screen=new PointF(647,405);var inv=typeof(CommandWindow).GetMethod("WorldOf",flags);
   var before=(PointF)inv.Invoke(f,new object[]{screen});float ui=(float)Field(f,"scale"),oldZoom=(float)Field(f,"zoom");
   typeof(CommandWindow).GetMethod("OnMouseWheel",flags).Invoke(f,new object[]{new MouseEventArgs(MouseButtons.None,0,(int)(screen.X*ui+(float)Field(f,"ox")),(int)(screen.Y*ui+(float)Field(f,"oy")),120)});
   var after=(PointF)inv.Invoke(f,new object[]{screen});if(Campaign.Dist(before,after)>2||(float)Field(f,"zoom")<=oldZoom)throw new Exception("cursor anchored zoom");Frame(f,null);
   Tap(f,"步兵");CardClick(f,0);if(((Campaign)Field(f,"world")).LastOrder!=null)throw new Exception("locked card accepted");
   Tap(f,"建筑");
   var w=(Campaign)Field(f,"world");Press(f,399,738);if(!w.Landing)throw new Exception("deploy button");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   CardClick(f,0);for(int i=0;i<80;i++)w.Tick(.1f);Frame(f,null);CardClick(f,0);MapClick(f,263,548);if(!w.Units.Any(o=>o.Side==0&&o.T.Key=="power"))throw new Exception("construction click");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   if(w.LessonKey!="refinery")throw new Exception("missing refinery stage");
   Press(f,767,90);Tap(f,"card-refinery");for(int i=0;i<180;i++)w.Tick(.1f);Frame(f,null);Tap(f,"card-refinery");MapClick(f,66,450);
   for(int i=0;i<14;i++)w.Tick(.1f);Frame(f,null);
   if(w.LessonKey!="ore")throw new Exception("ore stage "+w.LessonKey);
   Press(f,767,90);var miner=w.Live(0).First(o=>o.T.Key=="miner");
   if(!((System.Collections.Generic.List<Piece>)Field(f,"selection")).Contains(miner))throw new Exception("focus truck");
   for(int i=0;i<400&&w.LessonKey=="ore";i++)w.Tick(.1f);
   if(w.LessonKey!="receipt"||w.OreReceipts[0]!=0)throw new Exception("deposit lesson before sale");
   for(int i=0;i<400&&w.LessonKey=="receipt";i++)w.Tick(.1f);
   if(w.LessonKey!="barracks"||w.OreReceipts[0]<=0)throw new Exception("first receipt");
   typeof(CommandWindow).GetMethod("CenterAt",flags).Invoke(f,new object[]{new PointF(255,465)});Frame(f,null);
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);CardClick(f,1);for(int i=0;i<100;i++)w.Tick(.1f);Frame(f,null);CardClick(f,1);MapClick(f,123,610);for(int i=0;i<65;i++)w.Tick(.1f);
   Frame(f,null);CardClick(f,2);for(int i=0;i<170;i++)w.Tick(.1f);Frame(f,null);CardClick(f,2);MapClick(f,339,548);for(int i=0;i<110;i++)w.Tick(.1f);
   Tap(f,"步兵");CardClick(f,0);if(!w.Live(0).Any(o=>o.Queue.Count>0))throw new Exception("training click");
   CardClick(f,0);Tap(f,"总览");
   if(!(bool)Field(f,"queueOpen"))throw new Exception("queue tab");
   var source=w.Live(0).Single(o=>o.Queue.Count>0);if(source.Queue.Count!=2)throw new Exception("second order");
   Frame(f,args.Length==0?null:args[0]);int funds=w.Funds[0];Tap(f,"cancel-1");if(source.Queue.Count!=1||w.Funds[0]!=funds)throw new Exception("cancel waiting slot");
   if(w.LessonKey!="squad"||!w.Tutorial)throw new Exception("default tutorial step");
   for(int i=0;i<60;i++)w.Tick(.1f);Frame(f,null);
   if(w.LessonKey!="ifv")throw new Exception("soldier delivery plus prebuilt factory");
   Press(f,767,90);CardClick(f,0);for(int i=0;i<95;i++)w.Tick(.1f);Frame(f,null);
   if(args.Length>0){
    w.Upgrade();w.Train(Campaign.Info("tank"),0,null);for(int i=0;i<140;i++)w.Tick(.1f);
    Tap(f,"步兵");typeof(CommandWindow).GetMethod("CenterAt",flags).Invoke(f,new object[]{new PointF(300,495)});

    var armor=w.Live(0).FirstOrDefault(u=>u.T.Key=="tank");if(armor!=null){
     var picked=(System.Collections.Generic.List<Piece>)Field(f,"selection");picked.Clear();picked.Add(armor);
    }
    var factory=w.Live(0).First(u=>u.T.Key=="factory");w.Train(Campaign.Info("tank"),0,factory);w.Train(Campaign.Info("ifv"),0,factory);for(int t=0;t<28;t++)w.Tick(.1f);
    Frame(f,Path.Combine(Path.GetDirectoryName(args[0]),"等距战场.png"));f.CaptureDefaultScale(Path.Combine(Path.GetDirectoryName(args[0]),"战场比例.png"));while(factory.Queue.Count>0)w.CancelOrder(factory,0);
   }
   if(w.LessonKey!="eliminate")throw new Exception("ifv training");Press(f,767,90);
   typeof(CommandWindow).GetField("mouse",flags).SetValue(f,(PointF)typeof(CommandWindow).GetMethod("ScreenOf",flags).Invoke(f,new object[]{new PointF(1330,292)}));typeof(CommandWindow).GetMethod("ContextOrder",flags).Invoke(f,new object[]{Keys.Control|Keys.Shift});
   for(int i=0;i<1100;i++)w.Tick(.1f);Frame(f,null);if(!w.Won)throw new Exception("tutorial victory through interface: "+w.LessonKey+" "+w.Radio+" / "+String.Join(" ",w.Live(0).Where(u=>u.T.Foot).Select(u=>u.P+" to "+u.Goal)));
   // The live window advances only the visual clock after the final kill.
   w.TickVisuals(1.4f);Frame(f,null);
   Press(f,688,574);if(!(bool)Field(f,"mapReview"))throw new Exception("tutorial completion to map preview");
   typeof(CommandWindow).GetMethod("OnKeyDown",flags).Invoke(f,new object[]{new KeyEventArgs(Keys.Escape)});Frame(f,null);Press(f,1194,651);
   w=(Campaign)Field(f,"world");if(!w.Tutorial)throw new Exception("obsolete campaign became available");var trooper=w.Live(0).First(o=>o.T.Foot);var nearby=w.Spawn("squad",1,351,421);for(int i=0;i<3;i++)w.Tick(.1f);Frame(f,null);
   MapClick(f,(int)trooper.P.X,(int)trooper.P.Y);var units=(System.Collections.Generic.List<Piece>)Field(f,"selection");
   if(!units.Contains(trooper))throw new Exception("classic select");
   MapClick(f,(int)trooper.P.X,(int)trooper.P.Y);if(units.Count!=1||units[0]!=trooper)throw new Exception("repeat click must keep selection");
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
 internal void CheckPointerAndFullscreen(string folder){
  if(!fullscreen||FormBorderStyle!=FormBorderStyle.None||Bounds!=Screen.FromPoint(Cursor.Position).Bounds)throw new Exception("default fullscreen monitor bounds");
  OnKeyDown(new KeyEventArgs(Keys.Alt|Keys.Enter));if(fullscreen||FormBorderStyle!=FormBorderStyle.Sizable)throw new Exception("Alt Enter windowed from menu");
  OnKeyDown(new KeyEventArgs(Keys.Alt|Keys.Enter));if(!fullscreen||FormBorderStyle!=FormBorderStyle.None)throw new Exception("Alt Enter fullscreen");
  ClientSize=new Size(W,H);StartMission(false,true);CenterAt(new PointF(600,1000));hits.Clear();
  var unit=world.Spawn("tank",0,500,1000);world.Tick(.01f);selection.Clear();selection.Add(unit);
  mouse=ScreenOf(new PointF(700,1000));if(DescribePointer(Keys.None).Kind!=PointerKind.Move)throw new Exception("move pointer");
  if(DescribePointer(Keys.Control|Keys.Shift).Kind!=PointerKind.AttackMove)throw new Exception("attack move pointer");
  if(DescribePointer(Keys.Control|Keys.Alt).Kind!=PointerKind.Guard)throw new Exception("guard pointer");
  world.Move(unit,new PointF(700,1000),false);var path=unit.Path.ToArray();var goal=unit.Goal;attack=true;
  CenterAt(new PointF(600,75));mouse=ScreenOf(new PointF(600,75));if(DescribePointer(Keys.None).Kind!=PointerKind.Blocked)throw new Exception("water blocked pointer");
  ContextOrder(Keys.None);if(!attack||selection.Count!=1||unit.Goal!=goal||!unit.Path.SequenceEqual(path))throw new Exception("invalid destination cancelled pending attack or path");
  attack=false;ContextOrder(Keys.Alt);if(unit.Goal!=goal||!unit.Path.SequenceEqual(path))throw new Exception("invalid force move overwrote order");
  OnKeyDown(new KeyEventArgs(Keys.Z));ContextOrder(Keys.None);if(planned.Count!=0||!unit.Path.SequenceEqual(path))throw new Exception("invalid waypoint halted unit");OnKeyUp(new KeyEventArgs(Keys.Z));
  var enemy=world.Spawn("tank",1,600,1000);world.Tick(.01f);CenterAt(enemy.P);mouse=ScreenOf(enemy.P);if(DescribePointer(Keys.None).Kind!=PointerKind.Attack)throw new Exception("attack cursor");
  var miner=world.Spawn("miner",0,600,1030);world.Halt(miner);selection.Clear();selection.Add(miner);if(DescribePointer(Keys.None).Kind!=PointerKind.Blocked)throw new Exception("unarmed attack blocked");
  var patch=world.Ore[0];miner.P=patch.P;world.Tick(.01f);CenterAt(patch.P);mouse=ScreenOf(new PointF(patch.P.X+19,patch.P.Y));
  if(DescribePointer(Keys.None).Kind!=PointerKind.Harvest)throw new Exception("ore pointer");
  var plant=world.Spawn("refinery",0,600,1100);plant.StarterDelivered=true;CenterAt(plant.P);mouse=ScreenOf(plant.P);if(DescribePointer(Keys.None).Kind!=PointerKind.Unload)throw new Exception("unload pointer");
  selection.Clear();selection.Add(unit);mouse=ScreenOf(unit.P);if(DescribePointer(Keys.None).Kind!=PointerKind.Select)throw new Exception("friendly select pointer");
  place=Campaign.Info("power");CenterAt(new PointF(0,0));mouse=ScreenOf(new PointF(0,0));if(DescribePointer(Keys.None).Kind!=PointerKind.Blocked)throw new Exception("placement blocked pointer");place=null;
  if(folder!=null)using(var plate=new Bitmap(800,390))using(var g=Graphics.FromImage(plate)){
   g.Clear(ink);var kinds=(PointerKind[])Enum.GetValues(typeof(PointerKind));
   string[] labels={"默认","选择","移动","不可达","攻击","攻击移动","警戒","护卫","采矿","卸矿","集结点","建造","强制移动","平移","路径点","部分可达","维修","展开","出售"};
   for(int i=0;i<kinds.Length;i++){int x=(i%8)*100,y=(i/8)*130;using(var art=CursorArtwork(kinds[i]))g.DrawImageUnscaled(art,x+30,y+28);Txt(g,labels[i],x+10,y+88,13,pale);}
   plate.Save(Path.Combine(folder,"鼠标指针.png"),System.Drawing.Imaging.ImageFormat.Png);
  }
  CenterAt(new PointF(700,1000));mouse=ScreenOf(new PointF(700,1000));UpdateBattleCursor(true);if(Cursor==Cursors.Default||Cursor.Handle==IntPtr.Zero||cursorHandles.Count==0)throw new Exception("native custom cursor creation");
  if(Cursor.HotSpot!=new Point(20,20))throw new Exception("command hotspot");
  Restart();Console.WriteLine("Fullscreen/menu Alt+Enter, native cursor/hotspot, action previews, and invalid clicks preserving selection/orders passed.");
 }
 internal void CheckClassicOrders(){
  ClientSize=new Size(1472,819);StartMission(false,true);CenterAt(new PointF(600,1000));
  using(var frame=new Bitmap(1472,819))using(var g=Graphics.FromImage(frame))OnPaint(new PaintEventArgs(g,ClientRectangle));
  var old=world.Spawn("tank",0,500,1000);var moving=world.Spawn("squad",0,600,1000);world.Tick(.01f);
  selection.Clear();selection.Add(old);
  Action<Keys> tap=k=>{OnKeyDown(new KeyEventArgs(k));OnKeyUp(new KeyEventArgs(k));};
  foreach(float level in new[]{.62f,1.05f,2.4f}){
   zoom=level;var p=ScreenOf(moving.P);if(HitScreen(new PointF(p.X+4,p.Y-9))!=moving)throw new Exception("small infantry pick margin at "+level);
  }
  zoom=1.05f;mouse=ScreenOf(moving.P);var click=mouse;
  OnMouseDown(new MouseEventArgs(MouseButtons.Left,1,(int)click.X,(int)click.Y,0));
  moving.P=new PointF(moving.P.X+32,moving.P.Y); // Moves between mouse down and up.
  OnMouseUp(new MouseEventArgs(MouseButtons.Left,1,(int)click.X,(int)click.Y,0));
  if(selection.Count!=1||selection[0]!=moving||old.Path.Count!=0)throw new Exception("moving click must switch selection, not order old group");
  mouse=ScreenOf(moving.P);ContextOrder(Keys.None);if(selection.Count!=1)throw new Exception("repeat select toggled off");
  mouse=ScreenOf(old.P);ContextOrder(Keys.Shift);if(selection.Count!=2)throw new Exception("shift add");ContextOrder(Keys.Shift);if(selection.Count!=1)throw new Exception("shift remove");
  tap(Keys.Control|Keys.D9);selection.Clear();tap(Keys.D9);if(selection.Count!=1||selection[0]!=moving)throw new Exception("team 9 recall");
  tap(Keys.G);if(!moving.GuardHome.HasValue||showGrid)throw new Exception("G should guard, not show grid");tap(Keys.S);if(moving.GuardHome.HasValue)throw new Exception("S cancels guard");
  tap(Keys.A);if(attack)throw new Exception("A is not attack move in classic controls");
  mouse=ScreenOf(new PointF(800,1100));ContextOrder(Keys.Control|Keys.Alt);if(moving.GuardHome!=new PointF(800,1100))throw new Exception("guard destination modifiers");
  mouse=ScreenOf(old.P);ContextOrder(Keys.Control|Keys.Alt);if(moving.Escort!=old)throw new Exception("escort modifiers");
  tap(Keys.F);if(followUnit!=moving)throw new Exception("F follows");Pan(20,0);if(followUnit!=null)throw new Exception("manual pan releases follow");
  tap(Keys.X);if(moving.Path.Count==0||moving.GuardHome.HasValue)throw new Exception("scatter cancels escort");
  OnKeyDown(new KeyEventArgs(Keys.Z));mouse=ScreenOf(new PointF(700,1100));ContextOrder(Keys.None);mouse=ScreenOf(new PointF(800,1100));ContextOrder(Keys.None);
  if(moving.Path.Count!=0)throw new Exception("waypoints must wait for release");OnKeyUp(new KeyEventArgs(Keys.Z));
  if(moving.Waypoints.Count!=2||moving.Path.Count==0)throw new Exception("Z release commits route");
  tap(Keys.E);if(ProductionCards().Any(t=>!t.Foot))throw new Exception("E infantry tab");tap(Keys.R);if(ProductionCards().Any(t=>t.Foot||t.Building))throw new Exception("R vehicle tab");
  var enemy=world.Spawn("tank",1,650,1000);world.Tick(.01f);Mark(enemy.P,enemy,true);flashAt=watch.Elapsed.TotalSeconds-.051;
  if(TargetFlash(enemy)<.7f)throw new Exception("fast highlight peak");flashAt=watch.Elapsed.TotalSeconds-.21;if(TargetFlash(enemy)!=0)throw new Exception("fast highlight expiry");
  Restart();Console.WriteLine("Classic keys, small-target preselection at three zoom levels, moving click capture, team 9 and fast highlight passed.");
 }
 internal void CheckCommandFeedback(string folder){
  ClientSize=new Size(1920,1080);StartMission(false,true);CenterAt(new PointF(690,715));showGrid=true;
  var troops=Enumerable.Range(0,8).Select(i=>world.Spawn(i%2==0?"tank":"ifv",0,510+i%4*32,700+i/4*32)).ToArray();
  selection.Clear();selection.AddRange(troops);world.MarchTo(troops,new PointF(820,855),false,false);world.Tick(.01f);
  if(troops.Any(t=>t.Path.Count==0))throw new Exception("missing group member route");
  using(var picture=new Bitmap(1920,1080))using(var g=Graphics.FromImage(picture)){
   OnPaint(new PaintEventArgs(g,ClientRectangle));picture.Save(Path.Combine(folder,"编队路线.png"),System.Drawing.Imaging.ImageFormat.Png);
   mouse=ScreenOf(troops[3].P);OnPaint(new PaintEventArgs(g,ClientRectangle));picture.Save(Path.Combine(folder,"单位预选.png"),System.Drawing.Imaging.ImageFormat.Png);
   var enemy=world.Spawn("tank",1,850,700);world.Spawn("engineer",0,780,650);world.Tick(.22f);
   foreach(var t in troops)world.Attack(t,enemy);
   Mark(enemy.P,enemy,true);flashAt=watch.Elapsed.TotalSeconds-.05;
   if(TargetFlash(enemy)<.7f)throw new Exception("target flash absent");
   using(var flash=new Bitmap(160,160))using(var clear=Graphics.FromImage(flash)){
    DrawTargetFlash(clear,enemy,new PointF(80,100),100);
    int white=0;for(int y=0;y<160;y++)for(int x=0;x<160;x++){var c=flash.GetPixel(x,y);if(c.A>20&&c.R>230&&c.G>230&&c.B>230)white++;}
    if(white<100||flash.GetPixel(0,0).A!=0)throw new Exception("flash must follow opaque unit pixels");
   }
   flashAt=watch.Elapsed.TotalSeconds-.05;OnPaint(new PaintEventArgs(g,ClientRectangle));picture.Save(Path.Combine(folder,"集火反馈.png"),System.Drawing.Imaging.ImageFormat.Png);
   flashAt=watch.Elapsed.TotalSeconds-1;if(TargetFlash(enemy)!=0)throw new Exception("target flash did not expire");
  }
  if((W-MW)/(float)W>.12f)throw new Exception("sidebar exceeds target width");
  Console.WriteLine("Compact sidebar, eight independent routes, transparent silhouette flash and timed expiry passed.");
 }
 internal void CheckRadarGate(){
  StartMission(true,true);using(var image=new Bitmap(ClientSize.Width,ClientSize.Height))using(var g=Graphics.FromImage(image)){
   Action render=()=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));};
   render();if(RadarOnline()||hits.Any(h=>h.Id=="radar-map"))throw new Exception("minimap visible at start");
   var radar=world.Spawn("radar",0,220,550);radar.Ready=.5f;render();
   if(RadarOnline())throw new Exception("unfinished radar enabled minimap");
   radar.Ready=1;render();if(RadarOnline())throw new Exception("unpowered radar enabled minimap");
   var power=world.Spawn("power",0,310,550);render();
   if(!RadarOnline()||!hits.Any(h=>h.Id=="radar-map"))throw new Exception("completed powered radar did not enable minimap");
   if(world.Explored(0,new PointF(2100,1000)))throw new Exception("radar reveals remote terrain");
   if(hits.Any(h=>h.Id!=null&&h.Id.Contains("扫描")))throw new Exception("obsolete scan action remains");
   power.Hp=0;render();if(RadarOnline()||hits.Any(h=>h.Id=="radar-map"))throw new Exception("radar survives loss of power");
   power.Hp=power.T.Hp;radar.Hp=0;render();if(RadarOnline()||hits.Any(h=>h.Id=="radar-map"))throw new Exception("radar survives destruction");
  }
  Restart();Console.WriteLine("Minimap stays unavailable until radar completion and power; offline input and removed scan action passed.");
 }
 internal RectangleF TestControlBox(string id){return hits.Last(h=>h.Id==id).Box;}
 internal Point CardLocation(int index){return new Point(MW+SideInset+index%2*80+36,274+index/2*75+35);}
 internal void CheckTerrainCache(){
  ClientSize=new Size(1920,1080);scale=1920f/W;ox=0;oy=(1080-H*scale)/2;zoom=1.05f;camera=new PointF(800,650);
  using(var cached=new Bitmap(1920,1080))using(var rebuilt=new Bitmap(1920,1080)){
   Action<Bitmap> render=b=>{using(var g=Graphics.FromImage(b)){g.Clear(ink);g.TranslateTransform(ox,oy);g.ScaleTransform(scale,scale);g.SetClip(new Rectangle(0,MapTop,MW,MH));DrawGroundView(g);}};
   render(cached);Pan(100/scale,50/scale);render(cached);terrainZoom=-1;render(rebuilt);
   for(int y=80;y<1050;y+=3)for(int x=3;x<1380;x+=3)
    if(cached.GetPixel(x,y)!=rebuilt.GetPixel(x,y))throw new Exception("terrain shifts at cache boundary");
   zoom=3.1f;
   for(int i=0;i<18;i++){
    camera=new PointF(200+i%6*420,220+i/6*480);render(cached);
    if(terrainRasterBytes>TerrainBudget)throw new Exception("terrain raster cache exceeds memory budget");
   }
   // A cached bitmap must cover every edge without transparent tile seams.
   foreach(var p in terrainPatches.Where(p=>p.Raster!=null)){
    var b=p.Raster;
    foreach(var point in new[]{new Point(0,0),new Point(b.Width-1,0),new Point(0,b.Height-1),new Point(b.Width-1,b.Height-1)})
     if(b.GetPixel(point.X,point.Y).A!=255)throw new Exception("transparent terrain tile border");
   }
  }
  Console.WriteLine("Terrain overlap is pixel-identical across viewport refresh; opaque tile edges and the 64 MiB raster budget passed.");
 }
 internal void ProfileScrollWindow(){
  ClientSize=new Size(1260,706);StartMission(true,true);CenterAt(new PointF(850,650));Show();paused=false;
  var probe=new PaintTimes();probe.AssignHandle(Handle);int frames=0,run=0;float[] levels={1.05f,2.4f,.62f};
  try{using(var loop=new ApplicationContext())using(var motion=new Timer{Interval=16})using(var deadline=new Timer{Interval=4000}){
   motion.Tick+=(s,e)=>{Pan(frames++%160<80?12:-12,frames%160<80?6:-6);};
   deadline.Tick+=(s,e)=>{
    var times=probe.Intervals.Skip(12).OrderBy(v=>v).ToArray();
    if(times.Length==0)throw new Exception("no scrolling window frames");
    Console.WriteLine("Live scroll zoom {0}: {1:F1} paints/s, p95 {2:F1}ms, max {3:F1}ms",levels[run],1000/times.Average(),times[(int)(times.Length*.95)],times.Last());
    probe.Intervals.Clear();if(++run==levels.Length){motion.Stop();deadline.Stop();loop.ExitThread();}else zoom=levels[run];
   };
   motion.Start();deadline.Start();Application.Run(loop);
  }}finally{probe.ReleaseHandle();Close();}
 }
 internal void ProfileScroll(){
  ClientSize=new Size(1920,1080);StartMission(true,true);selection.Clear();
  foreach(var pos in new[]{new PointF(450,520),new PointF(1120,800),new PointF(2050,1030)}){
   world.Spawn("hq",0,pos.X,pos.Y);world.Spawn("power",0,pos.X+110,pos.Y);
   world.Spawn("factory",0,pos.X,pos.Y+145);
   for(int n=0;n<12;n++)world.Spawn(n%3==0?"squad":"tank",0,pos.X-120+n%4*35,pos.Y+50+n/4*32);
  }
  using(var surface=new Bitmap(1920,1080))using(var g=Graphics.FromImage(surface)){
   foreach(float level in new[]{1.05f,2.4f,.62f}){
    zoom=level;camera=new PointF(800,650);var normal=new System.Collections.Generic.List<double>();var rebuild=new System.Collections.Generic.List<double>();var ticks=new System.Collections.Generic.List<double>();
    for(int i=0;i<420;i++){
     var timer=System.Diagnostics.Stopwatch.StartNew();world.Tick(1f/60);ticks.Add(timer.Elapsed.TotalMilliseconds);
     Pan(i%210<105?12:-12,i%210<105?6:-6);var previous=terrainCamera;
     timer.Restart();OnPaint(new PaintEventArgs(g,ClientRectangle));g.Flush(System.Drawing.Drawing2D.FlushIntention.Sync);
     if(i>=12)(previous==terrainCamera?normal:rebuild).Add(timer.Elapsed.TotalMilliseconds);
    }
    Action<string,System.Collections.Generic.List<double>> report=(label,values)=>{
     values.Sort();if(values.Count==0)return;Console.WriteLine("Scroll zoom {0}: {1} n={2}, median={3:F2}ms p95={4:F2}ms p99={5:F2}ms max={6:F2}ms >33ms={7}",level,label,values.Count,values[values.Count/2],values[(int)(values.Count*.95)],values[(int)(values.Count*.99)],values.Last(),values.Count(v=>v>33));
    };
    report("cached",normal);report("terrain refresh",rebuild);report("simulation",ticks);
   }
  }
 }
 internal void CaptureDefaultScale(string path){
  var oldSize=ClientSize;float oldZoom=zoom;ClientSize=new Size(1920,1080);zoom=DefaultZoom;
  try{using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(path,System.Drawing.Imaging.ImageFormat.Png);
  }}finally{ClientSize=oldSize;zoom=oldZoom;}
 }
 internal void CheckInfantry(){
  CheckMobileAtlases();
  var feet=new PointF(96,120);const float size=100;var box=SoldierBox(feet,size);
  foreach(var key in new[]{"squad","at","engineer"})foreach(var side in new[]{0,1}){
   var unit=world.Spawn(key,side,100,650);unit.Dir=2;
   using(var frame=new Bitmap(192,160))using(var expected=new Bitmap(192,160))
   using(var g=Graphics.FromImage(frame))using(var reference=Graphics.FromImage(expected)){
    DrawFieldSprite(g,unit,feet,size);
    PaintFrame(reference,unit.T,side,0,box);
    for(int y=0;y<160;y+=2)for(int x=0;x<192;x+=2)
     if(frame.GetPixel(x,y)!=expected.GetPixel(x,y))throw new Exception("infantry differs from a single sprite");
    if(!MobileHit(unit,feet,size,feet)||MobileHit(unit,feet,size,new PointF(feet.X+size*.32f,feet.Y-size*.18f)))
     throw new Exception("single soldier hit bounds");
   }
  }Restart();
 }
 internal void CheckMobileAtlases(){
  CheckTankPaint();
  // Exercise embedded resources, actual source rectangles, faction-independent
  // silhouettes and the expanded eight-direction fall sheets together.
  foreach(var t in Campaign.Types.Where(t=>!t.Building)){
   if(t.Key=="tank")continue;
   var sheet=painted[t.Key];int count=t.Foot?8:t.Key=="tank"?64:16;
   if(t.Foot&&(sheet.Width!=FootViews*FootCell||sheet.Height!=FootCell))throw new Exception(t.Key+" eight-view infantry atlas");
   if(sheet.Size!=painted[t.Key+"-paint"].Size||(!t.Foot&&sheet.Size!=painted[t.Key+"-wreck"].Size))throw new Exception(t.Key+" layer dimensions");
   for(int i=0;i<count;i++){
    var blue=t.Key=="tank"?TankCell(0,i):SourceCell(t,0,t.Foot?i*2:i);
    var enemy=t.Key=="tank"?TankCell(1,i):SourceCell(t,1,t.Foot?i*2:i);int ink=0,colored=0;
    if(!new Rectangle(0,0,sheet.Width,sheet.Height).Contains(enemy))throw new Exception(t.Key+" source frame outside atlas");
    using(var first=new Bitmap(blue.Width,blue.Height))using(var second=new Bitmap(blue.Width,blue.Height)){
    using(var g=Graphics.FromImage(first))DrawLayeredSource(g,t.Key,blue,new RectangleF(0,0,blue.Width,blue.Height),Color.FromArgb(40,100,190));
    using(var g=Graphics.FromImage(second))DrawLayeredSource(g,t.Key,blue,new RectangleF(0,0,blue.Width,blue.Height),Color.FromArgb(65,180,70));
    for(int y=0;y<blue.Height;y+=3)for(int x=0;x<blue.Width;x+=3){
     var a=first.GetPixel(x,y);var b=second.GetPixel(x,y);
     if(a.A!=b.A)throw new Exception(t.Key+" faction changes silhouette");
     if(painted[t.Key+"-paint"].GetPixel(blue.X+x,blue.Y+y).A==0&&a!=b)throw new Exception(t.Key+" neutral material recolored");
     if(a.A>128){ink++;if(Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B)>60)colored++;}
    }
    }
    if(ink<40||colored<ink*.08)throw new Exception(t.Key+" missing body or faction markings at "+i+" "+colored+"/"+ink);
   }
   if(t.Foot)for(int d=0;d<16;d+=2){
    var unit=world.Spawn(t.Key,1,100,650);unit.Dir=d;unit.Hp=0;unit.Dead=.4f;
    using(var b=new Bitmap(140,140))using(var g=Graphics.FromImage(b)){
     DrawFieldSprite(g,unit,new PointF(70,70),100);int ink=0;
     for(int y=0;y<140;y+=2)for(int x=0;x<140;x+=2)if(b.GetPixel(x,y).A>64)ink++;
     if(ink<20)throw new Exception(t.Key+" missing fallen direction "+d);
    }
   }
  }
  CheckLoadedMinerArt();
  Console.WriteLine("Eight infantry views, 16 vehicle/64 tank views, identical faction silhouettes, visible team panels and all fall directions passed.");
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
  world.StartConstruction(Campaign.Info("power"));for(int n=0;n<80;n++)world.Tick(.1f);
  if(!world.Build(Campaign.Info("power"),new PointF(260,550)))throw new Exception("build power");
  for(int n=0;n<60;n++)world.Tick(.1f);
  world.StartConstruction(Campaign.Info("refinery"));for(int n=0;n<180;n++)world.Tick(.1f);
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
  var measurements=File.ReadAllLines(Path.Combine(Path.GetDirectoryName(shot),"../art/source/tank-prototype/tank-bearing-check.csv")).Skip(1)
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
  var keys=new[]{"hq","power","barracks","factory","radar","post","refinery","harbormaster"};
  using(var b=new Bitmap(1160,835))using(var g=Graphics.FromImage(b)){
   g.Clear(C("#253431"));g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   Txt(g,"灰港登陆  /  投影角度验收（非大小对比）",28,18,28,pale,true);
   Txt(g,"青色角标为实际地基四角；可建造建筑的台阶与坡道已收进占格。",29,60,16,dim);
   for(int i=0;i<keys.Length;i++){
    var k=keys[i];var t=Campaign.Info(k);string name=t==null?world.Harbor.Name:t.Name;
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
    var t=Campaign.Info(k);var sheet=painted[k];if(k=="tank"?sheet.Width!=8*TankCellSize||sheet.Height!=8*TankCellSize:sheet.Width!=MobileCell*8||sheet.Height!=MobileCell*2)throw new Exception(k+" directional sheet");
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
