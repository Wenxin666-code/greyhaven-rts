using System;
using System.Drawing;
using System.Linq;
using Greyhaven;
class CampaignStoryChecks {
 static int Main(){try{Campaign.CheckStoryAi();Campaign.CheckTerritorialAi();return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
namespace Greyhaven {public partial class Campaign {
 static void StoryAssert(bool ok,string message){if(!ok)throw new Exception(message);}
 static Campaign StoryFixture(bool island,int side,int army){
  var w=new Campaign(false,false,true,island);w.Units.Clear();w.Ore.Clear();w.Time=400;var home=w.Home(side);
  for(int s=0;s<w.FactionCount;s++)if(s!=side)w.Spawn("mcv",s,w.Home(s).X,w.Home(s).Y);
  w.Spawn("hq",side,home.X,home.Y);w.Spawn("power",side,home.X-7*Tile,home.Y-5*Tile);w.Spawn("power",side,home.X+7*Tile,home.Y-5*Tile);
  w.Spawn("barracks",side,home.X-6*Tile,home.Y);w.Spawn("factory",side,home.X+6*Tile,home.Y);
  w.Spawn("refinery",side,home.X-6*Tile,home.Y+6*Tile).StarterDelivered=true;w.Spawn("refinery",side,home.X+6*Tile,home.Y+6*Tile).StarterDelivered=true;
  for(int i=0;i<army;i++){var p=w.AiOpenPoint(new PointF(home.X+(-5+i%10)*Tile,home.Y+(-10+i/10)*Tile));w.Spawn(i%3==0?"tank":i%3==1?"ifv":"squad",side,p.X,p.Y);}
  w.PrepareBattleNavigation();return w;
 }
 internal static void CheckStoryAi(){
  var w=new Campaign(true);var sentry=w.Live(1).Single();var anchor=sentry.GuardHome;
  StoryAssert(anchor.HasValue&&!w.GuardAllows(sentry,w.Live(0).First()),"tutorial sentry must not chase the landing base");
  for(int i=0;i<120*30;i++)w.Tick(1f/30);
  StoryAssert(w.Live(1).Count()==1&&Dist(sentry.P,anchor.Value)<Tile&&!w.Lost,"tutorial silently escalated or guard left checkpoint");

  w=new Campaign(false,false,true,true);w.TickStoryIntelligence();StoryAssert(w.StoryNotice==null,"intelligence revealed unseen factions");
  var observed=w.Spawn("squad",3,w.Home(0).X+2*Tile,w.Home(0).Y);w.Reveal();w.Time=3;w.TickStoryIntelligence();
  StoryAssert(w.StoryNotice!=null&&w.StoryNotice.Contains("铁轨")&&w.storyContact.Count(v=>v)==1,"wrong first-contact identity");
  float expiry=w.StoryNoticeUntil;w.Radio="普通消息";w.Time=expiry+3;w.TickStoryIntelligence();StoryAssert(w.StoryNoticeUntil==expiry,"repeated faction introduction");
  var relay=new Campaign(false,false,false,false,true);relay.Time=300;relay.TickStoryIntelligence();StoryAssert(relay.StoryNotice==null&&relay.StoryRole(1)==CampaignRole.Standard,"story AI leaked into the relay mission");

  // With exactly enough uncommitted funds for a tank, infantry must not consume it.
  w=StoryFixture(false,1,0);var h=w.Home(1);for(int i=0;i<8;i++)w.Spawn("squad",1,h.X+i*12,h.Y-4*Tile);
  w.Spawn("ifv",1,h.X,h.Y-6*Tile);for(int i=0;i<4;i++)w.Spawn("miner",1,h.X+i*Tile,h.Y+10*Tile);
  var factory=w.Live(1).First(u=>u.T.Key=="factory");var barracks=w.Live(1).First(u=>u.T.Key=="barracks");
  int money=w.AiReserve(1)+Info("tank").Price;w.Funds[1]=money;int count=w.Units.Count;
  w.AiRecruit(1,w.Brain(1),new Piece[0]);StoryAssert(factory.Queue.Count==1&&factory.Queue.Peek().Key=="tank"&&barracks.Queue.Count==0,"rifle production starved planned armour");
  for(int i=0;i<30*30&&factory.Queue.Count>0;i++)w.UnitTick(factory,1f/30,true);
  StoryAssert(factory.Queue.Count==0&&w.Units.Count==count+1&&w.Funds[1]==money-Info("tank").Price,"armour was not produced through ordinary paid work");

  // Units already deployed elsewhere cannot fill the local assembly, even
  // when the faction has more units than the former all-army threshold.
  w=StoryFixture(false,1,0);h=w.Home(1);
  for(int i=0;i<50;i++)w.Spawn("squad",1,h.X-50*Tile-i%5*Tile,h.Y+(i/5)*Tile);
  for(int i=0;i<4;i++)w.Spawn("miner",1,h.X+i*Tile,h.Y+10*Tile);
  w.Funds[1]=12000;count=w.Units.Count;
  factory=w.Live(1).First(u=>u.T.Key=="factory");barracks=w.Live(1).First(u=>u.T.Key=="barracks");
  w.AiRecruit(1,w.Brain(1),new Piece[0]);
  StoryAssert(factory.Queue.Count==1&&barracks.Queue.Count==1&&w.Units.Count==count&&w.Funds[1]==12000,"remote army incorrectly prevented paid assembly orders");

  w=new Campaign(false,false,true);
  var barracksDoor=w.Live(1).Single(p=>p.T.Key=="barracks");
  var spawn=Middle(ExitApron(barracksDoor.T,Footprint(barracksDoor.T,barracksDoor.P)));
  var sniper=w.Spawn("sniper",1,spawn.X,spawn.Y);
  var nearbyTank=w.Spawn("tank",2,spawn.X-4*Tile,spawn.Y);
  w.Attack(sniper,nearbyTank);
  PointF unused;
  StoryAssert(!w.Exit(barracksDoor,Info("squad"),out unused)&&sniper.Path.Count>0&&
   !TrafficContact(sniper.T,sniper.Goal,Info("squad"),spawn),
   "a defending AI unit must step away from a blocked production door");

  w=new Campaign(false,false,true);
  w.RiverbendOperation.BeginOpening();
  w.Time=22;
  var brain=w.Brain(1);var rival=w.Brain(2);
  w.Spawn("tank",2,w.Home(1).X+3*Tile,w.Home(1).Y);
  w.Reveal();
  w.SmartTactics(1,true);w.SmartTactics(2,true);
  foreach(var force in new[]{brain,rival})
   StoryAssert(force.HomeGuard.Count==0&&force.FieldGuard.Count==0&&force.Wings.Count>0&&
     force.Wings.All(wing=>wing.Phase==2&&wing.Goal==wing.Objective),
     "Riverbend must dispatch direct attacks even when an enemy is near the base");
  StoryAssert(brain.Wings.SelectMany(wing=>wing.Units).All(u=>!w.RiverbendOperation.IsImpoundGuard(u)),
   "the story sentries should remain at the impound");

  var depleted=brain.Wings[0];
  var survivor=depleted.Units[0];
  foreach(var casualty in depleted.Units.Skip(1))casualty.Hp=0;
  depleted.InitialValue=survivor.T.Price;
  survivor.Hp=survivor.T.Hp*.21f;w.CancelAiMove(survivor);w.Halt(survivor);
  w.Time+=9;w.SmartTactics(1,true);
  StoryAssert(depleted.Units.SequenceEqual(new[]{survivor})&&depleted.Phase==2&&brain.Withdrawals==0&&
   w.aiMoveRequests.Any(order=>order.Unit==survivor&&order.AttackMove),"a wing above 20 percent must keep attacking");
  survivor.Hp=survivor.T.Hp*.20f;
  w.SmartTactics(1,true);
  StoryAssert(depleted.Phase==3&&brain.Withdrawals==1&&survivor.GuardReturning&&
   !w.aiMoveRequests.Last(order=>order.Unit==survivor).AttackMove,"a wing at 20 percent should receive one withdrawal");
  survivor.P=brain.Rally;w.SmartTactics(1,true);
  StoryAssert(depleted.RegroupUntil>w.Time&&brain.Wings.Contains(depleted),"survivor did not regroup after returning");
  w.Time=depleted.RegroupUntil;w.SmartTactics(1,true);
  StoryAssert(!brain.Wings.Contains(depleted)&&brain.Withdrawals==1,"a regrouped unit must rejoin the available force");

  w=new Campaign(false,false,true);
  brain=w.Brain(2);
  w.Time=60;
  w.SmartTactics(2,true);
  w.Time=brain.NextPush;
  w.SmartTactics(2,true);
  var prior=brain.Wings.Where(wing=>wing.Opponent==1).ToArray();
  StoryAssert(prior.Length>=2,"turn fixture needs multiple teams on the old front");
  var inContact=prior[0].Units[0];
  var player=w.Live(0).First();
  w.Attack(inContact,player);
  foreach(var enemy in w.Units.Where(unit=>unit.Alive&&unit.Side==1))enemy.Hp=0;
  float victory=w.Time;
  w.SmartTactics(2,true);
  StoryAssert(brain.Wings.Count==0&&!w.aiMovesPending.Any(unit=>unit.Side==2)&&inContact.Target==player&&
   brain.NextPush>=victory+RiverbendTurnPreparation,"opponent loss must stop mass redirects while preserving combat already in progress");
  w.Time=brain.NextPush-.1f;w.SmartTactics(2,true);
  StoryAssert(!brain.Wings.Any(wing=>wing.Opponent==0),"no new attack should launch during the turn preparation");
  w.Time=brain.NextPush;w.SmartTactics(2,true);
  var vanguard=brain.Wings.Single();
  StoryAssert(vanguard.Opponent==0&&vanguard.Units.Length==RiverbendVanguardSize,
   "the first new player attack must be a limited vanguard");
  w.Time=brain.NextPush-.1f;w.SmartTactics(2,true);
  StoryAssert(brain.Wings.Count==1,"follow-up must wait for the wave interval");
  w.Time=brain.NextPush;w.SmartTactics(2,true);
  StoryAssert(brain.Wings.Count==2&&brain.Wings[1].Opponent==0&&brain.Wings[1].Units.Length<=RiverbendFollowupSize,
   "the follow-up must resume pressure in a bounded group");

  w=StoryFixture(true,4,0);var housePlan=w.River.Houses.OrderBy(p=>Dist(w.Home(4),new PointF(p.Lot.X*Tile,p.Lot.Y*Tile))).First();
  var house=w.Spawn(housePlan.Kind,4,(housePlan.Lot.X+housePlan.Lot.Width*.5f)*Tile,(housePlan.Lot.Y+housePlan.Lot.Height*.5f)*Tile);
  var door=Middle(ExitApron(house.T,Footprint(house.T,house.P)));var foot=w.Spawn("squad",4,door.X+Tile,door.Y+Tile);w.Brain(4).FieldGuard.Add(foot);
  w.SmartHousing(4);StoryAssert(foot.Entering==house&&!w.Brain(4).FieldGuard.Contains(foot),"local field guard did not transfer to an owned house");
  for(int i=0;i<30*30&&foot.Carrier==null;i++)w.UnitTick(foot,1f/30,true);
  StoryAssert(foot.Carrier==house,"garrison entry did not complete");
  Console.WriteLine("Story AI: tutorial checkpoint, fog-limited introductions, paid armour budgeting, Riverbend attacks, withdrawals and staged retargeting, local garrison and relay isolation passed.");
 }
}}
