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
 const int MW=1200,MH=675,MapTop=67,W=1472,H=819;
 Campaign world; readonly Stopwatch watch=Stopwatch.StartNew(); readonly Timer pulse=new Timer(); double last; float scale,ox,oy;
 bool showGrid;bool briefing=true,paused,help,attack,army,drag,dragged; int speed=1; PointF mouse,start;TypeInfo place;
 List<Piece> selection=new List<Piece>();List<HitBox> hits=new List<HitBox>();
 Bitmap ground;Dictionary<string,Bitmap> atlas=new Dictionary<string,Bitmap>();Dictionary<string,Font> fonts=new Dictionary<string,Font>();
 Color ink=C("#111d20"),panel=C("#1d2c2c"),pale=C("#e3e6d3"),dim=C("#9eafa1"),gold=C("#ebc17c"),teal=C("#82d0b4"),red=C("#ee8b72");
 class HitBox {public RectangleF Box;public Action Run;}
 static Color C(string s){return ColorTranslator.FromHtml(s);}
 [System.Runtime.InteropServices.DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint ms);
 [System.Runtime.InteropServices.DllImport("winmm.dll")]static extern uint timeEndPeriod(uint ms);
 bool timerPrecision;
 protected override void OnShown(EventArgs e){base.OnShown(e);timerPrecision=timeBeginPeriod(1)==0;last=watch.Elapsed.TotalSeconds;}
 public CommandWindow(){
  Text="断线前沿  /  灰港登陆";StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(900,575);var area=Screen.FromPoint(Cursor.Position).WorkingArea;float fit=Math.Min(1f,Math.Min((area.Width-32f)/W,(area.Height-64f)/H));ClientSize=new Size((int)(W*fit),(int)(H*fit));BackColor=Color.FromArgb(17,29,32);
  KeyPreview=true;SetStyle(ControlStyles.ResizeRedraw|ControlStyles.Opaque|ControlStyles.AllPaintingInWmPaint,true);
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  world=new Campaign();ground=MakeGround();projectedGround=ProjectTerrain();radarGround=new Bitmap(488,214,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var rg=Graphics.FromImage(radarGround))rg.DrawImage(ground,0,0,488,214);
  foreach(var key in new[]{"hq","power","factory","barracks","radar","post","squad","at","engineer","tank","ifv","mcv","arty","depot","freight","harbormaster"})painted[key]=Raw((key=="squad"||key=="at"||key=="engineer"?"hd-":"iso-")+key);
  foreach(var t in Campaign.Types.Where(t=>t.Foot))atlas[t.Key]=WreckSheet(t);
  Restart();pulse.Interval=15;pulse.Tick+=(s,e)=>{double now=watch.Elapsed.TotalSeconds;float dt=(float)Math.Min(.12,now-last);last=now;
   if(!briefing&&!paused&&!world.Won&&!world.Lost){for(int i=0;i<speed;i++)world.Tick(dt);selection.RemoveAll(o=>!o.Alive||o.Carrier!=null);}
   Invalidate();};pulse.Start(); FormClosed+=(s,e)=>{pulse.Stop();pulse.Dispose();if(timerPrecision)timeEndPeriod(1);ground.Dispose();projectedGround.Dispose();radarGround.Dispose();if(terrainView!=null)terrainView.Dispose();ReleaseFrame();ClearStampCache();foreach(var b in painted.Values)b.Dispose();foreach(var i in atlas.Values)i.Dispose();foreach(var f in fonts.Values)f.Dispose();};
  Deactivate+=(s,e)=>{if(!briefing&&!world.Won&&!world.Lost)paused=true;drag=false;panning=false;Capture=false;};
 }
 static Bitmap WreckSheet(TypeInfo t){
  using(var stream=typeof(CommandWindow).Assembly.GetManifestResourceStream("Greyhaven.Art."+t.Key+".png"))
  using(var image=new Bitmap(stream)){
   int cell=image.Width/8,start=(t.Foot||t.Key=="factory"?6:3)*2*cell;
   return image.Clone(new Rectangle(0,start,image.Width,image.Height-start),PixelFormat.Format32bppPArgb);
  }
 }
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int length);
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);
  int dark=1,caption=ColorTranslator.ToWin32(ink),text=ColorTranslator.ToWin32(pale);
  DwmSetWindowAttribute(Handle,20,ref dark,4);DwmSetWindowAttribute(Handle,35,ref caption,4);DwmSetWindowAttribute(Handle,36,ref text,4);
 }
 void Restart(){markedPoint=null;markedEnemy=null;camera=new PointF(255,465);zoom=1.7f;sweep=false;panning=false;syncMarch=true;queueOpen=false;queueIndex=0;world=new Campaign();selection.Clear();selection.Add(world.Units[0]);briefing=true;paused=false;help=false;army=false;place=null;attack=false;speed=1;}
 Font F(float size,bool bold){string key=size.ToString()+bold;Font f;if(!fonts.TryGetValue(key,out f))fonts[key]=f=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);return f;}
 void Txt(Graphics g,string s,float x,float y,float size,Color c,bool bold=false){using(var b=new SolidBrush(c))g.DrawString(s,F(size,bold),b,x,y,StringFormat.GenericTypographic);}
 void Rect(Graphics g,float x,float y,float w,float h,Color c){using(var b=new SolidBrush(c))g.FillRectangle(b,x,y,w,h);}
 void Line(Graphics g,float x,float y,float xx,float yy,Color c,float width=1){using(var p=new Pen(c,width))g.DrawLine(p,x,y,xx,yy);}
 void Plate(Graphics g,RectangleF r,Color fill,Color edge){float x=r.X,y=r.Y,w=r.Width,h=r.Height;
  PointF[] corners={new PointF(x+8,y),new PointF(x+w,y),new PointF(x+w,y+h-8),new PointF(x+w-8,y+h),new PointF(x,y+h),new PointF(x,y+8)};
  using(var b=new SolidBrush(fill))g.FillPolygon(b,corners);using(var p=new Pen(edge))g.DrawPolygon(p,corners);
 }
 void Button(Graphics g,float x,float y,float w,float h,string text,Action action,bool lit=false){
  var r=new RectangleF(x,y,w,h);Color edge=lit?gold:action==null?C("#465149"):C("#627971");
  Plate(g,r,action==null?C("#151d1c"):r.Contains(mouse)&&action!=null?C("#47594b"):lit?C("#394739"):C("#253636"),edge);
  Rect(g,x+3,y+10,2,h-20,edge);string symbol=Symbol(text);float inset=11;
  if(symbol!=null){DrawSymbol(g,symbol,x+17,y+h/2,lit?gold:teal);inset=33;}
  Txt(g,text,x+inset,y+(h-18)/2,15,action==null?dim:lit?gold:pale,true);
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
 void Sprite(Graphics g,TypeInfo t,int side,int dir,int group,RectangleF box){var bmp=atlas[t.Key];g.DrawImage(bmp,box,new RectangleF((dir%8)*192,(group*2+dir/8)*192,192,192),GraphicsUnit.Pixel);}
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
  drawingClient=true;DrawBattle(g);
  if(refresh){
   hits.Clear();DrawHud(g);hudHitCount=hits.Count;hudTime=world.Time;hudWorld=world;hudDirty=false;
  }else if(hits.Count>hudHitCount)hits.RemoveRange(hudHitCount,hits.Count-hudHitCount);
  DrawCoach(g);drawingClient=false;previousOverlay=overlay;if(overlay)Overlay(g);
  g.Flush(FlushIntention.Sync);
  if(frameDC==IntPtr.Zero){e.Graphics.DrawImageUnscaled(frameBuffer,0,0);return;}
  var target=e.Graphics.GetHdc();bool copied;
  try{copied=BitBlt(target,0,0,frameBuffer.Width,frameBuffer.Height,frameDC,0,0,0x00CC0020);}
  finally{e.Graphics.ReleaseHdc(target);}
  if(!copied)e.Graphics.DrawImageUnscaled(frameBuffer,0,0);
 }
 Color Team(int s){return s==0?C("#6dcced"):red;}
 void DrawHud(Graphics g){
  Rect(g,0,0,W,MapTop,ink);Rect(g,0,MapTop-3,W,3,C("#5c7464"));Rect(g,0,0,7,MapTop,gold);
  Txt(g,"断 线 前 沿",22,9,29,pale,true);Txt(g,"GREYHAVEN   /   LANDING FORCE",23,44,10,gold,true);
  Line(g,240,12,240,54,C("#4d655b"));Txt(g,world.Tutorial?"野外训练  /  DRILL":world.Assisted?"宽松战役  /  OP.01":"标准战役  /  OP.01",258,9,12,teal,true);
  Txt(g,world.Tutorial?(world.Lesson+1)+" / 9　"+Campaign.Lessons[world.Lesson]:!world.Landing?"展开工程车并建立基地":world.Sites[2].Owner==0?"守住港务中心　"+(int)world.Hold+" / 45 秒":"派步兵夺取港务中心",258,27,20,pale,true);
  Metric(g,728,"军费  /  CR",world.Funds[0]+"  +"+world.Income(0)+"/秒",gold);Metric(g,930,"电力余量  /  PWR",world.Power(0).ToString("+#;-#;0"),world.Power(0)<0?red:teal);
  Metric(g,1085,"行动时钟",TimeSpan.FromSeconds(world.Time).ToString(@"mm\:ss"),pale);
  Button(g,1222,15,103,35,"速度 ×"+speed,()=>speed=speed==1?2:1);Button(g,1334,15,120,35,"暂停",()=>paused=true);
  Rect(g,MW,MapTop,W-MW,MH,panel);Line(g,MW,MapTop,MW,MapTop+MH,gold,2);
  float x=1214;Txt(g,"战区态势   /   TACTICAL",x,79,14,teal,true);DrawRadar(g,x);
  Button(g,x,221,76,34,"建设",()=>{army=false;queueOpen=false;place=null;},!army&&!queueOpen);Button(g,x+81,221,76,34,"生产",()=>{army=true;queueOpen=false;place=null;},army&&!queueOpen);Button(g,x+162,221,81,34,"队列 "+Pending(),OpenQueue,queueOpen);
  var cards=Campaign.Types.Where(t=>army?!t.Building:t.Building&&t.Key!="hq").ToArray();if(queueOpen)DrawQueue(g,x);else for(int i=0;i<cards.Length;i++)Card(g,cards[i],x+(i%2)*124,266+(i/2)*61);
  var unit=selection.FirstOrDefault();Txt(g,"指令席   /   ORDERS",x,521,13,teal,true);
  Txt(g,unit==null?"未选中单位":selection.Count>1?"混合编队  ×"+selection.Count:unit.T.Name+(unit.Ready<1?" · 建造中":""),x,545,20,pale,true);
  if(unit!=null&&unit.Queue.Count>0)Txt(g,"排产 "+unit.Queue.Peek().Name+"  "+(int)(unit.Work/unit.Queue.Peek().Seconds*100)+"%  ×"+unit.Queue.Count,x,574,13,gold);
  else Txt(g,unit!=null&&unit.T.Key=="engineer"?"靠近受损载具或建筑自动维修":unit!=null&&Campaign.HasGate(unit.T)?"左键空地设置出厂集结点":selection.Count==1&&unit.T.Hit>0?"虚线圈内自动锁敌 · 再点本单位取消选中":"左键选人 / 下令　右键取消",x,574,13,dim);
  if(unit!=null&&unit.T.Key=="mcv")Button(g,x,602,243,36,"展开指挥部   [D]",()=>world.Deploy(unit),true);
  else if(unit!=null&&unit.T.Key=="ifv")Button(g,x,602,243,36,"装载 / 卸载步兵   [D]",()=>world.Load(unit));
  else Button(g,x,602,243,36,"攻击移动   [A]",!selection.Any(o=>o.Deployed&&!o.T.Building)?(Action)null:()=>{place=null;attack=true;world.Radio="请点选攻击移动的目的地。";},attack);
  Button(g,x,647,119,34,world.Upgraded[0]?"装甲已升级":!world.Live(0).Any(o=>o.T.Key=="factory")?"需车辆工厂":"升级  $260",(world.Upgraded[0]||world.Funds[0]<260||!world.Live(0).Any(o=>o.T.Key=="factory"))?(Action)null:()=>world.Upgrade());
  bool scanReady=world.Power(0)>=0&&world.Live(0).Any(o=>o.T.Key=="radar")&&world.Funds[0]>=80&&world.Scan<=0;
  Button(g,x+125,647,118,34,scanReady?"扫描 $80":!world.Live(0).Any(o=>o.T.Key=="radar")?"需雷达站":world.Power(0)<0?"缺电":world.Scan>0?"扫描冷却":"需 $80",scanReady?(Action)(()=>{sweep=true;place=null;attack=false;world.Radio="左键选择揭图中心，右键取消。";}):null);
  Button(g,x,694,117,33,"手册 F1",()=>{paused=true;help=true;});Button(g,x+125,694,118,33,syncMarch?"队形开 F":"队形关 F",()=>syncMarch=!syncMarch,syncMarch);
  Rect(g,0,742,W,77,ink);Rect(g,15,755,4,47,gold);Txt(g,"作战电台　/　COMMS",28,751,12,gold,true);Txt(g,world.Radio,28,772,18,pale,true);
  DrawQueueStrip(g);
 }
 void Metric(Graphics g,int x,string label,string val,Color color){Txt(g,label,x,8,12,dim);Txt(g,val,x,27,21,color,true);}
 void Card(Graphics g,TypeInfo t,float x,float y){
  string reason=world.Require(t,0);var r=new RectangleF(x,y,117,54);bool ready=reason==null;
  Plate(g,r,ready?C("#35473e"):C("#283632"),place==t||(world.Tutorial&&world.LessonKey==t.Key)?gold:ready?C("#647e68"):C("#48584e"));
  DrawCardArt(g,t,new RectangleF(x+2,y+3,42,46));Txt(g,t.Name,x+45,y+6,13,ready?pale:dim,true);Txt(g,"$"+t.Price,x+46,y+28,14,ready?gold:dim);
  if(!ready){Rect(g,x+1,y+1,115,52,Color.FromArgb(212,ink));Txt(g,t.Name,x+9,y+4,14,dim,true);Txt(g,"锁 · "+reason.Replace("需要","需"),x+8,y+27,12,gold);return;}
  var active=world.Live(0).FirstOrDefault(o=>o.Queue.Count>0&&o.Queue.Peek()==t);
  if(active!=null){Rect(g,x+5,y+48,104,3,ink);Rect(g,x+5,y+48,104*active.Work/t.Seconds,3,teal);}
  hits.Add(new HitBox{Box=r,Run=()=>{string why=world.Require(t,0);if(why!=null){world.Radio=why;return;}if(t.Building){place=t;attack=false;world.Radio="部署"+t.Name+"：左键确认绿色位置，右键取消。";}
    else world.Train(t,0,selection.FirstOrDefault());}});
 }
 void Overlay(Graphics g){
  hits.Clear();Rect(g,0,0,W,H,Color.FromArgb(177,ink));var r=new RectangleF(285,172,900,468);Plate(g,r,C("#1f302f"),C("#a2b398"));Rect(g,286,205,6,112,gold);
  Txt(g,"LANDING FORCE      //      OPERATIONS COMMAND      01—GH",324,203,14,gold,true);
  string title=world.Won?(world.Tutorial?"训练完成":"行动成功"):world.Lost?"行动终止":briefing?"灰港登陆":help?"战地手册":"指挥已暂停";
  Txt(g,title,323,245,49,pale,true);Line(g,329,325,1140,325,C("#5d7265"));
  string[] lines=briefing?new[]{"新手训练　跟随九个小目标，亲手展开基地、出兵、占点并护送运输船。","训练港区只有一支守军。可以慢慢试，教官会在地图上标出下一步。","宽松战役　更多起始军费，首波敌袭推迟到 175 秒。","标准战役　首波敌袭在 92 秒，适合熟悉编队和生产后的指挥官。"}:world.Won?new[]{"运输船已接岸。港区走廊重新开放。","作战用时 "+TimeSpan.FromSeconds(world.Time).ToString(@"mm\:ss")+"　｜　存活部队 "+world.Live(0).Count(o=>!o.T.Building)}:
   world.Lost?new[]{"最后一座指挥部与工程车均已损失，登陆部队撤离。","先保护指挥部，再让步兵夺取仓库，扩大生产。"}:
   new[]{"01    选择工程车按 D 展开。建电站、兵营和车辆工厂，升级后可生产坦克。","02    左键选中我方部队，点地面移动，点敌方单位集火。A 后点地边打边走；Ctrl 点地压制，S 停火。","03    步兵靠近西岸仓库、中央货场可增加军费。夺下港务中心后清开近敌，守住 "+world.HoldTime+" 秒。","04    滚轮缩放，中键拖图或点小地图定位。F 切换混编队形，步兵也能揭开黑幕。"};
  for(int i=0;i<lines.Length;i++)Txt(g,lines[i],328,347+i*41,17,i==0?gold:pale);
  if(briefing){
   Button(g,328,553,235,49,"▶   新手训练   [Enter]",()=>StartMission(true,false),true);
   Button(g,582,553,210,49,"宽松战役",()=>StartMission(false,true));Button(g,814,553,210,49,"标准战役",()=>StartMission(false,false));return;
  }
  Button(g,328,553,241,49,world.Won||world.Lost?"返回任务选择":briefing?"▶   发起登陆":"▶   返回战场",()=>{if(world.Won||world.Lost)Restart();else {briefing=false;paused=false;help=false;}},true);
  if(world.Tutorial&&world.Won)Button(g,594,553,205,49,"进入宽松战役",()=>StartMission(false,true));
  if(!briefing&&!world.Won&&!world.Lost)Button(g,594,553,182,49,"重开本关",Restart);
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
  }else if(troops.Length==0&&selection.Count==1&&Campaign.HasGate(selection[0].T)){
   if(world.SetRally(selection[0],p))Mark(p,null,false);else world.Radio="集结点需要可通行的空地。";
  }else if(troops.Length==0){world.Radio="请先点选我方部队，或拖框选择多支部队。";}
  else{
   world.Radio=attack?"部队将沿途交战，向指定地点推进。":"部队正前往指定地点。";int failed=troops.Length-world.MarchTo(troops,p,attack,syncMarch);
   if(failed<troops.Length)Mark(p,null,attack);if(failed>0)world.Radio=failed+" 支部队的目标位置被阻挡。请点建筑外的空地。";
  }
  attack=false;
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);hudDirty=true;mouse=ToScreenPoint(e);
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
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);var old=mouse;mouse=ToScreenPoint(e);if(old!=mouse&&(old.X>=MW||mouse.X>=MW||old.Y<MapTop||mouse.Y<MapTop||old.Y>=MapTop+MH||mouse.Y>=MapTop+MH))hudDirty=true;if(panning){Pan(panPoint.X-mouse.X,panPoint.Y-mouse.Y);panPoint=mouse;Invalidate();return;}if(drag&&Campaign.Dist(start,mouse)>6)dragged=true;}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);hudDirty=true;if(panning){panning=false;Capture=false;return;}if(!drag)return;drag=false;Capture=false;mouse=ToScreenPoint(e);
  if(mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>=MapTop+MH){if(!dragged)return;}
  if(dragged){attack=false;if((ModifierKeys&Keys.Shift)==0)selection.Clear();var r=DragRect();foreach(var u in world.Live(0).Where(u=>!u.T.Building&&r.Contains(ScreenOf(u.P))))if(!selection.Contains(u))selection.Add(u);return;}
  ContextOrder(ModifierKeys);Invalidate();

 }
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);hudDirty=true;if(world.Won||world.Lost)return;
  if(briefing){if(e.KeyCode==Keys.Enter)StartMission(true,false);return;}
  if(e.KeyCode==Keys.Escape&&(place!=null||attack||sweep)){place=null;attack=false;sweep=false;return;}
  if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Escape){paused=!paused;help=false;return;}if(e.KeyCode==Keys.F1){paused=true;help=true;return;}if(paused)return;
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
