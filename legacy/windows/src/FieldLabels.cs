using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace Greyhaven {
partial class CommandWindow {
 readonly Dictionary<Tuple<string,Font,Color>,Bitmap> labels=new Dictionary<Tuple<string,Font,Color>,Bitmap>();
 readonly Queue<Tuple<string,Font,Color>> labelOrder=new Queue<Tuple<string,Font,Color>>();
 float labelScale;
 void ClearLabelCache(){foreach(var b in labels.Values)b.Dispose();labels.Clear();labelOrder.Clear();}
 void DrawLabel(Graphics g,string text,Font font,Color color,float x,float y){
  if(string.IsNullOrEmpty(text))return;
  if(labelScale!=scale){ClearLabelCache();labelScale=scale;}
  var key=Tuple.Create(text,font,color);Bitmap b;
  if(!labels.TryGetValue(key,out b)){
   if(labels.Count>=256){var first=labelOrder.Dequeue();labels[first].Dispose();labels.Remove(first);}
   var extent=g.MeasureString(text,font,PointF.Empty,StringFormat.GenericTypographic);
   b=new Bitmap(Math.Max(2,(int)Math.Ceiling((extent.Width+6)*scale)+2),Math.Max(2,(int)Math.Ceiling((extent.Height+4)*scale)+2),PixelFormat.Format32bppPArgb);
   using(var cg=Graphics.FromImage(b))using(var brush=new SolidBrush(color)){
    cg.ScaleTransform(scale,scale);cg.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    cg.DrawString(text,font,brush,2,1,StringFormat.GenericTypographic);
   }
   labels.Add(key,b);labelOrder.Enqueue(key);
  }
  var state=g.Save();g.ResetTransform();g.DrawImageUnscaled(b,(int)Math.Round(ox+(x-2)*scale),(int)Math.Round(oy+(y-1)*scale));g.Restore(state);
 }
}
}
