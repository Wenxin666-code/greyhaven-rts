using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 public readonly RiverbendMap River;
 public int MapWidth {get{return River==null?Width:River.WidthCells*Tile;}}
 public int MapHeight {get{return River==null?Height:River.HeightCells*Tile;}}
 static readonly RectangleF[] EmptyTerrain=new RectangleF[0];
 RectangleF[] MapRoads {get{return River==null?Roads:EmptyTerrain;}}
 RectangleF[] MapWalls {get{return HarborOperation!=null?HarborOperation.Barriers:River==null?YardWalls:BasinOperation?.Freight.DepotBarriers??EmptyTerrain;}}
 public PointF Home(int side){if(River==null)return side==0?new PointF(152,514):EnemyHome;var p=River.Starts[side];return new PointF((p.X+.5f)*Tile,(p.Y+.5f)*Tile);}
 void SetupRiverbend(){
  if(RelayMission){SetupRelayMission();return;}
  Ore.Clear();foreach(var p in River.Ore)Ore.Add(new OrePatch((p.X+.5f)*Tile,(p.Y+.5f)*Tile,2400));
  if(River.IsBasin){BasinOperation=new BasinOperation(this);Changed();}
  else if(River.IsRiverbendChapter)RiverbendOperation=new RiverbendOperation(this);
  else{
   for(int side=0;side<FactionCount;side++){
    var home=Home(side);Spawn("mcv",side,home.X,home.Y);Spawn("squad",side,home.X+48,home.Y-48);Spawn("squad",side,home.X+72,home.Y-48);Spawn("engineer",side,home.X,home.Y-48);
   }
   Radio="孤岛要塞：我方位于西北资源岛，堤桥是唯一陆路；五个敌方各自为战。";
  }
  foreach(var h in River.Houses)Spawn(h.Kind,-1,(h.Lot.X+h.Lot.Width*.5f)*Tile,(h.Lot.Y+h.Lot.Height*.5f)*Tile);
 }
 public float GroundHeight(PointF p){var c=Terrain.At((int)Math.Floor(p.X/Tile),(int)Math.Floor(p.Y/Tile));return c==null?0:c.Height;}
 static bool PassableGround(RiverbendMap.Cell cell){return cell!=null&&cell.Ground!=RiverbendMap.Surface.Water&&!cell.Tree&&!cell.Cliff;}
 bool CornerGround(float x,float y,float height){var c=Terrain.At((int)Math.Floor(x/Tile),(int)Math.Floor(y/Tile));return PassableGround(c)&&Math.Abs(c.Height-height)<=.27f;}
 bool TerrainOpen(PointF p,float pad){
  if(!Battlefield.ContainsFootprint(p,pad))return false;
  var cell=Terrain.At((int)Math.Floor(p.X/Tile),(int)Math.Floor(p.Y/Tile));
  if(!PassableGround(cell))return false;
  float h=cell.Height;
  return CornerGround(p.X-pad,p.Y-pad,h)&&CornerGround(p.X+pad,p.Y-pad,h)&&CornerGround(p.X-pad,p.Y+pad,h)&&CornerGround(p.X+pad,p.Y+pad,h);
 }
 static void TerrainCuts(System.Collections.Generic.List<float> cuts,float a,float b,float offset){
  if(Math.Abs(b-a)<.00001f)return;
  float lo=Math.Min(a,b)+offset,hi=Math.Max(a,b)+offset;
  for(int cell=(int)Math.Floor(lo/Tile)+1;cell*Tile<hi;cell++){
   float t=(cell*Tile-offset-a)/(b-a);if(t>0&&t<1)cuts.Add(t);
  }
 }
 readonly List<float> terrainCuts=new List<float>(32);
 // Inspect each terrain interval crossed by the center and all footprint corners.
 // Fixed-distance sampling can miss arbitrarily thin diagonal clips of a cell.
 bool TerrainLine(PointF a,PointF b,float pad=0){
  if(!TerrainOpen(a,pad)||!TerrainOpen(b,pad))return false;
  var cuts=terrainCuts;cuts.Clear();cuts.Add(0);cuts.Add(1);TerrainCuts(cuts,a.X,b.X,0);TerrainCuts(cuts,a.Y,b.Y,0);
  if(pad>0){TerrainCuts(cuts,a.X,b.X,-pad);TerrainCuts(cuts,a.X,b.X,pad);TerrainCuts(cuts,a.Y,b.Y,-pad);TerrainCuts(cuts,a.Y,b.Y,pad);}
  cuts.Sort();float prior=GroundHeight(a);
  for(int i=1;i<cuts.Count;i++){
   if(cuts[i]-cuts[i-1]<.000001f)continue;float t=(cuts[i]+cuts[i-1])*.5f;var p=new PointF(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t);float h=GroundHeight(p);
   if(!TerrainOpen(p,pad)||Math.Abs(h-prior)>.27f)return false;prior=h;
  }return Math.Abs(GroundHeight(b)-prior)<=.27f;
 }
 bool TerrainBuildable(RectangleF r){
  float? height=null;
  for(int y=(int)Math.Floor(r.Top/Tile);y<(int)Math.Ceiling(r.Bottom/Tile);y++)for(int x=(int)Math.Floor(r.Left/Tile);x<(int)Math.Ceiling(r.Right/Tile);x++){
   var c=Terrain.At(x,y);if(!PassableGround(c)||c.Railroad||c.Ground==RiverbendMap.Surface.Bridge||c.Ground==RiverbendMap.Surface.Ramp)return false;
   if(height.HasValue&&Math.Abs(height.Value-c.Height)>.05f)return false;height=c.Height;
  }return true;
 }
 public int HouseCapacity(Piece house){return house.T.Key=="bunker"?5:house.T.Key=="municipal"?8:house.T.Key=="shop"?6:4;}
 public int HouseCount(Piece house){return Units.Count(u=>u.Alive&&u.Carrier==house);}
 public bool CanEnterHouse(Piece unit,Piece house){
  if(unit!=null&&unit.T.Key=="engineer")return CanEngineer(unit,house);
  if(house==null||!house.Alive||!house.T.Garrison||unit==null||!unit.Deployed||!unit.T.Foot)return false;
  if(house.Side<0)return unit.T.Key=="engineer"&&!Units.Any(u=>u.Alive&&u!=unit&&(u.Carrier==house||u.Entering==house));
  return CanKeepEntering(unit,house)&&Units.Count(u=>u!=unit&&u.Alive&&(u.Carrier==house||u.Entering==house))<HouseCapacity(house);
 }
 bool CanKeepEntering(Piece unit,Piece house){return house.Deployed&&house.Side==unit.Side&&!house.Selling&&
  (unit.T.Key=="squad"||house.T.Key!="bunker"&&unit.T.Key=="at");}
 public bool EnterHouse(Piece unit,Piece house){
  if(unit!=null&&unit.T.Key=="engineer")return EngineerOrder(unit,house);
  if(!CanEnterHouse(unit,house)||!RouteToEntrance(unit,house,start:true))return false;
  unit.Entering=house;if(unit.Side==0)Radio="步兵正在前往驻防位置。";return true;
 }
 IEnumerable<PointF> BuildingEntrances(Piece house){
  var lot=Footprint(house.T,house.P);
  yield return Middle(ExitApron(house.T,lot));
  for(float x=lot.Left+Tile*.5f;x<lot.Right;x+=Tile){
   yield return new PointF(x,lot.Top-Tile*.5f);
   yield return new PointF(x,lot.Bottom+Tile*.5f);
  }
  for(float y=lot.Top+Tile*.5f;y<lot.Bottom;y+=Tile){
   yield return new PointF(lot.Left-Tile*.5f,y);
   yield return new PointF(lot.Right+Tile*.5f,y);
  }
 }
 bool RouteToEntrance(Piece unit,Piece house,bool start){
  var entries=BuildingEntrances(house).Distinct().OrderBy(point=>Dist(unit.P,point));
  var nearby=Units.Where(other=>other!=unit&&other.Deployed&&!other.T.Building&&
   (other.Entering==house||Dist(other.P,house.P)<4*Tile)).ToArray();
  foreach(var point in entries){
   if(nearby.Any(other=>other.Entering==house&&Dist(other.Goal,point)<Tile*.4f||
    TrafficContact(unit.T,point,other.T,other.P)))continue;
   var traffic=MineTraffic(unit,point);
   if(start?!Move(unit,point,false,traffic):!PlanRoute(unit,point,traffic))continue;
   unit.Goal=point;return true;
  }
  return false;
 }
 bool ReachEntrance(Piece unit,Piece house){
  if(Dist(unit.P,unit.Goal)<=9)return true;
  if((unit.Path.Count==0||unit.StalledFor>=.5f||unit.NoProgressFor>=.6f)&&Time>=unit.RouteRetryAt){
   unit.RouteRetryAt=Time+.6f;
   RouteToEntrance(unit,house,start:false);
  }
  return false;
 }
 bool TickEntry(Piece unit,float dt){
  if(unit.Entering!=null&&unit.Entering.T.Key=="ifv")return TickBoard(unit);
  if(unit.Entering!=null&&unit.T.Key=="engineer")return TickEngineerEntry(unit,dt);
  var house=unit.Entering;if(house==null)return false;
  if(!CanKeepEntering(unit,house)||HouseCount(house)>=HouseCapacity(house)){unit.Entering=null;unit.Path.Clear();return false;}
  if(!ReachEntrance(unit,house))return false;
  unit.Path.Clear();unit.Target=null;unit.Entering=null;unit.Carrier=house;unit.P=house.P;unit.Capturing=house.Side<0;unit.CaptureWork=0;return true;
 }
 bool ExitHouseUnit(Piece unit,Piece house){
  var body=Footprint(house.T,house.P);var door=Middle(ExitApron(house.T,body));
  for(int ring=0;ring<16;ring++)for(int i=0;i<(ring==0?1:16);i++){
   double a=i*Math.PI/8;var p=new PointF(door.X+(float)Math.Cos(a)*ring*12,door.Y+(float)Math.Sin(a)*ring*12);
   if(Inflated(body,5).Contains(p)||Blocked(p,Clearance(unit),null)||Units.Any(u=>u!=unit&&u.Deployed&&!u.T.Building&&TrafficContact(unit.T,p,u.T,u.P)))continue;
   unit.Carrier=null;unit.P=p;unit.Capturing=false;unit.CaptureWork=0;Halt(unit);return true;
  }return false;
 }
 public void EvacuateHouse(Piece house){foreach(var u in Units.Where(u=>u.Alive&&u.Carrier==house).ToArray()){
   if(!ExitHouseUnit(u,house)&&!house.Alive){u.Carrier=null;u.P=house.P;Halt(u);}
  }}
 void TickGarrison(Piece unit,float dt){
  var house=unit.Carrier;if(!house.Alive){ExitHouseUnit(unit,house);return;}
  if(unit.T.Key=="engineer"){ExitHouseUnit(unit,house);return;}
  unit.Cool-=dt;TickBurst(unit,dt);if(unit.Cool>0)return;
  float range=ReferenceBalance.Weapon(unit).Range;
  var enemy=unit.Target;
  if(enemy==null||!CanAutoTarget(unit,enemy)||!VisibleTo(unit.Side,enemy)||!CanShootAt(unit,enemy))
   unit.Target=enemy=FindContact(unit,range);
  if(enemy==null)return;Fire(unit,enemy);var shot=Shots.Last();shot.Garrison=true;shot.SourceId=house.Id;shot.A=TargetPoint(enemy.P,house);shot.A=new PointF(shot.A.X+(house.P.X-shot.A.X)*.12f,shot.A.Y+(house.P.Y-shot.A.Y)*.12f);
 }
 void EnemyHousing(int side){
  if(River!=null){SmartHousing(side);return;}
  foreach(var engineer in Live(side).Where(u=>u.T.Key=="engineer"&&u.Entering==null).ToArray()){
   var house=Units.Where(h=>h.Alive&&h.T.Civilian&&h.Side<0).OrderBy(h=>Dist(h.P,engineer.P)).FirstOrDefault();if(house!=null)EnterHouse(engineer,house);
  }
  foreach(var foot in Live(side).Where(u=>u.T.Foot&&u.T.Hit>0&&u.Entering==null&&u.Target==null).ToArray()){
   var house=Live(side).Where(h=>h.T.Civilian&&Dist(h.P,foot.P)<420&&HouseCount(h)<HouseCapacity(h)).OrderBy(h=>Dist(h.P,foot.P)).FirstOrDefault();if(house!=null)EnterHouse(foot,house);
  }
 }
}
}
