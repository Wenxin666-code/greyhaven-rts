using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Greyhaven;
class InfantryInterfaceChecks {
 [STAThread]static int Main(string[] args){try{using(var window=new CommandWindow())window.CheckInfantryInterface(args[0]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {partial class CommandWindow {
 internal void CheckInfantryInterface(string folder){
  Directory.CreateDirectory(folder);ClientSize=new Size(1920,1080);pulse.Stop();
  using(var frame=new Bitmap(1920,1080))using(var g=Graphics.FromImage(frame)){
   Action<string> capture=name=>{hudDirty=true;OnPaint(new PaintEventArgs(g,ClientRectangle));frame.Save(Path.Combine(folder,name),ImageFormat.Png);};
   OpenModelReview();modelUnit=Array.IndexOf(roster,"sniper");modelSpin=false;capture("01-狙击手.png");
   modelWalking=true;modelClock=.23f;capture("02-狙击手行走.png");modelWalking=false;
   modelUnit=Array.IndexOf(roster,"squad");modelEntrenched=true;capture("03-步枪兵部署.png");
   modelUnit=Array.IndexOf(roster,"at");capture("04-反装甲兵部署.png");modelEntrenched=false;
   modelBuildings=true;modelUnit=Array.IndexOf(buildingRoster,"bunker");modelBuildingMode=0;modelClock=.5f;capture("05-驻兵碉堡.png");
   modelBuildingMode=1;foreach(float t in new[]{.4f,1.2f,2.4f}){modelClock=t;capture("06-碉堡搭建-"+t.ToString("0.0")+".png");}
   modelBuildingMode=2;foreach(float t in new[]{1.4f,3.75f,4.1f}){modelClock=t;capture("07-碉堡受损-"+t.ToString("0.00")+".png");}
   CloseModelReview();rulesOpen=true;unitRoster=true;commandRules=false;veterancyRules=false;capture("08-单位图鉴.png");rulesOpen=false;
   StartMission(true,false);pulse.Stop();briefing=false;world.Units.Clear();world.Ore.Clear();selection.Clear();
   world.Spawn("mcv",1,2600,1700);world.Spawn("hq",0,500,600);world.Spawn("barracks",0,570,600);world.Spawn("radar",0,500,680);
   var bunker=world.Spawn("bunker",0,620,690);var squad=world.Spawn("squad",0,680,690);var at=world.Spawn("at",0,700,720);var sniper=world.Spawn("sniper",0,665,730);
   world.Tick(.01f);CenterAt(new PointF(590,660));selection.Add(squad);DeploySelection();if(!squad.Entrenched)throw new Exception("D deploy binding");
   world.Tick(Campaign.StanceSeconds+.01f);world.ToggleInfantry(at);world.Tick(Campaign.StanceSeconds+.01f);mouse=ScreenOf(squad.P);var hint=DescribePointer(Keys.None);if(hint.Kind!=PointerKind.Deploy)throw new Exception("deployment cursor");
   var passenger=world.Spawn("squad",0,640,730);passenger.Carrier=bunker;selection.Clear();selection.Add(bunker);world.Tick(.01f);capture("09-战场部署与驻防.png");
   DeploySelection();if(passenger.Carrier!=null)throw new Exception("D bunker evacuation binding");
   ProductionTab(1);if(!ProductionCards().Any(t=>t.Key=="bunker"))throw new Exception("bunker defense card missing");capture("10-防御生产栏.png");
   ProductionTab(2);if(!ProductionCards().Any(t=>t.Key=="sniper"))throw new Exception("sniper card missing");capture("11-步兵生产栏.png");
  }
  // Color-mask assertions: tint changes ink color without altering geometry alpha.
  foreach(string key in new[]{"sniper","at","bunker"}){
   var t=Campaign.Info(key);var crop=SourceCell(t,0,14);
   using(var a=BuildingTint(painted[key],painted[key+"-paint"],crop,Color.Red))using(var b=BuildingTint(painted[key],painted[key+"-paint"],crop,Color.Lime)){
    int changed=0,ink=0;for(int y=0;y<a.Height;y+=2)for(int x=0;x<a.Width;x+=2){var p=a.GetPixel(x,y);var q=b.GetPixel(x,y);if(p.A!=q.A)throw new Exception("tint changed silhouette "+key);if(p.A>80){ink++;if(p.R!=q.R||p.G!=q.G)changed++;}}
    if(changed<ink*.1f)throw new Exception("insufficient visible faction paint "+key);Console.WriteLine(key+" paint coverage sampled: "+changed+" / "+ink);
   }
  }
  DrawInfantryActionReview(folder);DrawDeploymentMotionReview();
  Console.WriteLine("PASS native infantry UI / models / construction / damage / controls / faction tint / distinct action frames");
 }
 void DrawInfantryActionReview(string folder){
  drawingClient=false;var frameDir=Path.Combine(folder,"animation-frames");Directory.CreateDirectory(frameDir);
  using(var im=new Bitmap(1500,1080))using(var g=Graphics.FromImage(im)){
   for(int frame=0;frame<72;frame++){
    g.Clear(Color.FromArgb(23,34,40));Txt(g,"步兵动作 · 行军 / 瞄准 / 开火",46,25,32,pale,true);
    string[] headings={"低持枪行军","停步 · 抬枪瞄准","开火 · 后坐复位"};for(int col=0;col<3;col++)Txt(g,headings[col],230+col*405,82,21,gold,true);
    for(int row=0;row<3;row++){
     string unit=new[]{"squad","sniper","at"}[row],pose=unit=="at"?"at-deployed":unit;int y=125+row*302;
     Txt(g,Campaign.Info(unit).Name,44,y+90,23,pale,true);Txt(g,unit=="at"?"导弹需部署":"原比例 · 原占地",44,y+129,15,dim);
     for(int col=0;col<3;col++){
      var box=new RectangleF(210+col*405,y,302,302);Line(g,box.X+35,box.Bottom-26,box.Right-35,box.Bottom-26,Color.FromArgb(50,65,70));
      if(col==0){var crop=InfantryGaitCrop(unit,7);DrawStamp(g,"gait/"+unit+"/7/"+(frame%12),new Rectangle(Point.Empty,crop.Size),FootCropBox(box,crop),modelColor);}
      else{int cycle=frame%36,index=col==1?(cycle<12?(int)(cycle/12f*8):cycle<26?8:Math.Max(0,(int)((36-cycle)/10f*8))):9+frame%8;
       DrawCombatStamp(g,"combat/"+pose+"/7/"+index,box,modelColor);
      }
     }
    }
    Txt(g,"独立阵营染色 · 8 个方向 · 行走关节 / 握枪联动 / 瞄准与射击状态",46,1042,17,dim);
    im.Save(Path.Combine(frameDir,frame.ToString("D3")+".png"),ImageFormat.Png);
    if(frame==18)im.Save(Path.Combine(folder,"12-步兵动作对比.png"),ImageFormat.Png);
   }
  }
 }

 void DrawDeploymentMotionReview(){
  string folder=Path.Combine(Environment.CurrentDirectory,"docs","infantry-motion-20261110");Directory.CreateDirectory(folder);
  string frames=Path.Combine(folder,"preview-frames");Directory.CreateDirectory(frames);drawingClient=false;
  using(var im=new Bitmap(1440,900))using(var g=Graphics.FromImage(im)){
   for(int frame=0;frame<144;frame++){
    float t=frame/24f,phase=t%3,progress=phase<1.1f?phase/1.1f:phase<1.5f?1:phase<2.6f?(2.6f-phase)/1.1f:0;
    g.Clear(Color.FromArgb(23,34,40));Txt(g,"连续动作 · 部署与收起",40,24,32,pale,true);
    Txt(g,phase<1.1f?"部署：蹲下 / 取出装备":phase<1.5f?"完成部署":phase<2.6f?"收起：折叠 / 背回装备":"恢复行军",42,74,21,gold,true);
    int view=frame<72?7:2;
    for(int col=0;col<3;col++){
     string key=new[]{"squad","at","sniper"}[col];var box=new RectangleF(15+col*475,150,460,460);
     Txt(g,Campaign.Info(key).Name,70+col*475,125,25,pale,true);
     if(key=="sniper")DrawCombatStamp(g,"combat/sniper/"+view+"/"+(int)Math.Round(progress*8),box,modelColor);
     else DrawStamp(g,"stance/"+key+"/"+view+"/"+(int)Math.Round(progress*32),new Rectangle(0,0,384,384),box,modelColor);
     string note=key=="squad"?"屈膝下蹲 · 放下并展开阵地":key=="at"?"收起机枪 · 背部取出发射器":"完整刚性枪械 · 抬枪瞄准";
     Txt(g,note,35+col*475,635,21,dim);
     var tiny=new RectangleF(180+col*475,715,105,105);
     if(key=="sniper")DrawCombatStamp(g,"combat/sniper/"+view+"/"+(int)Math.Round(progress*8),tiny,modelColor);
     else DrawStamp(g,"stance/"+key+"/"+view+"/"+(int)Math.Round(progress*32),new Rectangle(0,0,384,384),tiny,modelColor);
    }
    Txt(g,"八方向 · 同一模型连续运动 · 原尺寸与占地 · 独立阵营染色",40,851,18,dim);
    im.Save(Path.Combine(frames,frame.ToString("D3")+".png"),ImageFormat.Png);
    if(frame==27)im.Save(Path.Combine(folder,"部署动作对比.png"),ImageFormat.Png);
   }
  }
 }
}}
