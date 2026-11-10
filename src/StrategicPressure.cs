using System;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
    // Raid the same nearby opposition as the main army; do not send every base
    // across the map merely to cover a neglected faction. Production stays paid.
    int AiRaidOpponent(int side, AiBrain brain) {
        return AiOpponent(side,brain,AiKnown(side));
    }
    bool LaunchAiWing(int side, AiBrain brain, Piece[] units, int opponent, Piece[] known, bool raid) {
        if (opponent < 0 || units.Length == 0) return false;
        units=units.Where(p=>!brain.HomeGuard.Contains(p)).ToArray();if(units.Length==0)return false;
        PointF goal = AiObjective(side, opponent, known);
        if (QueueAiMarch(units, brain.Rally) == 0) return false;
        brain.Wings.Add(new AiWing { Units = units, Opponent = opponent, Goal = brain.Rally, Objective=goal, Phase=0, InitialValue=units.Sum(p=>p.T.Price*Math.Max(.25f,p.Hp/p.T.Hp)), OrderedAt = Time, Raid = raid });
        brain.LastDispatch[opponent] = Time;
        brain.Launches++;
        if (raid) brain.Raids++;
        return true;
    }
    void AiPressureRaid(int side, AiBrain brain, Piece[] available, Piece[] known, bool defending) {
        if (defending || Time < brain.NextRaid || brain.HomeGuard.Count<HomeGuardTarget(side) || brain.Wings.Count(w=>w.Raid&&w.Phase!=3)>=1 || !StoryRaidAllowed(side,brain,known)) return;
        var candidates = available.Where(p => Dist(p.P, Home(side)) < 800)
            .OrderBy(p => p.T.Key == "ifv" ? 0 : p.T.Key == "squad" ? 1 : 2)
            .ThenBy(p => Dist(p.P, brain.Rally)).Take(3).ToArray();
        if (candidates.Length < 2) return;
        int enemy = AiRaidOpponent(side, brain);
        if (LaunchAiWing(side, brain, candidates, enemy, known, true))
            brain.NextRaid = Time + StoryRaidInterval(side) + side % 3 * 5;
    }
}
}
