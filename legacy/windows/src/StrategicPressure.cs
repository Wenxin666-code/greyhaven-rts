using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
    int AiRaidOpponent(int side, AiBrain brain) {
        var known=AiKnown(side);
        if ((StoryBattle&&!IslandMission) || Time<240 || OreReceipts[side]<3*LoadValue || brain.Launches==0)
            return AiOpponent(side,brain,known);

        // Once the economy can support reconnaissance, give recent targets a
        // break. The route-distance term still favours nearer opponents.
        return Enumerable.Range(0,FactionCount).Where(enemy=>enemy!=side&&!HasSurrendered(enemy)&&Remaining(enemy)>0)
            .OrderBy(enemy=>brain.LastDispatch[enemy]+Dist(Home(side),Home(enemy))/Tile)
            .DefaultIfEmpty(-1).First();
    }
    float AiWingStrength(IEnumerable<Piece> units) {
        float minimumHealth=RiverbendOperation==null?.25f:0;
        return units.Sum(unit=>unit.T.Price*Math.Max(minimumHealth,unit.Hp/unit.T.Hp));
    }
    bool LaunchAiWing(int side, AiBrain brain, Piece[] units, int opponent, Piece[] known, bool raid) {
        if (opponent < 0 || units.Length == 0) return false;
        units=units.Where(p=>!brain.HomeGuard.Contains(p)).ToArray();if(units.Length==0)return false;
        PointF goal = AiObjective(side, opponent, known);
        bool direct=raid||RiverbendOperation!=null;
        var destination=direct?goal:brain.Rally;
        if (QueueAiMarch(units, destination, splitByMobility:true) == 0) return false;
        brain.Wings.Add(new AiWing { Units = units, Opponent = opponent, Goal = destination, Objective=goal, Phase=direct?2:0, InitialValue=AiWingStrength(units), OrderedAt = Time, Raid = raid });
        brain.LastDispatch[opponent] = Time;
        brain.Launches++;
        if (raid) brain.Raids++;
        return true;
    }
    void AiPressureRaid(int side, AiBrain brain, Piece[] available, Piece[] known, bool defending) {
        int startingGuard=Math.Min(6,HomeGuardTarget(side));
        if (defending || Time < brain.NextRaid || brain.HomeGuard.Count(p=>p.Deployed&&p.Side==side)<startingGuard || brain.Wings.Any(w=>w.Raid) || !StoryRaidAllowed(side,brain,known)) return;
        var candidates = available.Where(p=>!p.T.Foot&&Dist(p.P,Home(side))<800)
            .OrderByDescending(ReferenceBalance.Speed).ThenBy(p=>Dist(p.P,brain.Rally)).Take(RaidTeamSize).ToArray();
        if (candidates.Length < 2) return;
        int enemy = AiRaidOpponent(side, brain);
        if (LaunchAiWing(side, brain, candidates, enemy, known, true))
            brain.NextRaid = Time + StoryRaidInterval(side) + side % 3 * 5;
    }
}
}
