using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Greyhaven;
class PerformanceInterfaceChecks {
 [STAThread]static int Main(string[] args){try{using(var f=new CommandWindow()){f.CheckPerformanceInterface(args[0]);f.CheckParallelProductionInput(args[0]);}return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
partial class CommandWindow {
 internal void CheckPerformanceInterface(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);pulse.Stop();CheckCarrierControls();
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action<string> capture=name=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,name),ImageFormat.Png);};
   briefing=true;mapReview=false;rulesOpen=true;unitRoster=true;commandRules=false;
   capture("01-载员规则.png");
   islandMissionSelected=true;relayMissionSelected=false;OpenMapReview();
   if(riverMap.WidthCells!=270||riverMap.HeightCells!=180||riverMap.Houses.Count!=33)throw new Exception("compact island preview dimensions");
   capture("02-孤岛要塞总览.png");riverMap.Export(Path.Combine(folder,"island-fortress-v03.json"));
   StartIslandMission();capture("03-孤岛要塞开局.png");
  }
  Console.WriteLine("PASS native UI: passenger instructions, compact third-mission preview/export and opening; tactical zoom unchanged.");
 }
 void CheckCarrierControls(){
  StartMission(true,false);pulse.Stop();briefing=false;paused=false;world.Units.Clear();world.Ore.Clear();selection.Clear();
  world.Spawn("mcv",1,2400,1300);var car=world.Spawn("ifv",0,600,900);var eng=world.Spawn("engineer",0,680,900);var tank=world.Spawn("tank",0,750,900);tank.Hp=200;
  selection.Add(eng);ContextOrderAt(Keys.None,car.P,car);
  if(eng.Entering!=car||eng.Carrier!=null)throw new Exception("left-click must order walking into IFV, not instant loading");
  for(int i=0;i<400&&eng.Carrier==null;i++)world.Tick(.05f);
  if(eng.Carrier!=car||!world.RepairVehicle(car))throw new Exception("engineer boarding via mouse order");
  selection.Clear();selection.Add(car);ContextOrderAt(Keys.None,tank.P,tank);
  if(car.SupportTarget!=tank)throw new Exception("left-click repair vehicle target");
  for(int i=0;i<400&&tank.Hp==200;i++)world.Tick(.05f);
  if(tank.Hp<=200)throw new Exception("repair order failed to reach target and heal");
  OnKeyDown(new KeyEventArgs(Keys.S));if(car.SupportTarget!=null||car.Path.Count!=0)throw new Exception("S cancels repair pursuit");
  world.Move(car,new PointF(950,900),false);OnKeyDown(new KeyEventArgs(Keys.D));
  if(eng.Carrier!=null||!eng.Alive||car.Path.Count!=0||world.RepairVehicle(car))throw new Exception("D stops, unloads living engineer and restores missile");
  var building=world.Spawn("power",0,600,1080);building.Hp=200;selection.Clear();selection.Add(eng);ContextOrderAt(Keys.None,building.P,building);
  if(eng.Entering!=building)throw new Exception("unloaded engineer left-click building repair");
  for(int i=0;i<900&&eng.Alive;i++)world.Tick(.05f);
  if(eng.Alive||building.Hp!=building.T.Hp)throw new Exception("engineer enters, repairs building and is consumed");
  Console.WriteLine("PASS native controls: left-click walk/board, left-click repair, S stop, moving D unload, dismounted engineer building repair/consumption.");
 }
}
}
