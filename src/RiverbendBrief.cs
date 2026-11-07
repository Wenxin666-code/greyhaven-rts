using System;
using System.Drawing;
namespace Greyhaven {
partial class CommandWindow {
 Bitmap riverBriefMap,trainingBriefMap;
 void DrawRiverBrief(Graphics g,RectangleF box){
  if(riverBriefMap==null){
   EnsureRiverArt();var oldCenter=mapCenter;float oldZoom=mapZoom;bool oldBattle=riverBattleRender,oldLabels=mapLabels,oldClient=drawingClient;
   try{
    riverBattleRender=false;mapLabels=false;drawingClient=false;RiverFit();
    using(var full=new Bitmap(W,H))using(var bg=Graphics.FromImage(full)){
     DrawRiverMap(bg);
     var starts=new[]{riverMap.WestStart,riverMap.EastStart,riverMap.SouthStart};for(int i=0;i<starts.Length;i++){
      var p=RiverScreen(starts[i].X,starts[i].Y,riverMap.At(starts[i].X,starts[i].Y).Height);
      using(var brush=new SolidBrush(UnitColor(i)))bg.FillRectangle(brush,p.X-9,p.Y-9,18,18);
      using(var pen=new Pen(Color.White,2))bg.DrawRectangle(pen,p.X-9,p.Y-9,18,18);
     }
     riverBriefMap=new Bitmap(496,290);using(var output=Graphics.FromImage(riverBriefMap)){
      output.Clear(C("#152629"));output.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
      var view=mapViewport;float k=Math.Min(496/view.Width,290/view.Height);
      output.DrawImage(full,new RectangleF((496-view.Width*k)/2,(290-view.Height*k)/2,view.Width*k,view.Height*k),view,GraphicsUnit.Pixel);
     }
    }
   }finally{mapCenter=oldCenter;mapZoom=oldZoom;riverBattleRender=oldBattle;mapLabels=oldLabels;drawingClient=oldClient;}
  }
  g.DrawImage(riverBriefMap,box);
 }
 void DrawTrainingBrief(Graphics g,RectangleF box){
  if(trainingBriefMap==null){trainingBriefMap=new Bitmap(496,290);using(var bg=Graphics.FromImage(trainingBriefMap)){
   bg.Clear(C("#152629"));float k=466f/((Campaign.Width+Campaign.Height)*Axis);
   float x=15+Campaign.Height*Axis*k,y=(290-(Campaign.Width+Campaign.Height)*Rise*k)/2;
   bg.DrawImage(ground,new[]{new PointF(x,y),new PointF(x+Campaign.Width*Axis*k,y+Campaign.Width*Rise*k),new PointF(x-Campaign.Height*Axis*k,y+Campaign.Height*Rise*k)});
  }}g.DrawImage(trainingBriefMap,box);
 }
}
}
