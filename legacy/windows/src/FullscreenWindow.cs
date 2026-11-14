using System.Drawing;
using System.Windows.Forms;
namespace Greyhaven {
partial class CommandWindow {
 bool fullscreen;Rectangle windowedBounds;
 void SetFullscreen(bool enabled){
  if(fullscreen==enabled)return;var display=Visible?Screen.FromControl(this):Screen.FromPoint(Cursor.Position);StartPosition=FormStartPosition.Manual;
  ResetEdgeScroll();panning=false;drag=false;Capture=false;
  if(enabled){windowedBounds=Bounds;WindowState=FormWindowState.Normal;FormBorderStyle=FormBorderStyle.None;Bounds=display.Bounds;}
  else{WindowState=FormWindowState.Normal;FormBorderStyle=FormBorderStyle.Sizable;Bounds=windowedBounds;}
  fullscreen=enabled;hudDirty=true;Invalidate();
 }
}
}
