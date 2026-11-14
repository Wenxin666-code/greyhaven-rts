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
  if(Campaign.CanDeployInfantry(o))return world.ToggleInfantry(o);
  if(o.T.Key=="mcv")return world.Deploy(o);
  if(o.T.Garrison&&world.HouseCount(o)>0){world.EvacuateHouse(o);return true;}
  if(o.T.Key=="ifv"&&world.Units.Any(u=>u.Alive&&u.Carrier==o)){world.Load(o);return true;}
  return false;
 }
 Piece CardProducer(TypeInfo t){
  if(t.Producer=="factory")return world.VehicleQueue(0);
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
   if(!source.ProductionPaused){source.ProductionPaused=true;world.Radio=(source.T.Key=="factory"?"车辆队列":source.T.Name+" #"+source.Id)+" 暂停；左键继续，右键取消。";return;}
   world.CancelOrder(source,0);
  }
 }
 void ProductionSweep(Graphics g,RectangleF portrait,float progress){
  if(progress>=1)return;var state=g.Save();g.IntersectClip(portrait);
  using(var brush=new SolidBrush(Color.FromArgb(155,ink)))g.FillPie(brush,portrait.X-21,portrait.Y-35,portrait.Width+42,portrait.Width+42,-90+360*progress,360*(1-progress));g.Restore(state);
 }
 void PlacementPreview(Graphics g,TypeInfo t,PointF at,Piece skip=null){
  var result=world.AssessPlacement(t,at,skip,0,true);
  if(t.Defense)DrawDefenseRange(g,Campaign.Middle(result.Bounds),t.Key=="bunker"?ReferenceBalance.WUCPara.Range:t.Range,result.Valid);
  foreach(var tile in result.Tiles){bool valid=tile.Valid&&result.Global==PlacementBlock.None;
   DrawPlacementCell(g,tile.Bounds,valid);
  }
  string text=result.Valid?"全部地格可放置 · 左键落地":result.Summary;
  const float x=18;bool defense=t.Defense;float y=H-138,width=Math.Min(MW-36,Math.Max(defense?580:510,90+text.Length*14));
  Plate(g,new RectangleF(x,y,width,89),C("#1b2a2d"),result.Valid?teal:red);
  Txt(g,t.Name+"  "+t.FoundationWidth+" × "+t.FoundationHeight+"　绿格可用 · 红格受阻",x+12,y+8,14,pale,true);
  Txt(g,text,x+12,y+34,14,result.Valid?teal:C("#ff9c82"),true);
  string spacing=skip!=null?"基地车展开只检查地基，不受原基地距离限制":"与己方常规建筑之间最多隔 "+Campaign.BuildAdjacentCells(t)+" 个空格";
  Txt(g,spacing+(defense?"　·　射程 "+((t.Key=="bunker"?ReferenceBalance.WUCPara.Range:t.Range)/(float)Campaign.Tile).ToString("0.#")+" 格":""),x+12,y+60,13,gold,true);
 }

}
}
