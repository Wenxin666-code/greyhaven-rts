using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Collections.Generic;
namespace Greyhaven {
partial class CommandWindow {
 bool queueOpen;int queueIndex;
 void StartMission(bool tutorial,bool easy){
  Restart();world=new Campaign(tutorial,easy);selection.Clear();selection.Add(world.Units[0]);briefing=false;edgeArmed=true;
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
   if(o.T.Key=="barracks"){DrawTiles(g,Campaign.Muster(lot),teal,true);foreach(var seat in o.Berths)Rect(g,seat.X-3,seat.Y-3,6,6,gold);}
   for(int i=-1;i<=1;i++){float x=o.P.X+i*12,y=gate.Top+8;Line(g,x-4,y,x,y+4,gold);Line(g,x,y+4,x+4,y,gold);}
  }
  if(place!=null||showGrid){using(var pen=new Pen(Color.FromArgb(65,pale))){
   for(int x=0;x<Campaign.Width;x+=Campaign.Tile)g.DrawLine(pen,x,100,x,Campaign.Height);
   for(int y=120;y<Campaign.Height;y+=Campaign.Tile)g.DrawLine(pen,0,y,Campaign.Width,y);}}
  foreach(var o in world.Units.Where(o=>o.Alive&&o.T.Building&&world.Visible(o)))DrawTiles(g,Campaign.Footprint(o.T,o.P),Team(o.Side),place!=null||showGrid);
 }
 int Pending(){return world.Live(0).Sum(o=>o.Queue.Count);}
 List<Piece> Producers(){return world.Live(0).Where(o=>new[]{"hq","factory","barracks"}.Contains(o.T.Key)).OrderByDescending(o=>o.Queue.Count>0).ThenBy(o=>o.Id).ToList();}
 int queueId;
 void OpenQueue(){
  queueOpen=true;place=null;var list=Producers();var producer=selection.FirstOrDefault(o=>o.Queue.Count>0)??world.LastOrder;
  int i=list.IndexOf(producer);queueIndex=Math.Max(0,i);queueId=list.Count==0?0:list[queueIndex].Id;
 }
 void DrawQueue(Graphics g,float x){
  var list=Producers();if(list.Count==0){Txt(g,"展开指挥部后可查看排产。",x+6,357,16,dim);return;}
  queueIndex=list.FindIndex(o=>o.Id==queueId);if(queueIndex<0)queueIndex=0;var src=list[queueIndex];queueId=src.Id;
  Txt(g,src.T.Name+" #"+src.Id+"    "+src.Queue.Count+" / 4",x+6,330,18,pale,true);
  Button(g,x+267,324,39,32,"‹",()=>queueId=list[(queueIndex+list.Count-1)%list.Count].Id);
  Button(g,x+315,324,39,32,"›",()=>queueId=list[(queueIndex+1)%list.Count].Id);
  var jobs=src.Queue.ToArray();float seconds=-src.Work;
  for(int i=0;i<4;i++){
   float y=365+i*46;Plate(g,new RectangleF(x,y,362,41),panel,C("#526267"));
   Txt(g,"0"+(i+1),x+7,y+9,14,dim);if(i>=jobs.Length){Txt(g,"空位",x+45,y+9,15,dim);continue;}
   seconds+=jobs[i].Seconds;Portrait(g,jobs[i],new RectangleF(x+38,y+2,54,36));Txt(g,jobs[i].Name,x+102,y+2,15,pale,true);
   Txt(g,i==0?world.QueueState(src):"预计 "+Math.Ceiling(seconds/(world.Power(0)<0?.45:1))+" 秒",x+102,y+21,12,gold);
   int slot=i;Button(g,x+278,y+5,76,31,"取消",()=>{if(src.Queue.SequenceEqual(jobs))world.CancelOrder(src,slot);});
  }
 }
 void DrawQueueStrip(Graphics g){
  float x=MW+15;var src=world.Live(0).Where(o=>o.Queue.Count>0).OrderByDescending(o=>selection.Contains(o)).ThenByDescending(o=>o==world.LastOrder).FirstOrDefault();
  Line(g,x,569,W-16,569,C("#56686e"));Txt(g,"//  生产队列",x,577,18,pale,true);
  Button(g,x+253,574,109,27,"总览  "+Pending(),OpenQueue);
  Plate(g,new RectangleF(x,608,362,54),panel,C("#637277"));
  if(src==null)Txt(g,"生产线空闲",x+14,624,16,dim);
  else{
   var job=src.Queue.Peek();Portrait(g,job,new RectangleF(x+5,611,69,47));Txt(g,job.Name,x+85,611,16,pale,true);
   Rect(g,x+85,642,148,6,ink);Rect(g,x+85,642,148*Math.Min(1,src.Work/job.Seconds),6,gold);
   string state=world.QueueState(src);Txt(g,state.Length>8?state.Substring(0,8):state,x+234,626,13,gold);
   var snapshot=src.Queue.ToArray();
   Button(g,x+325,618,30,30,"×",()=>{if(src.Queue.SequenceEqual(snapshot))world.CancelOrder(src,0);});
  }
  var next=src==null?null:src.Queue.Skip(1).FirstOrDefault();
  Txt(g,next==null?"02   等待排产":"02   "+next.Name+"  ·  等待",x+10,675,14,dim,true);
  var ready=world.LastReady;
  Button(g,x+200,667,162,34,"定位最新单位",ready!=null&&ready.Deployed?(Action)(()=>{selection.Clear();selection.Add(ready);CenterAt(ready.P);}):null);
  Txt(g,world.Radio.Length>25?world.Radio.Substring(0,24)+"…":world.Radio,x+4,708,12,teal);
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
  if(world.LessonKey=="west")return world.Sites[0].P;
  if(world.LessonKey=="port"||world.LessonKey=="hold")return world.Sites[2].P;
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
  if(BuildingLesson()){army=false;var t=Campaign.Info(key);if(world.Require(t,0)==null&&!world.Units.Any(o=>o.Side==0&&o.Alive&&o.T==t))place=t;return;}
  if(key=="squad"||key=="ifv"){army=true;return;}
  var own=world.Live(0);var focus=own.Where(o=>!o.T.Building&&o.T.Hit>0);
  if(key=="hq")focus=own.Where(o=>o.T.Key=="mcv");
  if(key=="ore"||key=="receipt")focus=own.Where(o=>o.T.Key=="miner").Take(1);
  if(key=="west")focus=own.Where(o=>o.T.Foot);
  selection.Clear();selection.AddRange(focus);
 }
 string LessonHint(){
  string key=world.LessonKey;var own=world.Live(0);
  if(key=="hq")return world.Landing?"指挥部正在展开，完工后进入下一步。":selection.Any(o=>o.T.Key=="mcv")?"点右侧「展开指挥部」或按 D。":"先点金圈里的工程车，或点「定位本步」选中它。";
  if(BuildingLesson()){
   var t=Campaign.Info(key);var site=world.Units.FirstOrDefault(o=>o.Alive&&o.Side==0&&o.T==t);
   if(site!=null)return t.Name+" 正在施工，约 "+(int)Math.Ceiling((1-site.Ready)*t.Seconds)+" 秒完成。";
   return place==t?"左键点击金色推荐地基；红色预览表示此处不能建造。":"打开「建设」，点「"+t.Name+"」，再点地图推荐地基。";
  }
  if(key=="squad"||key=="ifv"){
   var t=Campaign.Info(key);var src=own.FirstOrDefault(o=>o.Queue.Contains(t));
   if(src!=null)return src.T.Name+" #"+src.Id+" 已接单。点「队列」查看进度，等部队出列。";
   return "点右侧「生产」，再点「"+t.Name+"」。军费立即扣除。";
  }
  if(key=="ore"||key=="receipt"){
   var truck=own.FirstOrDefault(o=>o.T.Key=="miner");
   if(truck==null)return "精炼厂完工会派出首车，请留出厂门。矿车损失后可在车辆工厂补造。";
   if(!truck.Mining)return "选中矿车，点右侧「恢复自动采矿」，或左键点已发现的金色矿石。";
   if(key=="ore")return truck.Patch==null?"矿车会向未探索地带找矿。也可派步兵先探路，再选矿车左键点金色矿石。":"矿车已找到金色矿点，正在驶近。点「定位本步」查看矿点和车上的载量。";
   return truck.Returning?"矿车正在返厂卸货。留出厂门，留意精炼厂上方显示的入账金额。":"采集进度 "+(int)truck.Cargo+" / "+Campaign.LoadValue+"。满载后自动返厂，也可选车点己方精炼厂提前卸矿。";
  }
  if(key=="hold")return "保持泊位畅通。运输船靠岸还需 "+Math.Ceiling(Math.Max(0,world.HoldTime-world.Hold))+" 秒。";
  var siteGoal=world.Sites[key=="west"?0:2];
  if(own.Any(o=>o.T.Foot&&Campaign.Dist(o.P,siteGoal.P)<Greyhaven.Site.CaptureRadius))return "我方步兵已进入占领区。请保持驻留，并清除圈内的敌方步兵以继续占领。";
  if(!selection.Any(o=>o.T.Foot))return "点「定位本步」选中步兵，再左键金圈内的空地。只有步兵能占点。";
  return key=="west"?"左键金圈内的空地，让步兵驻留到仓库变蓝。":"带上步兵和战车。按 A 后左键港务中心，清除守军并占领。";
 }
 bool nearbyPointNeedsFoot(){
  return selection.Any(o=>!o.T.Building&&!o.T.Foot&&world.Sites.Any(s=>s.Owner!=0&&Campaign.Dist(s.P,o.P)<60&&!world.Live(0).Any(f=>f.T.Foot&&Campaign.Dist(f.P,s.P)<Greyhaven.Site.CaptureRadius)));
 }
 void DrawCoach(Graphics g){
  string advice=world.Coach();
  if(!world.Tutorial&&advice==null&&nearbyPointNeedsFoot())advice="车辆可以掩护占点。把一个步兵班带进据点圈内，已装载的步兵要先下车。";
  if(!world.Tutorial&&advice==null)return;
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
