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
 void ResetClassicInput(){showRanges=false;showGrid=false;commandRules=false;productionFilter=0;pressedUnit=null;followUnit=null;resolvingPress=false;zHeld=false;planned.Clear();plannedLoops.Clear();}
 RectangleF PickBox(Piece u){
  var feet=ScreenOf(u.P);float size=u.T.Size*zoom;
  if(u.T.Building)return BuildingRect(Campaign.Footprint(u.T,u.P),feet,zoom);
  if(u.T.Foot){float w=Math.Max(12/Math.Max(.5f,scale),size*.24f),h=Math.Max(20/Math.Max(.5f,scale),size*.47f);return new RectangleF(feet.X-w/2,feet.Y-h,w,h+4);}
  return new RectangleF(feet.X-size*.35f,feet.Y-size*.36f,size*.7f,size*.52f);
 }
 Piece PickUnit(PointF point){
  var all=world.Units.Where(o=>o.Alive&&o.Carrier==null&&world.Visible(o)).ToArray();
  // Pick small mobiles before a building's broad footprint. Exact painted
  // pixels outrank forgiving margins, then use distance instead of draw order.
  var mobile=all.Where(o=>!o.T.Building).ToArray();
  var exact=mobile.Where(o=>MobileHit(o,ScreenOf(o.P),o.T.Size*zoom,point)).OrderBy(o=>Campaign.Dist(point,ScreenOf(o.P))).FirstOrDefault();
  if(exact!=null)return exact;
  var tolerant=mobile.Where(o=>PickBox(o).Contains(point)).OrderBy(o=>{
   var b=PickBox(o);return Campaign.Dist(point,new PointF(b.X+b.Width/2,b.Y+b.Height/2));}).FirstOrDefault();
  if(tolerant!=null)return tolerant;
  var w=WorldOf(point);
  return all.Where(o=>o.T.Building).OrderByDescending(o=>o.P.X+o.P.Y).FirstOrDefault(o=>Campaign.Footprint(o.T,o.P).Contains(w)||InkHit(o.T.Key,o.Side,PickBox(o),point));
 }
 void DrawPreselection(Graphics g){
  if(drag||panning||place!=null||briefing||paused||mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>=H||hits.Any(h=>h.Box.Contains(mouse)))return;
  var u=HitScreen(mouse);if(u==null)return;var r=PickBox(u);r.Inflate(3,3);var c=u.Side==0?teal:red;
  using(var pen=new Pen(c,1.5f))foreach(var p in new[]{new PointF(r.Left,r.Top),new PointF(r.Right,r.Top),new PointF(r.Right,r.Bottom),new PointF(r.Left,r.Bottom)}){
   float dx=p.X==r.Left?6:-6,dy=p.Y==r.Top?6:-6;g.DrawLine(pen,p,new PointF(p.X+dx,p.Y));g.DrawLine(pen,p,new PointF(p.X,p.Y+dy));
  }
  Txt(g,(u.Side==0?"选择 ":"敌方 ")+u.T.Name,mouse.X+12,mouse.Y-22,12,c,true);
 }
 void GuardSelection(){foreach(var u in selection)world.Guard(u);attack=false;world.Radio="警戒此处：迎敌后返回驻守位置。";}
 bool ClassicKey(KeyEventArgs e){
  if(e.KeyCode==Keys.Q||e.KeyCode==Keys.W||e.KeyCode==Keys.E||e.KeyCode==Keys.R){
   army=e.KeyCode==Keys.E||e.KeyCode==Keys.R;productionFilter=e.KeyCode==Keys.W?1:e.KeyCode==Keys.E?2:e.KeyCode==Keys.R?3:4;queueOpen=false;place=null;return true;
  }
  if(e.KeyCode==Keys.G){GuardSelection();return true;}
  if(e.KeyCode==Keys.F){followUnit=selection.FirstOrDefault(u=>u.Alive);if(followUnit!=null)CenterAt(followUnit.P);return true;}
  if(e.KeyCode==Keys.X){world.Scatter(selection.Where(u=>u.Deployed&&!u.T.Building).ToArray());return true;}
  if(e.KeyCode==Keys.Z){if(!zHeld){zHeld=true;planned.Clear();plannedLoops.Clear();}world.Radio="按住 Z 依次点击路径点，松开执行；点回首个路径点形成巡逻。";return true;}
  if(e.KeyCode==Keys.P){selection=world.Live(0).Where(u=>!u.T.Building).ToList();return true;}
  if(e.KeyCode==Keys.M||e.KeyCode==Keys.N){
   var units=world.Live(0).Where(u=>!u.T.Building).OrderBy(u=>u.Id).ToList();if(units.Count==0)return true;
   int current=units.IndexOf(selection.FirstOrDefault()),next=(current+(e.KeyCode==Keys.M?1:-1)+units.Count)%units.Count;
   selection.Clear();selection.Add(units[next]);return true;
  }
  return false;
 }
 void AddPlannedPoint(PointF p){
  foreach(var u in selection.Where(u=>u.Deployed&&!u.T.Building)){
   List<PointF> points;if(!planned.TryGetValue(u,out points)){world.Halt(u);planned[u]=points=new List<PointF>();}
   if(points.Count>1&&Campaign.Dist(points[0],p)<24){plannedLoops.Add(u);continue;}
   points.Add(p);
  }
  Mark(p,null,false);
 }
 void CommitPlans(){
  zHeld=false;int failed=0;foreach(var pair in planned)if(!world.SetWaypoints(pair.Key,pair.Value.ToArray(),plannedLoops.Contains(pair.Key)))failed++;
  world.Radio=failed>0?failed+" 个单位的首个路径点不可达。":"部队按规划路线出发。";planned.Clear();plannedLoops.Clear();
 }
 TypeInfo[] ProductionCards(){return Campaign.Types.Where(t=>army?!t.Building:t.Building&&t.Key!="hq").Where(t=>productionFilter==1?t.Key=="post":productionFilter==2?t.Foot:productionFilter==3?!t.Foot:productionFilter==4?t.Key!="post":true).ToArray();}
 void DrawPlannedPaths(Graphics g){
  foreach(var pair in planned){var points=new[]{pair.Key.P}.Concat(pair.Value).Select(ScreenOf).ToArray();if(points.Length<2)continue;
   using(var pen=new Pen(gold,1.5f))g.DrawLines(pen,points);
   for(int i=1;i<points.Length;i++)Txt(g,i.ToString(),points[i].X,points[i].Y,12,gold,true);
  }
 }
}
}
