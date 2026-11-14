using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Greyhaven {
partial class CommandWindow {
 readonly PointF?[] bookmarks=new PointF?[4];readonly float[] savedZoom=new float[4];
 TypeInfo selectType;double lastTypeTap;bool tHeld;
 void GoHome(){
  followUnit=null;
  var home=world.Units.Where(u=>u.Side==0&&u.Alive&&(u.T.Key=="hq"||u.T.Key=="mcv")).OrderBy(u=>u.T.Key=="hq"?0:1).FirstOrDefault();
  if(home!=null)CenterAt(home.P);
 }
 void GoEvent(){
  followUnit=null;
  if(world.LastEvent.HasValue){CenterAt(world.LastEvent.Value);world.Radio=world.EventLabel;}
  else world.Radio="暂无战场事件。";
 }
 bool FieldKey(KeyEventArgs e){
  if(e.KeyCode!=Keys.T)selectType=null;
  if(e.KeyCode==Keys.H){GoHome();return true;}
  if(e.KeyCode==Keys.Space){GoEvent();return true;}
  int slot=e.KeyValue-(int)Keys.F1;
  if(slot>=0&&slot<4){
   if(e.Control){bookmarks[slot]=camera;savedZoom[slot]=zoom;world.Radio="已记录镜头 F"+(slot+1)+"。";}
   else if(bookmarks[slot].HasValue){followUnit=null;zoom=Math.Max(MinBattleZoom,Math.Min(MaxBattleZoom,savedZoom[slot]));CenterAt(bookmarks[slot].Value);}
   else world.Radio="先按 Ctrl+F"+(slot+1)+" 记录当前镜头。";return true;
  }
  if(e.KeyCode!=Keys.T)return false;
  if(tHeld)return true;tHeld=true;SelectTypeNow();return true;
 }
}
}
