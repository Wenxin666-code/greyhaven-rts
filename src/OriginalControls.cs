using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 bool rightPending,rightDragging,advancedBarOpen=true,planningPinned,beaconMode,diplomacyOpen,keyboardOpen,keyboardCapturing;
 PointF rightOrigin,rightLast;int sidebarRow,keyboardPage;string keyboardCommand,keyboardError="";
 Keys planningPhysicalKey=Keys.None,typePhysicalKey=Keys.None;
 readonly HashSet<Keys> cameraKeys=new HashSet<Keys>();
 readonly HashSet<string> seenCameos=new HashSet<string>();
 readonly Dictionary<Piece,int> patrolStarts=new Dictionary<Piece,int>();
 readonly List<LocalBeacon> beacons=new List<LocalBeacon>();
 LocalBeacon selectedBeacon;bool beaconEditing;string beaconText="";Piece waypointOwner;int waypointSelected=-1;
 class LocalBeacon {public PointF Point;public string Text="";public float Created;}
 bool PlanningActive {get{return zHeld||planningPinned;}}
 void ResetOriginalControls(){rightPending=rightDragging=planningPinned=beaconMode=diplomacyOpen=keyboardOpen=keyboardCapturing=beaconEditing=false;selectedBeacon=null;waypointOwner=null;waypointSelected=-1;sidebarRow=0;seenCameos.Clear();beacons.Clear();patrolStarts.Clear();cameraKeys.Clear();planningPhysicalKey=typePhysicalKey=Keys.None;}
 void CancelMapClick(){
  if(beaconEditing){beaconEditing=false;world.Radio="已取消标记文字输入。";return;}
  if(place!=null||attack||structureMode!=StructureMode.None||beaconMode){place=null;attack=false;structureMode=StructureMode.None;beaconMode=false;world.Radio="已取消待下达指令。";}
  else{selection.Clear();selectedBeacon=null;markedPoint=null;markedEnemy=null;world.Radio=PlanningActive?"已取消选择，路径规划保留；可选择另一队。":"已取消选择。";}
 }
 void BeginRightDrag(){rightPending=true;rightDragging=false;rightOrigin=rightLast=mouse;Capture=true;}
 void MoveRightDrag(){
  if(!rightPending)return;
  if(!rightDragging&&Campaign.Dist(rightOrigin,mouse)*Math.Max(.5f,scale)>=4){rightDragging=true;drag=false;followUnit=null;}
  if(rightDragging){Pan((mouse.X-rightLast.X)*1.7f,(mouse.Y-rightLast.Y)*1.7f);rightLast=mouse;}
 }
 void EndRightDrag(){bool click=rightPending&&!rightDragging;rightPending=rightDragging=false;Capture=false;if(click)CancelMapClick();}
 void TickCameraControls(float dt){
  if(rightDragging){float x=mouse.X-rightOrigin.X,y=mouse.Y-rightOrigin.Y;float d=(float)Math.Sqrt(x*x+y*y);if(d>8)Pan(x/Math.Max(1,d)*Math.Min(1100,d*8)*dt,y/Math.Max(1,d)*Math.Min(1100,d*8)*dt);return;}
  int dx=(cameraKeys.Contains(Keys.Right)?1:0)-(cameraKeys.Contains(Keys.Left)?1:0),dy=(cameraKeys.Contains(Keys.Down)?1:0)-(cameraKeys.Contains(Keys.Up)?1:0);
  if(dx!=0||dy!=0)Pan(dx*570*dt,dy*570*dt);
 }
 void CenterSelection(){var own=selection.Where(p=>p.Alive).ToArray();if(own.Length>0){followUnit=null;CenterAt(new PointF(own.Average(p=>p.P.X),own.Average(p=>p.P.Y)));}}
 void StopSelection(){foreach(var unit in selection)world.Halt(unit);markedPoint=null;markedEnemy=null;attack=false;world.Radio="所选部队已停止。";}
 bool CanDeploySelection(Piece o){return o.Deployed&&(o.T.Key=="mcv"||o.T.Key=="hq"||o.T.Civilian&&world.HouseCount(o)>0||o.T.Key=="ifv"&&world.Units.Any(p=>p.Alive&&p.Carrier==o));}
 void DeploySelection(){foreach(var o in selection.ToArray()){if(o.T.Key=="hq")world.Relocate(o,o.P);else DeployClick(o);}}
 int GroupNumber(Keys key){int n=(int)key-(int)Keys.D0;return n>=0&&n<=9?(n==0?10:n):-1;}
 int lastTeam;double lastTeamAt=-100;
 void CreateTeam(int number){foreach(var p in world.Units.Where(p=>p.Side==0&&p.Group==number))p.Group=0;foreach(var p in selection.Where(p=>p.Side==0&&p.Deployed))p.Group=number;}
 void RecallTeam(int number,bool add,bool center){
  var team=world.Live(0).Where(p=>p.Group==number).ToArray();if(team.Length==0)return;
  if(!center){if(!add)selection.Clear();foreach(var p in team)if(!selection.Contains(p))selection.Add(p);}
  if(center||!add&&lastTeam==number&&watch.Elapsed.TotalMilliseconds-lastTeamAt<SystemInformation.DoubleClickTime){followUnit=null;CenterAt(new PointF(team.Average(p=>p.P.X),team.Average(p=>p.P.Y)));}
  lastTeam=number;lastTeamAt=watch.Elapsed.TotalMilliseconds;
 }
 void ClickTeam(int number){if(world.Live(0).Any(p=>p.Group==number))RecallTeam(number,false,false);else CreateTeam(number);}
 void DeleteTeam(int number){foreach(var p in world.Units.Where(p=>p.Side==0&&p.Group==number))p.Group=0;}
 void SelectTypeNow(){
  double now=watch.Elapsed.TotalMilliseconds;bool global=selectType!=null&&now-lastTypeTap<=SystemInformation.DoubleClickTime;var kind=global?selectType:selection.Select(p=>p.T).FirstOrDefault();
  if(kind!=null){var view=new RectangleF(0,MapTop,MW,MH);selection=world.Live(0).Where(p=>p.T==kind&&(global||view.Contains(ScreenOf(p.P)))).ToList();world.Radio=(global?"全图":"当前画面")+" · "+kind.Name+" × "+selection.Count;}
  selectType=global?null:kind;lastTypeTap=now;
 }
 List<Piece> rankSeed;int rankStep=-1;
 void SelectRank(){
  if(rankSeed==null||!selection.All(p=>rankSeed.Contains(p))){rankSeed=selection.Where(p=>p.Deployed&&!p.T.Building).ToList();rankStep=-1;}
  if(rankSeed.Count==0)return;rankStep=(rankStep+1)%3;int rank=rankStep==0?2:rankStep==1?1:0;selection=rankSeed.Where(p=>p.Deployed&&p.Rank==rank).ToList();world.Radio=(rank==2?"精英":rank==1?"老兵":"普通")+" · "+selection.Count;
 }
 void Cheer(){foreach(var p in world.Live(0).Where(p=>p.T.Foot&&p.Path.Count==0&&p.Target==null))p.CheerAt=world.Time;world.Radio="步兵庆祝。";}
 void TogglePlanning(){if(PlanningActive)CommitPlans();else{planningPinned=true;planned.Clear();plannedLoops.Clear();patrolStarts.Clear();waypointOwner=null;waypointSelected=-1;world.Radio="路径规划：可为不同队伍设点，再点路径按钮同时执行。";}}
 bool SelectWaypoint(PointF point){
  foreach(var pair in planned)for(int i=0;i<pair.Value.Count;i++)if(Campaign.Dist(ScreenOf(pair.Value[i]),point)<10){waypointOwner=pair.Key;waypointSelected=i;if(selection.Contains(pair.Key)&&pair.Value.Count>1&&i<pair.Value.Count-1){plannedLoops.Add(pair.Key);patrolStarts[pair.Key]=i;}return true;}return false;
 }
 void DeleteMapAnnotation(){
  if(selectedBeacon!=null){beacons.Remove(selectedBeacon);selectedBeacon=null;beaconEditing=false;return;}
  List<PointF> points;if(waypointOwner!=null&&planned.TryGetValue(waypointOwner,out points)&&waypointSelected>=0&&waypointSelected<points.Count){points.RemoveAt(waypointSelected);plannedLoops.Remove(waypointOwner);patrolStarts.Remove(waypointOwner);waypointSelected=-1;}
 }
 bool AnnotationClick(PointF point,PointF where,Piece hit,Keys mods){
  if(beaconMode){beacons.Add(selectedBeacon=new LocalBeacon{Point=point,Created=world.Time});beaconMode=false;world.Radio="标记已放置；选中后 Enter 写注记，Delete 删除。";return true;}
  if(PlanningActive){
   if(SelectWaypoint(where))return true;
   if(hit!=null&&hit.Side==0){if((mods&Keys.Shift)==0)selection.Clear();if(!selection.Contains(hit))selection.Add(hit);return true;}
   AddPlannedPoint(point);return true;
  }
  if(hit==null&&mods==Keys.None){var b=beacons.LastOrDefault(p=>Campaign.Dist(ScreenOf(p.Point),where)<13);if(b!=null){selectedBeacon=b;return true;}}
  selectedBeacon=null;return false;
 }
 void BeginBeacon(){beaconMode=!beaconMode;place=null;attack=false;structureMode=StructureMode.None;world.Radio=beaconMode?"点击地图放置本地战场标记。":"已退出标记模式。";}
 protected override void OnKeyPress(KeyPressEventArgs e){base.OnKeyPress(e);if(beaconEditing&&!char.IsControl(e.KeyChar)){if(beaconText.Length<80)beaconText+=e.KeyChar;e.Handled=true;hudDirty=true;}}
 bool EditBeaconKey(KeyEventArgs e){
  if(beaconEditing){if(e.KeyCode==Keys.Enter){selectedBeacon.Text=beaconText;beaconEditing=false;}else if(e.KeyCode==Keys.Escape)beaconEditing=false;else if(e.KeyCode==Keys.Back&&beaconText.Length>0)beaconText=beaconText.Substring(0,beaconText.Length-1);return true;}
  if(e.KeyCode==Keys.Enter&&selectedBeacon!=null){beaconEditing=true;beaconText=selectedBeacon.Text;return true;}return false;
 }
 void DrawBeacons(Graphics g){
  foreach(var b in beacons){var p=ScreenOf(b.Point);if(p.X<0||p.X>MW||p.Y<0||p.Y>H)continue;Color c=b==selectedBeacon?gold:teal;
   Line(g,p.X,p.Y,p.X,p.Y-24,c,2);using(var brush=new SolidBrush(c))g.FillPolygon(brush,new[]{new PointF(p.X,p.Y-24),new PointF(p.X+15,p.Y-20),new PointF(p.X,p.Y-15)});
   if(b.Text.Length>0)Txt(g,b.Text,p.X+7,p.Y+4,12,pale,true);
  }
  if(beaconEditing)Txt(g,"注记："+beaconText+"_",20,H-73,15,gold,true);
 }
 void OpenOptions(){paused=!paused;help=false;keyboardOpen=false;diplomacyOpen=false;cameraKeys.Clear();rightPending=rightDragging=false;Capture=false;}
 void OpenDiplomacy(){diplomacyOpen=!diplomacyOpen;keyboardOpen=false;rightPending=rightDragging=false;Capture=false;hudDirty=true;}
 void RadarAction(PointF point,bool right){
  if(!world.Battlefield.Contains(point))return;
  if(right||selection.Count==0){followUnit=null;CenterAt(point);return;}
  var target=world.Units.Where(p=>p.Alive&&p.Carrier==null&&world.Visible(p)&&(p.T.Building?Campaign.Footprint(p.T,p.P).Contains(point):Campaign.Dist(p.P,point)<Math.Max(12,world.MapWidth/148f))).OrderBy(p=>Campaign.Dist(p.P,point)).FirstOrDefault();
  ContextOrderAt(ModifierKeys,point,target);
 }
 string screenshotFolderOverride=null;
 void SaveScreenshot(){
  try{string folder=screenshotFolderOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Greyhaven RTS","Screenshots");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"Greyhaven-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".png");if(frameBuffer!=null)frameBuffer.Save(file,System.Drawing.Imaging.ImageFormat.Png);world.Radio="截图已保存："+file;}
  catch(IOException){world.Radio="截图保存失败，请检查保存目录。";}catch(UnauthorizedAccessException){world.Radio="截图保存目录不能写入。";}
 }
}
}
