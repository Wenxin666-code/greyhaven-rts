using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Collections.Generic;
namespace Greyhaven {
partial class CommandWindow {
 bool queueOpen;int queueIndex,queuePage,queuePageSource;
 void StartMission(bool tutorial,bool easy){
  Restart();ResetMatchClock(tutorial);world=new Campaign(tutorial,easy);selection.Clear();selection.Add(world.Units[0]);briefing=false;edgeArmed=true;
 }
 void DrawTiles(Graphics g,RectangleF r,Color color,bool lines){
  Rect(g,r.X,r.Y,r.Width,r.Height,Color.FromArgb(lines?38:48,color));
  using(var pen=new Pen(Color.FromArgb(lines?205:125,color),lines?2:1)){
   g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);
   if(lines){pen.Width=1;for(float x=r.Left+Campaign.Tile;x<r.Right;x+=Campaign.Tile)g.DrawLine(pen,x,r.Top,x,r.Bottom);
    for(float y=r.Top+Campaign.Tile;y<r.Bottom;y+=Campaign.Tile)g.DrawLine(pen,r.Left,y,r.Right,y);}
  }
 }
 void DrawTerrainPlan(Graphics g){
  foreach(var r in Campaign.YardWalls){Rect(g,r.X+3,r.Y+3,r.Width,r.Height,Color.FromArgb(80,ink));Rect(g,r.X,r.Y,r.Width,r.Height,C("#928c78"));Line(g,r.Left,r.Top,r.Right,r.Top,pale,2);for(float x=r.Left+4;x<r.Right-4;x+=15)Line(g,x,r.Bottom,x+5,r.Top,gold,2);}
  foreach(var o in world.Live(0).Where(u=>Campaign.HasGate(u.T))){
   var lot=Campaign.Footprint(o.T,o.P);var gate=Campaign.Gate(lot);
   if(o.T.Key=="refinery"&&showGrid)DrawTiles(g,Campaign.UnloadingLane(lot),gold,true);
   if(o.T.Key!="refinery"&&showGrid)DrawTiles(g,Campaign.ExitApron(o.T,lot),gold,true);
   for(int i=0;i<1;i++){float x=Campaign.Middle(Campaign.ExitApron(o.T,lot)).X,y=gate.Top+8;Line(g,x-4,y,x,y+4,gold);Line(g,x,y+4,x+4,y,gold);}
  }
  if(place!=null||showGrid){using(var pen=new Pen(Color.FromArgb(65,pale))){
   for(int x=0;x<Campaign.Width;x+=Campaign.Tile)g.DrawLine(pen,x,100,x,Campaign.Height);
   for(int y=120;y<Campaign.Height;y+=Campaign.Tile)g.DrawLine(pen,0,y,Campaign.Width,y);}}
  foreach(var o in world.Units.Where(o=>o.Alive&&o.T.Building&&world.Visible(o)))DrawTiles(g,Campaign.Footprint(o.T,o.P),Team(o.Side),place!=null||showGrid);
 }
 int Pending(){return world.Live(0).Sum(o=>o.Queue.Count)+Enumerable.Range(0,2).Count(c=>world.Construction(0,c)!=null);}
 List<Piece> Producers(){return world.Live(0).Where(o=>new[]{"hq","factory","barracks"}.Contains(o.T.Key)).OrderByDescending(o=>o.Queue.Count>0).ThenBy(o=>o.Id).ToList();}
 int queueId;
 void OpenQueue(){
  queueOpen=true;place=null;queuePage=0;queuePageSource=0;var list=Producers();var producer=selection.FirstOrDefault(o=>o.Queue.Count>0)??world.LastOrder;
  int i=list.IndexOf(producer);queueIndex=Math.Max(0,i);queueId=list.Count==0?0:list[queueIndex].Id;
 }
 PointF SuggestSpot(TypeInfo t){
  var h=world.Live(0).FirstOrDefault(o=>o.T.Key=="hq");if(h==null)return PointF.Empty;
  PointF ideal=t.Key=="refinery"?new PointF(h.P.X-84,h.P.Y-66):new PointF(h.P.X+(t.Key=="power"?100:t.Key=="factory"?177:-25),h.P.Y+(t.Key=="barracks"?100:30));
  for(int n=0;n<10;n++)for(int y=-n;y<=n;y++)for(int x=-n;x<=n;x++){
   if(Math.Max(Math.Abs(x),Math.Abs(y))!=n)continue;var p=new PointF(ideal.X+x*24,ideal.Y+y*24);if(world.CanPlace(t,p,null))return Campaign.Middle(Campaign.Footprint(t,p));}
  return PointF.Empty;
 }
 bool BuildingLesson(){var t=Campaign.Info(world.LessonKey);return t!=null&&t.Building&&t.Key!="hq";}
 PointF Marker(){
  if(!world.Tutorial)return PointF.Empty;
  if(BuildingLesson()){
   var t=Campaign.Info(world.LessonKey);var site=world.Units.FirstOrDefault(o=>o.Side==0&&o.Alive&&o.T==t);
   return site!=null?site.P:SuggestSpot(t);
  }
  if(world.LessonKey=="hq")return world.Units[0].P;
  if(world.LessonKey=="eliminate")return world.Live(1).Where(world.Visible).Select(o=>o.P).DefaultIfEmpty(world.Harbor.P).First();
  if(world.LessonKey=="ore"||world.LessonKey=="receipt"){
   var truck=world.Live(0).FirstOrDefault(o=>o.T.Key=="miner");if(truck==null){var plant=world.Live(0).FirstOrDefault(o=>o.T.Key=="refinery");return plant==null?PointF.Empty:plant.P;}
   return world.LessonKey=="ore"&&truck.Patch!=null?truck.Patch.P:truck.P;
  }
  var src=world.Live(0).FirstOrDefault(o=>o.T.Key==Campaign.Info(world.LessonKey).Producer);return src==null?PointF.Empty:src.P;
 }
 void DrawObjectiveMarker(Graphics g){
  if(!world.Tutorial)return;var p=Marker();if(p.IsEmpty)return;
  var q=ScreenOf(p);if(BuildingLesson()){var state=g.Save();GroundSpace(g);DrawTiles(g,Campaign.Footprint(Campaign.Info(world.LessonKey),p),gold,true);g.Restore(state);}
  using(var pen=new Pen(gold,2)){pen.DashStyle=DashStyle.Dash;g.DrawEllipse(pen,q.X-45,q.Y-25,90,50);}Txt(g,"▼ 训练目标",q.X-38,q.Y-61,15,gold,true);
 }
 void FocusLesson(){
  var marker=Marker();if(!marker.IsEmpty)CenterAt(marker);string key=world.LessonKey;queueOpen=false;place=null;attack=false;
  if(BuildingLesson()){army=false;var t=Campaign.Info(key);productionFilter=4;var job=world.Construction(t);if(job!=null&&job.Ready)place=t;return;}
  if(key=="squad"||key=="ifv"){ProductionTab(key=="squad"?2:3);return;}
  var own=world.Live(0);var focus=own.Where(o=>!o.T.Building&&o.T.Hit>0);
  if(key=="hq")focus=own.Where(o=>o.T.Key=="mcv");
  if(key=="ore"||key=="receipt")focus=own.Where(o=>o.T.Key=="miner").Take(1);
  selection.Clear();selection.AddRange(focus);
 }
 string LessonHint(){
  string key=world.LessonKey;var own=world.Live(0);
  if(key=="hq")return world.Landing?"指挥部正在展开，完工后进入下一步。":selection.Any(o=>o.T.Key=="mcv")?"再次点工程车或按 D 展开。":"先点金圈里的工程车，或点「定位本步」选中它。";
  if(BuildingLesson()){
   var t=Campaign.Info(key);var job=world.Construction(t);if(job!=null&&!job.Ready)return t.Name+"："+world.ConstructionState(job,0,speed)+"。右键图标暂停／再右键取消，左键继续。";var site=world.Units.FirstOrDefault(o=>o.Alive&&o.Side==0&&o.T==t);
   if(site!=null)return t.Name+" 正在施工，约 "+(int)Math.Ceiling((1-site.Ready)*Campaign.AssemblySeconds/speed)+" 秒完成。";
   return place==t?"左键点击金色推荐地基；红色预览表示此处不能建造。":"点「建筑」中的「"+t.Name+"」生产；就绪后再点击图标，放到推荐地基。";
  }
  if(key=="squad"||key=="ifv"){
   var t=Campaign.Info(key);var src=own.FirstOrDefault(o=>o.Queue.Contains(t));
   if(src!=null)return src.T.Name+" #"+src.Id+" 已接单。点「队列」查看进度，等部队出列。";
   return "按 E 选步兵、R 选车辆，再点「"+t.Name+"」。军费随进度扣除；缺钱自动等待。";
  }
  if(key=="ore"||key=="receipt"){
   var truck=own.FirstOrDefault(o=>o.T.Key=="miner");
   if(truck==null)return "精炼厂完工会派出首车，请留出厂门。矿车损失后可在车辆工厂补造。";
   if(!truck.Mining)return "选中矿车，左键点已发现的金色矿石，恢复自动采矿。";
   if(key=="ore")return truck.Patch==null?"矿车会向未探索地带找矿。也可派步兵先探路，再选矿车左键点金色矿石。":"矿车已找到金色矿点，正在驶近。点「定位本步」查看矿点和车上的载量。";
   return truck.Returning?"矿车正在返厂卸货。留出厂门，留意精炼厂上方显示的入账金额。":"采集进度 "+(int)truck.Cargo+" / "+Campaign.LoadValue+"。满载后自动返厂，也可选车点己方精炼厂提前卸矿。";
  }
  return "点「定位本步」选中战斗部队，按住 Ctrl+Shift 点击港务中心附近搜索，消灭最后的敌方守军。";
 }
 void DrawCoach(Graphics g){
  string advice=world.Coach();
  if(!world.Tutorial){if(advice!=null)Txt(g,advice,14,12,14,gold,true);else DrawStatusMessage(g);return;}
  hits.Add(new HitBox{Box=new RectangleF(16,MapTop+9,810,65),Run=()=>{}});
  Plate(g,new RectangleF(16,MapTop+9,810,65),panel,C("#b49457"));
  Rect(g,24,MapTop+16,5,24,gold);Rect(g,33,MapTop+16,115,24,gold);
  Txt(g,world.Tutorial?"训练  "+(world.Lesson+1)+" / "+Campaign.Lessons.Length:"战术提醒",40,MapTop+16,16,ink,true);
  if(world.Tutorial)Txt(g,Campaign.Lessons[world.Lesson],165,MapTop+14,19,pale,true);
  Txt(g,advice??LessonHint(),38,MapTop+43,15,pale);
  if(world.Tutorial)Button(g,715,MapTop+20,101,36,"定位本步",FocusLesson);
 }
}
}
