using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {public partial class Campaign {
 enum CampaignRole { Standard,NorthLine,Mining,Transport,LocalDefense,Frontier }
 bool StoryBattle {get{return River!=null&&!FirstChapter&&!RelayMission;}}
 CampaignRole StoryRole(int side){return StoryBattle&&side>0&&side<FactionCount?(CampaignRole)side:CampaignRole.Standard;}
 public string StoryFactionName(int side){
  if(side<0)return "中立";
  if(side==0)return "我方先遣队";
  if(Tutorial||RelayMission)return side==1?"北线驻防军":FactionName(side);
  return StoryBattle&&side<FactionCount?new[]{"我方先遣队","北线驻防军","白石矿务警卫队","铁轨运输卫队","南岸地方防卫队","边境独立旅"}[side]:FactionName(side);
 }
 public string StoryFactionTask(int side){
  switch(StoryRole(side)){
   case CampaignRole.NorthLine:return "装甲推进 · 交通线警戒";
   case CampaignRole.Mining:return "保护矿运 · 维修生产设施";
   case CampaignRole.Transport:return "机动夺路 · 小队出击";
   case CampaignRole.LocalDefense:return "城镇驻防 · 近邻反击";
   case CampaignRole.Frontier:return "前沿部署 · 袭扰薄弱矿路";
   default:return side==0?"恢复灰港补给线":RelayMission?"守住中继站 · 工兵反夺":Tutorial?"守住旧码头封锁点":"经营基地 · 守备出击";
  }
 }
 void StoryVehicleMix(int side,ref float cars,ref float guns){
  switch(StoryRole(side)){
   case CampaignRole.NorthLine:cars=.20f;guns=.18f;break;
   case CampaignRole.Mining:cars=.34f;guns=.18f;break;
   case CampaignRole.Transport:cars=.55f;guns=.13f;break;
   case CampaignRole.LocalDefense:cars=.32f;guns=.30f;break;
   case CampaignRole.Frontier:cars=.48f;guns=.18f;break;
  }
 }
 float StorySecondRefinery(int side){return StoryRole(side)==CampaignRole.Mining?75:MatchTempo.SecondRefinery;}
 float StorySecondFactory(int side){return StoryRole(side)==CampaignRole.LocalDefense?240:MatchTempo.SecondFactory;}
 float StorySecondBarracks(int side){return StoryRole(side)==CampaignRole.LocalDefense?165:MatchTempo.SecondBarracks;}
 float StoryRepairThreshold(int side){return StoryRole(side)==CampaignRole.Mining?.92f:StoryRole(side)==CampaignRole.LocalDefense?.86f:.8f;}
 int StoryWingMinimum(int side){var role=StoryRole(side);return role==CampaignRole.Transport||role==CampaignRole.Frontier?6:role==CampaignRole.NorthLine?8:Time<240?6:8;}
 int StoryWingMaximum(int side){var role=StoryRole(side);return role==CampaignRole.Transport||role==CampaignRole.Frontier?8:Time<360?8:12;}
 float StoryRaidInterval(int side){switch(StoryRole(side)){case CampaignRole.Mining:return 80;case CampaignRole.Transport:return 42;case CampaignRole.LocalDefense:return 90;case CampaignRole.Frontier:return 38;default:return MatchTempo.RaidInterval;}}
 float StoryWithdrawal(int side){switch(StoryRole(side)){case CampaignRole.Mining:case CampaignRole.Transport:return .60f;case CampaignRole.LocalDefense:case CampaignRole.Frontier:return .65f;default:return .5f;}}
 bool StoryRaidAllowed(int side,AiBrain brain,Piece[] known){
  var role=StoryRole(side);if(role!=CampaignRole.Mining&&role!=CampaignRole.LocalDefense)return true;
  if(Time>=170&&OreReceipts[side]>=3*LoadValue)return true;
  if(brain.Attacks.Any(t=>Time-t<100))return true;
  return known.Any(e=>e.T.Hit>0&&Live(side).Any(a=>(a.T.Key=="refinery"||a.T.Key=="miner"||a.T.Civilian)&&Dist(a.P,e.P)<480));
 }
 float StoryTargetPriority(int side,Piece target,Piece[] known){
  var role=StoryRole(side);string key=target.T.Key;
  float priority=key=="refinery"?400:key=="miner"?330:key=="factory"?260:key=="hq"?200:0;
  if(role==CampaignRole.NorthLine)priority=key=="factory"?420:key=="hq"?360:key=="refinery"?240:key=="miner"?80:0;
  if(role==CampaignRole.Transport)priority=key=="miner"?420:key=="refinery"?340:key=="factory"?220:0;
  if(role==CampaignRole.Frontier){
   priority=key=="miner"?600:key=="refinery"?500:priority;
   priority-=Math.Min(320,known.Count(e=>e!=target&&e.Side==target.Side&&e.T.Hit>0&&TargetDistance(e,target)<ReferenceBalance.Range(e)+3*Tile)*80);
  }
  if(role==CampaignRole.Mining||role==CampaignRole.LocalDefense){
   var assets=Live(side).Where(a=>role==CampaignRole.Mining?a.T.Key=="miner"||a.T.Key=="refinery":a.T.Civilian||a.T.Key=="hq").ToArray();
   if(target.T.Hit>0&&assets.Any(a=>TargetDistance(target,a)<480))priority+=700;
  }
  return priority;
 }
 float StoryOpponentPressure(int side,int enemy,Piece[] known){
  var role=StoryRole(side);if(role==CampaignRole.Standard)return 0;
  var assets=Live(side).Where(a=>a.T.Building||a.T.Key=="miner").ToArray();
  return known.Any(e=>e.Side==enemy&&e.T.Hit>0&&assets.Any(a=>TargetDistance(e,a)<480))?110:0;
 }
 PointF StoryBuildDirection(int side,PointF original){
  if(StoryRole(side)!=CampaignRole.Frontier)return original;
  var h=Home(side);var point=Brain(side).FieldPoint;
  if(point.IsEmpty)return original;float distance=Math.Max(Tile,Dist(h,point));
  return new PointF(h.X+(point.X-h.X)*10*Tile/distance,h.Y+(point.Y-h.Y)*10*Tile/distance);
 }
 PointF StoryGuardCenter(int side,int index,PointF original){
  if(StoryRole(side)!=CampaignRole.Mining||index%3!=1)return original;
  var mine=Live(side).Where(u=>u.T.Key=="refinery").OrderByDescending(u=>Dist(u.P,Home(side))).FirstOrDefault();return mine==null?original:mine.P;
 }
 int StoryHousingLimit(int side){return StoryRole(side)==CampaignRole.LocalDefense?(Time<180?3:6):Time<180?2:4;}
 float StoryArmouredShare(int side){
  if(RelayMission)return .60f;
  switch(StoryRole(side)){
   case CampaignRole.NorthLine:return .60f;
   case CampaignRole.Transport:return .65f;
   case CampaignRole.LocalDefense:return .35f;
   case CampaignRole.Mining:return .45f;
   default:return .50f;
  }
 }
 float storyCheckAt;
 readonly bool[] storyContact=new bool[MaxFactions];
 public string StoryNotice {get;private set;}
 public float StoryNoticeUntil {get;private set;}
 void TickStoryIntelligence(){
  if(!StoryBattle||Time<storyCheckAt||Time<StoryNoticeUntil||BasinOperation is { CommandTaken:false } or { StillBriefing:true })return;storyCheckAt=Time+2;
  var scouts=BasinOperation==null?null:Live(0).Where(o=>!o.T.Building).ToArray();
  for(int side=1;side<FactionCount;side++){
   if(storyContact[side]||!Units.Any(u=>u.Alive&&u.Side==side&&u.Carrier==null&&Visible(u)&&
       (scouts==null||scouts.Any(scout=>Dist(scout.P,u.P)<=Sight(scout)))))continue;
   storyContact[side]=true;
   string[] intel={"","这支北线驻防军重视交通线，注意装甲与路口警戒。","白石警卫队主要保护采矿和生产设施，受袭后会回防。","铁轨卫队偏好机动小队，注意交通节点和侧翼。","南岸防卫队依托城镇据守，侦察房屋归属再推进。","独立旅更愿意袭扰薄弱矿路，保护落单矿车。"};
   string message=BasinOperation!=null?BasinOperation.ContactReport(side):
    RiverbendOperation!=null&&side==2?"白石警卫队重视矿运，出击时会盯着对手的矿车和精炼厂。":intel[side];
   StoryNotice=message;StoryNoticeUntil=Time+14;Radio="许岑："+message;break;
  }
 }
}}
