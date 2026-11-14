using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 public bool RelayMission {get{return River!=null&&River.IsRelay;}}
 public static readonly TypeInfo RelayType=new TypeInfo("relay","中继控制站","",720,1800,0,0,150,0,0,0);
 public Piece RelayStation {get;private set;}
 public const float RelayDuration=360;
 public const int RelaySupportFunds=7500;
 public float RelayProgress {get;private set;}
 public string RelayFailure {get;private set;}
 public bool RelaySupportResolved {get;private set;}
 public bool RelaySupportGranted {get;private set;}
 public float RelaySupportAt {get;private set;}
 public string RelaySupportNotice {get;private set;}
 int lastRelaySide=1,relayMilestone;bool relayEngineerWarning;
 public static bool IsRelayStation(Piece piece){return piece!=null&&piece.T.Key=="relay";}
 public string RelayStatus {get{return Lost?RelayFailure:Won?"区域通信已恢复":RelayStation==null||!RelayStation.Alive?"中继站已毁":RelayStation.Side==0?"正在恢复通信":RelayProgress>0?"失守暂停 · 重新占领":"派工兵占领中继站";}}
 Piece RelaySpawn(string key,int side,PointF origin,float dx,float dy){return Spawn(key,side,origin.X+dx*Tile,origin.Y+dy*Tile);}
 void SetupRelayMission(){
  Ore.Clear();foreach(var p in River.Ore)Ore.Add(new OrePatch((p.X+.5f)*Tile,(p.Y+.5f)*Tile,2400));
  var p0=Home(0);RelaySpawn("mcv",0,p0,0,0);RelaySpawn("squad",0,p0,2,-3);RelaySpawn("squad",0,p0,3.5f,-2);RelaySpawn("engineer",0,p0,-2,-3);RelaySpawn("ifv",0,p0,4,2);
  foreach(var h in River.Houses)Spawn(h.Kind,-1,(h.Lot.X+h.Lot.Width*.5f)*Tile,(h.Lot.Y+h.Lot.Height*.5f)*Tile);
  var p1=Home(1);RelaySpawn("hq",1,p1,0,0);RelaySpawn("power",1,p1,-7,-5);RelaySpawn("power",1,p1,7,-5);
  var refinery=RelaySpawn("refinery",1,p1,-9,7);refinery.StarterDelivered=true;
  var miner=Spawn("miner",1,DockPoint(refinery).X,DockPoint(refinery).Y);miner.DepartingDock=refinery;
  RelaySpawn("barracks",1,p1,5,6);RelaySpawn("factory",1,p1,10,0);RelaySpawn("post",1,p1,-7,-1);
  for(int i=0;i<4;i++)RelayGuard("squad",new PointF(p1.X+(-3+i)*Tile,p1.Y+4*Tile));
  RelaySpawn("tank",1,p1,1,8);RelaySpawn("ifv",1,p1,3,9);RelaySpawn("engineer",1,p1,-4,5);
  foreach(var origin in new[]{RiverbendMap.RelayWorld(53,48),RiverbendMap.RelayWorld(58,23)}){
   RelaySpawn("post",1,origin,5,0);RelayGuard("squad",new PointF(origin.X-2*Tile,origin.Y));RelayGuard("squad",new PointF(origin.X-2*Tile,origin.Y+2*Tile));RelayGuard("at",new PointF(origin.X+3*Tile,origin.Y+2*Tile));
  }
  var station=RiverbendMap.RelayWorld(53,21);RelayStation=Spawn("relay",1,station.X,station.Y);
  Radio="周砚：中继站坐标已标注。点击左上角“定位中继站”查看目标，再返回基地准备工兵与护卫。";
 }
 void RelayGuard(string key,PointF p){var unit=Spawn(key,1,p.X,p.Y);Guard(unit);}
 void TickRelayObjective(float dt){
  if(RelayStation==null||!RelayStation.Alive){Lost=true;RelayFailure="中继控制站被摧毁，通信备份丢失。";return;}
  if(!Units.Any(u=>u.Alive&&u.Side==0&&!IsRelayStation(u))){Lost=true;RelayFailure="先遣队已失去全部部队与基地。";return;}
  if(RelayStation.Side!=0&&!Units.Any(u=>u.Alive&&u.Side==0&&(u.T.Key=="engineer"||u.T.Key=="barracks"||u.T.Key=="hq"||u.T.Key=="mcv"||u.T.Key=="factory"))&&!Enumerable.Range(0,2).Any(i=>Construction(0,i)!=null&&(Construction(0,i).Type.Key=="barracks"||Construction(0,i).Type.Key=="factory"))){Lost=true;RelayFailure="已失去工兵及重建生产的能力，无法继续夺控。";return;}
  if(lastRelaySide!=RelayStation.Side){
   lastRelaySide=RelayStation.Side;string message=lastRelaySide==0?"中继站已夺控：恢复通信中，请守住入口并准备备用工兵。":lastRelaySide<0?"敌军已放弃中继站，仍需工兵接管。":"中继站失守：恢复进度保留，派工兵重新夺回。";Radio=message;SetEvent(RelayStation.P,message);
  }
  if(RelayStation.Side==0)RelayProgress=Math.Min(RelayDuration,RelayProgress+dt);
  int milestone=(int)(RelayProgress/90);if(milestone>relayMilestone){relayMilestone=milestone;if(milestone==1)Radio="周砚：备用链路已接通，正在核对调度记录。";else if(milestone==3)Radio="周砚：原始签名读取中，继续保持站区控制。";}
  bool engineers=Units.Any(u=>u.Alive&&u.Side==0&&u.T.Key=="engineer");if(!engineers&&!relayEngineerWarning&&RelayStation.Side!=0){relayEngineerWarning=true;Radio="工兵已损失，请在兵营补造；登车工兵仍可按 D 卸载。";}if(engineers)relayEngineerWarning=false;
  ResolveRelaySupport();
  if(RelayStation.Side==0&&RelayProgress>=RelayDuration-.001f){Won=true;Radio="区域通信恢复。周砚确认封路命令来自贺峻的统辖部；下一步是石门电站。";}
 }
 void ResolveRelaySupport(){
  if(!RelayMission||RelaySupportResolved||RelayProgress<RelayDuration*.5f)return;
  // Resolve once against cumulative restoration, including a large simulation step.
  // Defeated factions stay defeated; aid uses the normal economy and production.
  RelaySupportResolved=true;RelaySupportAt=Time;
  RelaySupportGranted=!HasSurrendered(1)&&CanStillResist(1);
  if(RelaySupportGranted){
   Funds[1]+=RelaySupportFunds;
   RelaySupportNotice="截获通报：通信恢复已达 50%，统辖部向敌方拨付 "+RelaySupportFunds+" 经费。敌军正在组织反夺编队。";
  }else RelaySupportNotice="通信恢复已达 50%。敌方已失去抵抗能力，统辖部取消了本次经济支援。";
  Radio=RelaySupportNotice;SetEvent(RelayStation.P,RelaySupportNotice);
 }
}
}
