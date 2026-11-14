using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
namespace Greyhaven {
partial class CommandWindow {
 // Rendering palette is deliberately independent of the multi-faction
 // simulation. Any faction ID can have a custom RGB color without new artwork.
 readonly Dictionary<int,Color> unitColors=new Dictionary<int,Color>{{0,Color.FromArgb(62,133,190)},{1,Color.FromArgb(184,66,51)}};
 static readonly Color[] paintSwatches={Color.FromArgb(62,133,190),Color.FromArgb(184,66,51),Color.FromArgb(74,146,82),Color.FromArgb(209,161,51),Color.FromArgb(137,85,177),Color.FromArgb(39,172,166),Color.FromArgb(219,112,47),Color.FromArgb(210,215,209)};
 Color UnitColor(int faction){if(faction<0)return Color.FromArgb(222,225,221);Color c;return unitColors.TryGetValue(faction,out c)?c:paintSwatches[(faction%paintSwatches.Length+paintSwatches.Length)%paintSwatches.Length];}
 void SetUnitColor(int faction,Color color){unitColors[faction]=Color.FromArgb(color.R,color.G,color.B);ClearStampCache();hudDirty=true;}
 void DrawLayeredSource(Graphics g,string key,Rectangle source,RectangleF box,Color? faction){
  if(key.StartsWith("combat/",StringComparison.Ordinal)||key.StartsWith("stance/",StringComparison.Ordinal)||key.StartsWith("b08/",StringComparison.Ordinal)||key.StartsWith("gait/",StringComparison.Ordinal)||key.StartsWith("harbor/",StringComparison.Ordinal))EnsureB08(key);
  if(faction.HasValue&&BuildingV08.Contribution(key)){using(var tint=BuildingTint(painted[key],painted[key+"-paint"],source,faction.Value))g.DrawImage(tint,box,new Rectangle(Point.Empty,tint.Size),GraphicsUnit.Pixel);return;}
  g.DrawImage(painted[key],box,source,GraphicsUnit.Pixel);
  if(!faction.HasValue)return;
  var c=faction.Value;
  // The grey paint pass contains lighting, occlusion and panel detail. Multiply
  // only that pass by the requested color; neutral metal/rubber/glass never tint.
  var matrix=new ColorMatrix(new[]{new[]{c.R/255f,0f,0f,0f,0f},new[]{0f,c.G/255f,0f,0f,0f},new[]{0f,0f,c.B/255f,0f,0f},new[]{0f,0f,0f,1f,0f},new[]{0f,0f,0f,0f,1f}});
  using(var attr=new ImageAttributes()){
   attr.SetColorMatrix(matrix);attr.SetWrapMode(System.Drawing.Drawing2D.WrapMode.Clamp);
   g.DrawImage(painted[key+"-paint"],new[]{new PointF(box.Left,box.Top),new PointF(box.Right,box.Top),new PointF(box.Left,box.Bottom)},source,GraphicsUnit.Pixel,attr);
  }
 }
}
}
