using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
 // Maximum EMPTY cells between footprint edges, not center-to-center distance.
 // Adjacent belongs to the building being placed. Diagonal gaps use both grid
 // axes (a rectangular neighborhood), not a circular world-space radius.
 // A defensive structure does not become an anchor for further expansion.
 public static int BuildAdjacentCells(TypeInfo type){return type.IsWall?8:type.Key=="post"?4:2;}

 bool AiDefenseGround(RectangleF body){
  for(int y=(int)Math.Floor(body.Top/Tile);y<(int)Math.Ceiling(body.Bottom/Tile);y++)for(int x=(int)Math.Floor(body.Left/Tile);x<(int)Math.Ceiling(body.Right/Tile);x++){
   var cell=Terrain.At(x,y);if(cell==null||cell.Ground==RiverbendMap.Surface.Road||cell.Ground==RiverbendMap.Surface.Bridge||cell.Ground==RiverbendMap.Surface.Ramp)return false;
  }
  return true;
 }
 float AiDefenseSiteScore(int side,PointF point,PointF preferred){
  float score=Dist(point,preferred);
  foreach(var post in Units.Where(p=>p.Alive&&p.Side==side&&p.T.Key=="post")){
   float overlap=Math.Max(0,Info("post").Range*.8f-Dist(point,post.P));score+=overlap*3;
  }
  return score;
 }
 int DesiredDefenses(int side){
  if((Tutorial&&Time<180)||!Live(side).Any(p=>p.T.Key=="hq")||!Live(side).Any(p=>p.T.Key=="refinery")||!Live(side).Any(p=>p.T.Key=="barracks"))return 0;
  if(Time<55)return 0;
  int refineries=AiCount(side,"refinery"),factories=AiCount(side,"factory");
  int desired=factories>=2&&refineries>=2?6:factories>0&&refineries>=2?4:factories>0?2:1;
  return StoryBattle&&desired>2?desired-1:desired;
 }
 void AiFortify(int side,AiBrain brain,Piece[] known){
  AiPlaceReady(side,brain,1);
  if(HasSurrendered(side)||!Live(side).Any(p=>p.T.Key=="hq")||Construction(side,1)!=null)return;
  if(!Live(side).Any(p=>p.T.Key=="refinery")||!Live(side).Any(p=>p.T.Key=="barracks"))return;
  if(Tutorial&&Time<180)return;
  var assets=Live(side).Where(p=>p.T.Building&&!p.T.Civilian&&p.T.Key!="post"&&!p.Selling).ToArray();
  var danger=known.Where(p=>p.T.Hit>0&&assets.Any(a=>Dist(a.P,p.P)<Info("post").Range*3+Tile*6))
   .OrderBy(p=>assets.Min(a=>Dist(a.P,p.P))).FirstOrDefault();
  bool urgent=danger!=null;
  int desired=DesiredDefenses(side);
  if(urgent){
   int response=Time>=600?8:Time>=260?5:2;
   if(StoryBattle&&response>2)response--;
   desired=Math.Max(desired,response);
  }
  int count=AiCount(side,"post");if(count>=desired)return;
  if(OreReceipts[side]==0&&Funds[side]<2750)return;
  int reserve=urgent?Math.Min(100,AiReserve(side,false)):AiReserve(side,false);
  if(Power(side)<0){
   if(Construction(side,0)==null&&AiBudget(side,reserve)>=Info("power").Price)AiPlanBuild(side,brain,"power",Home(side));
   return;
  }
  if(AiBudget(side,reserve)<Info("post").Price)return;
  var posts=Units.Where(p=>p.Alive&&p.Side==side&&p.T.Key=="post").ToArray();
  var asset=urgent?assets.OrderBy(a=>Dist(a.P,danger.P)).First():assets
   .OrderBy(a=>posts.Count(p=>Dist(p.P,a.P)<p.T.Range)*1000-(a.T.Key=="refinery"?140:a.T.Key=="factory"?90:a.T.Key=="hq"?60:0)).First();
  var front=urgent?danger.P:AiDefenseFront(side,asset.P,posts.Length);float dx=front.X-asset.P.X,dy=front.Y-asset.P.Y;
  float length=(float)Math.Sqrt(dx*dx+dy*dy);if(length<1){dx=1;dy=1;length=1.414214f;}
  float outward=Tile*(Math.Max(asset.T.FoundationWidth,asset.T.FoundationHeight)*.5f+2.5f);
  AiPlanBuild(side,brain,"post",new PointF(asset.P.X+dx/length*outward,asset.P.Y+dy/length*outward));
 }
}
}
