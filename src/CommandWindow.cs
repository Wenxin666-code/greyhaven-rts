using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Linq;
using System.Windows.Forms;

namespace Greyhaven {
partial class CommandWindow:Form {
 const int MW=1080,MH=766,MapTop=53,W=1472,H=819;
 Campaign world; readonly Stopwatch watch=Stopwatch.StartNew(); readonly Timer pulse=new Timer(); double last; float scale,ox,oy;
 bool showGrid;bool briefing=true,paused,help,attack,army,drag,dragged; int speed=1; PointF mouse,start;TypeInfo place;
 List<Piece> selection=new List<Piece>();List<HitBox> hits=new List<HitBox>();
 Bitmap ground;Dictionary<string,Font> fonts=new Dictionary<string,Font>();
 Color ink=C("#101b20"),panel=C("#202c32"),pale=C("#f3eee0"),dim=C("#abb8b5"),gold=C("#ecb54e"),teal=C("#82d0b4"),red=C("#ee8b72");
 class HitBox {public RectangleF Box;public Action Run;}
 static readonly Dictionary<string,Color> palette=new Dictionary<string,Color>();
 static Color C(string s){Color c;if(!palette.TryGetValue(s,out c))palette[s]=c=ColorTranslator.FromHtml(s);return c;}
 [System.Runtime.InteropServices.DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint ms);
 [System.Runtime.InteropServices.DllImport("winmm.dll")]static extern uint timeEndPeriod(uint ms);
 bool timerPrecision;
 protected override void OnShown(EventArgs e){base.OnShown(e);timerPrecision=timeBeginPeriod(1)==0;last=watch.Elapsed.TotalSeconds;}
 public CommandWindow(){
  Text="断线前沿  /  灰港登陆";StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(900,575);var area=Screen.FromPoint(Cursor.Position).WorkingArea;float fit=Math.Min(1f,Math.Min((area.Width-32f)/W,(area.Height-64f)/H));ClientSize=new Size((int)(W*fit),(int)(H*fit));BackColor=Color.FromArgb(17,29,32);
  KeyPreview=true;SetStyle(ControlStyles.ResizeRedraw|ControlStyles.Opaque|ControlStyles.AllPaintingInWmPaint,true);
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  world=new Campaign();ground=MakeGround();projectedGround=ProjectTerrain();radarGround=new Bitmap(488,214,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var rg=Graphics.FromImage(radarGround))rg.DrawImage(ground,0,0,488,214);
  foreach(var key in new[]{"hq","power","factory","barracks","radar","post","squad","at","engineer","tank","ifv","mcv","arty","harbormaster","refinery","miner","miner-full","refining"})painted[key]=Raw((key=="squad"||key=="at"||key=="engineer"?"hd-":"iso-")+key);
  foreach(var key in new[]{"portraits","plate","harbor"})painted[key]=Raw(key);
  painted["ore"]=OreSprites();
  foreach(var t in Campaign.Types.Where(t=>t.Foot))painted["fallen-"+t.Key]=FallenSheet(painted[t.Key]);
  Restart();pulse.Interval=15;pulse.Tick+=(s,e)=>{double now=watch.Elapsed.TotalSeconds;float dt=(float)Math.Min(.12,now-last);last=now;
   if(!briefing&&!paused&&!world.Won&&!world.Lost){if(ContainsFocus&&Control.MouseButtons==MouseButtons.None)PollEdgeScroll(dt);else edgeDwell=0;for(int i=0;i<speed;i++)world.Tick(dt);selection.RemoveAll(o=>!o.Alive||o.Carrier!=null);}
   if(!briefing||hudDirty)Invalidate();};pulse.Start(); FormClosed+=(s,e)=>{pulse.Stop();pulse.Dispose();if(timerPrecision)timeEndPeriod(1);ground.Dispose();projectedGround.Dispose();radarGround.Dispose();if(terrainView!=null)terrainView.Dispose();ReleaseFrame();ClearLabelCache();ClearStampCache();foreach(var b in painted.Values)b.Dispose();foreach(var f in fonts.Values)f.Dispose();};
  Deactivate+=(s,e)=>{ResetEdgeScroll();frontEnterHeld=false;tHeld=false;selectType=null;if(!briefing&&!world.Won&&!world.Lost)paused=true;drag=false;panning=false;Capture=false;};
 }
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int length);
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);
  int dark=1,caption=ColorTranslator.ToWin32(ink),text=ColorTranslator.ToWin32(pale);
  DwmSetWindowAttribute(Handle,20,ref dark,4);DwmSetWindowAttribute(Handle,35,ref caption,4);DwmSetWindowAttribute(Handle,36,ref text,4);
 }
 void Restart(){ResetEdgeScroll();rulesOpen=false;missionPicker=false;hudDirty=true;drag=false;Capture=false;Array.Clear(bookmarks,0,bookmarks.Length);selectType=null;tHeld=false;markedPoint=null;markedEnemy=null;camera=new PointF(255,465);zoom=DefaultZoom;sweep=false;panning=false;syncMarch=true;queueOpen=false;queueIndex=0;world=new Campaign();selection.Clear();selection.Add(world.Units[0]);briefing=true;paused=false;help=false;army=false;place=null;attack=false;speed=1;}
 Font F(float size,bool bold){string key=size.ToString()+bold;Font f;if(!fonts.TryGetValue(key,out f))fonts[key]=f=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);return f;}
 void Txt(Graphics g,string s,float x,float y,float size,Color c,bool bold=false){var font=F(size,bold);
  if(!drawingClient){using(var b=new SolidBrush(c))g.DrawString(s,font,b,x,y,StringFormat.GenericTypographic);return;}
  DrawLabel(g,s,font,c,x,y);}
 void Rect(Graphics g,float x,float y,float w,float h,Color c){using(var b=new SolidBrush(c))g.FillRectangle(b,x,y,w,h);}
 void Line(Graphics g,float x,float y,float xx,float yy,Color c,float width=1){using(var p=new Pen(c,width))g.DrawLine(p,x,y,xx,yy);}
 void Plate(Graphics g,RectangleF r,Color fill,Color edge){float x=r.X,y=r.Y,w=r.Width,h=r.Height;
  PointF[] corners={new PointF(x+8,y),new PointF(x+w,y),new PointF(x+w,y+h-8),new PointF(x+w-8,y+h),new PointF(x,y+h),new PointF(x,y+8)};
  using(var b=new LinearGradientBrush(r,fill,ControlPaint.Dark(fill,.13f),90f))g.FillPolygon(b,corners);
  using(var p=new Pen(edge))g.DrawPolygon(p,corners);
  Line(g,x+9,y+2,x+w-2,y+2,Color.FromArgb(48,pale));Line(g,x+2,y+h-2,x+w-9,y+h-2,Color.FromArgb(115,Color.Black));
 }
 void Button(Graphics g,float x,float y,float w,float h,string text,Action action,bool lit=false){
  var r=new RectangleF(x,y,w,h);Color edge=lit?gold:action==null?C("#465149"):C("#627971");
  Plate(g,r,action==null?C("#1b2428"):lit?(r.Contains(mouse)?C("#ffd077"):gold):r.Contains(mouse)?C("#425259"):C("#29383e"),edge);
  Rect(g,x+3,y+10,2,h-20,edge);string symbol=Symbol(text);float inset=11;
  if(symbol!=null){DrawSymbol(g,symbol,x+17,y+h/2,lit?ink:teal);inset=33;}
  float fontSize=h>=56?23:15;
  Txt(g,text,x+inset,y+(h-fontSize-3)/2,fontSize,action==null?dim:lit?ink:pale,true);
  if(action!=null)hits.Add(new HitBox{Box=r,Run=action});
 }
 static string Symbol(string text){
  if(text.StartsWith("展开")||text.StartsWith("装载"))return "deploy";
  if(text.StartsWith("攻击"))return "aim";if(text.StartsWith("升级"))return "upgrade";if(text.StartsWith("侦察"))return "radar";
  if(text.StartsWith("战地"))return "book";if(text.StartsWith("暂停"))return "pause";return null;
 }
 void DrawSymbol(Graphics g,string id,float x,float y,Color c){
  using(var p=new Pen(c,1.6f)){
   if(id=="aim"){g.DrawEllipse(p,x-6,y-6,12,12);g.DrawLine(p,x-9,y,x-4,y);g.DrawLine(p,x+4,y,x+9,y);g.DrawLine(p,x,y-9,x,y-4);g.DrawLine(p,x,y+4,x,y+9);}
   else if(id=="radar"){g.DrawArc(p,x-7,y-7,14,14,15,280);g.DrawArc(p,x-4,y-4,8,8,15,260);g.DrawLine(p,x,y,x+7,y-7);}
   else if(id=="upgrade"){for(int n=-3;n<=3;n+=6){g.DrawLine(p,x-6,y+n+2,x,y+n-3);g.DrawLine(p,x,y+n-3,x+6,y+n+2);}}
   else if(id=="book"){g.DrawRectangle(p,x-7,y-6,14,12);g.DrawLine(p,x,y-6,x,y+6);}
   else if(id=="pause"){Rect(g,x-5,y-6,3,12,c);Rect(g,x+2,y-6,3,12,c);}
   else{g.DrawRectangle(p,x-7,y-4,14,9);g.DrawLine(p,x,y-8,x,y-1);g.DrawLine(p,x-4,y-2,x,y+1);g.DrawLine(p,x,y+1,x+4,y-2);}
  }
 }

 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
 struct DibHeader {
  public uint Size;public int Width,Height;public ushort Planes,BitCount;
  public uint Compression,ImageSize;public int XPels,YPels;public uint Colors,Important;
 }
 [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern IntPtr CreateDIBSection(IntPtr dc,ref DibHeader header,uint usage,out IntPtr pixels,IntPtr section,uint offset);
 [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 [System.Runtime.InteropServices.DllImport("gdi32.dll")]static extern bool BitBlt(IntPtr dest,int x,int y,int w,int h,IntPtr src,int sx,int sy,uint op);
 Bitmap frameBuffer;Graphics frameGraphics;IntPtr frameDC,frameDib,oldDib;
 void ReleaseFrame(){
  if(frameGraphics!=null){frameGraphics.Dispose();frameGraphics=null;}
  if(frameBuffer!=null){frameBuffer.Dispose();frameBuffer=null;}
  if(oldDib!=IntPtr.Zero){SelectObject(frameDC,oldDib);oldDib=IntPtr.Zero;}
  if(frameDib!=IntPtr.Zero){DeleteObject(frameDib);frameDib=IntPtr.Zero;}
  if(frameDC!=IntPtr.Zero){DeleteDC(frameDC);frameDC=IntPtr.Zero;}
 }
 void AllocateFrame(){
  ReleaseFrame();int w=ClientSize.Width,h=ClientSize.Height;IntPtr pixels;
  var header=new DibHeader{Size=40,Width=w,Height=-h,Planes=1,BitCount=32};
  frameDC=CreateCompatibleDC(IntPtr.Zero);frameDib=CreateDIBSection(frameDC,ref header,0,out pixels,IntPtr.Zero,0);
  if(frameDC!=IntPtr.Zero&&frameDib!=IntPtr.Zero)oldDib=SelectObject(frameDC,frameDib);
  if(oldDib!=IntPtr.Zero&&oldDib!=new IntPtr(-1))frameBuffer=new Bitmap(w,h,w*4,System.Drawing.Imaging.PixelFormat.Format32bppPArgb,pixels);
  else{ReleaseFrame();frameBuffer=new Bitmap(w,h,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);}
  frameGraphics=Graphics.FromImage(frameBuffer);
 }
 bool hudDirty=true,previousOverlay;int hudHitCount;float hudTime;Campaign hudWorld;
 protected override void OnPaint(PaintEventArgs e){
  if(ClientSize.Width<=0||ClientSize.Height<=0)return;
  if(frameBuffer==null||frameBuffer.Size!=ClientSize){AllocateFrame();hudDirty=true;}
  var g=frameGraphics;g.ResetTransform();
  scale=Math.Min(ClientSize.Width/(float)W,ClientSize.Height/(float)H);ox=(ClientSize.Width-W*scale)/2;oy=(ClientSize.Height-H*scale)/2;
  bool overlay=briefing||paused||world.Won||world.Lost;
  bool refresh=hudDirty||overlay||previousOverlay||hudWorld!=world||world.Time-hudTime>=.1f;
  if(refresh)g.Clear(ink);
  g.TranslateTransform(ox,oy);g.ScaleTransform(scale,scale);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.Bilinear;
  drawingClient=true;
  if(briefing){hits.Clear();DrawFront(g);hudDirty=false;}
  else{
  DrawBattle(g);
  if(refresh){
   hits.Clear();DrawHud(g);hudHitCount=hits.Count;hudTime=world.Time;hudWorld=world;hudDirty=false;
  }else if(hits.Count>hudHitCount)hits.RemoveRange(hudHitCount,hits.Count-hudHitCount);
  DrawInspector(g);DrawCoach(g);if(overlay)Overlay(g);
  }
  drawingClient=false;previousOverlay=overlay;
  g.Flush(FlushIntention.Sync);
  if(frameDC==IntPtr.Zero){e.Graphics.DrawImageUnscaled(frameBuffer,0,0);return;}
  var target=e.Graphics.GetHdc();bool copied;
  try{copied=BitBlt(target,0,0,frameBuffer.Width,frameBuffer.Height,frameDC,0,0,0x00CC0020);}
  finally{e.Graphics.ReleaseHdc(target);}
  if(!copied)e.Graphics.DrawImageUnscaled(frameBuffer,0,0);
 }
 Color Team(int s){return s==0?C("#6dcced"):red;}
 void DrawHud(Graphics g){
  Metal(g,0,0,W,MapTop);Emblem(g,31,26,14);Rect(g,64,1,7,49,gold);
  Txt(g,"断线前沿",87,8,28,pale,true);Txt(g," /  灰港登陆",216,14,20,dim,true);
  Txt(g,world.Tutorial?"野外训练":"歼灭敌方   ·   剩余 "+world.Remaining(1),450,18,14,dim,true);
  Txt(g,"军费",700,17,15,dim,true);Txt(g,world.Funds[0].ToString(),753,9,24,gold,true);
  Txt(g,"电力",887,17,15,dim,true);Txt(g,world.Power(0).ToString("+#;-#;0"),939,9,24,world.Power(0)<0?red:teal,true);
  Txt(g,TimeSpan.FromSeconds(world.Time).ToString(@"mm\:ss"),1070,10,23,pale,true);
  Button(g,1205,8,105,36,"速度 ×"+speed,()=>speed=speed==1?2:1);Button(g,1323,8,130,36,"暂停",()=>paused=true);
  float x=MW+15;Metal(g,MW,MapTop,W-MW,H-MapTop);Line(g,MW,MapTop,MW,H,gold,2);
  Txt(g,"//  战区雷达",x,62,18,pale,true);Txt(g,"GREYHAVEN  /  TAC",x+200,66,12,dim,true);
  Rect(g,x,89,362,140,ink);DrawRadar(g,x+4);
  Button(g,x,235,178,32,"基地  H",GoHome);Button(g,x+185,235,177,32,"最近事件",world.LastEvent.HasValue?(Action)GoEvent:null);
  Button(g,x,278,115,37,"建设",()=>{army=false;queueOpen=false;place=null;},!army&&!queueOpen);
  Button(g,x+123,278,115,37,"生产",()=>{army=true;queueOpen=false;place=null;},army&&!queueOpen);
  Button(g,x+247,278,115,37,"队列 "+Pending(),OpenQueue,queueOpen);
  if(queueOpen)DrawQueue(g,x);
  else{var cards=Campaign.Types.Where(t=>army?!t.Building:t.Building&&t.Key!="hq").ToArray();
   for(int i=0;i<cards.Length;i++)Card(g,cards[i],x+i%2*184,324+i/2*59);}
  DrawQueueStrip(g);
  Button(g,x,730,178,31,world.Upgraded[0]?"装甲已升级":!world.Live(0).Any(o=>o.T.Key=="factory")?"需车辆工厂":"升级  $260",(world.Upgraded[0]||world.Funds[0]<260||!world.Live(0).Any(o=>o.T.Key=="factory"))?(Action)null:()=>world.Upgrade());
  bool scanReady=world.Power(0)>=0&&world.Live(0).Any(o=>o.T.Key=="radar")&&world.Funds[0]>=80&&world.Scan<=0;
  Button(g,x+185,730,177,31,scanReady?"扫描 $80":!world.Live(0).Any(o=>o.T.Key=="radar")?"需雷达站":world.Power(0)<0?"缺电":world.Scan>0?"扫描冷却":"需 $80",scanReady?(Action)(()=>{sweep=true;place=null;attack=false;world.Radio="左键选择揭图中心，右键取消。";}):null);
  Button(g,x,771,178,31,"手册 F5",()=>{paused=true;help=true;});Button(g,x+185,771,177,31,syncMarch?"队形开 F":"队形关 F",()=>syncMarch=!syncMarch,syncMarch);
 }
 void Card(Graphics g,TypeInfo t,float x,float y){
  string reason=world.Require(t,0);var r=new RectangleF(x,y,178,55);bool ready=reason==null;
  bool hot=place==t||(world.Tutorial&&world.LessonKey==t.Key)||r.Contains(mouse);
  Plate(g,r,ready?C("#2c383c"):C("#1c282d"),hot?gold:C("#536367"));
  Portrait(g,t,new RectangleF(x+4,y+4,67,47));
  if(!ready)Rect(g,x+4,y+4,67,47,Color.FromArgb(148,ink));
  Txt(g,t.Name,x+77,y+5,15,ready?pale:dim,true);
  Txt(g,ready?"$ "+t.Price:"锁 · "+reason.Replace("需要","需"),x+77,y+30,ready?15:12,ready?gold:dim,true);
  if(!ready)return;
  var active=world.Live(0).FirstOrDefault(o=>o.Queue.Count>0&&o.Queue.Peek()==t);
  if(active!=null){Rect(g,x+74,y+51,98,2,ink);Rect(g,x+74,y+51,98*active.Work/t.Seconds,3,teal);}
  hits.Add(new HitBox{Box=r,Run=()=>{string why=world.Require(t,0);if(why!=null){world.Radio=why;return;}if(t.Building){place=t;attack=false;world.Radio="部署"+t.Name+"：左键确认绿色位置，右键取消。";}
    else world.Train(t,0,selection.FirstOrDefault());}});
 }
 void Overlay(Graphics g){
  hits.Clear();Rect(g,0,0,W,H,Color.FromArgb(177,ink));var r=new RectangleF(285,172,900,468);Plate(g,r,C("#1f302f"),C("#a2b398"));Rect(g,286,205,6,112,gold);
  Txt(g,"LANDING FORCE      //      OPERATIONS COMMAND      01—GH",324,203,14,gold,true);
  string title=world.Won?(world.Tutorial?"训练完成":"行动成功"):world.Lost?"行动终止":help?"战地手册":"指挥已暂停";
  Txt(g,title,323,245,49,pale,true);Line(g,329,325,1140,325,C("#5d7265"));
  string[] lines=world.Won?new[]{"敌方单位与建筑已全部清除。","作战用时 "+TimeSpan.FromSeconds(world.Time).ToString(@"mm\:ss")+"　｜　存活部队 "+world.Live(0).Count(o=>!o.T.Building)}:
   world.Lost?new[]{"我方单位与建筑已全部损失，本次行动结束。","先保护指挥部，再恢复采矿和生产，再消灭敌方部队和基地。"}:
   new[]{"01    选择工程车按 D 展开。先建电站和精炼厂，再建兵营和车辆工厂，升级后可生产坦克。","02    左键选中我方部队，点地面移动，点敌方单位集火。A 后点地边打边走；Ctrl 点地压制，S 停火。","03    矿车卸货换取军费，车辆工厂可补造矿车。摧毁敌方全部单位与建筑即可获胜。","04    H 回基地，空格看事件，Ctrl+F1～F4 存镜头，F1～F4 返回。T 选同类，连按两次选全图。"};
  for(int i=0;i<lines.Length;i++)Txt(g,lines[i],328,347+i*41,17,i==0?gold:pale);
  Button(g,328,553,241,49,world.Won||world.Lost?"返回选关":"返回战场",()=>{if(world.Won||world.Lost)OpenMissions();else{paused=false;help=false;}},true);
  if(world.Tutorial&&world.Won)Button(g,594,553,205,49,"进入宽松战役",()=>StartMission(false,true));
  if(!briefing&&!world.Won&&!world.Lost){Button(g,594,553,182,49,"重开本关",()=>StartMission(world.Tutorial,world.Assisted));Button(g,796,553,170,49,"返回选关",OpenMissions);}
 }
 RectangleF DragRect(){return RectangleF.FromLTRB(Math.Min(start.X,mouse.X),Math.Min(start.Y,mouse.Y),Math.Max(start.X,mouse.X),Math.Max(start.Y,mouse.Y));}
 PointF ToScreenPoint(MouseEventArgs e){return new PointF((e.X-ox)/Math.Max(.01f,scale),(e.Y-oy)/Math.Max(.01f,scale));}
 void ContextOrder(Keys mods){
  var p=WorldOf(mouse);var hit=HitScreen(mouse);
  bool shortcut=(mods&(Keys.Control|Keys.Shift))==(Keys.Control|Keys.Shift);
  if((mods&Keys.Control)!=0&&!shortcut){
   int count=selection.Count(o=>world.Suppress(o,p));attack=false;
   if(count>0){Mark(p,null,true);world.Radio="压制指定地面；按 S 停火。";}return;
  }
  if(!attack&&mods==Keys.None&&selection.Count>0&&selection.All(u=>u.Deployed&&u.T.Key=="miner")){
   var plant=hit!=null&&hit.Deployed&&hit.Side==0&&hit.T.Key=="refinery"?hit:null;
   var patch=hit==null&&world.Explored(0,p)?world.OreAt(p):null;
   if(plant!=null||patch!=null){foreach(var u in selection)world.MineOrder(u,patch,plant);world.Radio=patch!=null?"已指定矿点，装满后自动返厂。":"有载货的矿车将返回精炼厂卸货。";return;}
  }
  if(!attack&&!shortcut&&hit!=null&&hit.Side==0){
   if((mods&Keys.Shift)!=0){if(!selection.Remove(hit))selection.Add(hit);}
   else if(selection.Count==1&&selection[0]==hit)selection.Clear();
   else{selection.Clear();selection.Add(hit);}
   return;
  }
  if(selection.Count==0)return;attack|=shortcut;Issue(ApproachSite(mouse,p),hit);
 }
 Piece markedEnemy;PointF? markedPoint;float markTime;bool markHostile;
 void Mark(PointF p,Piece enemy,bool hostile){markedPoint=p;markedEnemy=enemy;markTime=world.Time;markHostile=hostile;}
 void Issue(PointF p,Piece enemy){
  var troops=selection.Where(o=>o.Deployed&&!o.T.Building).ToArray();
  if(enemy!=null&&enemy.Side==1){
   var fighters=selection.Where(o=>o.Deployed&&o.T.Hit>0).ToArray();
   if(fighters.Length==0)world.Radio="所选部队没有可攻击的武器。";
   else{foreach(var o in fighters)world.Attack(o,enemy);Mark(enemy.P,enemy,true);world.Radio="集火："+enemy.T.Name+" #"+enemy.Id;}
  }else if(troops.Length==0&&selection.Count==1&&selection[0].T.Key!="refinery"&&Campaign.HasGate(selection[0].T)){
   if(world.SetRally(selection[0],p))Mark(p,null,false);else world.Radio="集结点需要可通行的空地。";
  }else if(troops.Length==0){world.Radio="请先点选我方部队，或拖框选择多支部队。";}
  else{
   world.Radio=attack?"部队将沿途交战，向指定地点推进。":"部队正前往指定地点。";int failed=troops.Length-world.MarchTo(troops,p,attack,syncMarch);
   if(failed<troops.Length)Mark(p,null,attack);if(failed>0)world.Radio=failed+" 支部队的目标位置被阻挡。请点建筑外的空地。";
  }
  attack=false;
 }
 protected override void OnMouseDown(MouseEventArgs e){selectType=null;base.OnMouseDown(e);edgeDwell=0;hudDirty=true;mouse=ToScreenPoint(e);
  if(e.Button==MouseButtons.Left){var b=hits.LastOrDefault(h=>h.Box.Contains(mouse));if(b!=null){b.Run();Invalidate();return;}}
  if(briefing||paused||world.Won||world.Lost||mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>MapTop+MH)return;
  var p=WorldOf(mouse);
  if(e.Button==MouseButtons.Middle){panning=true;panPoint=mouse;Capture=true;return;}
  if(sweep){sweep=false;if(e.Button==MouseButtons.Left)world.Sweep(p);return;}
  if(e.Button==MouseButtons.Right){
   if(place!=null||attack){place=null;attack=false;world.Radio="已取消待确认命令。";}
   else{selection.Clear();markedPoint=null;markedEnemy=null;world.Radio="已取消选择。";}Invalidate();return;
  }
  if(e.Button!=MouseButtons.Left)return;
  if(place!=null){if(world.Build(place,p))place=null;return;}
  start=mouse;drag=true;dragged=false;Capture=true;
 }
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);edgeArmed=true;var old=mouse;mouse=ToScreenPoint(e);if(briefing&&old!=mouse)hudDirty=true;if(old!=mouse&&(old.X>=MW||mouse.X>=MW||old.Y<MapTop||mouse.Y<MapTop||old.Y>=MapTop+MH||mouse.Y>=MapTop+MH))hudDirty=true;if(panning){Pan(panPoint.X-mouse.X,panPoint.Y-mouse.Y);panPoint=mouse;Invalidate();return;}if(drag&&Campaign.Dist(start,mouse)>6)dragged=true;}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);hudDirty=true;if(panning){panning=false;Capture=false;return;}if(!drag)return;drag=false;Capture=false;mouse=ToScreenPoint(e);
  if(mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>=MapTop+MH){if(!dragged)return;}
  if(dragged){attack=false;if((ModifierKeys&Keys.Shift)==0)selection.Clear();var r=DragRect();foreach(var u in world.Live(0).Where(u=>!u.T.Building&&r.Contains(ScreenOf(u.P))))if(!selection.Contains(u))selection.Add(u);return;}
  ContextOrder(ModifierKeys);Invalidate();

 }
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);hudDirty=true;if(briefing){if(e.KeyCode==Keys.Enter){if(frontEnterHeld)return;frontEnterHeld=true;}FrontKey(e.KeyCode);return;}
  if(world.Won||world.Lost)return;
  if(e.KeyCode==Keys.Escape&&(place!=null||attack||sweep)){place=null;attack=false;sweep=false;return;}
  if(e.KeyCode==Keys.Pause||e.KeyCode==Keys.Escape){paused=!paused;edgeDwell=0;help=false;return;}if(e.KeyCode==Keys.F5){paused=true;help=true;return;}if(paused)return;
  if(FieldKey(e))return;
  if(e.KeyCode==Keys.S){foreach(var unit in selection)world.Halt(unit);markedPoint=null;markedEnemy=null;attack=false;world.Radio="所选部队已停止。";}
  if(e.KeyCode==Keys.G)showGrid=!showGrid;
  if(e.KeyCode==Keys.F)syncMarch=!syncMarch;
  if(e.KeyCode==Keys.Left)Pan(-95,0);if(e.KeyCode==Keys.Right)Pan(95,0);if(e.KeyCode==Keys.Up)Pan(0,-95);if(e.KeyCode==Keys.Down)Pan(0,95);
  if(e.KeyCode==Keys.A&&selection.Any(o=>o.Deployed&&!o.T.Building)){attack=true;place=null;}if(e.KeyCode==Keys.D){var o=selection.FirstOrDefault();if(o!=null){if(o.T.Key=="mcv")world.Deploy(o);else world.Load(o);}}
  int number=e.KeyValue-(int)Keys.D0;if(number>=1&&number<=5){if(e.Control){foreach(var o in world.Units.Where(o=>o.Group==number))o.Group=0;foreach(var o in selection)o.Group=number;}
   else{selection=world.Live(0).Where(o=>o.Group==number).ToList();}}
 }
 [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new CommandWindow());}
}
}
