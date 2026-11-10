using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 enum PointerKind { Arrow,Select,Move,Blocked,Attack,AttackMove,Guard,Escort,Harvest,Unload,Rally,Build,ForceMove,Pan,Waypoint,Partial,Repair,Deploy,Sell }
 struct PointerHint {public PointerKind Kind;public string Text;public PointerHint(PointerKind kind,string text){Kind=kind;Text=text;}}
 readonly Dictionary<PointerKind,Cursor> battleCursors=new Dictionary<PointerKind,Cursor>();
 readonly List<IntPtr> cursorHandles=new List<IntPtr>();
 PointerHint pointerHint;double nextPointerRefresh;Keys pointerMods;
 [StructLayout(LayoutKind.Sequential)]struct CursorInfo { [MarshalAs(UnmanagedType.Bool)]public bool Icon;public uint X,Y;public IntPtr Mask,Color; }
 [DllImport("user32.dll")]static extern IntPtr CreateIconIndirect(ref CursorInfo info);
 [DllImport("user32.dll")]static extern bool DestroyCursor(IntPtr cursor);
 PointerHint MovementHint(PointF p,PointerKind kind,string text){
  var troops=selection.Where(u=>u.Deployed&&!u.T.Building).ToArray();
  int count=troops.Count(u=>world.CanMoveTo(u,p));
  return count==0?new PointerHint(PointerKind.Blocked,"不可达 · 请换一个落点"):
   count<troops.Length?new PointerHint(PointerKind.Partial,"部分单位可达 · "+count+" / "+troops.Length):new PointerHint(kind,text);
 }
 PointerHint DescribePointer(Keys mods){
  if(briefing||paused||diplomacyOpen||world.Won||world.Lost||mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>=MapTop+MH||hits.Any(h=>h.Box.Contains(mouse)))return new PointerHint(PointerKind.Arrow,"");
  if(panning||rightDragging)return new PointerHint(PointerKind.Pan,"拖动视角");
  if(drag&&dragged)return new PointerHint(PointerKind.Select,"");
  var p=WorldOf(mouse);var hit=HitScreen(mouse);
  if(place!=null)return world.Construction(place)!=null&&world.Construction(place).Ready&&world.Live(0).Any(u=>u.T.Key=="hq")&&world.CanPlace(place,p,null)?new PointerHint(PointerKind.Build,"放置 "+place.Name):new PointerHint(PointerKind.Blocked,world.AssessPlacement(place,p,null,0,true).Summary);
  if(structureMode!=StructureMode.None){bool valid=structureMode==StructureMode.Sell?world.CanSell(hit):hit!=null&&hit.Deployed&&hit.Side==0&&hit.T.Building&&(hit.Hp<hit.T.Hp||hit.Repairing);
   return new PointerHint(valid?(structureMode==StructureMode.Sell?PointerKind.Sell:PointerKind.Repair):PointerKind.Blocked,valid?(structureMode==StructureMode.Sell?"出售建筑 · 返还 50% 基础价格":"切换建筑维修"):"此处不能执行建筑工具");}
  if(!attack&&mods==Keys.None&&hit!=null&&selection.Count==1&&selection[0]==hit){
   if(Campaign.CanDeployInfantry(hit))return new PointerHint(PointerKind.Deploy,hit.Entrenched?"收起阵地 · 左键或 D":"部署阵地 · 左键或 D");
   if(hit.T.Key=="mcv")return new PointerHint(world.CanDeploy(hit)?PointerKind.Deploy:PointerKind.Blocked,world.CanDeploy(hit)?"展开基地 · 左键或 D":"空间不足，无法展开");
   if(hit.Deployed&&(hit.T.Garrison||hit.T.Key=="ifv")&&world.HouseCount(hit)>0)return new PointerHint(PointerKind.Deploy,"卸载／撤出 · 左键或 D");
  }
  if(!attack&&mods==Keys.None&&hit==null&&selection.Count==1&&selection[0].T.Key=="hq")return new PointerHint(world.CanRelocate(selection[0],p)?PointerKind.Move:PointerKind.Blocked,"收起基地并移动");
  if(beaconMode)return new PointerHint(PointerKind.Waypoint,"放置战场标记");
  if(PlanningActive)return MovementHint(p,PointerKind.Waypoint,"添加路径点");
  if((mods&(Keys.Control|Keys.Alt))==(Keys.Control|Keys.Alt)){
   if(hit!=null&&hit.Side==0)return selection.Any(u=>u!=hit&&u.Deployed&&!u.T.Building)?new PointerHint(PointerKind.Escort,"护卫 "+hit.T.Name):new PointerHint(PointerKind.Blocked,"请选其他部队进行护卫");
   return MovementHint(p,PointerKind.Guard,"到达后警戒");
  }
  if((mods&Keys.Alt)!=0)return MovementHint(p,PointerKind.ForceMove,"强制移动");
  bool advance=(mods&(Keys.Control|Keys.Shift))==(Keys.Control|Keys.Shift);
  if((mods&Keys.Control)!=0&&!advance)return selection.Any(u=>u.Deployed&&world.Armed(u))&&world.Battlefield.Contains(p)?new PointerHint(PointerKind.Attack,Campaign.IsRelayStation(hit)?"关键设施 · 强制摧毁将导致失败":"强制开火"):new PointerHint(PointerKind.Blocked,"无法强制开火");
  bool miners=selection.Count>0&&selection.All(u=>u.Deployed&&u.T.Key=="miner");
  if(!attack&&mods==Keys.None&&miners){
   if(hit!=null&&hit.Side==0&&hit.Deployed&&hit.T.Key=="refinery")return MovementHint(Campaign.DockPoint(hit),PointerKind.Unload,"返厂卸矿");
   if(hit==null&&world.Explored(0,p)&&world.OreAt(p)!=null)return MovementHint(world.OreAt(p).P,PointerKind.Harvest,"采集矿石");
  }
  if(!attack&&mods==Keys.None&&hit!=null){
   if(selection.Any(u=>world.CanEngineer(u,hit)))return new PointerHint(hit.Side==0?PointerKind.Repair:PointerKind.Unload,hit.Side==0?"进入维修 · 消耗工兵":"进入占领 · 消耗工兵");
   if(selection.Any(u=>world.CanBoard(u,hit)))return new PointerHint(PointerKind.Unload,"步兵登车 · 机枪／反装甲导弹／维修模式");
   if(selection.Any(u=>world.CanRepairVehicle(u,hit)))return new PointerHint(PointerKind.Repair,"维修车辆");
  }
  if(!attack&&!advance&&hit!=null&&hit.T.Garrison&&(hit.Side<0||hit.Side==0)&&selection.Any(u=>u.T.Foot))return selection.Any(u=>world.CanEnterHouse(u,hit))?new PointerHint(PointerKind.Unload,hit.Side<0?"进入并占领房屋":"进驻房屋"):new PointerHint(PointerKind.Blocked,hit.Side<0?"需要空闲工兵":hit.T.Key=="bunker"?"碉堡只接受步枪兵 · 最多 5 人":"房屋已满或单位不能驻防");
  if(!attack&&!advance&&hit!=null&&hit.Side==0)return new PointerHint(PointerKind.Select,"选择 "+hit.T.Name);
  if(selection.Count==0)return new PointerHint(PointerKind.Arrow,"");
  if(Campaign.IsRelayStation(hit))return new PointerHint(PointerKind.Blocked,"任务设施 · 需工兵占领");
  if(hit!=null&&hit.Side>0)return selection.Any(u=>u.Deployed&&world.Armed(u))?new PointerHint(PointerKind.Attack,"攻击 "+hit.T.Name):new PointerHint(PointerKind.Blocked,"所选单位没有武器");
  p=ApproachSite(mouse,p);
  if(selection.Count==1&&selection[0].T.Building)return world.CanRallyTo(selection[0],p)?new PointerHint(PointerKind.Rally,"设置集结点"):new PointerHint(PointerKind.Blocked,"无法设置集结点");
  return MovementHint(p,attack||advance?PointerKind.AttackMove:PointerKind.Move,attack||advance?"攻击移动":"移动");
 }
 void RejectDestination(){world.Radio="此处不可达，已保留选择与原命令，请换一个落点。";hudDirty=true;UpdateBattleCursor(true);}
 void UpdateBattleCursor(bool force=false){
  if(resourcesReleased)return;double now=watch.Elapsed.TotalSeconds;var mods=ModifierKeys;
  if(!force&&now<nextPointerRefresh&&mods==pointerMods)return;
  nextPointerRefresh=now+.07;pointerMods=mods;pointerHint=DescribePointer(mods);
  Cursor c;if(!battleCursors.TryGetValue(pointerHint.Kind,out c)){
   using(var bitmap=CursorArtwork(pointerHint.Kind))using(var mask=new Bitmap(40,40,System.Drawing.Imaging.PixelFormat.Format1bppIndexed)){
    var info=new CursorInfo{X=(uint)(pointerHint.Kind==PointerKind.Arrow?5:20),Y=(uint)(pointerHint.Kind==PointerKind.Arrow?4:20),Color=bitmap.GetHbitmap(Color.FromArgb(0)),Mask=mask.GetHbitmap()};
    IntPtr handle;
    try{handle=CreateIconIndirect(ref info);}finally{DeleteObject(info.Color);DeleteObject(info.Mask);}
    if(handle==IntPtr.Zero){Cursor=Cursors.Default;return;}
    cursorHandles.Add(handle);c=new Cursor(handle);battleCursors[pointerHint.Kind]=c;
   }
  }
  if(Cursor!=c)Cursor=c;
 }
 void ReleaseBattleCursors(){Cursor=Cursors.Default;foreach(var c in battleCursors.Values)c.Dispose();battleCursors.Clear();foreach(var h in cursorHandles)DestroyCursor(h);cursorHandles.Clear();}
 void DrawPointerHint(Graphics g){
  if(pointerHint.Kind!=PointerKind.Blocked&&pointerHint.Kind!=PointerKind.Partial)return;
  if(briefing||paused||drag||mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>=H||hits.Any(h=>h.Box.Contains(mouse)))return;
  float x=Math.Min(MW-235,mouse.X+22),y=Math.Max(MapTop+4,mouse.Y-30);
  Rect(g,x-5,y-3,230,24,Color.FromArgb(220,ink));Txt(g,pointerHint.Text,x,y,12,pointerHint.Kind==PointerKind.Blocked?red:gold,true);
 }
 static Bitmap CursorArtwork(PointerKind kind){
  var bitmap=new Bitmap(40,40,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(bitmap)){
   g.SmoothingMode=SmoothingMode.None;
   Color color=kind==PointerKind.Blocked||kind==PointerKind.Attack||kind==PointerKind.AttackMove?C("#ff856c"):
    kind==PointerKind.Harvest||kind==PointerKind.Rally||kind==PointerKind.Partial||kind==PointerKind.Waypoint||kind==PointerKind.Deploy||kind==PointerKind.Sell?C("#ffd36e"):C("#8de1bd");
   // Two-pass strokes stay legible on both concrete and dark shroud.
   for(int pass=0;pass<2;pass++)using(var pen=new Pen(pass==0?C("#101b20"):color,pass==0?5:2)){
    pen.LineJoin=LineJoin.Miter;pen.StartCap=LineCap.Flat;pen.EndCap=LineCap.Flat;
    if(kind==PointerKind.Deploy){g.DrawPolygon(pen,new[]{new Point(14,10),new Point(26,10),new Point(30,14),new Point(30,26),new Point(26,30),new Point(14,30),new Point(10,26),new Point(10,14)});g.DrawLines(pen,new[]{new Point(15,6),new Point(20,1),new Point(25,6)});g.DrawLines(pen,new[]{new Point(15,34),new Point(20,39),new Point(25,34)});g.DrawLines(pen,new[]{new Point(6,15),new Point(1,20),new Point(6,25)});g.DrawLines(pen,new[]{new Point(34,15),new Point(39,20),new Point(34,25)});continue;}
    if(kind==PointerKind.Sell){g.DrawLines(pen,new[]{new Point(29,9),new Point(13,9),new Point(9,13),new Point(13,19),new Point(27,21),new Point(31,27),new Point(27,31),new Point(11,31)});g.DrawLine(pen,20,4,20,36);continue;}
    if(kind==PointerKind.Repair){g.DrawLines(pen,new[]{new Point(25,5),new Point(20,11),new Point(27,18),new Point(34,12),new Point(33,23),new Point(24,25),new Point(12,36),new Point(5,29),new Point(17,18),new Point(16,9),new Point(25,5)});continue;}
    if(kind==PointerKind.Arrow){
     var arrow=new[]{new Point(5,4),new Point(6,29),new Point(12,23),new Point(17,34),new Point(22,31),new Point(17,20),new Point(28,19)};
     if(pass==0){using(var fill=new SolidBrush(C("#e9ead9")))g.FillPolygon(fill,arrow);}g.DrawPolygon(pen,arrow);continue;
    }
    if(kind==PointerKind.Blocked){g.DrawEllipse(pen,8,8,24,24);g.DrawLine(pen,11,29,29,11);continue;}
    if(kind==PointerKind.Attack||kind==PointerKind.AttackMove){
     g.DrawEllipse(pen,10,10,20,20);g.DrawLine(pen,20,4,20,13);g.DrawLine(pen,20,27,20,36);g.DrawLine(pen,4,20,13,20);g.DrawLine(pen,27,20,36,20);
     if(kind==PointerKind.AttackMove)g.DrawLines(pen,new[]{new Point(27,30),new Point(33,35),new Point(38,30)});continue;
    }
    if(kind==PointerKind.Guard||kind==PointerKind.Escort){
     g.DrawPolygon(pen,new[]{new Point(20,5),new Point(32,10),new Point(30,25),new Point(20,35),new Point(10,25),new Point(8,10)});
     g.DrawLine(pen,20,13,20,26);if(kind==PointerKind.Escort)g.DrawLine(pen,14,19,26,19);continue;
    }
    if(kind==PointerKind.Harvest){g.DrawPolygon(pen,new[]{new Point(20,6),new Point(33,19),new Point(20,33),new Point(7,19)});g.DrawLine(pen,7,19,33,19);g.DrawLine(pen,20,6,20,33);continue;}
    if(kind==PointerKind.Unload){g.DrawLines(pen,new[]{new Point(21,6),new Point(32,6),new Point(32,34),new Point(21,34)});g.DrawLine(pen,5,20,25,20);g.DrawLines(pen,new[]{new Point(19,14),new Point(25,20),new Point(19,26)});continue;}
    if(kind==PointerKind.Rally||kind==PointerKind.Waypoint){g.DrawLine(pen,13,6,13,33);g.DrawPolygon(pen,new[]{new Point(13,6),new Point(32,10),new Point(13,18)});g.DrawEllipse(pen,8,28,13,7);continue;}
    if(kind==PointerKind.Build){g.DrawPolygon(pen,new[]{new Point(20,7),new Point(35,17),new Point(20,27),new Point(5,17)});g.DrawLine(pen,5,17,5,27);g.DrawLine(pen,35,17,35,27);g.DrawLines(pen,new[]{new Point(5,27),new Point(20,37),new Point(35,27)});continue;}
    if(kind==PointerKind.Select){foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1}){g.DrawLine(pen,20+sx*12,20+sy*12,20+sx*5,20+sy*12);g.DrawLine(pen,20+sx*12,20+sy*12,20+sx*12,20+sy*5);}continue;}
    foreach(int sign in new[]{-1,1}){
     g.DrawLines(pen,new[]{new Point(20-5,20+sign*7),new Point(20,20+sign*13),new Point(20+5,20+sign*7)});
     g.DrawLines(pen,new[]{new Point(20+sign*7,20-5),new Point(20+sign*13,20),new Point(20+sign*7,20+5)});
    }
    if(kind==PointerKind.Partial){g.DrawLine(pen,34,25,34,30);g.DrawEllipse(pen,33,34,2,2);}
    if(kind==PointerKind.ForceMove)g.DrawLine(pen,13,20,27,20);
   }
  }return bitmap;
 }
}
}
