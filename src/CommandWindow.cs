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
class CommandWindow:Form {
 const int MW=1200,MH=675,MapTop=67,W=1472,H=819;
 Campaign world; readonly Stopwatch watch=Stopwatch.StartNew(); readonly Timer pulse=new Timer(); double last; float scale,ox,oy;
 bool briefing=true,paused,help,attack,army,drag,dragged; int speed=1; PointF mouse,start;TypeInfo place;
 List<Piece> selection=new List<Piece>();List<HitBox> hits=new List<HitBox>();
 Bitmap ground;Dictionary<string,Bitmap> atlas=new Dictionary<string,Bitmap>();Dictionary<string,Font> fonts=new Dictionary<string,Font>();
 Color ink=C("#111d20"),panel=C("#1d2c2c"),pale=C("#e3e6d3"),dim=C("#9eafa1"),gold=C("#ebc17c"),teal=C("#82d0b4"),red=C("#ee8b72");
 class HitBox {public RectangleF Box;public Action Run;}
 static Color C(string s){return ColorTranslator.FromHtml(s);}
 public CommandWindow(){
  Text="断线前沿  /  灰港登陆";StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(900,575);ClientSize=new Size(1260,706);BackColor=Color.FromArgb(17,29,32);
  DoubleBuffered=true;KeyPreview=true;SetStyle(ControlStyles.ResizeRedraw,true);
  Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  ground=Asset("port",false);
  foreach(var t in Campaign.Types)atlas[t.Key]=Asset(t.Key,true);
  Restart();pulse.Interval=32;pulse.Tick+=(s,e)=>{double now=watch.Elapsed.TotalSeconds;float dt=(float)Math.Min(.12,now-last);last=now;
   if(!briefing&&!paused&&!world.Won&&!world.Lost){for(int i=0;i<speed;i++)world.Tick(dt);selection.RemoveAll(o=>!o.Alive||o.Carrier!=null);}
   Invalidate();};pulse.Start(); FormClosed+=(s,e)=>{pulse.Stop();pulse.Dispose();ground.Dispose();foreach(var i in atlas.Values)i.Dispose();foreach(var f in fonts.Values)f.Dispose();};
  Deactivate+=(s,e)=>{if(!briefing&&!world.Won&&!world.Lost)paused=true;drag=false;};
 }
 static Bitmap ScaleImage(Bitmap source,int w,int h){var b=new Bitmap(w,h,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(b)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Rectangle(0,0,w,h));}return b;}
 static Bitmap Asset(string name,bool atlasImage){
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Greyhaven.Art."+name+".png")){
   if(stream==null)throw new InvalidDataException("缺少游戏素材 "+name);
   using(var b=new Bitmap(stream))return ScaleImage(b,atlasImage?b.Width/2:1200,atlasImage?b.Height/2:675);
  }
 }
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int length);
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);
  int dark=1,caption=ColorTranslator.ToWin32(ink),text=ColorTranslator.ToWin32(pale);
  DwmSetWindowAttribute(Handle,20,ref dark,4);DwmSetWindowAttribute(Handle,35,ref caption,4);DwmSetWindowAttribute(Handle,36,ref text,4);
 }
 void Restart(){world=new Campaign();selection.Clear();selection.Add(world.Units[0]);briefing=true;paused=false;help=false;army=false;place=null;attack=false;speed=1;}
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
  Plate(g,r,r.Contains(mouse)&&action!=null?C("#47594b"):lit?C("#394739"):C("#253636"),edge);
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
 void Sprite(Graphics g,TypeInfo t,int side,int dir,int group,RectangleF box){var bmp=atlas[t.Key];g.DrawImage(bmp,box,new RectangleF((dir%8)*96,(group*2+dir/8)*96,96,96),GraphicsUnit.Pixel);}
 int Frame(Piece o){
  bool action=o.T.Foot||o.T.Key=="factory";if(!o.Alive){if(o.T.Foot&&o.Dead<.4)return 7+o.Side;return action?6:3;}
  if(o.T.Key=="factory"&&o.Door>.85f)return 7+o.Side;
  return (action?2:1)*o.Side+(action&&(o.Door>0||o.Path.Count>0&&(int)(world.Time*5)%2==0)?1:0);
 }
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;scale=Math.Min(ClientSize.Width/(float)W,ClientSize.Height/(float)H);ox=(ClientSize.Width-W*scale)/2;oy=(ClientSize.Height-H*scale)/2;
  g.Clear(ink);g.TranslateTransform(ox,oy);g.ScaleTransform(scale,scale);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.Bilinear;
  hits.Clear();g.DrawImage(ground,0,MapTop,MW,MH);DrawBattle(g);DrawHud(g);if(briefing||paused||world.Won||world.Lost)Overlay(g);
 }
 Color Team(int s){return s==0?C("#6dcced"):red;}
 void DrawBattle(Graphics g){
  var save=g.Save();g.TranslateTransform(0,MapTop);g.SetClip(new Rectangle(0,0,MW,MH));
  if(world.Hold>0){
   float progress=Math.Min(1,world.Hold/45f),bx=310+730*progress,by=42+18*progress;
   PointF[] hull={new PointF(bx-47,by-10),new PointF(bx+32,by-10),new PointF(bx+53,by),new PointF(bx+31,by+10),new PointF(bx-47,by+10)};
   using(var b=new SolidBrush(C("#334849")))g.FillPolygon(b,hull);using(var pen=new Pen(C("#a0ae99"),2))g.DrawPolygon(pen,hull);
   Rect(g,bx-39,by-8,17,16,pale);for(int j=0;j<3;j++){Rect(g,bx-18+j*16,by-7,13,14,j==1?C("#8e6b54"):C("#667b74"));}
   Line(g,bx-49,by-6,bx-65,by-12,teal);Line(g,bx-49,by+6,bx-65,by+12,teal);
  }
  foreach(var site in world.Sites){var p=site.P;Color c=site.Owner<0?gold:Team(site.Owner);using(var pen=new Pen(Color.FromArgb(165,c),2)){pen.DashStyle=DashStyle.Dash;g.DrawEllipse(pen,p.X-39,p.Y-22,78,44);}
   Rect(g,p.X-38,p.Y-43,76,21,Color.FromArgb(201,ink));Txt(g,site.Name,p.X-32,p.Y-42,14,c,true);
   Line(g,p.X-24,p.Y-20,p.X+24,p.Y-20,ink,4);Line(g,p.X,p.Y-20,p.X+site.Capture*4.8f,p.Y-20,site.Capture>0?Team(0):Team(1),3);}
  bool radar=world.Power(0)>=0&&world.Live(0).Any(o=>o.T.Key=="radar");
  foreach(var o in world.Units.Where(a=>a.Carrier==null).OrderBy(a=>a.P.Y)){
   bool visible=world.Visible(o);if(!visible){if(radar&&o.Alive&&!o.T.Foot&&!o.T.Building&&o.Path.Count>0){using(var p=new Pen(red,2))g.DrawEllipse(p,o.P.X-6,o.P.Y-6,12,12);}continue;}
   var pt=o.P;float s=o.T.Size;if(o.Rollout>0){double a=o.Dir*Math.PI/8;pt.X-=(float)Math.Sin(a)*o.Rollout*36;pt.Y-=(float)Math.Cos(a)*o.Rollout*29;}
   bool sel=selection.Contains(o);if(sel){using(var p=new Pen(teal,2.6f))g.DrawEllipse(p,pt.X-s*.33f,pt.Y-s*.10f,s*.66f,s*.31f);}
   float h=s*(.22f+.78f*o.Ready);Sprite(g,o.T,o.Side,o.Dir,Frame(o),new RectangleF(pt.X-s/2,pt.Y-h*.59f,s,h));
   if(o.Alive){float y=pt.Y+s*.18f;Color tag=Team(o.Side);if(o.Side==0)Rect(g,pt.X-3,y,7,5,tag);else{Line(g,pt.X-4,y,pt.X,y-4,tag,2);Line(g,pt.X,y-4,pt.X+4,y,tag,2);}
    if(sel||o.Hp<o.T.Hp||o.Ready<1){Rect(g,pt.X-17,y+7,34,4,ink);Rect(g,pt.X-17,y+7,34*(o.Ready<1?o.Ready:o.Hp/o.T.Hp),4,o.Ready<1?gold:tag);}
    if(o.T.Building&&o.Ready<1)Txt(g,"施工",pt.X-13,y+12,12,gold,true);
   }
  }
  foreach(var b in world.Shots){Color c=b.Shell?gold:Team(b.Side);Line(g,b.A.X,b.A.Y-5,b.B.X,b.B.Y-5,Color.FromArgb((int)(185*b.Life/.23f),c),b.Shell?3:1.5f);using(var p=new Pen(gold,2))g.DrawEllipse(p,b.B.X-4,b.B.Y-8,8,8);}
  if(selection.Count==1&&selection[0].Path.Count>0){
   var u=selection[0];var end=u.Path.Last();using(var p=new Pen(Color.FromArgb(148,teal),1.4f)){
    p.DashStyle=DashStyle.Dash;var points=new List<PointF>{u.P};points.AddRange(u.Path);g.DrawLines(p,points.ToArray());g.DrawEllipse(p,end.X-6,end.Y-4,12,8);}
  }
  if(place!=null&&mouse.X<MW&&mouse.Y>=MapTop&&mouse.Y<MapTop+MH){var p=new PointF(mouse.X,mouse.Y-MapTop);float s=place.Size;Sprite(g,place,0,2,0,new RectangleF(p.X-s/2,p.Y-s*.59f,s,s));using(var pen=new Pen(world.CanPlace(place,p,null)?teal:red,2))g.DrawRectangle(pen,p.X-s*.28f,p.Y-s*.20f,s*.56f,s*.40f);}
  if(drag&&dragged){RectangleF r=DragRect();r.Y-=MapTop;Rect(g,r.X,r.Y,r.Width,r.Height,Color.FromArgb(35,teal));using(var p=new Pen(teal))g.DrawRectangle(p,r.X,r.Y,r.Width,r.Height);}
  g.Restore(save);
 }
 void DrawHud(Graphics g){
  Rect(g,0,0,W,MapTop,ink);Rect(g,0,MapTop-3,W,3,C("#5c7464"));Rect(g,0,0,7,MapTop,gold);
  Txt(g,"断 线 前 沿",22,9,29,pale,true);Txt(g,"GREYHAVEN   /   LANDING FORCE",23,44,10,gold,true);
  Line(g,240,12,240,54,C("#4d655b"));Txt(g,"作战目标  /  OP.01",258,9,12,teal,true);
  Txt(g,!world.Landing?"展开工程车并建立基地":world.Sites[2].Owner==0?"守住港务中心　"+(int)world.Hold+" / 45 秒":"派步兵夺取港务中心",258,27,20,pale,true);
  Metric(g,728,"军费  /  CR",world.Funds[0]+"  +"+world.Income(0)+"/秒",gold);Metric(g,930,"电力余量  /  PWR",world.Power(0).ToString("+#;-#;0"),world.Power(0)<0?red:teal);
  Metric(g,1085,"行动时钟",TimeSpan.FromSeconds(world.Time).ToString(@"mm\:ss"),pale);
  Button(g,1222,15,103,35,"速度 ×"+speed,()=>speed=speed==1?2:1);Button(g,1334,15,120,35,"暂停",()=>paused=true);
  Rect(g,MW,MapTop,W-MW,MH,panel);Line(g,MW,MapTop,MW,MapTop+MH,gold,2);
  float x=1214;Txt(g,"战区态势   /   TACTICAL",x,79,14,teal,true);g.DrawImage(ground,x,102,244,107);
  foreach(var o in world.Units.Where(a=>a.Alive&&a.Carrier==null&&world.Visible(a))){Rect(g,x+o.P.X/1200*244-2,102+o.P.Y/675*107-2,o.T.Building?5:3,o.T.Building?5:3,Team(o.Side));}
  foreach(var s in world.Sites){using(var p=new Pen(gold))g.DrawRectangle(p,x+s.P.X/1200*244-4,102+s.P.Y/675*107-4,8,8);}
  Button(g,x,221,117,34,"01  基地建设",()=>{army=false;place=null;},!army);Button(g,x+125,221,118,34,"02  部队生产",()=>{army=true;place=null;},army);
  var cards=Campaign.Types.Where(t=>army?!t.Building:t.Building&&t.Key!="hq").ToArray();for(int i=0;i<cards.Length;i++)Card(g,cards[i],x+(i%2)*124,266+(i/2)*61);
  var unit=selection.FirstOrDefault();Txt(g,"指令席   /   ORDERS",x,521,13,teal,true);
  Txt(g,unit==null?"未选中单位":selection.Count>1?"混合编队  ×"+selection.Count:unit.T.Name+(unit.Ready<1?" · 建造中":""),x,545,20,pale,true);
  if(unit!=null&&unit.Queue.Count>0)Txt(g,"排产 "+unit.Queue.Peek().Name+"  "+(int)(unit.Work/unit.Queue.Peek().Seconds*100)+"%  ×"+unit.Queue.Count,x,574,13,gold);
  else Txt(g,unit!=null&&unit.T.Key=="engineer"?"靠近受损载具或建筑自动维修":"左键选择　右键行军 / 集火",x,574,13,dim);
  if(unit!=null&&unit.T.Key=="mcv")Button(g,x,602,243,36,"展开指挥部   [D]",()=>world.Deploy(unit),true);
  else if(unit!=null&&unit.T.Key=="ifv")Button(g,x,602,243,36,"装载 / 卸载步兵   [D]",()=>world.Load(unit));
  else Button(g,x,602,243,36,"攻击移动   [A]",()=>{place=null;attack=true;world.Radio="请点选攻击移动的目的地。";},attack);
  Button(g,x,647,119,34,world.Upgraded[0]?"装甲已升级":"升级  $260",world.Upgraded[0]?(Action)null:()=>world.Upgrade());
  Button(g,x+125,647,118,34,"侦察 $80",()=>{if(world.Power(0)>=0&&world.Live(0).Any(o=>o.T.Key=="radar")&&world.Funds[0]>=80&&world.Scan<=0){world.Funds[0]-=80;world.Scan=10;world.Radio="全区侦察开启，持续十秒。";}else world.Radio="侦察需要通电的雷达站、80 军费，并等待上一次扫描结束。";});
  Button(g,x,694,243,33,"战地手册   [F1]",()=>{paused=true;help=true;});
  Rect(g,0,742,W,77,ink);Rect(g,15,755,4,47,gold);Txt(g,"作战电台　/　COMMS",28,751,12,gold,true);Txt(g,world.Radio,28,772,18,pale,true);
  Txt(g,"框选编队   ·   Ctrl + 1~5 编组   ·   空格暂停",940,769,15,dim);
 }
 void Metric(Graphics g,int x,string label,string val,Color color){Txt(g,label,x,8,12,dim);Txt(g,val,x,27,21,color,true);}
 void Card(Graphics g,TypeInfo t,float x,float y){
  string reason=world.Require(t,0);var r=new RectangleF(x,y,117,54);bool ready=reason==null;
  Plate(g,r,ready?C("#35473e"):C("#283632"),place==t?gold:ready?C("#647e68"):C("#48584e"));
  Sprite(g,t,0,14,0,new RectangleF(x+1,y+2,48,48));Txt(g,t.Name,x+45,y+6,13,ready?pale:dim,true);Txt(g,"$"+t.Price,x+46,y+28,14,ready?gold:dim);
  var active=world.Live(0).FirstOrDefault(o=>o.Queue.Count>0&&o.Queue.Peek()==t);
  if(active!=null){Rect(g,x+5,y+48,104,3,ink);Rect(g,x+5,y+48,104*active.Work/t.Seconds,3,teal);}
  hits.Add(new HitBox{Box=r,Run=()=>{string why=world.Require(t,0);if(why!=null){world.Radio=why;return;}if(t.Building){place=t;attack=false;world.Radio="部署"+t.Name+"：左键确认绿色位置，右键取消。";}
    else world.Train(t,0,selection.FirstOrDefault());}});
 }
 void Overlay(Graphics g){
  hits.Clear();Rect(g,0,0,W,H,Color.FromArgb(177,ink));var r=new RectangleF(285,172,900,468);Plate(g,r,C("#1f302f"),C("#a2b398"));Rect(g,286,205,6,112,gold);
  Txt(g,"LANDING FORCE      //      OPERATIONS COMMAND      01—GH",324,203,14,gold,true);
  string title=world.Won?"行动成功":world.Lost?"行动终止":briefing?"灰港登陆":help?"战地手册":"指挥已暂停";
  Txt(g,title,323,245,49,pale,true);Line(g,329,325,1140,325,C("#5d7265"));
  string[] lines=world.Won?new[]{"运输船已接岸。港区走廊重新开放。","作战用时 "+TimeSpan.FromSeconds(world.Time).ToString(@"mm\:ss")+"　｜　存活部队 "+world.Live(0).Count(o=>!o.T.Building)}:
   world.Lost?new[]{"最后一座指挥部与工程车均已损失，登陆部队撤离。","先保护指挥部，再让步兵夺取仓库，扩大生产。"}:
   new[]{"01    选择工程车按 D 展开。建电站、兵营和车辆工厂，升级后可生产坦克。","02    框选蓝军，右键行军或集火。A 后点击地面，部队会边推进边交战。","03    步兵靠近西岸仓库、中央货场可增加军费。夺下港务中心后清开近敌，守住 45 秒。","04    工程组近距离维修，战车按 D 接送步兵。失去所有指挥部和工程车即告失败。"};
  for(int i=0;i<lines.Length;i++)Txt(g,lines[i],328,347+i*41,17,i==0?gold:pale);
  Button(g,328,553,241,49,world.Won||world.Lost?"重新登陆":briefing?"▶   发起登陆":"▶   返回战场",()=>{if(world.Won||world.Lost)Restart();else {briefing=false;paused=false;help=false;}},true);
  if(!briefing&&!world.Won&&!world.Lost)Button(g,594,553,182,49,"重开本关",Restart);
 }
 RectangleF DragRect(){return RectangleF.FromLTRB(Math.Min(start.X,mouse.X),Math.Min(start.Y,mouse.Y),Math.Max(start.X,mouse.X),Math.Max(start.Y,mouse.Y));}
 PointF ToScreenPoint(MouseEventArgs e){return new PointF((e.X-ox)/Math.Max(.01f,scale),(e.Y-oy)/Math.Max(.01f,scale));}
 Piece Hit(PointF p){return world.Units.Where(o=>o.Alive&&o.Carrier==null&&world.Visible(o)).OrderByDescending(o=>o.P.Y).FirstOrDefault(o=>Math.Abs(p.X-o.P.X)<o.T.Size*.36f&&p.Y>o.P.Y-o.T.Size*.43f&&p.Y<o.P.Y+o.T.Size*.26f);}
 void Issue(PointF p,Piece enemy){
  var troops=selection.Where(o=>o.Deployed&&!o.T.Building).ToArray();int n=0,cols=(int)Math.Ceiling(Math.Sqrt(troops.Length));
  if(troops.Length>0)world.Radio=enemy!=null&&enemy.Side==1?"集中火力攻击敌方"+enemy.T.Name:"指令已接收，部队正在推进。";
  foreach(var o in troops){int i=n++;if(enemy!=null&&enemy.Side==1)world.Attack(o,enemy);
   else{PointF dest=new PointF(p.X+((i%cols)-(cols-1)*.5f)*22,p.Y+((i/cols)-(troops.Length-1)/cols*.5f)*18);
    if(!world.Move(o,dest,attack))world.Radio="地形或建筑挡住了目标位置。";}}
  attack=false;
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);mouse=ToScreenPoint(e);
  if(e.Button==MouseButtons.Left){var b=hits.LastOrDefault(h=>h.Box.Contains(mouse));if(b!=null){b.Run();Invalidate();return;}}
  if(briefing||paused||world.Won||world.Lost||mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>MapTop+MH)return;
  var p=new PointF(mouse.X,mouse.Y-MapTop);
  if(e.Button==MouseButtons.Right){if(place!=null||attack){place=null;attack=false;world.Radio="已取消指令。";}else Issue(p,Hit(p));return;}
  if(e.Button!=MouseButtons.Left)return;
  if(place!=null){if(world.Build(place,p))place=null;return;}if(attack){Issue(p,Hit(p));return;}
  start=mouse;drag=true;dragged=false;Capture=true;
 }
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);mouse=ToScreenPoint(e);if(drag&&Campaign.Dist(start,mouse)>6)dragged=true;}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!drag)return;drag=false;Capture=false;mouse=ToScreenPoint(e);
  if((ModifierKeys&Keys.Shift)==0)selection.Clear();
  if(dragged){var r=DragRect();r.Y-=MapTop;foreach(var o in world.Live(0).Where(o=>!o.T.Building&&r.Contains(o.P)))if(!selection.Contains(o))selection.Add(o);}
  else{var o=Hit(new PointF(mouse.X,mouse.Y-MapTop));if(o!=null&&o.Side==0&&!selection.Contains(o))selection.Add(o);}
 }
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(world.Won||world.Lost)return;
  if(briefing){if(e.KeyCode==Keys.Enter)briefing=false;return;}
  if(e.KeyCode==Keys.Escape&&(place!=null||attack)){place=null;attack=false;return;}
  if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Escape){paused=!paused;help=false;return;}if(e.KeyCode==Keys.F1){paused=true;help=true;return;}if(paused)return;
  if(e.KeyCode==Keys.A){attack=true;place=null;}if(e.KeyCode==Keys.D){var o=selection.FirstOrDefault();if(o!=null){if(o.T.Key=="mcv")world.Deploy(o);else world.Load(o);}}
  int number=e.KeyValue-(int)Keys.D0;if(number>=1&&number<=5){if(e.Control){foreach(var o in world.Units.Where(o=>o.Group==number))o.Group=0;foreach(var o in selection)o.Group=number;}
   else{selection=world.Live(0).Where(o=>o.Group==number).ToList();}}
 }
 [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new CommandWindow());}
}
}
