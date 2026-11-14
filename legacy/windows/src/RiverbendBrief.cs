using System;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
partial class CommandWindow {
 Bitmap riverBriefMap,trainingBriefMap,islandBriefMap,relayBriefMap;
 void DrawRiverBrief(Graphics g,RectangleF box){
  bool island=islandMissionSelected,relay=relayMissionSelected;var cache=relay?relayBriefMap:island?islandBriefMap:riverBriefMap;
  if(cache==null){
   if(riverMap==null||riverMap.IsTraining||riverMap.IsIsland!=island||riverMap.IsRelay!=relay){riverMap=relay?RiverbendMap.Relay():new RiverbendMap(island);riverViewZoom=-1;}
   EnsureRiverArt();var oldCenter=mapCenter;float oldZoom=mapZoom;bool oldBattle=riverBattleRender,oldLabels=mapLabels,oldClient=drawingClient;
   try{
    riverBattleRender=false;mapLabels=false;drawingClient=false;RiverFit();
    using(var full=new Bitmap(W,H))using(var bg=Graphics.FromImage(full)){
     DrawRiverMap(bg);
     var starts=riverMap.Starts;for(int i=0;i<starts.Length;i++){
      var p=RiverScreen(starts[i].X,starts[i].Y,riverMap.At(starts[i].X,starts[i].Y).Height);
      using(var brush=new SolidBrush(UnitColor(i)))bg.FillRectangle(brush,p.X-9,p.Y-9,18,18);
      using(var pen=new Pen(Color.White,2))bg.DrawRectangle(pen,p.X-9,p.Y-9,18,18);
     }
     cache=new Bitmap(496,290);using(var output=Graphics.FromImage(cache)){
      output.Clear(C("#152629"));output.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
      var view=mapViewport;float k=Math.Min(496/view.Width,290/view.Height);
      output.DrawImage(full,new RectangleF((496-view.Width*k)/2,(290-view.Height*k)/2,view.Width*k,view.Height*k),view,GraphicsUnit.Pixel);
     }
    }
   }finally{mapCenter=oldCenter;mapZoom=oldZoom;riverBattleRender=oldBattle;mapLabels=oldLabels;drawingClient=oldClient;}
  }
  if(relay)relayBriefMap=cache;else if(island)islandBriefMap=cache;else riverBriefMap=cache;g.DrawImage(cache,box);
 }
 void DrawTrainingBrief(Graphics g,RectangleF box){
  if(trainingBriefMap==null){
   var oldMap=riverMap;var oldCenter=mapCenter;float oldZoom=mapZoom;bool oldBattle=riverBattleRender,oldLabels=mapLabels,oldClient=drawingClient;
   try{
    riverMap=RiverbendMap.ChapterOne();EnsureRiverArt();riverBattleRender=false;mapLabels=false;drawingClient=false;RiverFit();
    using(var full=new Bitmap(W,H))using(var bg=Graphics.FromImage(full)){
     DrawRiverMap(bg);trainingBriefMap=new Bitmap(496,290);using(var output=Graphics.FromImage(trainingBriefMap)){
      output.Clear(C("#152629"));output.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
      var view=mapViewport;float k=Math.Min(496/view.Width,290/view.Height);
      output.DrawImage(full,new RectangleF((496-view.Width*k)/2,(290-view.Height*k)/2,view.Width*k,view.Height*k),view,GraphicsUnit.Pixel);
     }
    }
   }finally{riverMap=oldMap;mapCenter=oldCenter;mapZoom=oldZoom;riverBattleRender=oldBattle;mapLabels=oldLabels;drawingClient=oldClient;}
  }g.DrawImage(trainingBriefMap,box);
 }
}
}
