using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Greyhaven {
// Cell axes remain isometric. The complete battlefield is bounded in x-y/x+y
// space. Every cell inside that projected rectangle has its own terrain type.
public struct BattlefieldBounds {
 public readonly float Width,Height;
 public BattlefieldBounds(float width,float height){Width=width;Height=height;}
 public float Span {get{return Width+Height;}}
 public bool Contains(PointF p){float u=p.X-p.Y,v=p.X+p.Y;return u>=-Height&&u<=Width&&v>=0&&v<=Span;}
 public bool ContainsFootprint(PointF p,float pad){return Contains(new PointF(p.X-pad,p.Y-pad))&&Contains(new PointF(p.X+pad,p.Y-pad))&&Contains(new PointF(p.X-pad,p.Y+pad))&&Contains(new PointF(p.X+pad,p.Y+pad));}
 public PointF ClampCenter(PointF p,float halfU,float halfV){
  float u=p.X-p.Y,v=p.X+p.Y;
  u=Span<=halfU*2?(Width-Height)*.5f:Math.Max(-Height+halfU,Math.Min(Width-halfU,u));
  v=Span<=halfV*2?Span*.5f:Math.Max(halfV,Math.Min(Span-halfV,v));
  return new PointF((u+v)*.5f,(v-u)*.5f);
 }
 public static readonly Color SeaColor=Color.FromArgb(53,94,104);
}
public partial class Campaign {
 public BattlefieldBounds Battlefield {get{return new BattlefieldBounds(MapWidth,MapHeight);}}
 public float FogOriginX {get{return -MapHeight*.5f;}}
 public float FogOriginY {get{return -MapWidth*.5f;}}
 public int FogColumn(float x){return (int)Math.Floor((x-FogOriginX)/FogSize);}
 public int FogRow(float y){return (int)Math.Floor((y-FogOriginY)/FogSize);}
}
partial class CommandWindow {
 void DrawSea(Graphics g,RectangleF area,PointF origin,float k){
  const string key="coastal-sea";
  if(!painted.ContainsKey(key)){
   var tile=new Bitmap(128,64);using(var cg=Graphics.FromImage(tile)){
    cg.Clear(BattlefieldBounds.SeaColor);cg.SmoothingMode=SmoothingMode.None;
    using(var pen=new Pen(Color.FromArgb(27,148,187,176),1))
     for(int i=0;i<18;i++){int x=(i*37+13)%128,y=(i*23+7)%64;cg.DrawLine(pen,x,y,x+4+i%8,y);}
   }painted[key]=tile;
  }
  using(var brush=new TextureBrush(painted[key],WrapMode.Tile)){
   brush.TranslateTransform(origin.X,origin.Y);brush.ScaleTransform(k,k);g.FillRectangle(brush,area);
  }
 }
}
}
