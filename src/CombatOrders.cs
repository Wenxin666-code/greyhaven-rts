using System;
using System.Drawing;

namespace Greyhaven {
public partial class Campaign {
 static bool Targetable(Piece e){return e.Alive&&e.Carrier==null&&(e.Ready>=1||e.T.Building);}
 Piece FindContact(Piece o){
  Piece best=null;float bestDistance=float.MaxValue;
  foreach(var e in Units){
   if(e.Side==o.Side||!Targetable(e)||!VisibleTo(o.Side,e))continue;
   float d=Dist(o.P,e.P);if(d>o.T.LockRange)continue;
   bool armed=e.Deployed&&e.T.Hit>0;
   bool bestArmed=best!=null&&best.Deployed&&best.T.Hit>0;
   if(best==null||(armed&&!bestArmed)||(armed==bestArmed&&d<bestDistance)){best=e;bestDistance=d;}
  }
  return best;
 }
 void TakeContact(Piece o,Piece e,ContactOrder kind){
  if(o.Target==null)o.ResumeMarch=o.Path.Count!=0;
  o.Contact=kind;o.Target=e;o.LastContact=e.P;o.Replan=0;o.Path.Clear();
 }
 void EndContact(Piece o){
  o.Target=null;o.Contact=ContactOrder.None;o.Path.Clear();
  if(o.ResumeMarch)Route(o,o.Goal);
  o.ResumeMarch=false;
 }
 void OnIncomingFire(Piece defender,Piece attacker){
  if(!defender.Deployed||defender.T.Hit==0||defender.FirePoint.HasValue||defender.Contact==ContactOrder.Directed)return;
  if(defender.T.Building&&Dist(defender.P,attacker.P)>defender.T.Range)return;
  if(defender.Target==attacker){defender.LastContact=attacker.P;defender.Contact=ContactOrder.ReturnFire;return;}
  if(defender.Contact==ContactOrder.ReturnFire&&defender.Target!=null&&Targetable(defender.Target)&&VisibleTo(defender.Side,defender.Target)&&Dist(defender.P,defender.Target.P)<=defender.T.Range)return;
  TakeContact(defender,attacker,ContactOrder.ReturnFire);
 }
 bool FightContact(Piece o,float dt){
  o.ScanIn-=dt;var e=o.Target;
  if(e!=null){
   bool visible=VisibleTo(o.Side,e);
   if(!Targetable(e)||(!visible&&o.Contact!=ContactOrder.ReturnFire)||
      (o.Contact==ContactOrder.Automatic&&Dist(o.P,e.P)>o.T.LockRange)){EndContact(o);e=null;}
  }
  if(e==null&&o.ScanIn<=0){
   o.ScanIn=.25f;e=FindContact(o);if(e!=null)TakeContact(o,e,ContactOrder.Automatic);
  }
  if(e==null)return false;
  bool seen=VisibleTo(o.Side,e);
  if(seen)o.LastContact=e.P;
  if(seen&&Dist(o.P,e.P)<=o.T.Range){
   if(!o.T.Building)Face(o,e.P,dt);
   if(o.Cool<=0&&(o.T.Key!="tank"||Aligned(o,e.P)))Fire(o,e);
   return true;
  }
  if(o.T.Building)return false;
  if(!seen&&Explored(o.Side,o.LastContact)){EndContact(o);return false;}
  if(o.Replan<=0){
   o.Replan=1;
   if(seen)ApproachPoint(o,o.LastContact);
   else if(!Route(o,o.LastContact))ApproachPoint(o,o.LastContact);
  }
  return false;
 }
}
}
