using System;
namespace Greyhaven {
// World-space dimensions, never screen pixels or zoom. Length is the measured
// chassis below 1.4 model units (excludes barrel/antenna), in ground cells.
public static class UnitGroundMetric {
 public static float LengthCells(string key){
  switch(key){case "tank":return 1f;case "ifv":return .95f;case "arty":return 1.1f;case "mcv":case "miner":return 1.05f;default:throw new ArgumentException(key);}
 }
 public static float Radius(string key){
  return Campaign.Tile*LengthCells(key)*UnitGroundGeometry.RadiusPerLength(key);
 }
}
}
