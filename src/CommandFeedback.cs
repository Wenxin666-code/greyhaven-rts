using System;
using System.Drawing;
using System.Drawing.Imaging;
namespace Greyhaven {
partial class CommandWindow {
 Piece flashTarget;double flashAt=-100;
 float TargetFlash(Piece unit){
  double age=watch.Elapsed.TotalSeconds-flashAt;
  return unit!=flashTarget||!unit.Alive||age<0||age>.20?0:(float)(Math.Pow(Math.Sin(age/.20*Math.PI*2),2)*.78);
 }
 void DrawTargetFlash(Graphics g,Piece unit,PointF position,float size){
  float strength=TargetFlash(unit);if(strength<=.01f)return;
  if(unit.T.Building)position=DeploymentPoint(unit,position);
  var t=unit.T;var source=t.Key=="tank"?TankCell(unit.Side,TankFrame(unit)):SourceCell(t,unit.Side,(unit.Dir+14)%16);
  var key=t.Building?BuildingVisual(unit,out source):LiveArtKey(unit);
  var box=t.Building?BuildingRect(Campaign.Footprint(t,unit.P),position,zoom):t.Foot?SoldierBox(SoldierFeet(unit,position,zoom,world.Time),size):VehicleRect(t,position,size);
  var matrix=new ColorMatrix(new[]{new float[]{0,0,0,0,0},new float[]{0,0,0,0,0},new float[]{0,0,0,0,0},new float[]{0,0,0,strength,0},new float[]{1,1,1,0,1}});
  using(var attributes=new ImageAttributes()){
   attributes.SetColorMatrix(matrix);
   g.DrawImage(painted[key],new[]{new PointF(box.Left,box.Top),new PointF(box.Right,box.Top),new PointF(box.Left,box.Bottom)},source,GraphicsUnit.Pixel,attributes);
  }
 }
}
}
