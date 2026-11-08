using System;
using System.Drawing;
using System.Globalization;

namespace Greyhaven {
// Rules retain their existing game-second units. One rate controls production,
// movement, mining, weapons and AI together; rendering and camera input do not scale.
internal static class MatchTempo {
    internal const float DefaultRate = 2f;
    internal const float TutorialRate = 1f;
    internal const double Step = 1.0 / 30.0;
    internal const double MaxFrameElapsed = .25;
    internal const int MaxStepsPerFrame = 8;
    internal const float DecisionInterval = .75f;
    internal const float FirstPush = 48f;
    internal const float PushInterval = 24f;
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
        int steps = 0;
        while (Pending + 1e-8 >= MatchTempo.Step && steps < MatchTempo.MaxStepsPerFrame) {
            tick((float)MatchTempo.Step);
            Pending = Math.Max(0, Pending - MatchTempo.Step);
            steps++;
        }
        return steps;
    }
}

partial class CommandWindow {
    readonly SimulationStepper simulation = new SimulationStepper();
    float speed = MatchTempo.DefaultRate;
    double matchRealTime;
    double DisplayBattleSeconds { get { return matchRealTime > 0 ? matchRealTime : world.Time / speed; } }
    string MatchSpeedLabel { get { return "×" + speed.ToString("0.#", CultureInfo.InvariantCulture); } }

    void ResetMatchClock(bool tutorial) {
        simulation.Reset();
        matchRealTime = 0;
        speed = tutorial ? MatchTempo.TutorialRate : MatchTempo.DefaultRate;
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
        var values = new[] { 1f, 2f, 3f, 4f };
        var names = new[] { "慢速 1×", "标准 2×", "快速 3×", "极速 4×" };
        for (int i = 0; i < values.Length; i++) {
            float value = values[i];
            Button(g, 430 + i * 175, 664, 160, 34, names[i], () => ChangeMatchSpeed(value), Math.Abs(speed - value) < .01f);
            hits[hits.Count - 1].Id = "match-speed-" + ((int)value).ToString();
        }
    }
}
}
