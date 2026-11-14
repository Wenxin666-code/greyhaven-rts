using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 // Autonomous exploration is a resumable search. A disconnected shore or a
 // blocked frontier must not trigger thousands of synchronous route requests.
 sealed class SurveySearch {
  internal PointF Origin;internal int Topology,Side,Next;internal PointF[] Points;
 }
 readonly Dictionary<Piece,SurveySearch> surveys=new Dictionary<Piece,SurveySearch>();
 readonly Dictionary<float,PointF[]> surveyGround=new Dictionary<float,PointF[]>();
 float surveyBudgetAt=-1;int surveyRoutes;
 internal int SurveyRouteAttempts,SurveyCandidateBuilds;
 PointF[] SurveyGround(float pad){
  PointF[] result;if(surveyGround.TryGetValue(pad,out result))return result;
  const int spacing=4*Tile;var points=new List<PointF>();
  for(int y=(int)FogOriginY+48;y<FogOriginY+MapWidth+MapHeight;y+=spacing)
   for(int x=(int)FogOriginX+48;x<FogOriginX+MapWidth+MapHeight;x+=spacing){
    var p=new PointF(x,y);if(Battlefield.ContainsFootprint(p,pad)&&TerrainOpen(p,pad))points.Add(p);
   }
  result=points.ToArray();surveyGround.Add(pad,result);return result;
 }
 bool ExploreGround(Piece unit){
  if(surveyBudgetAt!=Time){surveyBudgetAt=Time;surveyRoutes=0;}
  if(surveyRoutes>=2)return false;
  SurveySearch search;
  if(!surveys.TryGetValue(unit,out search)||search.Topology!=topology||search.Side!=unit.Side||Dist(search.Origin,unit.P)>4*Tile){
   foreach(var dead in surveys.Keys.Where(p=>!p.Alive).ToArray())surveys.Remove(dead);
   search=new SurveySearch{Origin=unit.P,Topology=topology,Side=unit.Side,
    Points=SurveyGround(Clearance(unit)).Where(p=>!Explored(unit.Side,p)).OrderBy(p=>Dist(unit.P,p)).ToArray()};
   surveys[unit]=search;SurveyCandidateBuilds++;
  }
  while(search.Next<search.Points.Length&&surveyRoutes<2){
   var point=search.Points[search.Next++];if(Explored(unit.Side,point))continue;
   surveyRoutes++;SurveyRouteAttempts++;
   if(Route(unit,point))return true;
  }
  return false;
 }
}
}
