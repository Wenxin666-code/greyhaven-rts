using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {partial class CommandWindow {
 RectangleF relayMarkerHit;
 void PrepareRelayArt(){
  // One mission-specific pack fits below the existing 64 MiB decoded-frame cap.
  // Decode its moving layers during level entry, not on successive combat frames.
  EnsureB08("b08/relay/background");
  for(int level=1;level<=3;level++){EnsureB08("b08/relay/damage-"+level);EnsureB08("b08/relay/damage-"+level+"-background");}
  for(int frame=0;frame<BuildingV08.IdleFrames;frame++)B08Frame("relay","idle",frame);
 }
 void DrawRelayObjective(Graphics g){
  relayMarkerHit=RectangleF.Empty;if(!world.RelayMission)return;
  // An intelligence marker is not terrain reconnaissance: do not reveal fog or radar.
  var station=world.RelayStation;if(station==null)return;
  var p=ScreenOf(station.P);
  if(world.Visible(station)){var box=BuildingRect(Campaign.Footprint(station.T,station.P),p,zoom);var spriteBounds=SpriteInk("relay",B08Source());p.Y=box.Top+box.Height*spriteBounds.Top/FrameSize-24;}
  float dx=p.X-MW*.5f,dy=p.Y-H*.5f;
  bool inside=p.X>110&&p.X<MW-110&&p.Y>84&&p.Y<H-65;
  if(!inside){float k=Math.Min((MW*.5f-110)/Math.Max(1,Math.Abs(dx)),(H*.5f-84)/Math.Max(1,Math.Abs(dy)));p=new PointF(MW*.5f+dx*k,H*.5f+dy*k);}
  // Keep the directional label clear of the objective card and support bulletin.
  if(!inside&&p.X<490&&p.Y<246)p.Y=246;
  if(!inside&&world.RelaySupportResolved&&world.Time-world.RelaySupportAt<30&&p.X>MW-465&&p.Y<181)p.Y=181;
  PixelCorners(g,new RectangleF(p.X-13,p.Y-13,26,26),gold);
  if(!inside){
   float length=(float)Math.Sqrt(dx*dx+dy*dy);float ux=dx/Math.Max(1,length),uy=dy/Math.Max(1,length);
   var arrow=new[]{new PointF(p.X+ux*10,p.Y+uy*10),new PointF(p.X-ux*7-uy*6,p.Y-uy*7+ux*6),new PointF(p.X-ux*3,p.Y-uy*3),new PointF(p.X-ux*7+uy*6,p.Y-uy*7-ux*6)};
   using(var brush=new SolidBrush(gold))g.FillPolygon(brush,arrow);
  }else{Line(g,p.X-5,p.Y,p.X+5,p.Y,gold);Line(g,p.X,p.Y-5,p.X,p.Y+5,gold);}
  float labelX=p.X>MW-225?p.X-204:p.X+22;
  relayMarkerHit=new RectangleF(labelX,p.Y-24,182,49);
  Rect(g,labelX,p.Y-24,182,49,Color.FromArgb(232,ink));Rect(g,labelX,p.Y-24,2,49,gold);
  Txt(g,"北岭中继站",labelX+10,p.Y-20,14,gold,true);
  Txt(g,inside?(station.Side==0?"我方已夺控 · 守住恢复进度":world.Explored(0,station.P)?"任务目标 · 工兵夺控":"情报坐标 · 周边尚未侦察"):"目标方向 · 点击定位",labelX+10,p.Y+2,11,pale);
 }
 void LocateRelay(){
  if(world.RelayStation==null)return;
  followUnit=null;CenterAt(world.RelayStation.P);hudDirty=true;
 }
 void ReturnFromRelay(){GoHome();if(!world.Live(0).Any(u=>u.T.Key=="hq"||u.T.Key=="mcv"))CenterAt(world.Home(0));hudDirty=true;}
 void RelayGuidance(out string primary,out string secondary){
  var station=world.RelayStation;bool engineer=world.Live(0).Any(u=>u.T.Key=="engineer");
  if(station.Side==0){primary="守住站区入口，保留护卫和备用工兵。";secondary="失守只暂停进度；保护中继站，避免强制开火。";}
  else if(world.RelayProgress>0){primary="中继站失守：进度保留，重新护送工兵夺回。";secondary=engineer?"工兵登车时先按 D 卸载，再点中继站。":"先在兵营补造工兵，再随护卫一起前往。";}
  else if(!world.Live(0).Any(u=>u.T.Key=="hq")){primary=world.Live(0).Any(u=>u.T.Key=="mcv")?"先选中工程车，按 D 展开基地。":"在车辆工厂补造工程车，恢复基地。";secondary="目标在地图北部；点击下方按钮查看情报坐标。";}
  else if(!world.Live(0).Any(u=>u.T.Key=="refinery")){primary="建电站、精炼厂，建立采矿与部队生产。";secondary="准备工兵与护卫，再向标注的北岭中继站推进。";}
  else if(!engineer){primary="在兵营补造工兵，由战斗部队护送。";secondary="战车不能占领目标；工兵登车后须按 D 卸载。";}
  else if(!world.Explored(0,station.P)){primary="沿道路侦察北部站区，工兵跟随护卫前进。";secondary="金色标记是目标坐标，周边守军需要实地侦察。";}
  else{primary="先清理守军，再选工兵左键点击中继站。";secondary="占领会消耗工兵；请勿强制攻击中继设备。";}
 }
 void DrawRelayInterface(Graphics g){
  if(!world.RelayMission||world.RelayStation==null)return;
  // Register after the cached HUD: DrawBattle hit boxes are cleared on HUD refresh.
  if(!relayMarkerHit.IsEmpty)hits.Add(new HitBox{Id="relay-locator",Box=relayMarkerHit,Run=LocateRelay});
  var card=new RectangleF(16,42,386,176);
  Rect(g,card.X,card.Y,card.Width,card.Height,Color.FromArgb(235,ink));Rect(g,16,42,3,176,gold);
  hits.Add(new HitBox{Id="relay-objective-panel",Box=card,Run=()=>{}});
  Txt(g,"04 / 夺控北岭中继站",29,49,14,pale,true);
  Txt(g,world.RelayStatus+"   "+(int)(world.RelayProgress*100/Campaign.RelayDuration)+"%",29,73,12,world.RelayStation.Side==0?teal:gold);
  Rect(g,29,94,356,3,C("#425252"));Rect(g,29,94,356*world.RelayProgress/Campaign.RelayDuration,3,teal);
  string primary,secondary;RelayGuidance(out primary,out secondary);
  Txt(g,primary,29,106,12,pale);Txt(g,secondary,29,127,11,dim);
  Button(g,29,153,174,30,"定位中继站",LocateRelay,true);Button(g,213,153,174,30,"返回基地",ReturnFromRelay);
  string support=world.RelaySupportResolved?(world.RelaySupportGranted?"50% 记录：敌方已获 "+Campaign.RelaySupportFunds+" 经费，仅此一次。":"50% 记录：敌方已失去抵抗能力，支援取消。"):(world.HasSurrendered(1)?"敌方已投降；接管中继站后继续恢复通信。":"情报：恢复达 50% 时，敌方将获 "+Campaign.RelaySupportFunds+" 经费。 ");
  Txt(g,support,29,192,11,world.RelaySupportGranted?gold:dim);
  if(world.RelaySupportResolved&&world.Time-world.RelaySupportAt<30&&!world.OutcomeReady){
   float x=MW-410;
   Rect(g,x,44,394,114,Color.FromArgb(242,ink));Rect(g,x,44,4,114,gold);
   hits.Add(new HitBox{Id="relay-support-bulletin",Box=new RectangleF(x,44,394,114),Run=()=>{}});
   Txt(g,"截获通报 / 通信恢复达到 50%",x+16,53,12,gold,true);
   Txt(g,world.RelaySupportGranted?"敌方收到统辖部军费 +"+Campaign.RelaySupportFunds:"敌方已失去抵抗能力，支援取消",x+16,77,16,pale,true);
   Txt(g,world.RelaySupportGranted?"敌军将通过现有兵营和工厂补充兵力。":"继续守住中继站，完成剩余通信恢复。",x+16,106,12,pale);
   Txt(g,"一次性事件；失守后重新占领不会再次触发。",x+16,130,11,dim);
  }
 }
}}
