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
 const int MW=1304,W=1472,H=819,MapTop=0,MH=H;
 Campaign world; readonly Stopwatch watch=Stopwatch.StartNew(); readonly Timer pulse=new Timer(); double last; float scale,ox,oy;
 bool showGrid;bool briefing=true,paused,help,attack,army,drag,dragged; PointF mouse,start;TypeInfo place;
 bool clickToRelocateBase;
 List<Piece> selection=new List<Piece>();List<HitBox> hits=new List<HitBox>();
 Bitmap ground;Dictionary<string,Font> fonts=new Dictionary<string,Font>();
 Color ink=C("#101b20"),panel=C("#202c32"),pale=C("#f3eee0"),dim=C("#abb8b5"),gold=C("#ecb54e"),teal=C("#82d0b4"),red=C("#ee8b72");
 class HitBox {public RectangleF Box;public Action Run,Right;public string Id;}
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
  world=new Campaign();ground=MakeGround();ProjectTerrain();radarGround=new Bitmap(488,214,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);using(var rg=Graphics.FromImage(radarGround))rg.DrawImage(ground,0,0,488,214);
  foreach(var key in new[]{"hq","power","factory","barracks","radar","satellite","post","harbormaster","refinery","residence","shop","municipal","relay","bunker"}){painted[key]=Raw("iso-"+key);painted[key+"-paint"]=Raw("iso-"+key+"-paint");}
  foreach(var key in new[]{"squad","at","engineer","sniper","tank","ifv","ifv-repair","mcv","arty","miner","miner-full"}){
   painted[key]=Raw(key+"-base");painted[key+"-paint"]=Raw(key+"-paint");
   if(key!="squad"&&key!="at"&&key!="engineer"&&key!="sniper")painted[key+"-wreck"]=Raw(key+"-wreck");
  }
  LoadTankLayers();
  foreach(var key in new[]{"plate","harbor"})painted[key]=Raw(key);
  painted["ore"]=OreSprites();
  foreach(var t in Campaign.Types.Where(t=>t.Foot)){
   painted["fallen-"+t.Key]=FallenSheet(painted[t.Key]);painted["fallen-"+t.Key+"-paint"]=FallenSheet(painted[t.Key+"-paint"]);
  }
  foreach(var effect in new[]{"chimney","steam","mill","weld","diesel"})EnsureAmbient(effect);
  foreach(var effect in new[]{"flame","soot"})EnsureHazard(effect);
  EnsureBlastArt();
  painted.Add("removed-building",new Bitmap(FrameSize,FrameSize,System.Drawing.Imaging.PixelFormat.Format32bppPArgb));
  painted.Add("removed-building-paint",new Bitmap(FrameSize,FrameSize,System.Drawing.Imaging.PixelFormat.Format32bppPArgb));
  Restart();pulse.Interval=15;pulse.Tick+=(s,e)=>{double now=watch.Elapsed.TotalSeconds;double elapsed=now-last;float dt=(float)Math.Min(.12,elapsed);last=now;
   if(RiverbendScenePlaying||RiverbendResultPlaying)AdvanceRiverbendScene(elapsed,ContainsFocus);
   else if(ChapterScenePlaying||HarborDepartureResult)AdvanceChapterScene(elapsed,ContainsFocus);
   else{
   if(!briefing&&!paused&&!world.Won&&!world.Lost){if(ContainsFocus){TickCameraControls(dt);if(Control.MouseButtons==MouseButtons.None&&!rightPending&&cameraKeys.Count==0)PollEdgeScroll(dt);else edgeDwell=0;}else edgeDwell=0;AdvanceMatch(elapsed);selection.RemoveAll(o=>!o.Alive||o.Carrier!=null);if(followUnit!=null){if(followUnit.Alive)CenterAt(followUnit.P);else followUnit=null;}}
   if(briefing||paused||world.Won||world.Lost)simulation.Reset();
   if(modelReview&&modelSpin){modelClock+=dt;if(!modelBuildings&&!modelWalking&&!modelEntrenched&&!modelAiming)modelAngle=(modelAngle+dt*5)%64;hudDirty=true;}
   if(!briefing&&!paused&&(world.Won||world.Lost)&&world.VisualTime-world.OutcomeAt<6)world.TickVisuals(dt*speed);
   if(!briefing&&!paused){BeginHarborDeparture();BeginRiverbendDeparture();}
   }
   if(ContainsFocus)UpdateBattleCursor();
   if(!briefing||hudDirty)Invalidate();};SetFullscreen(true);UpdateBattleCursor(true);pulse.Start();
  Deactivate+=(s,e)=>{mapDragging=false;rightPending=rightDragging=false;cameraKeys.Clear();if(zHeld){zHeld=false;planningPinned=planned.Count>0;}ResetEdgeScroll();frontEnterHeld=escapeHeld=false;tHeld=false;selectType=null;if(!briefing&&!ChapterScenePlaying&&!world.Won&&!world.Lost)paused=true;drag=false;panning=false;Capture=false;};
 }
 bool resourcesReleased;
 protected override void Dispose(bool disposing){
  if(disposing){ClearRiverGroundCache();if(relayBriefMap!=null){relayBriefMap.Dispose();relayBriefMap=null;}if(islandBriefMap!=null){islandBriefMap.Dispose();islandBriefMap=null;}if(trainingBriefMap!=null){trainingBriefMap.Dispose();trainingBriefMap=null;}if(riverBriefMap!=null){riverBriefMap.Dispose();riverBriefMap=null;}if(projectedRadar!=null){projectedRadar.Dispose();projectedRadar=null;}if(riverTextures!=null){foreach(var texture in riverTextures)texture.Dispose();riverTextures=null;}if(riverView!=null){riverView.Dispose();riverView=null;}}
  if(disposing&&!resourcesReleased){resourcesReleased=true;ReleaseBlastFragments();ReleaseBattleCursors();pulse.Stop();pulse.Dispose();if(timerPrecision)timeEndPeriod(1);ground.Dispose();ClearTerrainRasters();foreach(var patch in terrainPatches)patch.Image.Dispose();radarGround.Dispose();if(terrainView!=null)terrainView.Dispose();ReleaseFrame();ClearLabelCache();ClearStampCache();foreach(var b in painted.Values)b.Dispose();foreach(var f in fonts.Values)f.Dispose();}
  base.Dispose(disposing);
 }
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int length);
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);
  int dark=1,caption=ColorTranslator.ToWin32(ink),text=ColorTranslator.ToWin32(pale);
  DwmSetWindowAttribute(Handle,20,ref dark,4);DwmSetWindowAttribute(Handle,35,ref caption,4);DwmSetWindowAttribute(Handle,36,ref text,4);
 }
 void Restart(){mapReview=false;mapDragging=false;modelReview=false;ResetClassicInput();ResetOriginalControls();ResetEdgeScroll();rulesOpen=false;missionPicker=false;hudDirty=true;drag=false;Capture=false;Array.Clear(bookmarks,0,bookmarks.Length);selectType=null;tHeld=false;markedPoint=null;markedEnemy=null;camera=new PointF(255,465);zoom=DefaultZoom;panning=false;syncMarch=true;queueOpen=false;queueIndex=0;world=new Campaign();selection.Clear();selection.Add(world.Units[0]);briefing=true;paused=false;help=false;army=false;place=null;attack=false;ResetMatchClock();}
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
  float fontSize=h>=56?23:w<100?12:15;
  Txt(g,text,x+inset,y+(h-fontSize-3)/2,fontSize,action==null?dim:lit?ink:pale,true);
  if(action!=null)hits.Add(new HitBox{Box=r,Run=action,Id=text});
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
  bool overlay=briefing||paused||diplomacyOpen||world.OutcomeReady||ChapterControlsLocked;
  bool refresh=hudDirty||overlay||previousOverlay||hudWorld!=world||world.Time-hudTime>=.1f;
  if(refresh)g.Clear(ink);
  g.TranslateTransform(ox,oy);g.ScaleTransform(scale,scale);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.Bilinear;
  drawingClient=true;
  if(briefing){hits.Clear();DrawFront(g);hudDirty=false;}
  else if(RiverbendScenePlaying||RiverbendResultPlaying){hits.Clear();DrawBattle(g);DrawRiverbendScene(g);hudDirty=false;}
  else if(ChapterScenePlaying||HarborDepartureResult){hits.Clear();DrawBattle(g);DrawChapterScene(g);hudDirty=false;}
  else if(HarborDeparturePending||RiverbendDeparturePending){
   hits.Clear();DrawBattle(g);Metal(g,MW,0,W-MW,H);Txt(g,HarborDeparturePending?"港区守军已清除":"东岸封锁已解除",MW+15,26,16,gold,true);hudDirty=false;
  }
  else{
  DrawBattle(g);
  if(refresh){
   hits.Clear();DrawHud(g);hudHitCount=hits.Count;hudTime=world.Time;hudWorld=world;hudDirty=false;
  }else if(hits.Count>hudHitCount)hits.RemoveRange(hudHitCount,hits.Count-hudHitCount);
  DrawRelayInterface(g);DrawAdvancedCommands(g);if(!world.OutcomeReady||!world.FirstChapter)DrawCoach(g);DrawCardHint(g);DrawPointerHint(g);if(overlay)Overlay(g);
  }
  if(!briefing&&paused&&ChapterControlsLocked)DrawChapterPause(g);
  drawingClient=false;previousOverlay=overlay;
  g.Flush(FlushIntention.Sync);
  if(frameDC==IntPtr.Zero){e.Graphics.DrawImageUnscaled(frameBuffer,0,0);return;}
  var target=e.Graphics.GetHdc();bool copied;
  try{copied=BitBlt(target,0,0,frameBuffer.Width,frameBuffer.Height,frameDC,0,0,0x00CC0020);}
  finally{e.Graphics.ReleaseHdc(target);}
  if(!copied)e.Graphics.DrawImageUnscaled(frameBuffer,0,0);
 }
 Color Team(int s){return UnitColor(s);}
 string BattleSummary(){
  return "作战用时 "+TimeSpan.FromSeconds(DisplayBattleSeconds).ToString(@"mm\:ss")+"　｜　存活部队 "+world.Live(0).Count(o=>!o.T.Building);
 }
 void Overlay(Graphics g){
  hits.Clear();if(keyboardOpen){Rect(g,0,0,W,H,Color.FromArgb(177,ink));DrawKeyboard(g);return;}if(diplomacyOpen){Rect(g,0,0,W,H,Color.FromArgb(177,ink));DrawDiplomacy(g);return;}Rect(g,0,0,W,H,Color.FromArgb(177,ink));var r=new RectangleF(285,172,900,550);Plate(g,r,C("#1f302f"),C("#a2b398"));Rect(g,286,205,6,112,gold);
  var mission=MissionBriefing.For(world);
  Txt(g,mission.Code,324,203,14,gold,true);
  string title=world.Won?(world.Tutorial&&!world.FirstChapter?"训练完成":"行动成功"):world.Lost?"行动终止":help?"任务简报":"指挥已暂停";
  Txt(g,title,323,245,49,pale,true);Line(g,329,325,1140,325,C("#5d7265"));
  Txt(g,mission.Title,941,275,21,gold,true);
  if(world.Won||world.Lost){
   string[] lines=world.Won?new[]{"敌方已被歼灭或投降，行动结束。",BattleSummary()}:
    new[]{"我方单位与建筑已全部损失，本次行动结束。","先保护指挥部，再恢复采矿和生产，再消灭敌方部队和基地。"};
   if(world.RelayMission)lines=world.Won?new[]{"中继信号恢复，断线前的命令记录已送回指挥部。","周砚：签发时间早于断线，签名是贺峻。","许岑：把原件留好。下一站，石门电站。"}:new[]{world.RelayFailure,"保护中继设备，保留工兵与重建能力后再组织夺控。"};
   else if(world.Won)lines=StoryVictoryLines(lines);
   for(int i=0;i<lines.Length;i++)Txt(g,lines[i],328,347+i*41,17,i==0?gold:pale);
  }else DrawMissionSummary(g,mission);
  Button(g,328,553,241,49,world.Won||world.Lost?"返回选关":"返回战场",()=>{if(world.Won||world.Lost)OpenMissions();else{paused=false;help=false;}},!(world.FirstChapter&&world.Won));
  if(!world.Won&&!world.Lost){Button(g,328,615,391,36,"键盘与鼠标",OpenKeyboard);Button(g,741,615,391,36,"生产队列",()=>{paused=false;help=false;OpenQueue();});}
  if(!world.Won&&!world.Lost)DrawMatchSpeed(g);
  if(world.FirstChapter&&world.Won)Button(g,594,553,205,49,"下一关 · 河湾镇",StartRiverMission,true);
  else if(world.Tutorial&&world.Won)Button(g,594,553,205,49,"预览新地图",OpenMapReview);
  if(!briefing&&!world.Won&&!world.Lost){Button(g,594,553,182,49,"重开本关",()=>{if(world.FirstChapter)StartFirstChapter();else if(world.RelayMission)StartRelayMission();else if(world.IslandMission)StartIslandMission();else if(world.River!=null)StartRiverMission();else StartMission(world.Tutorial,world.Assisted);});Button(g,796,553,170,49,"返回选关",OpenMissions);}
  if(!briefing&&!help&&!world.Won&&!world.Lost)
   Button(g,991,553,141,49,clickToRelocateBase?"点地迁移 开":"点地迁移 关",()=>clickToRelocateBase=!clickToRelocateBase,clickToRelocateBase);
 }
 RectangleF DragRect(){return RectangleF.FromLTRB(Math.Min(start.X,mouse.X),Math.Min(start.Y,mouse.Y),Math.Max(start.X,mouse.X),Math.Max(start.Y,mouse.Y));}
 void CheckDragDistance(){if(!dragged&&Campaign.Dist(start,mouse)>(pressedUnit==null?7:10)*PickPixel)dragged=true;}
 PointF ToScreenPoint(MouseEventArgs e){return new PointF((e.X-ox)/Math.Max(.01f,scale),(e.Y-oy)/Math.Max(.01f,scale));}
 void ContextOrder(Keys mods){ContextOrderAt(mods,null,null);}
 void ContextOrderAt(Keys mods,System.Drawing.PointF? pointOverride,Piece hitOverride){
  var p=pointOverride??WorldOf(mouse);var hit=pointOverride.HasValue?hitOverride:resolvingPress?pressedUnit:HitScreen(mouse,false,mods);
  if(hit!=null&&(!hit.Alive||hit.Carrier!=null||!world.Visible(hit)))return;
  if(structureMode!=StructureMode.None){if(structureMode==StructureMode.Repair)world.ToggleRepair(hit);else world.Sell(hit);return;}
  if(AnnotationClick(p,pointOverride.HasValue?ScreenOf(p):mouse,hit,mods))return;
  if((mods&(Keys.Control|Keys.Alt))==(Keys.Control|Keys.Alt)){
   int accepted=0;foreach(var u in selection.ToArray()){if(hit!=null&&hit.Side==0){if(world.EscortUnit(u,hit))accepted++;}else if(world.GuardAt(u,p))accepted++;}
   if(accepted==0){RejectDestination();return;}attack=false;Mark(p,null,false);world.Radio=hit!=null&&hit.Side==0?"护卫目标，迎敌后归队。":"抵达指定位置后警戒。";return;
  }
  if((mods&Keys.Alt)!=0){int accepted=selection.Count(u=>world.ForceMove(u,p));if(accepted==0){RejectDestination();return;}attack=false;Mark(p,null,false);return;}
  bool shortcut=(mods&(Keys.Control|Keys.Shift))==(Keys.Control|Keys.Shift);
  if((mods&Keys.Control)!=0&&!shortcut){
   int count=0;foreach(var o in selection){if(hit!=null&&hit!=o&&o.Deployed&&world.Armed(o)){world.Attack(o,hit,true);count++;}else if(world.Suppress(o,p))count++;}attack=false;
   if(count>0){Mark(p,null,true);world.Radio=Campaign.IsRelayStation(hit)?"正在强制攻击中继站，摧毁会导致任务失败；按 S 停火。":"压制指定地面；按 S 停火。";}return;
  }
  if(MiningOrdersActive(mods)){
   var plant=hit!=null&&hit.Deployed&&hit.Side==0&&hit.T.Key=="refinery"?hit:null;
   var patch=hit==null&&world.Explored(0,p)?world.OreAt(p):null;
   if(plant!=null||patch!=null){var destination=plant!=null?Campaign.DockPoint(plant):patch.P;var available=selection.Where(u=>world.CanMoveTo(u,destination)).ToArray();if(available.Length==0){RejectDestination();return;}foreach(var u in available)world.MineOrder(u,patch,plant);world.Radio=patch!=null?"已指定矿点，装满后自动返厂。":"有载货的矿车将返回精炼厂卸货。";return;}
  }
  if(!attack&&!shortcut&&mods==Keys.None&&hit!=null){
   var engineer=selection.FirstOrDefault(u=>world.CanEngineer(u,hit));
   if(engineer!=null){if(!world.EngineerOrder(engineer,hit))RejectDestination();return;}
   var passenger=selection.FirstOrDefault(u=>world.CanBoard(u,hit));
   if(passenger!=null){if(!world.Board(passenger,hit))RejectDestination();return;}
   var repairers=selection.Where(u=>world.CanRepairVehicle(u,hit)).ToArray();
   if(repairers.Length>0){foreach(var u in repairers)world.RepairOrder(u,hit);world.Radio="维修车前往修复目标。";return;}
  }
  if(!attack&&!shortcut&&mods==Keys.None&&hit!=null&&hit.T.Garrison&&(hit.Side<0||hit.Side==0)&&selection.Any(u=>u.T.Foot)){
   int entered=0;foreach(var unit in selection.ToArray())if(world.EnterHouse(unit,hit))entered++;
   if(entered==0)world.Radio=hit.Side<0?"需要空闲工兵才能占领中立房屋。":"房屋已满，或所选单位不能驻防。";return;
  }
  if(!attack&&!shortcut&&hit!=null&&hit.Side==0){
   if((mods&Keys.Shift)!=0){if(!selection.Remove(hit))selection.Add(hit);}
   else if(selection.Count==1&&selection[0]==hit){DeployClick(hit);}
   else{selection.Clear();selection.Add(hit);}
   return;
  }
  if(selection.Count==0)return;
  if(!attack&&mods==Keys.None&&hit==null&&selection.Count==1&&selection[0].T.Building){
   var structure=selection[0];
   if(clickToRelocateBase&&structure.T.Key=="hq"){world.Relocate(structure,p);return;}
   if(world.CanSetRally(structure)){
    if(!world.SetRally(structure,pointOverride??ApproachSite(mouse,p)))world.Radio="集结点不可达，已保留原集结点。";
   }else selection.Clear();
   return;
  }
  attack|=shortcut;Issue(pointOverride.HasValue?p:ApproachSite(mouse,p),hit);
 }
 Piece markedEnemy;PointF? markedPoint;float markTime;bool markHostile;
 void Mark(PointF p,Piece enemy,bool hostile){markedPoint=p;markedEnemy=enemy;markTime=world.Time;markHostile=hostile;if(hostile&&enemy!=null){flashTarget=enemy;flashAt=watch.Elapsed.TotalSeconds;}}
 void Issue(PointF p,Piece enemy){
  if(Campaign.IsRelayStation(enemy)){world.Radio=enemy.Side==0?world.RelayStatus:"任务设施需要工兵占领；请勿摧毁。";return;}
  var troops=selection.Where(o=>o.Deployed&&!o.T.Building).ToArray();
  if(enemy!=null&&enemy.Side>0){
   var fighters=selection.Where(o=>o.Deployed&&world.Armed(o)).ToArray();
   if(fighters.Length==0){world.Radio="所选部队没有可攻击的武器。";return;}
   else{foreach(var o in fighters)world.Attack(o,enemy);Mark(enemy.P,enemy,true);world.Radio="集火："+enemy.T.Name+" #"+enemy.Id;}
  }else if(troops.Length==0){world.Radio="请先点选我方部队，或拖框选择多支部队。";}
  else{
   var eligible=troops.Where(u=>world.CanMoveTo(u,p)).ToArray();if(eligible.Length==0){RejectDestination();return;}
   world.Radio=attack?"部队将沿途交战，向指定地点推进。":"部队正前往指定地点。";int failed=troops.Length-world.MarchTo(eligible,p,attack,syncMarch);
   if(failed==troops.Length){RejectDestination();return;}Mark(p,null,attack);if(failed>0)world.Radio=failed+" 支部队的目标位置被阻挡。请点建筑外的空地。";
  }
  attack=false;
 }
 protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);edgeDwell=0;hudDirty=true;mouse=ToScreenPoint(e);UpdateBattleCursor(true);
  if(e.Button==MouseButtons.Left||e.Button==MouseButtons.Right){var b=hits.LastOrDefault(h=>h.Box.Contains(mouse));if(b!=null){if(b.Id!="command-type")selectType=null;if(e.Button==MouseButtons.Left)b.Run();else if(b.Right!=null)b.Right();Invalidate();return;}}
  if(ChapterControlsLocked)return;
  if(mapReview){if(mapViewport.Contains(mouse)&&(e.Button==MouseButtons.Left||e.Button==MouseButtons.Middle||e.Button==MouseButtons.Right)){mapDragging=true;mapDragPoint=mouse;Capture=true;}return;}
  if(briefing||paused||diplomacyOpen||world.Won||world.Lost||mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>MapTop+MH)return;
  var p=WorldOf(mouse);
  if(e.Button==MouseButtons.Middle){panning=true;panPoint=mouse;Capture=true;return;}

  if(e.Button==MouseButtons.Right){BeginRightDrag();return;}
  if(e.Button!=MouseButtons.Left)return;selectType=null;rankSeed=null;
  if(place!=null){if(world.Build(place,p))place=null;return;}
  var unit=HitScreen(mouse,true,ModifierKeys);
  if(e.Clicks>=2&&ModifierKeys==Keys.None&&unit!=null&&unit.Side==0&&world.SetPrimary(unit)){selection.Clear();selection.Add(unit);drag=false;return;}
  start=mouse;pressedUnit=unit;drag=true;dragged=false;Capture=true;
 }
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);var old=mouse;mouse=ToScreenPoint(e);if(ChapterControlsLocked)return;edgeArmed=true;if(mapReview){if(mapDragging){RiverPan(mapDragPoint.X-mouse.X,mapDragPoint.Y-mouse.Y);mapDragPoint=mouse;}hudDirty=true;Invalidate();return;}if(briefing&&old!=mouse)hudDirty=true;if(old!=mouse&&(old.X>=MW||mouse.X>=MW||old.Y<MapTop||mouse.Y<MapTop||old.Y>=MapTop+MH||mouse.Y>=MapTop+MH))hudDirty=true;if(rightPending){MoveRightDrag();UpdateBattleCursor(true);Invalidate();return;}if(panning){Pan(panPoint.X-mouse.X,panPoint.Y-mouse.Y);panPoint=mouse;Invalidate();return;}if(drag)CheckDragDistance();UpdateBattleCursor();}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(ChapterControlsLocked)return;hudDirty=true;if(e.Button==MouseButtons.Right&&rightPending){mouse=ToScreenPoint(e);EndRightDrag();UpdateBattleCursor(true);Invalidate();return;}if(mapReview){mapDragging=false;Capture=false;return;}if(panning){panning=false;Capture=false;return;}if(e.Button!=MouseButtons.Left||!drag)return;drag=false;Capture=false;mouse=ToScreenPoint(e);
  CheckDragDistance();
  if(!dragged&&(mouse.X<0||mouse.X>=MW||mouse.Y<MapTop||mouse.Y>=MapTop+MH)){pressedUnit=null;return;}
  if(dragged){pressedUnit=null;attack=false;if((ModifierKeys&Keys.Shift)==0)selection.Clear();var r=DragRect();foreach(var u in world.Live(0).Where(u=>!u.T.Building&&r.Contains(ScreenOf(u.P))))if(!selection.Contains(u))selection.Add(u);return;}
  resolvingPress=true;try{ContextOrder(ModifierKeys);}finally{resolvingPress=false;pressedUnit=null;}UpdateBattleCursor(true);Invalidate();

 }
 [STAThread]static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new CommandWindow());}
}
}
