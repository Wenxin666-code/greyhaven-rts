using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;

class DefenseChecks {
 [STAThread]static int Main(string[] args){try{
  Campaign.CheckDefenseRules();
  if(args.Length>0)using(var window=new CommandWindow())window.CheckDefensePreview(args[0]);
  return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {
public partial class Campaign {
 internal void DefenseRevealAll(int side=0){for(int x=0;x<MapFogCols;x++)for(int y=0;y<MapFogRows;y++)discovered[side,x,y]=true;}
 static void DefenseAssert(bool ok,string reason){if(!ok)throw new Exception(reason);}
 internal static void CheckDefenseRules(){
  var w=new Campaign(true);w.Units.Clear();w.Ore.Clear();
  var hq=w.Spawn("hq",0,480,600);var lot=Footprint(hq.T,hq.P);var post=Info("post");
  foreach(int side in new[]{0,1,2}){
   var r=new RectangleF(lot.Right+4*Tile,lot.Top,Tile,Tile);
   if(side>0)w.Spawn("hq",side,hq.P.X,hq.P.Y);
   DefenseAssert(w.InBuildRange(post,r,side),"Defence 4-cell edge gap "+side);
   r.X+=Tile;DefenseAssert(!w.InBuildRange(post,r,side),"Defence beyond 4-cell gap "+side);
   r.X=lot.Right+3*Tile;DefenseAssert(!w.InBuildRange(Info("power"),r,side),"Ordinary building still limited to 2-cell gap");
  }
  w.Units.Clear();var tower=w.Spawn("post",0,600,600);
  DefenseAssert(!w.InBuildRange(post,new RectangleF(630,600,Tile,Tile),0),"Turret does not expand construction anchors");
  var tank=w.Spawn("tank",1,tower.P.X+post.Range-12,tower.P.Y);float health=tank.Hp;
  for(int i=0;i<25;i++){w.Time+=.1f;w.Reveal();w.UnitTick(tower,.1f,true);}
  Console.WriteLine("Turret engagement: target HP "+tank.Hp+" / "+health+", turret HP "+tower.Hp+", target distance "+TargetDistance(tank,tower)+", enemy range "+tank.T.Range+", sees target "+w.Explored(0,tank.P)+", turret at "+tower.P+", target at "+tank.P);
  DefenseAssert(tank.Hp<health&&tower.Hp==tower.T.Hp,"Turret autonomously engages within its continuous range");
  DefenseAssert(Sight(tower)>=tower.T.Range&&tower.T.Hp==400&&tower.T.Hit==50&&Math.Abs(tower.T.Reload-26/15f)<.001,"Turret combat stats and sight");
  var artillery=w.Spawn("arty",1,900,600);health=artillery.Hp;w.Halt(tower);
  for(int i=0;i<15;i++){w.Time+=.1f;w.Reveal();w.UnitTick(tower,.1f,true);}
  DefenseAssert(artillery.Hp==health&&artillery.T.Range>post.Range,"Artillery remains a longer-range counter");

  for(int side=1;side<=5;side++){
   w=new Campaign(false,false,true,true);w.Units.Clear();w.Ore.Clear();w.Time=130;w.Funds[side]=2000;w.OreReceipts[side]=180;
   var home=w.Home(side);w.Spawn("hq",side,home.X,home.Y);w.Spawn("power",side,home.X+96,home.Y-72);
   w.Spawn("refinery",side,home.X,home.Y+144);w.Spawn("barracks",side,home.X+96,home.Y+24);w.Spawn("factory",side,home.X-96,home.Y+96);
   w.DefenseRevealAll(side);
   var brain=w.Brain(side);DefenseAssert(w.AiPlanBuild(side,brain,"radar",home),"Plan main construction "+side);
   var mainPoint=brain.BuildPoints[0];int count=w.Units.Count,cash=w.Funds[side];w.AiFortify(side,brain,new Piece[0]);
   DefenseAssert(w.Construction(side,1)!=null&&w.Construction(side,0)!=null&&w.Units.Count==count,"Defence plans alongside building; no free spawn "+side);
   var defensePoint=brain.BuildPoints[1];DefenseAssert(brain.BuildPoints[0]==mainPoint&&mainPoint!=defensePoint,"Separate placement plans "+side);
   for(int i=0;i<(int)(Math.Max(Info("radar").Seconds,post.Seconds)*10)+1;i++)w.TickConstruction(.1f);
   DefenseAssert(w.Construction(side,1).Ready&&w.Funds[side]==cash-Info("radar").Price-post.Price,"Both production categories charge actual cost "+side);
   w.AiPlaceReady(side,brain);var built=w.Units.Single(p=>p.T.Key=="post");
   DefenseAssert(built.P==Middle(Footprint(post,defensePoint))&&w.Units.Any(p=>p.T.Key=="radar"&&p.P==Middle(Footprint(Info("radar"),mainPoint))),"Both buildings use their own sites "+side);
   DefenseAssert(w.AiDefenseGround(Footprint(post,built.P)),"AI avoids roads, bridges and ramps "+side);
   w.Funds[side]=0;w.AiFortify(side,brain,new Piece[0]);DefenseAssert(w.Construction(side,1)==null,"No free emergency defence "+side);
   w.Time=300;w.Funds[side]=300;DefenseAssert(w.AiReserve(side)>=w.AiReserve(side,false)+post.Price,"Recruitment reserves planned defence credits "+side);
  }
  Console.WriteLine("Defence: 4/2-cell placement, no turret expansion, reference sentry fire, artillery counter, five paid AI defence plans, independent construction sites and recruitment reservation passed.");
 }
}
partial class CommandWindow {
 internal void CheckDefensePreview(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);StartMission(true,false);pulse.Stop();world.Units.Clear();world.Ore.Clear();
  world.Spawn("hq",0,480,600);world.Spawn("mcv",1,2800,1600);place=Campaign.Info("post");selection.Clear();
  var at=new PointF(600,648);var lot=Campaign.Footprint(place,at);
  world.DefenseRevealAll();
  CenterAt(at);mouse=ScreenOf(at);briefing=false;hudDirty=true;
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,"炮台建造射程.png"),ImageFormat.Png);
  }
  Console.WriteLine("Native isometric defence range and placement preview saved.");
 }
}
}
