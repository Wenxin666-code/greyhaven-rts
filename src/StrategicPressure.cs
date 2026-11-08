using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
    // Every faction covers neglected opponents; no preference for the human side
    // and no access to unexplored enemy army positions or free production.
    int AiRaidOpponent(int side, AiBrain brain) {
        return Enumerable.Range(0, FactionCount)
            .Where(enemy => enemy != side && Remaining(enemy) > 0 && !HasSurrendered(enemy))
            .OrderBy(enemy => brain.LastDispatch[enemy])
            .ThenBy(enemy => Dist(Home(side), Home(enemy)))
            .DefaultIfEmpty(-1).First();
    }
    bool LaunchAiWing(int side, AiBrain brain, Piece[] units, int opponent, Piece[] known, bool raid) {
        if (opponent < 0 || units.Length == 0) return false;
        PointF goal = AiObjective(side, opponent, known);
        if (QueueAiMarch(units, goal) == 0) return false;
        brain.Wings.Add(new AiWing { Units = units, Opponent = opponent, Goal = goal, OrderedAt = Time, Raid = raid });
        brain.LastDispatch[opponent] = Time;
        brain.Launches++;
        if (raid) brain.Raids++;
        return true;
    }
    void AiPressureRaid(int side, AiBrain brain, Piece[] available, Piece[] known, bool defending) {
        if (defending || Time < brain.NextRaid) return;
        var candidates = available.Where(p => Dist(p.P, Home(side)) < 800)
            .OrderBy(p => p.T.Key == "ifv" ? 0 : p.T.Key == "squad" ? 1 : 2)
            .ThenBy(p => Dist(p.P, brain.Rally)).Take(3).ToArray();
        if (candidates.Length < 2) return;
        int enemy = AiRaidOpponent(side, brain);
        if (LaunchAiWing(side, brain, candidates, enemy, known, true))
            brain.NextRaid = Time + MatchTempo.RaidInterval + side % 3 * 5;
    }
}
}
