using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
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

  w=StoryFixture(false,1,40);int originalCount=w.Units.Count;money=w.Funds[1];var brain=w.Brain(1);w.SmartTactics(1,true);
  StoryAssert(brain.HomeGuard.Count==w.HomeGuardTarget(1)&&brain.FieldGuard.Count==4&&brain.Wings.Count>0,"base / checkpoint / attack allocation");
  var deployed=brain.Wings.SelectMany(g=>g.Units).Concat(brain.FieldGuard).Concat(brain.HomeGuard).ToArray();
  StoryAssert(deployed.Distinct().Count()==deployed.Length&&w.Units.Count==originalCount&&w.Funds[1]==money,"duplicated roles or free reinforcement");
  StoryAssert(brain.FieldPoint.X>w.River.Bridges.OrderBy(b=>b.Top).First().Right*Tile,"northern post must remain on the east approach");
  for(int side=1;side<w.FactionCount;side++)w.nextAiDecision[side]=float.MaxValue;
  for(int i=0;i<160*30&&brain.FieldGuard.Any(u=>!u.GuardHome.HasValue||Dist(u.P,brain.FieldPoint)>6*Tile);i++){
   w.Tick(1f/30);if(i%30==0)w.MaintainStoryPost(1,brain,w.Live(1).Where(u=>!u.T.Building&&u.T.Hit>0).ToArray(),w.AiKnown(1),new HashSet<Piece>(brain.HomeGuard));
  }
  StoryAssert(brain.FieldGuard.Count==4&&brain.FieldGuard.All(u=>u.GuardHome.HasValue&&Dist(u.P,brain.FieldPoint)<6*Tile),"northern checkpoint team failed physical arrival");
  var invader=w.Spawn("tank",2,w.Home(1).X+3*Tile,w.Home(1).Y);w.Reveal();
  w.MaintainStoryPost(1,brain,w.Live(1).Where(u=>!u.T.Building&&u.T.Hit>0).ToArray(),w.AiKnown(1),new HashSet<Piece>(brain.HomeGuard));
  StoryAssert(brain.FieldReturning,"checkpoint detachment ignored an attack on its own base");

  w=StoryFixture(true,4,0);var housePlan=w.River.Houses.OrderBy(p=>Dist(w.Home(4),new PointF(p.Lot.X*Tile,p.Lot.Y*Tile))).First();
  var house=w.Spawn(housePlan.Kind,4,(housePlan.Lot.X+housePlan.Lot.Width*.5f)*Tile,(housePlan.Lot.Y+housePlan.Lot.Height*.5f)*Tile);
  var door=Middle(ExitApron(house.T,Footprint(house.T,house.P)));var foot=w.Spawn("squad",4,door.X+Tile,door.Y+Tile);w.Brain(4).FieldGuard.Add(foot);
  w.SmartHousing(4);StoryAssert(foot.Entering==house&&!w.Brain(4).FieldGuard.Contains(foot),"local field guard did not transfer to an owned house");
  for(int i=0;i<30*30&&foot.Carrier==null;i++)w.UnitTick(foot,1f/30,true);
  StoryAssert(foot.Carrier==house,"garrison entry did not complete");
  Console.WriteLine("Story AI: tutorial checkpoint, fog-limited introductions, paid armour budgeting, separate base/field/attack roles, physical bridge deployment, return to base, local garrison and relay isolation passed.");
 }
}}
