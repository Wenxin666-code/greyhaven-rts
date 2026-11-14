using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace Greyhaven {
partial class CommandWindow {
 const int TankLayerSize=256;
 const string TankHullKey="tank-hull",TankTurretKey="tank-turret";
 readonly PointF[] tankMounts=new PointF[64],tankMuzzles=new PointF[64];

 struct TankPose {
  public Rectangle HullCell,TurretCell;
  public RectangleF HullBox,TurretBox;
 }

 void LoadTankLayers(){
  foreach(var key in new[]{TankHullKey,TankTurretKey}){
   painted[key]=Raw(key+"-base");
   painted[key+"-paint"]=Raw(key+"-paint");
  }
  using(var reader=new StreamReader(typeof(CommandWindow).Assembly.GetManifestResourceStream("Greyhaven.Art.tank-sockets.csv"))){
   if(reader.ReadLine()!="frame,mountX,mountY,muzzleX,muzzleY")throw new InvalidDataException("Invalid tank socket table.");
   for(int frame=0;frame<tankMounts.Length;frame++){
    var line=reader.ReadLine();
    if(line==null)throw new InvalidDataException("Missing tank socket frame.");
    var columns=line.Split(',');
    if(columns.Length!=5||Int32.Parse(columns[0],CultureInfo.InvariantCulture)!=frame)throw new InvalidDataException("Invalid tank socket frame.");
    tankMounts[frame]=new PointF(SocketCoordinate(columns[1]),SocketCoordinate(columns[2]));
    tankMuzzles[frame]=new PointF(SocketCoordinate(columns[3]),SocketCoordinate(columns[4]));
   }
  }
 }
 static float SocketCoordinate(string value){return Single.Parse(value,CultureInfo.InvariantCulture);}
 static Rectangle TankLayerCell(int frame){return new Rectangle(frame%8*TankLayerSize,frame/8*TankLayerSize,TankLayerSize,TankLayerSize);}
 RectangleF TankTurretBox(RectangleF hull,int hullFrame){
  var mount=tankMounts[hullFrame];
  hull.Offset(hull.Width*mount.X,hull.Height*mount.Y);
  return hull;
 }
 TankPose TankPoseFor(Piece tank,PointF position,float size){
  int hullFrame=TankFrame(tank),turretFrame=TankFrame(tank.TurretTurn);
  return new TankPose{
   HullCell=TankLayerCell(hullFrame),TurretCell=TankLayerCell(turretFrame),
   HullBox=VehicleRect(tank.T,position,size),
   TurretBox=TankTurretBox(VehicleRect(tank.T,RecoilPoint(tank,position),size),hullFrame)
  };
 }
 PointF TankMuzzle(Shot shot){
  var type=Campaign.Info("tank");
  var hull=VehicleRect(type,ScreenOf(shot.A),type.Size*zoom);
  var turret=TankTurretBox(hull,TankFrame(shot.Heading));
  var muzzle=tankMuzzles[TankFrame(shot.TurretHeading)];
  return new PointF(turret.X+turret.Width*muzzle.X,turret.Y+turret.Height*muzzle.Y);
 }
}
}
