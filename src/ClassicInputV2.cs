using System;
using System.Linq;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 bool escapeHeld;
 protected override bool IsInputKey(Keys data){var code=data&Keys.KeyCode;return code==Keys.Escape||code==Keys.Tab||code==Keys.Left||code==Keys.Right||code==Keys.Up||code==Keys.Down||base.IsInputKey(data);}
 protected override void OnKeyDown(KeyEventArgs raw){
  base.OnKeyDown(raw);hudDirty=true;
  if(raw.Alt&&raw.KeyCode==Keys.Enter){SetFullscreen(!fullscreen);raw.Handled=true;raw.SuppressKeyPress=true;return;}
  if(raw.KeyCode==Keys.Escape){
   raw.Handled=true;
   if(!escapeHeld){escapeHeld=true;EscapeOneLevel(raw);}
   return;
  }
  if(ChapterControlsLocked){
   if(raw.KeyCode==Keys.Enter&&!frontEnterHeld){
    frontEnterHeld=true;
    if(paused)OpenOptions();
    else if(ChapterScenePlaying)ContinueChapterScene();
   }
   raw.Handled=true;raw.SuppressKeyPress=true;return;
  }
  if(briefing){if(raw.KeyCode==Keys.Enter){if(frontEnterHeld)return;frontEnterHeld=true;}FrontKey(raw.KeyCode);return;}
  if(EditBeaconKey(raw)||CaptureKeyboard(raw)){raw.Handled=true;return;}
  if(diplomacyOpen){if(raw.KeyCode==Keys.Tab)diplomacyOpen=false;return;}
  KeyEventArgs e;if(!MapHotkey(raw,out e)){
   if(!paused&&!world.Won&&!world.Lost&&(raw.KeyCode==Keys.Left||raw.KeyCode==Keys.Right||raw.KeyCode==Keys.Up||raw.KeyCode==Keys.Down)){
    if(cameraKeys.Add(raw.KeyCode))Pan(raw.KeyCode==Keys.Left?-20:raw.KeyCode==Keys.Right?20:0,raw.KeyCode==Keys.Up?-20:raw.KeyCode==Keys.Down?20:0);
   }return;
  }
  raw.Handled=true;raw.SuppressKeyPress=true;
  if(e.KeyCode==Keys.Escape){OpenOptions();return;}if(world.Won||world.Lost||paused)return;
  if(e.KeyCode!=Keys.Y)rankSeed=null;
  if(e.KeyCode>=Keys.F5&&e.KeyCode<=Keys.F12){world.Radio="当前为单机对局，没有在线玩家接收语音。";return;}
  if(e.KeyCode==Keys.Tab){OpenDiplomacy();return;}
  if(e.KeyCode==Keys.S&&e.Shift){SaveScreenshot();return;}
  if(FieldKey(e)||ClassicKey(e))return;
  if(e.KeyCode==Keys.S){StopSelection();return;}
  if(e.KeyCode==Keys.D){DeploySelection();return;}
  if(e.KeyCode==Keys.Clear){CenterSelection();return;}
  if(e.KeyCode==Keys.NumPad2||e.KeyCode==Keys.NumPad8){SidebarScroll(e.KeyCode==Keys.NumPad2?1:-1);return;}
  if(e.KeyCode==Keys.B){BeginBeacon();return;}
  if(e.KeyCode==Keys.C){Cheer();return;}
  if(e.KeyCode==Keys.Delete){DeleteMapAnnotation();return;}
  if(e.KeyCode==Keys.Y){SelectRank();return;}
  if(e.KeyCode==Keys.F13){var seed=selection.ToArray();if(seed.Length>0){int band=(int)Math.Min(2,seed[0].Hp/seed[0].T.Hp*3);selection=seed.Where(p=>(int)Math.Min(2,p.Hp/p.T.Hp*3)==band).ToList();}return;}
  if(e.KeyCode==Keys.A){world.Radio="本关阵营互相敌对，结盟功能被关卡规则锁定。";return;}
  if(e.KeyCode==Keys.U){world.Radio="当前为单机对局，没有在线玩家可私聊。";return;}
  int number=GroupNumber(e.KeyCode);if(number>0){if(e.Control)CreateTeam(number);else RecallTeam(number,e.Shift,e.Alt);}
 }
 void EscapeOneLevel(KeyEventArgs raw){
  if(briefing){FrontKey(Keys.Escape);return;}
  if(EditBeaconKey(raw)||CaptureKeyboard(raw)){hits.Clear();Invalidate();return;}
  if(diplomacyOpen)diplomacyOpen=false;
  else if(help){help=false;paused=true;}
  else if(paused||(!ChapterControlsLocked&&world.OutcomeReady)){OpenMissions();return;}
  else{OpenOptions();return;}
  hits.Clear();Invalidate();
 }
 protected override void OnKeyUp(KeyEventArgs raw){
  base.OnKeyUp(raw);cameraKeys.Remove(raw.KeyCode);if(raw.KeyCode==Keys.Enter)frontEnterHeld=false;
  if(raw.KeyCode==Keys.Escape)escapeHeld=false;
  if(raw.KeyCode==typePhysicalKey||raw.KeyCode==Keys.T){tHeld=false;typePhysicalKey=Keys.None;}
  if((raw.KeyCode==planningPhysicalKey||raw.KeyCode==Keys.Z)&&zHeld){CommitPlans();planningPhysicalKey=Keys.None;}
  UpdateBattleCursor(true);
 }
}
}
