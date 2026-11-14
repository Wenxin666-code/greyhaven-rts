using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class OriginalControlChecks {
 [STAThread]static int Main(string[] args){try{
  Campaign.CheckControlCombat();using(var f=new CommandWindow())f.CheckOriginalControls(args[0],args.Length>1?args[1]:null);
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 internal static void CheckControlCombat(){
  var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();w.Funds[0]=0;var source=w.Spawn("factory",0,500,600);
  if(w.Require(Info("tank"),0)!=null||w.Require(Info("arty"),0)!=null)throw new Exception("armor still requires global upgrade");
  if(!w.Train(Info("tank"),0,null)||w.Funds[0]!=0)throw new Exception("armor order must not buy an upgrade or prepay");
  var soldier=w.Spawn("squad",0,700,1100);
  for(int i=0;i<6;i++){var target=w.Spawn("squad",1,730,1100);target.Hp=1;w.Fire(soldier,target);if(i==2&&soldier.Rank!=1)throw new Exception("veteran selection data not earned");}
  if(soldier.Rank!=2||soldier.T.Hit!=Info("squad").Hit)throw new Exception("rank data must not silently alter combat attributes");
  Console.WriteLine("Direct factory armor, progressive unit payment and earned veterancy labels without new combat multipliers passed.");
 }
}
partial class CommandWindow {
 static void AssertControl(bool ok,string message){if(!ok)throw new Exception(message);}
 void ControlKey(Keys key){OnKeyDown(new KeyEventArgs(key));OnKeyUp(new KeyEventArgs(key));}
 void ControlMouse(MouseButtons button,PointF point,bool down){var e=new MouseEventArgs(button,1,(int)(ox+point.X*scale),(int)(oy+point.Y*scale),0);if(down)OnMouseDown(e);else OnMouseUp(e);}
 void ControlClick(PointF point,MouseButtons button=MouseButtons.Left){ControlMouse(button,point,true);ControlMouse(button,point,false);}
 void ControlRender(){using(var b=new Bitmap(ClientSize.Width,ClientSize.Height))using(var g=Graphics.FromImage(b)){hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));}}
 void ControlTap(string id,MouseButtons button=MouseButtons.Left){ControlRender();var hit=hits.Last(p=>p.Id==id);ControlClick(new PointF(hit.Box.X+hit.Box.Width/2,hit.Box.Y+hit.Box.Height/2),button);}
 void ControlWorld(){
  StartMission(true,false);world.Units.Clear();world.Ore.Clear();world.Spawn("mcv",1,2900,1800);selection.Clear();paused=help=queueOpen=false;CenterAt(new PointF(650,1100));ClientSize=new Size(1920,1080);ControlRender();
 }
 internal void CheckOriginalControls(string fixture,string folder){
  EnsureHotkeys();DefaultHotkeys();int compared=0;
  foreach(string line in File.ReadAllLines(fixture)){if(line.StartsWith("#")||line.StartsWith("command")||String.IsNullOrWhiteSpace(line))continue;var parts=line.Split(',');AssertControl(hotkeyValues.ContainsKey(parts[0])&&hotkeyValues[parts[0]]==int.Parse(parts[1]),"default key differs: "+line);int encoded=int.Parse(parts[1]);Keys physical=(Keys)(encoded&255);if((encoded&256)!=0)physical|=Keys.Shift;if((encoded&512)!=0)physical|=Keys.Control;if((encoded&1024)!=0)physical|=Keys.Alt;
   KeyEventArgs mapped;AssertControl(MapHotkey(new KeyEventArgs(physical),out mapped),"default dispatcher dropped: "+parts[0]);Keys expected=physical;if((encoded&255)==110)expected=(physical&Keys.Modifiers)|Keys.Delete;AssertControl(mapped.KeyData==expected,"default dispatcher changed: "+parts[0]);compared++;}
  AssertControl(compared==85,"independent keyboard fixture count");AssertControl(IsInputKey(Keys.Tab),"native Tab must reach battle dispatcher");
  ControlWorld();var tank=world.Spawn("tank",0,600,1100);var second=world.Spawn("ifv",0,650,1150);var foot=world.Spawn("squad",0,660,1100);var miner=world.Spawn("miner",0,700,1150);miner.Mining=false;selection.Add(tank);ControlRender();
  var click=new PointF(500,300);ControlClick(click,MouseButtons.Right);AssertControl(selection.Count==0,"right click cancellation is delayed until release");
  selection.Add(tank);attack=true;var cameraBefore=camera;var goal=tank.Goal;var path=tank.Path.ToArray();
  ControlMouse(MouseButtons.Right,click,true);AssertControl(selection.Contains(tank)&&attack,"right press must preserve selection until classified");
  OnMouseMove(new MouseEventArgs(MouseButtons.Right,0,(int)(ox+(click.X+70)*scale),(int)(oy+(click.Y+25)*scale),0));
  AssertControl(rightDragging&&camera.X-camera.Y>cameraBefore.X-cameraBefore.Y,"right drag scroll direction");var draggedCamera=camera;TickCameraControls(.05f);AssertControl(camera!=draggedCamera,"held right drag continues scrolling");
  ControlMouse(MouseButtons.Right,new PointF(click.X+70,click.Y+25),false);
  AssertControl(selection.Contains(tank)&&attack&&tank.Goal==goal&&tank.Path.SequenceEqual(path)&&!rightPending&&!Capture,"right drag must not issue, cancel or lose capture");
  ControlClick(new PointF(600,300),MouseButtons.Right);AssertControl(!attack&&selection.Contains(tank),"right click cancels current command before selection");
  Console.WriteLine("Native right mouse: click cancel, drag direction, held scrolling, command/selection preservation and capture release passed.");

  selection.Clear();selection.Add(tank);ControlKey(Keys.Control|Keys.D0);selection.Clear();ControlKey(Keys.D0);AssertControl(selection.SequenceEqual(new[]{tank})&&tank.Group==10,"control group zero");
  selection.Clear();selection.Add(second);ControlKey(Keys.Control|Keys.D1);selection.Clear();selection.Add(tank);ControlKey(Keys.Shift|Keys.D1);AssertControl(selection.Contains(tank)&&selection.Contains(second),"shift group merge");
  CenterAt(new PointF(1000,1000));var beforeSelection=selection.ToArray();ControlKey(Keys.Alt|Keys.D0);AssertControl(selection.SequenceEqual(beforeSelection)&&Campaign.Dist(camera,tank.P)<1,"alt group center only");
  ControlKey(Keys.Clear);AssertControl(Campaign.Dist(camera,new PointF((tank.P.X+second.P.X)/2,(tank.P.Y+second.P.Y)/2))<1,"keypad center selection");
  ControlKey(Keys.F);AssertControl(followUnit==tank,"F follow");ControlKey(Keys.F);AssertControl(followUnit==null,"F toggles follow off");
  ControlKey(Keys.P);AssertControl(selection.Contains(tank)&&selection.Contains(second)&&selection.Contains(foot)&&!selection.Contains(miner),"P excludes economy and support");
  selection.Clear();selection.Add(tank);ControlKey(Keys.N);AssertControl(selection[0]==second,"N is next per KeyboardMD default");ControlKey(Keys.M);AssertControl(selection[0]==tank,"M is previous");
  ControlKey(Keys.F5);AssertControl(!paused&&!help,"F5 must not open a custom manual");ControlKey(Keys.Tab);AssertControl(diplomacyOpen&&!paused,"Tab roster");ControlKey(Keys.Tab);AssertControl(!diplomacyOpen,"Tab roster closes");
  ControlKey(Keys.D1);ControlKey(Keys.D1);AssertControl(Campaign.Dist(camera,second.P)<1,"double-tap team centering");
  Console.WriteLine("85 default bindings, groups 0-9, Shift merge, Alt/double-tap focus, keypad5, F toggle, combatant filter, M/N and F5/Tab passed.");

  ControlWorld();tank=world.Spawn("tank",0,600,1100);second=world.Spawn("tank",0,640,1140);selection.Add(tank);ControlRender();
  var p1=new PointF(700,1100);var p2=new PointF(800,1100);OnKeyDown(new KeyEventArgs(Keys.Z));mouse=ScreenOf(p1);ContextOrder(Keys.None);mouse=ScreenOf(p2);ContextOrder(Keys.None);
  ControlClick(new PointF(500,300),MouseButtons.Right);AssertControl(PlanningActive&&selection.Count==0&&planned.ContainsKey(tank),"right click during planning retains staged group");
  mouse=ScreenOf(second.P);ContextOrder(Keys.None);AssertControl(selection.Count==1&&selection[0]==second,"select next group during planning");mouse=ScreenOf(new PointF(820,1180));ContextOrder(Keys.None);
  AssertControl(tank.Path.Count==0&&second.Path.Count==0,"multiple groups wait until Z release");OnKeyUp(new KeyEventArgs(Keys.Z));AssertControl(tank.Path.Count>0&&second.Path.Count>0&&!PlanningActive,"simultaneous staged execution");
  world.Halt(tank);selection.Clear();selection.Add(tank);TogglePlanning();mouse=ScreenOf(p1);ContextOrder(Keys.None);mouse=ScreenOf(p2);ContextOrder(Keys.None);mouse=ScreenOf(new PointF(850,1160));ContextOrder(Keys.None);mouse=ScreenOf(p2);ContextOrder(Keys.None);
  AssertControl(plannedLoops.Contains(tank)&&patrolStarts[tank]==1,"loop can close at the second waypoint");CommitPlans();AssertControl(tank.Patrol&&tank.PatrolStart==1,"patrol preserves approach and loop start");
  world.Halt(tank);TogglePlanning();mouse=ScreenOf(p1);ContextOrder(Keys.None);mouse=ScreenOf(p2);ContextOrder(Keys.None);mouse=ScreenOf(p1);ContextOrder(Keys.None);ControlKey(Keys.Delete);AssertControl(planned[tank].Count==1,"Delete removes selected waypoint");CommitPlans();
  ControlKey(Keys.B);mouse=ScreenOf(new PointF(720,1200));ContextOrder(Keys.None);AssertControl(beacons.Count==1&&selectedBeacon!=null&&!beaconMode,"beacon placement");ControlKey(Keys.Enter);OnKeyPress(new KeyPressEventArgs('A'));ControlKey(Keys.Enter);AssertControl(beacons[0].Text=="A","local beacon annotation");ControlKey(Keys.Delete);AssertControl(beacons.Count==0,"Delete removes selected beacon");
  ControlKey(Keys.B);ControlClick(ScreenOf(new PointF(780,1120)),MouseButtons.Left);ControlKey(Keys.Enter);AssertControl(beaconEditing,"beacon text starts");ControlClick(ScreenOf(new PointF(780,1120)),MouseButtons.Right);AssertControl(!beaconEditing&&selectedBeacon!=null,"right click cancels beacon text without losing selected beacon");ControlKey(Keys.Enter);ControlKey(Keys.Enter);ControlKey(Keys.Delete);
  Console.WriteLine("Planning toolbar/held Z, two-group staging, second-node patrol, waypoint Delete, local beacon and text editing passed.");

  ControlWorld();tank=world.Spawn("tank",0,600,1100);selection.Add(tank);world.Spawn("power",0,350,700);world.Spawn("radar",0,350,800);world.Tick(.1f);ControlRender();
  var target=new PointF(1200,1450);var oldCamera=camera;var minimap=RadarPoint(target);var at=new PointF(MW+SideInset+2+minimap.X,88+minimap.Y);
  ControlClick(at);AssertControl(tank.Path.Count>0&&camera==oldCamera&&Campaign.Dist(tank.Goal,target)<100,"minimap left click orders selected troops");var oldGoal=tank.Goal;
  ControlClick(at,MouseButtons.Right);AssertControl(selection.Contains(tank)&&tank.Goal==oldGoal&&Campaign.Dist(camera,target)<100,"minimap right click centers without cancelling selection");
  selection.Clear();CenterAt(new PointF(600,1100));ControlClick(at);AssertControl(Campaign.Dist(camera,target)<100,"minimap left click without selection centers");
  Console.WriteLine("Native projected radar: selected left-order, empty left-focus and right-focus preserving orders passed.");

  ControlWorld();var factory=world.Spawn("factory",0,500,800);world.Funds[0]=5000;selection.Add(factory);ControlKey(Keys.D);AssertControl(!factory.Primary,"D does not change primary factory");
  var infantry=world.Spawn("squad",0,600,1100);var car=world.Spawn("ifv",0,620,1100);selection.Clear();selection.Add(car);ControlKey(Keys.D);AssertControl(infantry.Carrier==null,"empty transport D must not auto-load nearby infantry");
  selection.Clear();selection.Add(infantry);ControlKey(Keys.C);AssertControl(infantry.CheerAt==world.Time,"C celebrates idle infantry");
  infantry.Experience=2;second=world.Spawn("squad",0,660,1120);second.Experience=1;tank=world.Spawn("tank",0,700,1100);selection.Clear();selection.AddRange(new[]{infantry,second,tank});ControlKey(Keys.Y);AssertControl(selection.SequenceEqual(new[]{infantry}),"elite filter");ControlKey(Keys.Y);AssertControl(selection.SequenceEqual(new[]{second}),"veteran filter");ControlKey(Keys.Y);AssertControl(selection.SequenceEqual(new[]{tank}),"regular filter");
  ControlRender();AssertControl(!hits.Any(h=>h.Id!=null&&(h.Id.Contains("装甲")||h.Id.Contains("定位最新")||h.Id.Contains("手册 F5"))),"removed custom HUD actions still present");
  AssertControl(hits.Any(h=>h.Id=="command-toggle")&&!hits.Any(h=>h.Box.Y>H-106&&h.Box.X<MW&&h.Box.Width>400),"portrait inspector still intercepts map");
  AssertControl(MapTop==0&&MH==H,"map must use full available height");
  Console.WriteLine("Deployment-only D, explicit transport loading, cheer, rank selection, no portrait/upgrade buttons and full-height battlefield passed.");

  if(folder==null)folder=Path.GetDirectoryName(fixture);Directory.CreateDirectory(folder);
  string dataFolder=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fixture),"../../.cache/controls-test-data"));Directory.CreateDirectory(dataFolder);
  screenshotFolderOverride=Path.Combine(dataFolder,"screenshots");Directory.CreateDirectory(screenshotFolderOverride);int oldShots=Directory.GetFiles(screenshotFolderOverride,"*.png").Length;ControlRender();ControlKey(Keys.Shift|Keys.S);AssertControl(Directory.GetFiles(screenshotFolderOverride,"*.png").Length==oldShots+1,"default Shift+S screenshot");
  hotkeyPathOverride=Path.Combine(dataFolder,"keyboard-local-test.ini");OpenKeyboard();keyboardCommand="StopObject";keyboardCapturing=true;CaptureKeyboard(new KeyEventArgs(Keys.J));SaveHotkeys();paused=false;
  world.Move(tank,new PointF(820,1100),false);selection.Clear();selection.Add(tank);ControlKey(Keys.J);AssertControl(tank.Path.Count==0,"custom binding executes");world.Move(tank,new PointF(830,1100),false);ControlKey(Keys.S);AssertControl(tank.Path.Count>0,"old key stops acting after rebinding");
  hotkeysLoaded=false;EnsureHotkeys();AssertControl(hotkeyValues["StopObject"]==(int)Keys.J,"binding persistence");OpenKeyboard();keyboardCommand="StopObject";keyboardCapturing=true;CaptureKeyboard(new KeyEventArgs(Keys.K));AssertControl(!keyboardCapturing&&hotkeyValues["StopObject"]==(int)Keys.K&&hotkeyValues["ToggleRepair"]==0,"reassign conflict unbinds previous command");CancelKeyboard();AssertControl(hotkeyValues["StopObject"]==(int)Keys.J&&hotkeyValues["ToggleRepair"]==(int)Keys.K,"cancel restores both reassigned commands");DefaultHotkeys();
  ControlKey(Keys.J);AssertControl(tank.Path.Count>0,"cancelled keyboard editor does not leak temporary keys");
  AssertControl(paused&&!keyboardOpen,"cancel keyboard returns to paused options");ControlKey(Keys.Escape);AssertControl(briefing&&missionPicker&&!paused,"Escape from pause returns to mission selection");ControlKey(Keys.Escape);AssertControl(briefing&&!missionPicker,"Escape from mission selection returns to title");
  Console.WriteLine("Shift+S capture, editable key bindings, old-key release, conflict detection, save/load and cancellation passed.");
  StartMission(false,true);world.Units.Clear();world.Ore.Clear();world.Spawn("mcv",1,2900,1800);selection.Clear();paused=help=queueOpen=false;ClientSize=new Size(1920,1080);var baseHome=world.Spawn("hq",0,600,750);world.Spawn("power",0,720,750);factory=world.Spawn("factory",0,700,900);world.Spawn("barracks",0,560,900);
  tank=world.Spawn("tank",0,600,1070);world.Spawn("squad",0,655,1080);world.Funds[0]=1800;selection.Clear();selection.Add(tank);ProductionTab(3);CenterAt(new PointF(650,875));world.Train(Campaign.Info("tank"),0,null);world.Tick(.2f);
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action<string> capture=name=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,name),ImageFormat.Png);};
   capture("经典操作界面-本地.png");ControlTap("command-toggle");capture("经典操作界面-收起指令栏.png");ControlTap("command-toggle");OpenKeyboard();capture("键盘设置-本地.png");CancelKeyboard();paused=false;OpenDiplomacy();capture("阵营信息-本地.png");diplomacyOpen=false;
  }
  Console.WriteLine("Original control audit and native UI captures passed.");
 }
}
}
