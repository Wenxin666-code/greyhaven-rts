using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
public partial class Campaign {
    internal const int BasinTestReinforcementCount = 16;

    internal List<Piece> SendBasinTestReinforcements() {
        var reinforcements = new List<Piece>(BasinTestReinforcementCount);
        if (BasinOperation == null || Won || Lost) return reinforcements;
        var anchor = Live(0).FirstOrDefault(p => p.T.Key is "hq" or "mcv") ?? Live(0).FirstOrDefault();
        if (anchor == null) return reinforcements;
        var type = Info("arty");
        for (int ring = 2; ring <= 12; ring++) {
            for (int y = -ring; y <= ring; y++) for (int x = -ring; x <= ring; x++) {
                if (Math.Max(Math.Abs(x), Math.Abs(y)) != ring) continue;
                var point = new PointF(anchor.P.X + x * Tile, anchor.P.Y + y * Tile);
                if (OreAt(point) != null || !ExitClear(null, type, point)) continue;
                reinforcements.Add(Spawn(type.Key, 0, point.X, point.Y));
                if (reinforcements.Count == BasinTestReinforcementCount) return reinforcements;
            }
        }
        return reinforcements;
    }

    internal bool DefeatBasinTestFaction(int defeated, int victor) {
        if (BasinOperation == null || Won || Lost || defeated <= 0 || defeated >= FactionCount ||
            victor < 0 || victor >= FactionCount || victor == defeated) return false;
        var winner = Live(victor).FirstOrDefault();
        var units = Units.Where(unit => unit.Alive && unit.Side == defeated).OrderBy(unit => unit.T.Building).ToArray();
        if (winner == null || units.Length == 0) return false;
        foreach (var unit in units)
            if (unit.Alive) CombatKill(unit, winner);
        return true;
    }
}
}
