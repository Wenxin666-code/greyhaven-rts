using System.Globalization;
using System.Text;

namespace Greyhaven {
public sealed partial class RiverbendMap {
 // Core cells are retained for older review tools. This authoritative layer
 // covers the complete radar domain, including negative-coordinate cells.
 void AppendRadarTerrain(StringBuilder output){
  var bounds=TerrainBounds;
  output.Append(",\"radarTerrain\":{\"format\":\"whole-cell-v1\",\"cellWorldSize\":24,\"bounds\":[")
   .Append(bounds.X).Append(',').Append(bounds.Y).Append(',').Append(bounds.Width).Append(',').Append(bounds.Height)
   .Append("],\"projection\":\"u=x-y;v=x+y\",\"surfaceLegend\":[\"meadow\",\"soil\",\"rock\",\"marsh\",\"water\",\"road\",\"bridge\",\"square\",\"ramp\",\"sand\"],\"cellFields\":[\"x\",\"y\",\"surface\",\"height\",\"tree\"],\"cells\":[");
  bool first=true;
  for(int y=bounds.Top;y<bounds.Bottom;y++)for(int x=bounds.Left;x<bounds.Right;x++){
   var cell=At(x,y);if(cell==null)continue;
   if(!first)output.Append(',');first=false;
   output.Append('[').Append(x).Append(',').Append(y).Append(',').Append((int)cell.Ground).Append(',')
    .Append(cell.Height.ToString("0.000",CultureInfo.InvariantCulture)).Append(',').Append(cell.Tree?1:0).Append(']');
  }
  output.Append("]}");
 }
}
}
