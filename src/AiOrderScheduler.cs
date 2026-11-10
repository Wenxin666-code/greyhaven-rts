using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
namespace Greyhaven {
public partial class Campaign {
 sealed class AiMoveRequest {public Piece Unit;public PointF Goal;public March Formation;public int Attempt,Version;public bool AttackMove;}
 readonly Queue<AiMoveRequest> aiMoveRequests=new Queue<AiMoveRequest>();
 int aiMoveSerial;readonly Dictionary<Piece,int> aiMoveVersions=new Dictionary<Piece,int>();
 readonly HashSet<Piece> aiMovesPending=new HashSet<Piece>();
 int QueueAiMarch(Piece[] troops,PointF point,bool attackMove=true,bool replace=false){
  troops=troops.Where(p=>p.Deployed&&(replace||!aiMovesPending.Contains(p))).OrderBy(p=>p.Id).ToArray();if(troops.Length==0)return 0;
  var center=new PointF(troops.Average(p=>p.P.X),troops.Average(p=>p.P.Y));float distance=Math.Max(1,Dist(center,point));
  var formation=new March{Forward=new PointF((point.X-center.X)/distance,(point.Y-center.Y)/distance)};
  int columns=(int)Math.Ceiling(Math.Sqrt(troops.Length)),rows=(troops.Length+columns-1)/columns;float pitch=troops.Any(p=>!p.T.Foot)?Tile:Tile/2;
  var slots=new PointF[troops.Length];for(int i=0;i<slots.Length;i++)slots[i]=new PointF(point.X+(i%columns-(Math.Min(columns,slots.Length-i/columns*columns)-1)*.5f)*pitch,point.Y+((rows-1)*.5f-i/columns)*pitch);
  var match=MatchSlots(troops,slots);
  for(int i=0;i<troops.Length;i++){aiMovesPending.Add(troops[i]);int version=++aiMoveSerial;aiMoveVersions[troops[i]]=version;aiMoveRequests.Enqueue(new AiMoveRequest{Unit=troops[i],Goal=slots[match[i]],Formation=formation,Version=version,AttackMove=attackMove});}
  return troops.Length;
 }
 void TickAiMarchRequests(){
  // Human commands stay immediate. Only strategic AI group orders are spread
  // over frames, avoiding ten long A* searches in one rendering update.
  while(aiMoveRequests.Count>0){
   var request=aiMoveRequests.Dequeue();var unit=request.Unit;int version;
   if(!aiMoveVersions.TryGetValue(unit,out version)||version!=request.Version)continue;
   if(!unit.Deployed||unit.Side<=0||unit.Entering!=null||unit.Target!=null&&unit.Target.Alive){CancelAiMove(unit);continue;}
   // One alternate destination per update. A blocked group landing must not
   // run seventeen full-map searches in a single rendering callback.
   int attempt=request.Attempt++;float radius=attempt==0?0:attempt>8?42:23;double angle=attempt*Math.PI/4;
   var slot=new PointF(request.Goal.X+radius*(float)Math.Cos(angle),request.Goal.Y+radius*(float)Math.Sin(angle));
   if(Move(unit,slot,request.AttackMove)){
    CancelAiMove(unit);unit.Formation=request.Formation;request.Formation.Members.Add(unit);request.Formation.Starts[unit]=unit.P;
   }else if(request.Attempt<17)aiMoveRequests.Enqueue(request);else CancelAiMove(unit);
   break;
  }
 }
}
}
