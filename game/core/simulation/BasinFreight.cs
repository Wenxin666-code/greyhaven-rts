using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Greyhaven {
internal sealed class BasinFreight {
    internal readonly record struct Car(PointF Position, PointF Direction);
    const float DepartureStart = 24.4f * Campaign.Tile;
    readonly PointF[] track = RiverbendMap.BasinRail.Select(p => new PointF(p.X * Campaign.Tile, p.Y * Campaign.Tile)).ToArray();
    readonly float[] joints;
    readonly HashSet<Piece> clearing = new();
    readonly List<Piece> railTraffic = new();
    readonly Campaign world;
    readonly RectangleF[] parkedBarriers;
    float distance = DepartureStart, nextClearance;
    bool departing;
    internal float Travel { get; private set; }
    internal float DepartureProgress => (distance - DepartureStart) / (joints[^1] - DepartureStart);
    internal float Speed { get; private set; }
    internal bool AtEnd => distance >= joints[^1] - .1f;
    internal bool RouteClear { get; private set; }
    internal RectangleF[] DepotBarriers => departing ? Array.Empty<RectangleF>() : parkedBarriers;
    internal int Loads { get; set; }
    internal Car Engine => AlongTrack(distance);
    internal Car Wagon(int index) => AlongTrack(distance - (index + 1) * 3.7f * Campaign.Tile);
    internal PointF LoadingBay(int index) {
        var wagon = Wagon(index);
        float offset = Campaign.Tile * 2.3f;
        return new PointF(wagon.Position.X - wagon.Direction.Y * offset, wagon.Position.Y + wagon.Direction.X * offset);
    }

    internal BasinFreight(Campaign world) {
        this.world = world;
        joints = new float[track.Length];
        for (int i = 1; i < track.Length; i++)
            joints[i] = joints[i - 1] + Campaign.Dist(track[i - 1], track[i]);
        parkedBarriers = new[] { Engine, Wagon(0), Wagon(1) }.Select(car => {
            float x = Campaign.Tile * (Math.Abs(car.Direction.X) * 1.58f + Math.Abs(car.Direction.Y) * .67f);
            float y = Campaign.Tile * (Math.Abs(car.Direction.Y) * 1.58f + Math.Abs(car.Direction.X) * .67f);
            return RectangleF.FromLTRB(car.Position.X - x, car.Position.Y - y, car.Position.X + x, car.Position.Y + y);
        }).ToArray();
    }

    Car AlongTrack(float at) {
        int next = Array.BinarySearch(joints, Math.Clamp(at, 0, joints[^1]));
        if (next < 0) next = ~next;
        next = Math.Clamp(next, 1, track.Length - 1);
        var a = track[next - 1];
        var b = track[next];
        float length = joints[next] - joints[next - 1];
        float t = Math.Clamp((at - joints[next - 1]) / length, 0, 1);
        return new Car(new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t),
            new PointF((b.X - a.X) / length, (b.Y - a.Y) / length));
    }

    bool FindTrack(PointF p, out float along) {
        along = 0;
        float closest = Campaign.Tile * 1.45f;
        bool found = false;
        for (int i = 1; i < track.Length; i++) {
            var a = track[i - 1];
            var b = track[i];
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float length = joints[i] - joints[i - 1];
            float t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (length * length), 0, 1);
            float gap = Campaign.Dist(p, new PointF(a.X + t * dx, a.Y + t * dy));
            if (gap >= closest) continue;
            closest = gap;
            along = joints[i - 1] + length * t;
            found = true;
        }
        return found;
    }

    internal void PrepareDeparture() {
        railTraffic.AddRange(world.Live(0).Where(u => !u.T.Building && IsOnTrack(u.P)));
        RouteClear = railTraffic.Count == 0;
    }

    internal bool IsOnTrack(PointF point) => FindTrack(point, out _);

    internal void ClearTrack(float dt) {
        foreach (var unit in clearing.ToArray()) {
            world.MoveHarborVehicle(unit, dt, 4.1f, replanTraffic: true, followTraffic: false);
            if (unit.Path.Count == 0 || !unit.Deployed) clearing.Remove(unit);
        }
        if (world.VisualTime < nextClearance) return;
        nextClearance = world.VisualTime + .17f;
        railTraffic.RemoveAll(unit => !unit.Deployed || !clearing.Contains(unit) && !IsOnTrack(unit.P));
        foreach (var unit in railTraffic)
            if (!clearing.Contains(unit) && FindTrack(unit.P, out float along)) OrderPullOff(unit, along);
        RouteClear = railTraffic.Count == 0;
    }

    void OrderPullOff(Piece unit, float along) {
        foreach (float shift in new[] { 0f, 2.5f, -2.5f, 5f, -5f, 9f, -9f, 14f, -14f, 20f, -20f }) {
            var sample = AlongTrack(along + shift * Campaign.Tile);
            var from = sample.Position;
            float preferred = (unit.P.X - from.X) * -sample.Direction.Y + (unit.P.Y - from.Y) * sample.Direction.X;
            float side = preferred < 0 ? -1 : 1;
            foreach (float offset in new[] { 2.7f * side, -2.7f * side, 4.7f * side, -4.7f * side }) {
                var target = new PointF(from.X - sample.Direction.Y * offset * Campaign.Tile,
                    from.Y + sample.Direction.X * offset * Campaign.Tile);
                if (FindTrack(target, out _) || clearing.Any(other => Campaign.Dist(other.Goal, target) < Campaign.Tile * 1.4f))
                    continue;
                if (!world.TrySetHarborRoute(unit, target) && !world.MoveHarborDeparture(unit, target)) continue;
                clearing.Add(unit);
                return;
            }
        }
    }

    internal void Roll(float dt) {
        if (!RouteClear || AtEnd) return;
        if (!departing) {
            departing = true;
            world.ReleaseBasinDepot();
        }
        Speed = Math.Min(395, Speed + 180 * dt);
        float advance = Math.Min(Speed * dt, joints[^1] - distance);
        distance += advance;
        Travel += advance;
    }
}

public partial class Campaign {
    internal void ReleaseBasinDepot() => Changed();
}
}
