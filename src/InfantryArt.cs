using System;
using System.Drawing;
namespace Greyhaven {partial class CommandWindow {
 static Rectangle InfantryGaitCrop(string key,int view){return key=="squad"||key=="at"||key=="sniper"?InfantryV12.Crops["gait/"+(key=="at"?"at-mobile":key)][view]:GaitV09.Crop(key,view);}
 string CombatFrame(Piece p){
  float age=world.VisualTime-p.FiredAt;int frame=age>=0&&age<.23f?9+Math.Min(7,(int)(age/.23f*8)):Math.Max(0,Math.Min(8,(int)Math.Round(p.WeaponAim*8)));
  return "combat/"+p.T.Key+(p.Stance>=.999f?"-deployed":"")+"/"+(p.Dir+14)%16/2+"/"+frame;
 }
 bool CombatPose(Piece p){return Campaign.CombatInfantry(p)&&p.Alive&&(!Campaign.ChangingStance(p)||p.WeaponAim>.001f&&(p.Stance<=.001f||p.Stance>=.999f))&&(p.Stance>0||p.WeaponAim>0||world.Time-p.MovedAt>=.065f);}
 static Rectangle CombatCrop(string key){int last=key.LastIndexOf('/'),prior=key.LastIndexOf('/',last-1);return InfantryV12.Crops[key.Substring(0,prior)][key[prior+1]-'0'];}
 static RectangleF FootCropBox(RectangleF box,Rectangle crop){return new RectangleF(box.X+box.Width*crop.X/384,box.Y+box.Height*crop.Y/384,box.Width*crop.Width/384,box.Height*crop.Height/384);}
 void DrawCombatStamp(Graphics g,string key,RectangleF box,Color color){var crop=CombatCrop(key);DrawStamp(g,key,new Rectangle(Point.Empty,crop.Size),FootCropBox(box,crop),color);}
 void DrawInfantryCombat(Graphics g,Piece p,PointF feet,float size){DrawCombatStamp(g,CombatFrame(p),GaitBox(feet,size),UnitColor(p.Side));}
 void DrawInfantryStance(Graphics g,string key,int view,float stance,PointF feet,float size,Color color){
  string frame="stance/"+key+"/"+view+"/"+Math.Max(0,Math.Min(32,(int)Math.Round(stance*32)));
  DrawStamp(g,frame,new Rectangle(0,0,384,384),GaitBox(feet,size),color);
 }
}}
