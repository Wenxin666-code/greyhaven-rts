using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Collections.Generic;
namespace Greyhaven {
partial class CommandWindow {
 bool queueOpen;int queueIndex;
 void StartMission(bool tutorial,bool easy){
  Restart();world=new Campaign(tutorial,easy);selection.Clear();selection.Add(world.Units[0]);briefing=false;
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
  foreach(var s in world.Sites.Take(2)){
   var p=s.P;Rect(g,p.X-42,p.Y-25,84,50,Color.FromArgb(110,ink));
   for(int i=0;i<8;i++)Line(g,p.X-39+i*10,p.Y+24,p.X-35+i*10,p.Y+18,gold,2);
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
  var list=Producers();if(list.Count==0){Txt(g,"展开指挥部后可查看排产。",x,284,15,dim);return;}
  queueIndex=list.FindIndex(o=>o.Id==queueId);if(queueIndex<0)queueIndex=0;var src=list[queueIndex];queueId=src.Id;
  Txt(g,src.T.Name+" #"+src.Id+"　"+src.Queue.Count+" / 4",x,270,17,pale,true);
  Button(g,x+157,263,39,33,"‹",()=>queueId=list[(queueIndex+list.Count-1)%list.Count].Id);
  Button(g,x+201,263,39,33,"›",()=>queueId=list[(queueIndex+1)%list.Count].Id);
  var jobs=src.Queue.ToArray();float remaining=-src.Work;bool blocked=jobs.Length>0&&src.Work>=jobs[0].Seconds;float rate=world.Power(0)<0?.45f:1f;for(int i=0;i<4;i++){
   float y=303+i*42;Rect(g,x,y,242,38,i==0?C("#34483c"):C("#253633"));
   Txt(g,"0"+(i+1),x+5,y+9,13,dim);if(i>=jobs.Length){Txt(g,"空位",x+34,y+9,14,dim);continue;}
   remaining+=jobs[i].Seconds;Txt(g,jobs[i].Name,x+31,y+2,15,pale,true);Txt(g,i==0?world.QueueState(src):blocked?"等待门口清空":"预计 "+Math.Ceiling(remaining/rate)+" 秒后出列",x+31,y+20,12,gold);
   if(i==0){Rect(g,x+2,y+36,162,2,ink);Rect(g,x+2,y+36,162*src.Work/jobs[0].Seconds,2,teal);}
   int slot=i;float work=src.Work;Button(g,x+170,y+4,68,29,"取消",()=>{
    if(src.Work>=work&&src.Queue.SequenceEqual(jobs))world.CancelOrder(src,slot);
    else world.Radio="队列刚刚变化，请核对后再取消。";
   });
  }
  Txt(g,"取消退回全额军费。",x,478,14,dim);
 }
 void DrawQueueStrip(Graphics g){
  int count=Pending();var src=world.Live(0).FirstOrDefault(o=>o.Queue.Count>0);
  string s=src==null?"生产总览　空闲":"排产 "+count+"　"+src.Queue.Peek().Name+" · "+world.QueueState(src);
  Button(g,925,759,322,39,s,OpenQueue);
  var ready=world.LastReady;Button(g,1261,759,192,39,ready!=null&&ready.Alive?"选中最新 "+ready.T.Name:"暂无新兵",ready!=null&&ready.Deployed?(Action)(()=>{selection.Clear();selection.Add(ready);CenterAt(ready.P);queueOpen=false;}):null);
 }
 PointF SuggestSpot(TypeInfo t){
  var h=world.Live(0).FirstOrDefault(o=>o.T.Key=="hq");if(h==null)return PointF.Empty;
  PointF ideal=new PointF(h.P.X+(t.Key=="power"?100:t.Key=="factory"?177:-25),h.P.Y+(t.Key=="barracks"?100:30));
  for(int n=0;n<10;n++)for(int y=-n;y<=n;y++)for(int x=-n;x<=n;x++){
   if(Math.Max(Math.Abs(x),Math.Abs(y))!=n)continue;var p=new PointF(ideal.X+x*24,ideal.Y+y*24);if(world.CanPlace(t,p,null))return Campaign.Middle(Campaign.Footprint(t,p));}
  return PointF.Empty;
 }
 bool BuildingLesson(){return world.Lesson==1||world.Lesson==2||world.Lesson==5;}
 PointF Marker(){
  if(!world.Tutorial)return PointF.Empty;
  if(BuildingLesson()){
   var t=Campaign.Info(world.LessonKey);var site=world.Units.FirstOrDefault(o=>o.Side==0&&o.Alive&&o.T==t);
   return site!=null?site.P:SuggestSpot(t);
  }
  if(world.Lesson==0)return world.Units[0].P;
  if(world.Lesson==4)return world.Sites[0].P;if(world.Lesson>=7)return world.Sites[2].P;
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
  var focus=world.Live(0).Where(o=>key=="hq"?o.T.Key=="mcv":world.Lesson==4?o.T.Foot:!o.T.Building&&o.T.Hit>0).ToList();
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
  if(key=="hold")return "保持泊位畅通。运输船靠岸还需 "+Math.Ceiling(Math.Max(0,world.HoldTime-world.Hold))+" 秒。";
  var siteGoal=world.Sites[key=="west"?0:2];
  if(own.Any(o=>o.T.Foot&&Campaign.Dist(o.P,siteGoal.P)<49))return "步兵已进圈。保持驻留；圈内还有红军时，占领会暂停。";
  if(!selection.Any(o=>o.T.Foot))return "点「定位本步」选中步兵，再左键金圈内的空地。只有步兵能占点。";
  return key=="west"?"左键金圈内的空地，让步兵驻留到仓库变蓝。":"带上步兵和战车。按 A 后左键港务中心，清除守军并占领。";
 }
 bool nearbyPointNeedsFoot(){
  return selection.Any(o=>!o.T.Building&&!o.T.Foot&&world.Sites.Any(s=>s.Owner!=0&&Campaign.Dist(s.P,o.P)<60&&!world.Live(0).Any(f=>f.T.Foot&&Campaign.Dist(f.P,s.P)<49)));
 }
 void DrawCoach(Graphics g){
  string advice=world.Coach();
  if(!world.Tutorial&&advice==null&&nearbyPointNeedsFoot())advice="车辆可以掩护占点。把一个步兵班带进据点圈内，已装载的步兵要先下车。";
  if(!world.Tutorial&&advice==null)return;
  Rect(g,18,MapTop+8,820,62,Color.FromArgb(226,ink));Rect(g,18,MapTop+8,4,62,gold);
  Txt(g,world.Tutorial?"教官　"+(world.Lesson+1)+" / 9　"+Campaign.Lessons[world.Lesson]:"战术提醒",32,MapTop+11,15,gold,true);
  Txt(g,advice??LessonHint(),32,MapTop+34,16,pale);
  if(world.Tutorial)Button(g,697,MapTop+20,126,37,"定位本步",FocusLesson);
 }
}
}
