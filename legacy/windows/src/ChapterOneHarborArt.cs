using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace Greyhaven {
    internal sealed class HarborSprite {
        readonly string name;
        readonly Rectangle bounds;
        internal readonly int Frames;

        internal HarborSprite(string name, int frames, Rectangle bounds) {
            this.name = name;
            this.bounds = bounds;
            Frames = frames;
        }

        internal string Frame(float phase) {
            var number = (int)Math.Round(Math.Max(0, Math.Min(1, phase)) * (Frames - 1));
            return "harbor/" + name + "/" + number;
        }

        internal Rectangle Source { get { return new Rectangle(Point.Empty, bounds.Size); } }
        internal int PivotColumn { get { return -bounds.X; } }

        internal RectangleF At(PointF screen, float zoom) {
            float scale = zoom / HarborSprites.Density;
            return new RectangleF(screen.X + bounds.X * scale, screen.Y + bounds.Y * scale,
                bounds.Width * scale, bounds.Height * scale);
        }
    }

    internal static partial class HarborSprites {
        internal static readonly Lazy<IndexedSpritePack> Atlas =
            new Lazy<IndexedSpritePack>(() => new IndexedSpritePack("harbor-v01.pak"));
    }

    partial class CommandWindow {
        static readonly Color HarborCoat = Color.FromArgb(171, 181, 172);
        static readonly PointF CraneGround = RiverbendMap.PortPoint(47, 16.7f);
        static readonly PointF QuayBollard = RiverbendMap.PortPoint(48.15f, 15.8f);

        static PointF HarborOffset(PointF center, float x, float y) {
            return new PointF(center.X + x * Campaign.Tile, center.Y + y * Campaign.Tile);
        }

        PointF HarborScreen(PointF ground, float height = 0) {
            var p = ScreenOf(ground);
            return new PointF(p.X, p.Y - height * Campaign.Tile * zoom);
        }

        bool InHarborShot(PointF p, bool cinematic) {
            if (!cinematic && !world.Explored(0, p)) return false;
            var screen = ScreenOf(p);
            return screen.X >= -460 && screen.X <= MW + 460 && screen.Y >= -180 && screen.Y <= MH + 210;
        }

        void DrawHarborSprite(Graphics g, HarborSprite sprite, PointF screen, float magnification, float phase, Color? tint) {
            DrawStamp(g, sprite.Frame(phase), sprite.Source, sprite.At(screen, magnification), tint);
        }

        void DrawHarborOperation(Graphics g, bool cinematic) {
            var harbor = world.HarborOperation;
            if (harbor == null) return;

            var berthGap = (HarborOperation.CargoBerth.Y - harbor.CargoShip.Y) / Campaign.Tile;
            float turn = HarborOperation.Blend(0, 1, (berthGap - .65f) / 5.65f);
            var craft = harbor.LandingHull;
            if (!harbor.CargoOutside && InHarborShot(harbor.CargoShip, cinematic)) {
                DrawVesselWater(g, harbor.CargoShip, turn, 4.45f, 1.35f, harbor.CargoWake);
                DrawHarborSprite(g, HarborSprites.Supply, HarborScreen(harbor.CargoShip, Bob(.027f)), zoom, turn, HarborCoat);
            }
            if (InHarborShot(craft, cinematic)) {
                DrawVesselWater(g, craft, 0, 2.31f, .91f, harbor.LandingWake);
                float bob = Bob(.024f * (1 - harbor.RampOpen));
                var anchor = HarborScreen(craft, bob);
                var coat = UnitColor(0);
                DrawHarborSprite(g, HarborSprites.Landing, anchor, zoom, harbor.RampOpen, coat);
                var passengers = world.Units.Where(harbor.AboardLandingCraft).OrderBy(o => o.P.X + o.P.Y).ToArray();
                foreach (var actor in passengers)
                    DrawFieldSprite(g, actor, HarborScreen(actor.P, harbor.DeckHeight(actor) + bob), actor.T.Size * zoom);
                if (passengers.Length > 0)
                    DrawHarborSprite(g, HarborSprites.Cover, anchor, zoom, harbor.RampOpen, coat);
            }

            if (InHarborShot(QuayBollard, cinematic)) DrawQuayFittings(g, harbor, berthGap);
            if (InHarborShot(CraneGround, cinematic)) DrawQuayCrane(g, harbor, berthGap);
            DrawQuayTruckBays(g, cinematic);

            var gate = RiverbendMap.HarborGateCenter;
            if (InHarborShot(gate, cinematic)) {
                DrawHarborContact(g, RiverbendMap.HarborGatePost(-1), 23, 10);
                DrawHarborContact(g, RiverbendMap.HarborGatePost(1), 23, 10);
                if (harbor.GateLift < 1) {
                    DrawHarborSprite(g, HarborSprites.Gate, HarborScreen(gate), zoom, harbor.GateLift, HarborCoat);
                    DrawFreightGateLamp(g, -1, harbor.GateBlocked);
                    DrawFreightGateLamp(g, 1, harbor.GateBlocked);
                }
            }

            var sign = RiverbendMap.PortPoint(65.5f, 28);
            if (InHarborShot(sign, cinematic)) DrawHarborRoadSign(g, sign);
        }

        void DrawQuayTruckBays(Graphics g, bool cinematic) {
            var paint = Color.FromArgb(195, 201, 174, 104);
            foreach (var bay in HarborOperation.QuayTruckBays) {
                if (!InHarborShot(bay, cinematic)) continue;
                var left = HarborScreen(HarborOffset(bay, -.62f, -.86f));
                var right = HarborScreen(HarborOffset(bay, .62f, -.86f));
                HarborLine(g, left, right, paint, 1.15f);
                HarborLine(g, left, HarborScreen(HarborOffset(bay, -.62f, .86f)), paint, 1.15f);
                HarborLine(g, right, HarborScreen(HarborOffset(bay, .62f, .86f)), paint, 1.15f);
            }
        }

        void DrawHarborTruckLights(Graphics g, Piece vehicle) {
            var harbor = world.HarborOperation;
            if (!vehicle.Alive || harbor == null || vehicle != harbor.RoadVehicle) return;
            if (harbor.Opening && harbor.Darkness > .32f)
                DrawHarborTruckLamps(g, vehicle.P, -.49f, C("#e3c78c"), C("#fff0bf"));
            if (harbor.BrakeLights)
                DrawHarborTruckLamps(g, vehicle.P, .49f, C("#e94b2f"), C("#ff9b6d"));
        }

        void DrawHarborTruckLamps(Graphics g, PointF position, float bumper, Color halo, Color light) {
            using (var glow = new SolidBrush(Color.FromArgb(96, halo))) {
                for (int side = -1; side <= 1; side += 2) {
                    var p = HarborScreen(HarborOffset(position, bumper, side * .31f), .26f);
                    g.FillEllipse(glow, p.X - 3 * zoom, p.Y - 2 * zoom, 6 * zoom, 4 * zoom);
                    Rect(g, p.X - .85f * zoom, p.Y - .65f * zoom, 1.7f * zoom, 1.3f * zoom, light);
                }
            }
        }

        float Bob(float height) {
            return (float)Math.Sin(world.VisualTime * 1.44) * height;
        }

        void DrawVesselWater(Graphics g, PointF center, float turn, float halfLength, float halfWidth, float motion) {
            double angle = turn * Math.PI * .5;
            var f = new PointF((float)Math.Cos(angle), -(float)Math.Sin(angle));
            var side = new PointF(-f.Y, f.X);
            float clock = world.VisualTime * .51f;
            for (int ring = 0; ring < 3; ring++) {
                float drift = (clock + ring / 3f) % 1;
                float alpha = (1 - drift) * (36 + 65 * motion);
                for (int sign = -1; sign <= 1; sign += 2) {
                    float across = sign * (halfWidth + .18f + drift * (.30f + .62f * motion));
                    float wake = halfLength * .72f + motion * (.45f + drift * 1.14f);
                    var start = HarborOffset(center, side.X * across - f.X * wake, side.Y * across - f.Y * wake);
                    var bend = HarborOffset(center, side.X * (across + sign * .12f), side.Y * (across + sign * .12f));
                    var end = HarborOffset(center, side.X * across + f.X * halfLength * .54f,
                        side.Y * across + f.Y * halfLength * .54f);
                    if (!world.WaterAt(start) || !world.WaterAt(bend) || !world.WaterAt(end)) continue;
                    using (var pen = new Pen(Color.FromArgb((int)alpha, 178, 210, 199), Math.Max(.65f, .70f * zoom)))
                        g.DrawCurve(pen, new[] { ScreenOf(start), ScreenOf(bend), ScreenOf(end) }, .32f);
                }
            }
        }

        void DrawQuayFittings(Graphics g, HarborOperation harbor, float berthGap) {
            DrawHarborSprite(g, HarborSprites.Bollard, HarborScreen(QuayBollard), zoom, 0, null);
            var second = HarborOffset(QuayBollard, 0, 2.02f);
            DrawHarborSprite(g, HarborSprites.Bollard, HarborScreen(second), zoom, 0, null);
            for (int i = 0; i < 8; i++) {
                var a = HarborOffset(QuayBollard, .25f, -.7f + i * .45f);
                var b = HarborOffset(a, 0, .29f);
                HarborLine(g, ScreenOf(a), ScreenOf(b), Color.FromArgb(175, 185, 157, 91), 1.45f);
            }
            if (Math.Abs(berthGap) > .32f) return;
            HarborLine(g, HarborScreen(QuayBollard,.22f), HarborScreen(HarborOffset(harbor.CargoShip,-4.04f,.9f),.64f),
                C("#a09a80"), .75f);
            HarborLine(g, HarborScreen(second,.22f), HarborScreen(HarborOffset(harbor.CargoShip,-2.9f,1.31f),.65f),
                C("#8a836d"), .7f);
        }

        void DrawQuayCrane(Graphics g, HarborOperation harbor, float berthGap) {
            DrawHarborContact(g, CraneGround, 34, 13);
            var pickup = HarborOffset(CraneGround, 5.156f, -.816f);
            var drop = HarborOffset(CraneGround, -3.153f, 4.157f);
            bool returning = harbor.Scene > HarborScene.Battle;
            if (harbor.CargoOnQuay > 0 && returning)
                DrawHarborSprite(g, HarborSprites.Cargo, HarborScreen(HarborOffset(drop,-1.04f,.13f)), zoom, 0, null);
            if (harbor.CargoOnQuay >= (returning ? 2 : 1))
                DrawHarborSprite(g, HarborSprites.Cargo, HarborScreen(drop), zoom, 0, null);
            int frameCount = HarborSprites.Crane.Frames - 1;
            float pose = (int)(harbor.CraneCycle * frameCount + .0001f) / (float)frameCount;
            if (Math.Abs(berthGap) < .22f && pose < .18f)
                DrawHarborSprite(g, HarborSprites.Cargo, HarborScreen(pickup,.68f), zoom, 0, null);
            DrawHarborSprite(g, HarborSprites.Crane, HarborScreen(CraneGround), zoom, pose, HarborCoat);
        }

        void DrawRaisedGatePost(Graphics g, int side) {
            var sprite = HarborSprites.Gate;
            var source = sprite.Source;
            var box = sprite.At(HarborScreen(RiverbendMap.HarborGateCenter), zoom);
            float scale = zoom / HarborSprites.Density;
            // The positive map Y post projects to the left half of the raised gate frame.
            if (side > 0) {
                source.Width = sprite.PivotColumn;
            } else {
                source.X = sprite.PivotColumn;
                source.Width -= source.X;
                box.X += source.X * scale;
            }
            box.Width = source.Width * scale;
            DrawStamp(g, sprite.Frame(1), source, box, HarborCoat);
            DrawFreightGateLamp(g, side, false);
        }

        void DrawFreightGateLamp(Graphics g, int side, bool blocked) {
            Color lamp = blocked ? C("#e47b53") : C("#9bd0a7");
            var p = HarborScreen(HarborOffset(RiverbendMap.HarborGatePost(side), .228f, 0), .96f);
            using (var glow = new SolidBrush(Color.FromArgb(62, lamp)))
                g.FillEllipse(glow, p.X - 4 * zoom, p.Y - 4 * zoom, 8 * zoom, 8 * zoom);
            Rect(g, p.X - 1.15f * zoom, p.Y - 1.15f * zoom, 2.3f * zoom, 2.3f * zoom, lamp);
        }

        void DrawHarborContact(Graphics g, PointF point, float width, float height) {
            var p = ScreenOf(point);
            using (var outline = new GraphicsPath()) {
                outline.AddEllipse(p.X - width * zoom * .32f, p.Y - height * zoom * .26f,
                    width * zoom, height * zoom);
                using (var brush = new PathGradientBrush(outline)) {
                    brush.CenterColor = Color.FromArgb(92, 17, 23, 24);
                    brush.SurroundColors = new[] { Color.FromArgb(0, 17, 23, 24) };
                    g.FillPath(brush, outline);
                }
            }
        }

        void HarborLine(Graphics g, PointF a, PointF b, Color color, float width) {
            using (var pen = new Pen(color, Math.Max(.75f, width * zoom))) g.DrawLine(pen, a, b);
        }

        void DrawHarborRoadSign(Graphics g, PointF point, string text = "河湾镇  ›") {
            const float height = .92f;
            var center = HarborScreen(point, height);
            var state = g.Save();
            // Project the board along the road's world X axis, with a vertical post.
            using (var face = new Matrix(Axis * zoom, Rise * zoom, 0, zoom, center.X, center.Y))
                g.MultiplyTransform(face);
            Rect(g, -1.25f, 0, 2.5f, height * Campaign.Tile, C("#626d62"));
            Rect(g, -1.25f, 0, .75f, height * Campaign.Tile, C("#a3a396"));
            Rect(g, -25, -6, 50, 12, C("#2d4a45"));
            Rect(g, -25, -6, 50, .8f, C("#acb8a0"));
            Rect(g, -25, 5.2f, 50, .8f, C("#192b29"));
            if (zoom >= 1.24f)
                using (var lettering = new SolidBrush(pale))
                    g.DrawString(text, F(8.7f, true), lettering, -22, -5, StringFormat.GenericTypographic);
            g.Restore(state);
        }

        float HarborOfficeState { get { return world.HarborOperation.PortOnline ? 1 : 0; } }

        bool HarborOfficeHit(PointF screen, PointF origin) {
            var office = HarborSprites.Office;
            var box = office.At(origin, zoom);
            if (!box.Contains(screen)) return false;
            return PixelHit(EnsureB08(office.Frame(HarborOfficeState)), office.Source, box, screen);
        }
    }
}
