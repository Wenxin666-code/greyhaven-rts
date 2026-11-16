using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public enum PlacementBlock { None,OutsideMap,Unexplored,Water,Trees,Bridge,Slope,Uneven,Building,Unit,Ore,Wall,OutOfRange,NoBase,Rail }
public sealed class PlacementTile {
 public RectangleF Bounds;public PlacementBlock Block;
 public bool Valid {get{return Block==PlacementBlock.None;}}
}
public sealed class PlacementAssessment {
 public RectangleF Bounds;public float Height;public PlacementBlock Global;
 public readonly List<PlacementTile> Tiles=new List<PlacementTile>();
 public IReadOnlyList<RectangleF> WallExtensions {get; internal set;}=Array.Empty<RectangleF>();
 public bool Valid {get{return Global==PlacementBlock.None&&Tiles.All(c=>c.Valid);}}
 public string Summary {
  get{
   var reasons=Tiles.Where(c=>!c.Valid).GroupBy(c=>c.Block).Select(c=>Campaign.PlacementReason(c.Key)+" ×"+c.Count()).ToList();
   if(Global!=PlacementBlock.None)reasons.Insert(0,Campaign.PlacementReason(Global));
   return reasons.Count==0?"地基可放置":String.Join("　·　",reasons.Take(4));
  }
 }
}
public partial class Campaign {
 public static string PlacementReason(PlacementBlock block){
  switch(block){
   case PlacementBlock.OutsideMap:return "超出战区";case PlacementBlock.Unexplored:return "未探索";
   case PlacementBlock.Water:return "水域";
   case PlacementBlock.Trees:return "树木";case PlacementBlock.Bridge:return "桥梁";case PlacementBlock.Rail:return "铁路";
   case PlacementBlock.Slope:return "坡道";case PlacementBlock.Uneven:return "地面高差";
   case PlacementBlock.Building:return "建筑占用";case PlacementBlock.Unit:return "单位占用";
   case PlacementBlock.Ore:return "矿石";case PlacementBlock.Wall:return "墙体占用";
   case PlacementBlock.OutOfRange:return "超出扩建范围";case PlacementBlock.NoBase:return "需要展开的基地";
   default:return "可建造";
  }
 }
 PlacementBlock LocalPlacementBlock(RectangleF cell,Piece skip,int side,float? level){
  if(!Battlefield.ContainsFootprint(Middle(cell),Tile*.5f-.01f))return PlacementBlock.OutsideMap;
  if(skip==null&&!Explored(side,Middle(cell)))return PlacementBlock.Unexplored;
  var c=Terrain.At((int)Math.Floor(cell.X/Tile),(int)Math.Floor(cell.Y/Tile));
  if(c==null)return PlacementBlock.OutsideMap;
  if(c.Ground==RiverbendMap.Surface.Water)return PlacementBlock.Water;
  if(c.Cliff)return PlacementBlock.Uneven;
  if(c.Tree)return PlacementBlock.Trees;
  if(c.Ground==RiverbendMap.Surface.Bridge)return PlacementBlock.Bridge;
  if(c.Railroad)return PlacementBlock.Rail;
  if(c.Ground==RiverbendMap.Surface.Ramp)return PlacementBlock.Slope;
  if(level.HasValue&&Math.Abs(level.Value-c.Height)>.05f)return PlacementBlock.Uneven;
  if(MapWalls.Any(r=>r.IntersectsWith(cell))||Sites.Any(s=>s.Body.IntersectsWith(cell)))return PlacementBlock.Wall;
  if(Ore.Any(p=>p.Stock>0&&cell.Contains(p.P)))return PlacementBlock.Ore;
  foreach(var o in Units){if(o==skip||!o.Alive||o.Carrier!=null)continue;
   if(o.T.Building){if(Footprint(o.T,o.P).IntersectsWith(cell))return o.T.IsWall?PlacementBlock.Wall:PlacementBlock.Building;}
   else if(Inflated(cell,Clearance(o)).Contains(o.P))return PlacementBlock.Unit;
  }
  return PlacementBlock.None;
 }
 public PlacementAssessment AssessPlacement(TypeInfo type,PointF point,Piece skip=null,int side=0,bool requireBase=false){
  var result=new PlacementAssessment{Bounds=Footprint(type,point)};var levels=new List<float>();
  for(float y=result.Bounds.Top;y<result.Bounds.Bottom;y+=Tile)for(float x=result.Bounds.Left;x<result.Bounds.Right;x+=Tile){
   var cell=new RectangleF(x,y,Tile,Tile);var block=LocalPlacementBlock(cell,skip,side,null);
   if(block==PlacementBlock.None)levels.Add(GroundHeight(Middle(cell)));
   result.Tiles.Add(new PlacementTile{Bounds=cell,Block=block});
  }
  result.Height=levels.Count==0?0:levels.GroupBy(h=>(int)Math.Round(h*20)).OrderByDescending(g=>g.Count()).First().First();
  foreach(var tile in result.Tiles)if(tile.Valid&&Math.Abs(GroundHeight(Middle(tile.Bounds))-result.Height)>.05f)tile.Block=PlacementBlock.Uneven;
  if(skip==null){
   if(requireBase&&!Live(side).Any(o=>o.T.Key=="hq"))result.Global=PlacementBlock.NoBase;
   else if(!InBuildRange(type,result.Bounds,side))result.Global=PlacementBlock.OutOfRange;
  }
  if(type.IsWall&&result.Valid)result.WallExtensions=WallExtensions(result.Bounds,side,result.Height);
  return result;
 }
}
}
