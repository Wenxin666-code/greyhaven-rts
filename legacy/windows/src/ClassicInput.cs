using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 int productionFilter;
 Piece pressedUnit,followUnit;bool resolvingPress,zHeld,showRanges;
 readonly Dictionary<Piece,List<PointF>> planned=new Dictionary<Piece,List<PointF>>();
 readonly HashSet<Piece> plannedLoops=new HashSet<Piece>();
 struct DrawnMobile {public PointF Position;public float Depth;}
 readonly Dictionary<int,DrawnMobile> drawnMobiles=new Dictionary<int,DrawnMobile>();
 Campaign drawnPickWorld;PointF drawnPickCamera;float drawnPickZoom;
 void ResetClassicInput(){showRanges=false;showGrid=false;commandRules=false;veterancyRules=false;productionFilter=4;structureMode=StructureMode.None;pressedUnit=null;followUnit=null;resolvingPress=false;zHeld=false;planned.Clear();plannedLoops.Clear();drawnPickWorld=null;drawnMobiles.Clear();}
 void BeginPickFrame(){
  drawnPickWorld=world;drawnPickCamera=camera;drawnPickZoom=zoom;drawnMobiles.Clear();
 }
 float PickPixel {get{return 1/Math.Max(.5f,scale);}}
 RectangleF MobilePickBody(Piece u,PointF p,float size){
  if(u.T.Key!="tank")return VisibleUnitBox(u,p,size);
  var pose=TankPoseFor(u,p,size);
  return VisibleInk(TankHullKey,pose.HullCell,pose.HullBox);
 }
 RectangleF MobilePickArea(RectangleF body,PointF ground,bool foot){
  var area=RectangleF.FromLTRB(Math.Min(body.Left,ground.X),Math.Min(body.Top,ground.Y),Math.Max(body.Right,ground.X),Math.Max(body.Bottom,ground.Y));
  float px=PickPixel;
  area.Inflate((foot?5:6)*px,7*px);
  area.Inflate(Math.Max(0,26*px-area.Width)*.5f,Math.Max(0,(foot?30:23)*px-area.Height)*.5f);
  return area;
 }
 RectangleF PickBox(Piece u){
  var position=ScreenOf(u.P);
  if(u.T.Building)return BuildingRect(Campaign.Footprint(u.T,u.P),DeploymentPoint(u,position),zoom);
  return MobilePickArea(MobilePickBody(u,position,u.T.Size*zoom),position,u.T.Foot);
 }
 static float PickDistance(RectangleF body,PointF point){
  float x=Math.Max(0,Math.Max(body.Left-point.X,point.X-body.Right));
  float y=Math.Max(0,Math.Max(body.Top-point.Y,point.Y-body.Bottom));
  float cx=point.X-(body.Left+body.Width*.5f),cy=point.Y-(body.Top+body.Height*.5f);
  return x*x+y*y+.12f*(cx*cx+cy*cy);
 }
 bool MiningOrdersActive(Keys mods){
  return !attack&&mods==Keys.None&&selection.Count>0&&selection.All(u=>u.Deployed&&u.T.Key=="miner");
 }
 Piece HitScreen(PointF point,bool useDrawnPositions=false,Keys mods=Keys.None){
  if(point.X<0||point.X>=MW||point.Y<MapTop||point.Y>=MapTop+MH)return null;
  bool shown=useDrawnPositions&&drawnPickWorld==world&&drawnPickCamera==camera&&drawnPickZoom==zoom;
  float margin=16*PickPixel;
  Piece exact=null,near=null,foundation=null;
  float exactDepth=float.MinValue,nearDepth=float.MinValue,foundationDepth=float.MinValue,nearScore=float.MaxValue;
  PointF? ground=null;
  foreach(var u in world.Units){
   if(!u.Alive||u.Carrier!=null)continue;
   float depth=u.P.X+u.P.Y;
   if(u.T.Building){
    var box=PickBox(u);
    if(!box.Contains(point)||!world.Visible(u))continue;
    if(depth>=exactDepth&&BuildingInkHit(u,box,point)){exact=u;exactDepth=depth;}
    else if(exact==null&&depth>=foundationDepth){
     if(!ground.HasValue)ground=WorldOf(point);
     if(Campaign.Footprint(u.T,u.P).Contains(ground.Value)){foundation=u;foundationDepth=depth;}
    }
    continue;
   }
   if(structureMode!=StructureMode.None)continue;
   DrawnMobile previous;
   PointF position;
   if(shown&&drawnMobiles.TryGetValue(u.Id,out previous)){position=previous.Position;depth=previous.Depth;}
   else position=ScreenOf(u.P);
   float size=u.T.Size*zoom,reach=size*.75f+margin;
   if(Math.Abs(point.X-position.X)>reach||Math.Abs(point.Y-position.Y)>reach||!world.Visible(u))continue;
   // Resolve overlapping painted pixels in the same order as DrawBattle.
   if(depth>=exactDepth&&MobileHit(u,position,size,point)){exact=u;exactDepth=depth;continue;}
   if(exact!=null)continue;
   var body=MobilePickBody(u,position,size);
   if(!MobilePickArea(body,position,u.T.Foot).Contains(point))continue;
   float score=PickDistance(body,point);
   if(score<nearScore||score==nearScore&&depth>=nearDepth){near=u;nearScore=score;nearDepth=depth;}
  }
  if(exact!=null)return exact;
  if(near!=null&&near.T.Key=="miner"&&selection.Contains(near)&&MiningOrdersActive(mods)){
   var location=ground??WorldOf(point);
   if(world.Explored(0,location)&&world.OreAt(location)!=null)return foundation;
  }
  return near??foundation;
 }
 void DrawPreselection(Graphics g,Piece u){
  if(u==null||drag||panning||rightPending||place!=null||briefing||paused||diplomacyOpen||hits.Any(h=>h.Box.Contains(mouse)))return;
  var r=VisibleUnitBox(u,ScreenOf(u.P),u.T.Size*zoom);r.Inflate(3*PickPixel,3*PickPixel);var c=u.Side<0?pale:u.Side==0?teal:Team(u.Side);
  PixelCorners(g,r,c);
  Txt(g,PreselectionLabel(u),mouse.X+12,mouse.Y-22,12,c,true);
 }
 string PreselectionLabel(Piece unit){
  string owner=unit.Side<0?(world.CanCaptureBuilding(unit)?"中立 · 工兵可占领 ":"中立 "):
   unit.Side==0?"我方 ":"敌方 "+unit.Side+" ";
  return owner+unit.T.Name+(unit.T.Key=="ifv"?" · "+ReferenceBalance.IFVRole(unit):"");
 }
 void GuardSelection(){foreach(var u in selection)world.Guard(u);attack=false;world.Radio="警戒此处：迎敌后返回驻守位置。";}
 bool ClassicKey(KeyEventArgs e){
  if(e.KeyCode==Keys.Q||e.KeyCode==Keys.W||e.KeyCode==Keys.E||e.KeyCode==Keys.R){
   ProductionTab(e.KeyCode==Keys.W?1:e.KeyCode==Keys.E?2:e.KeyCode==Keys.R?3:4);return true;
  }
  if(e.KeyCode==Keys.K){StructureTool(StructureMode.Repair);return true;}
  if(e.KeyCode==Keys.L){StructureTool(StructureMode.Sell);return true;}
  if(e.KeyCode==Keys.G){GuardSelection();return true;}
  if(e.KeyCode==Keys.F){var chosen=selection.FirstOrDefault(u=>u.Alive);followUnit=followUnit==chosen?null:chosen;if(followUnit!=null)CenterAt(followUnit.P);return true;}
  if(e.KeyCode==Keys.X){world.Scatter(selection.Where(u=>u.Deployed&&!u.T.Building).ToArray());return true;}
  if(e.KeyCode==Keys.Z){if(!zHeld){if(!PlanningActive){planned.Clear();plannedLoops.Clear();patrolStarts.Clear();}zHeld=true;}world.Radio="按住 Z 依次点击路径点，松开执行；点回首个路径点形成巡逻。";return true;}
  if(e.KeyCode==Keys.P){selection=world.Live(0).Where(u=>!u.T.Building&&u.T.Hit>0).ToList();return true;}
  if(e.KeyCode==Keys.M||e.KeyCode==Keys.N){
   var units=world.Live(0).Where(u=>!u.T.Building).OrderBy(u=>u.Id).ToList();if(units.Count==0)return true;
   int current=units.IndexOf(selection.FirstOrDefault()),next=(current+(e.KeyCode==Keys.N?1:-1)+units.Count)%units.Count;
   selection.Clear();selection.Add(units[next]);return true;
  }
  return false;
 }
 void AddPlannedPoint(PointF p){
  var eligible=selection.Where(u=>world.CanMoveTo(u,p)).ToArray();if(eligible.Length==0){RejectDestination();return;}
  foreach(var u in eligible){
   List<PointF> points;if(!planned.TryGetValue(u,out points)){world.Halt(u);planned[u]=points=new List<PointF>();}
   int loop=points.FindIndex(q=>Campaign.Dist(q,p)<12);if(points.Count>1&&loop>=0&&loop<points.Count-1){plannedLoops.Add(u);patrolStarts[u]=loop;continue;}
   points.Add(p);
  }
  Mark(p,null,false);
 }
 void CommitPlans(){
  zHeld=false;planningPinned=false;if(planned.Count==0){world.Radio="没有可执行的路径点，保留原命令。";plannedLoops.Clear();return;}int failed=0;foreach(var pair in planned)if(!world.SetWaypoints(pair.Key,pair.Value.ToArray(),plannedLoops.Contains(pair.Key),patrolStarts.ContainsKey(pair.Key)?patrolStarts[pair.Key]:0))failed++;
  world.Radio=failed>0?failed+" 个单位的首个路径点不可达。":"部队按规划路线出发。";planned.Clear();plannedLoops.Clear();patrolStarts.Clear();waypointOwner=null;waypointSelected=-1;
 }
 TypeInfo[] ProductionCards(){
  if(world.Live(0).Any(u=>u.T.Key=="barracks"))seenCameos.Add("sniper");
  foreach(var type in Campaign.Types){string reason=world.Require(type,0);if(reason==null||reason.Contains("队列已满")||world.Construction(type)!=null)seenCameos.Add(type.Key);}
  return Campaign.Types.Where(t=>seenCameos.Contains(t.Key)&&(army?!t.Building:t.Building&&t.Key!="hq")).Where(t=>productionFilter==1?t.Defense:productionFilter==2?t.Foot:productionFilter==3?!t.Foot:productionFilter==4?!t.Defense:true).ToArray();
 }
 void DrawPlannedPaths(Graphics g){
  foreach(var pair in planned){var points=new[]{pair.Key.P}.Concat(pair.Value).Select(ScreenOf).ToArray();if(points.Length<2)continue;
   using(var pen=new Pen(gold,1.5f))g.DrawLines(pen,points);
   for(int i=1;i<points.Length;i++)Txt(g,i.ToString(),points[i].X,points[i].Y,12,gold,true);
  }
 }
}
}
