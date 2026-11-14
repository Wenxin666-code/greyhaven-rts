using System;
using System.Drawing;
using System.Globalization;

namespace Greyhaven {
// Rules retain their existing game-second units. One rate controls production,
// movement, mining, weapons and AI together; rendering and camera input do not scale.
internal static class MatchTempo {
    internal const float DefaultRate = 2f;
    internal const double Step = 1.0 / 30.0;
    internal const double MaxFrameElapsed = .25;
    internal const int MaxStepsPerFrame = 8;
    internal const int FastestMultiplier = 4;
    internal const int FastestStepsPerFrame = 12;
    internal const float DecisionInterval = .75f;
    internal const float FirstPush = 48f;
    internal const float PushInterval = 21f;
    internal const float UnderfilledWait = 18f;
    internal const float FirstRaid = 85f;
    internal const float RaidInterval = 52f;
    internal const float SecondRefinery = 90f;
    internal const float SecondFactory = 180f;
    internal const float SecondBarracks = 210f;
}

internal sealed class SimulationStepper {
    internal double Pending { get; private set; }
    internal void Reset() { Pending = 0; }
    internal int Advance(double elapsed, float rate, Action<float> tick) {
        if (elapsed <= 0 || Double.IsNaN(elapsed) || Double.IsInfinity(elapsed)) return 0;
        // Bound work in a single render callback, retaining short stalls for the
        // following callback instead of feeding a large movement/collision step.
        Pending = Math.Min(Pending + Math.Min(elapsed, MatchTempo.MaxFrameElapsed) * rate, rate);
        int limit = rate >= MatchTempo.DefaultRate * MatchTempo.FastestMultiplier
            ? MatchTempo.FastestStepsPerFrame : MatchTempo.MaxStepsPerFrame;
        int steps = 0;
        while (Pending + 1e-8 >= MatchTempo.Step && steps < limit) {
            tick((float)MatchTempo.Step);
            Pending = Math.Max(0, Pending - MatchTempo.Step);
            steps++;
        }
        return steps;
    }
}

#if !GODOT
partial class CommandWindow {
    readonly SimulationStepper simulation = new SimulationStepper();
    float speed = MatchTempo.DefaultRate;
    double matchRealTime;
    double DisplayBattleSeconds { get { return world.FirstChapter || world.RiverbendOperation != null || matchRealTime > 0 ? matchRealTime : world.Time / speed; } }
    string MatchSpeedLabel { get { return "×" + (speed / MatchTempo.DefaultRate).ToString("0.#", CultureInfo.InvariantCulture); } }

    void ResetMatchClock() {
        simulation.Reset();
        matchRealTime = 0;
        speed = MatchTempo.DefaultRate;
    }
    void AdvanceMatch(double elapsed) {
        if (briefing || paused || world.Won || world.Lost) { simulation.Reset(); return; }
        if (elapsed <= 0 || Double.IsNaN(elapsed) || Double.IsInfinity(elapsed)) return;
        matchRealTime += elapsed;
        simulation.Advance(elapsed, speed, dt => { if (!world.Won && !world.Lost) world.Tick(dt); });
    }
    void ChangeMatchSpeed(float rate) {
        speed = rate;
        simulation.Reset();
        hudDirty = true;
    }
    void DrawMatchSpeed(Graphics g) {
        Txt(g, "对局速度", 328, 673, 15, pale, true);
        for (int multiplier = 1; multiplier <= MatchTempo.FastestMultiplier; multiplier++) {
            float rate = MatchTempo.DefaultRate * multiplier;
            string label = multiplier.ToString(CultureInfo.InvariantCulture);
            Button(g, 430 + (multiplier - 1) * 179, 664, 164, 34, label + "×", () => ChangeMatchSpeed(rate), speed == rate);
            hits[hits.Count - 1].Id = "match-speed-" + label;
        }
    }
}
#endif
}
