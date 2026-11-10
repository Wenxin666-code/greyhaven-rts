using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 void CommandGlyph(Graphics g,string key,float x,float y,Color c){
  using(var pen=new Pen(c,1.5f)){
   if(key=="deploy"){g.DrawRectangle(pen,x-7,y-6,14,12);g.DrawLines(pen,new[]{new PointF(x-9,y-10),new PointF(x,y-2),new PointF(x+9,y-10)});g.DrawLine(pen,x,y-13,x,y-3);}
   else if(key=="guard"){g.DrawPolygon(pen,new[]{new PointF(x-8,y-8),new PointF(x+8,y-8),new PointF(x+7,y+3),new PointF(x,y+10),new PointF(x-7,y+3)});g.DrawLine(pen,x,y-5,x,y+5);g.DrawLine(pen,x-4,y,x+4,y);}
   else if(key=="type"){g.DrawRectangle(pen,x-9,y-8,8,8);g.DrawRectangle(pen,x+2,y-8,8,8);g.DrawRectangle(pen,x-4,y+3,8,8);}
   else if(key=="waypoint"){g.DrawLines(pen,new[]{new PointF(x-9,y+7),new PointF(x-3,y-6),new PointF(x+4,y+3),new PointF(x+10,y-8)});foreach(var p in new[]{new PointF(x-9,y+7),new PointF(x-3,y-6),new PointF(x+4,y+3),new PointF(x+10,y-8)})g.DrawRectangle(pen,p.X-2,p.Y-2,4,4);}
   else if(key=="beacon"){g.DrawLine(pen,x-5,y+10,x-5,y-11);g.DrawPolygon(pen,new[]{new PointF(x-5,y-11),new PointF(x+10,y-7),new PointF(x-5,y-2)});g.DrawLine(pen,x-11,y+10,x+4,y+10);}
  }
 }
 void ToolbarButton(Graphics g,float x,string id,string label,string glyph,Action action,bool enabled=true,bool lit=false,Action right=null){
  float y=H-43;var box=new RectangleF(x,y,44,35);Plate(g,box,lit?C("#765f35"):panel,enabled?C("#8a8e7d"):C("#38494a"));
  if(glyph!=null)CommandGlyph(g,glyph,x+22,y+16,enabled?pale:dim);else Txt(g,label,x+12,y+7,17,enabled?pale:dim,true);
  if(enabled)hits.Add(new HitBox{Box=box,Id=id,Run=action,Right=right});
  if(box.Contains(mouse)){float width=255,left=Math.Min(MW-width-6,Math.Max(6,x));Plate(g,new RectangleF(left,H-83,width,31),panel,gold);Txt(g,label,left+9,H-77,12,pale);}
 }
 void DrawAdvancedCommands(Graphics g){
  if(!advancedBarOpen){Button(g,5,H-27,27,23,"›",()=>advancedBarOpen=true);hits.Last().Id="command-toggle";return;}
  Metal(g,4,H-47,385,43);Button(g,8,H-40,24,30,"‹",()=>advancedBarOpen=false);hits.Last().Id="command-toggle";
  ToolbarButton(g,39,"command-team-1","1",null,()=>ClickTeam(1),world.Live(0).Any(p=>p.Group==1)||selection.Count>0,false,()=>DeleteTeam(1));
  ToolbarButton(g,87,"command-team-2","2",null,()=>ClickTeam(2),world.Live(0).Any(p=>p.Group==2)||selection.Count>0,false,()=>DeleteTeam(2));
  ToolbarButton(g,135,"command-type","同类选择"+Shortcut("TypeSelect"),"type",SelectTypeNow,selection.Count>0);
  ToolbarButton(g,183,"command-deploy","部署／收起／卸载"+Shortcut("DeployObject"),"deploy",DeploySelection,selection.Any(CanDeploySelection));
  ToolbarButton(g,231,"command-guard","原地警戒"+Shortcut("GuardObject"),"guard",GuardSelection,selection.Any(p=>p.Deployed&&!p.T.Building));
  ToolbarButton(g,279,"command-waypoints","路径规划"+Shortcut("PlanningMode"),"waypoint",TogglePlanning,true,PlanningActive);
  ToolbarButton(g,327,"command-beacon","战场标记"+Shortcut("PlaceBeacon"),"beacon",BeginBeacon,true,beaconMode);
 }
 void DrawDiplomacy(Graphics g){
  Plate(g,new RectangleF(280,134,910,535),panel,gold);Txt(g,"阵营信息",312,158,30,pale,true);Txt(g,"本关阵营关系固定；各敌方互相敌对。",314,205,16,dim);
  for(int side=0;side<world.FactionCount;side++){float y=250+side*50;Rect(g,315,y+8,19,19,UnitColor(side));Txt(g,world.StoryFactionName(side),352,y+7,19,pale,true);Txt(g,side==0?"我方":world.HasSurrendered(side)?"已投降":world.Remaining(side)==0?"已歼灭":"互相敌对",607,y+8,16,dim);Txt(g,world.StoryFactionTask(side),787,y+10,14,dim);}
  Button(g,935,601,205,39,"返回战场 [Tab]",()=>diplomacyOpen=false,true);
 }
 void SidebarScroll(int direction){int rows=(ProductionCards().Length+1)/2;sidebarRow=Math.Max(0,Math.Min(Math.Max(0,rows-6),sidebarRow+direction));hudDirty=true;}
 void DrawStatusMessage(Graphics g){string message=world.Time<world.StoryNoticeUntil?world.StoryNotice:world.Radio;if(!String.IsNullOrEmpty(message))Txt(g,message.Length>78?message.Substring(0,78):message,14,12,14,gold,true);}
 void DrawSelectionPips(Graphics g,Piece unit,PointF p){
  if(!selection.Contains(unit)||!unit.Alive)return;
  if(unit.Rank>0){float x=p.X-8,y=p.Y-5;for(int i=0;i<unit.Rank;i++){Line(g,x,y-i*5,x+4,y-i*5-3,gold);Line(g,x+4,y-i*5-3,x+8,y-i*5,gold);}}
  if(unit.T.Key=="miner"){int fill=(int)Math.Ceiling(unit.Cargo/Campaign.LoadValue*5);for(int i=0;i<5;i++)Rect(g,p.X-10+i*4,p.Y+5,3,2,i<fill?gold:ink);}
 }
}
}
