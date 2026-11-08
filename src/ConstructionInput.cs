using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 enum StructureMode { None,Repair,Sell }
 StructureMode structureMode;
 void ProductionTab(int filter){
  productionFilter=filter;army=filter==2||filter==3;queueOpen=false;sidebarRow=0;place=null;attack=false;structureMode=StructureMode.None;
  if(!army){var job=world.Construction(0,filter==1?1:0);if(job!=null&&job.Ready)place=job.Type;}
 }
 void StructureTool(StructureMode mode){structureMode=structureMode==mode?StructureMode.None:mode;place=null;attack=false;world.Radio=structureMode==StructureMode.Sell?"出售模式：点己方建筑，右键退出。":structureMode==StructureMode.Repair?"维修模式：点受损建筑开关维修，右键退出。":"已退出建筑工具。";}
 bool DeployClick(Piece o){
  if(o==null||!o.Deployed||o.Side!=0)return false;
  if(o.T.Key=="mcv")return world.Deploy(o);
  if(o.T.Civilian&&world.HouseCount(o)>0){world.EvacuateHouse(o);return true;}
  if(o.T.Key=="ifv"&&world.Units.Any(u=>u.Alive&&u.Carrier==o)){world.Load(o);return true;}
  return false;
 }
 Piece CardProducer(TypeInfo t){
  return world.Live(0).Where(p=>p.T.Key==t.Producer&&p.Queue.Contains(t))
   .OrderByDescending(p=>selection.Contains(p)).ThenByDescending(p=>p.Queue.Peek()==t)
   .ThenByDescending(p=>p.ProductionPaused&&p.Queue.Peek()==t).ThenByDescending(p=>p.Queue.Peek()==t?p.Work/t.Seconds:0)
   .ThenByDescending(p=>p.Primary).ThenBy(p=>p.Id).FirstOrDefault()??world.PrimaryFactory(0,t.Producer);
 }
 void BuildCardClick(TypeInfo t,int count=1){
  if(t.Building){var job=world.Construction(t);if(job!=null&&job.Ready){place=t;attack=false;structureMode=StructureMode.None;world.Radio="左键放置绿色地基；右键收回预览，保留就绪建筑。";}else world.StartConstruction(t);}
  else{var source=CardProducer(t);if(source!=null&&source.ProductionPaused&&source.Queue.Count>0&&source.Queue.Peek()==t)source.ProductionPaused=false;else for(int i=0;i<count;i++)if(!world.Train(t,0,selection.FirstOrDefault()))break;}
 }
 void BuildCardRight(TypeInfo t,int count=1){
  if(t.Building){world.HoldConstruction(t);if(world.Construction(t)==null&&place==t)place=null;return;}
  for(int i=0;i<count;i++){
   var source=CardProducer(t);if(source==null)return;var jobs=source.Queue.ToArray();int index=Array.LastIndexOf(jobs,t);if(index<0)return;
   if(index>0){world.CancelOrder(source,index);continue;}
   if(!source.ProductionPaused){source.ProductionPaused=true;world.Radio=source.T.Name+" #"+source.Id+" 暂停；左键继续，右键取消。";return;}
   world.CancelOrder(source,0);
  }
 }
 void ProductionSweep(Graphics g,RectangleF portrait,float progress){
  if(progress>=1)return;var state=g.Save();g.IntersectClip(portrait);
  using(var brush=new SolidBrush(Color.FromArgb(155,ink)))g.FillPie(brush,portrait.X-21,portrait.Y-35,portrait.Width+42,portrait.Width+42,-90+360*progress,360*(1-progress));g.Restore(state);
 }
 void PlacementPreview(Graphics g,TypeInfo t,PointF at,Piece skip=null){
  var r=Campaign.Footprint(t,at);bool range=world.FoundationLevel(t,at)&&(skip!=null||world.InBuildRange(t,r,0)&&world.Live(0).Any(u=>u.T.Key=="hq"));
  for(float y=r.Top;y<r.Bottom;y+=Campaign.Tile)for(float x=r.Left;x<r.Right;x+=Campaign.Tile){
   var cell=new RectangleF(x,y,Campaign.Tile,Campaign.Tile);bool valid=range&&world.PlacementCell(cell,skip,0);
   DrawPlacedTiles(g,cell,valid?Color.FromArgb(83,235,91):Color.FromArgb(242,66,51));
  }
 }
}
}
