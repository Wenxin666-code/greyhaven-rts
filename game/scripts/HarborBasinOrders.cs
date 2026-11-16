using System;
using Godot;
using Greyhaven;

public partial class HarborGame
{
    private enum RoadOrderPhase { Hidden, Arriving, Ready, TurningOut, TurningIn, Closing }
    private const float OrderArrival = .58f, OrderTurnOut = .18f, OrderTurnIn = .28f, OrderClose = .36f;
    private static readonly Vector2 OrderPaperSize = new(760, 520);
    private RoadOrderPhase roadOrderPhase;
    private float roadOrderElapsed;
    private ColorRect roadOrderOverlay;
    private Control roadOrderPaper, roadOrderRadio;
    private Label roadOrderProgress, roadOrderReference, roadOrderTime;
    private RichTextLabel roadOrderDirective, roadOrderOrigin, roadOrderCheckpoint, roadOrderRadioText;
    private Button roadOrderNext;
    private bool HasRoadOrders => started && !world.Lost && !SceneRunning && world.BasinOperation?.Orders.UnreadCount > 0;
    private bool ReadingRoadOrder => HasRoadOrders || roadOrderPhase != RoadOrderPhase.Hidden;

    private void MakeRoadOrderPanel()
    {
        roadOrderOverlay = new ColorRect { Color = new Color("#101517"), Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 4 };
        ui.AddChild(roadOrderOverlay);
        roadOrderOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        roadOrderRadio = Strip(roadOrderOverlay, "#202c32f5", "#ac925f");
        roadOrderRadio.Visible = false;
        roadOrderRadio.MouseFilter = Control.MouseFilterEnum.Ignore;
        Bold(roadOrderRadio, ChapterSequence.Speaker, 15, 5, 420, 20, 13).Modulate = Gold;
        roadOrderRadioText = Paragraph(roadOrderRadio, 15, 28, 730, 53, 17);

        roadOrderPaper = new Control { Size = OrderPaperSize, PivotOffset = OrderPaperSize * .5f,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        roadOrderOverlay.AddChild(roadOrderPaper);
        Accent(roadOrderPaper, 6, 10, 752, 507, new Color("#17130ca3"));
        var sheet = new TextureRect { Texture = DrawOrderPaper(), MouseFilter = Control.MouseFilterEnum.Ignore };
        roadOrderPaper.AddChild(sheet);
        Position(sheet, 0, 0, OrderPaperSize.X, OrderPaperSize.Y);

        var serif = GD.Load<Font>("res://assets/fonts/GreyhavenOrders.ttf");
        var ink = new Color("#302c24");
        var red = new Color("#843528");
        Label Typed(string content, float x, float y, float width, float height, int size, Color color)
        {
            var label = LabelAt(roadOrderPaper, content, x, y, width, height, size);
            label.AddThemeFontOverride("font", serif);
            label.AddThemeColorOverride("font_color", color);
            return label;
        }
        RichTextLabel TypedParagraph(float x, float y, float width, float height, int size)
        {
            var label = Paragraph(roadOrderPaper, x, y, width, height, size);
            label.AddThemeFontOverride("normal_font", serif);
            label.AddThemeColorOverride("default_color", ink);
            return label;
        }

        Typed("沿 线 调 度", 54, 30, 325, 29, 21, red);
        Typed("行 车 调 度 令", 51, 61, 530, 61, 44, red);
        Accent(roadOrderPaper, 54, 137, 652, 2, red);
        Accent(roadOrderPaper, 54, 142, 652, 1, new Color(red, .55f));
        var copy = Strip(roadOrderPaper, "#dec89f24", "#843528ab");
        Position(copy, 616, 33, 86, 29);
        Typed("执行联", 631, 32, 67, 29, 18, red);
        roadOrderReference = Typed("", 529, 91, 178, 28, 16, red);
        roadOrderCheckpoint = TypedParagraph(54, 161, 501, 34, 21);
        roadOrderTime = Typed("", 558, 163, 148, 29, 18, ink);
        roadOrderDirective = TypedParagraph(54, 224, 652, 158, 21);
        roadOrderDirective.AddThemeConstantOverride("line_separation", 8);
        Accent(roadOrderPaper, 54, 386, 652, 1, new Color("#9c7d5e"));
        roadOrderOrigin = TypedParagraph(54, 397, 652, 29, 17);
        roadOrderProgress = Typed("", 54, 462, 402, 31, 16, new Color("#6a5841"));
        roadOrderNext = AddButton(roadOrderPaper, "继续", 512, 443, 194, 42, AcknowledgeRoadOrder, 17);
        roadOrderNext.AddThemeFontOverride("font", serif);
        roadOrderNext.AddThemeColorOverride("font_color", red);
        roadOrderNext.AddThemeColorOverride("font_hover_color", red);
        roadOrderNext.AddThemeColorOverride("font_pressed_color", red);
        roadOrderNext.AddThemeStyleboxOverride("normal", InkButton("#d6bf94"));
        roadOrderNext.AddThemeStyleboxOverride("hover", InkButton("#e6cca2"));
        roadOrderNext.AddThemeStyleboxOverride("pressed", InkButton("#cbb086"));

        StyleBoxFlat InkButton(string fill) => new()
        {
            BgColor = new Color(fill), BorderColor = new Color(red, .65f), BorderWidthBottom = 1,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, ContentMarginLeft = 14
        };
    }

    private static ImageTexture DrawOrderPaper()
    {
        const int width = 760, height = 520;
        using var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        var random = new Random(3147);
        for (int y = 0; y < height; y++)
        {
            float fibres = (float)random.NextDouble() * .016f;
            for (int x = 0; x < width; x++)
            {
                float edgeX = Math.Min(x, width - 1 - x);
                float edgeY = Math.Min(y, height - 1 - y);
                float edge = Math.Min(edgeX - 2 - 1.4f * MathF.Sin(y * .071f), edgeY - 2 - MathF.Sin(x * .058f));
                if (edge < 0) continue;
                float wear = .055f * MathF.Exp(-edge / 14);
                float grain = (float)random.NextDouble() * .036f + fibres;
                float crease = MathF.Max(0, 1 - MathF.Abs(y - 343 - x * .012f) / 4.5f) * .027f;
                float tone = grain + wear + crease;
                image.SetPixel(x, y, new Color(.925f - tone, .869f - tone * 1.16f, .748f - tone * 1.3f, Math.Min(1, edge)));
            }
        }
        return ImageTexture.CreateFromImage(image);
    }

    private void FillRoadOrder()
    {
        var orders = world.BasinOperation.Orders;
        var (document, from) = orders.Current;
        var file = BasinRoadOrder.All[document - 1];
        roadOrderProgress.Text = $"缴获文件  {orders.ReadCount + 1} / {BasinRoadOrder.All.Length}";
        string holder = world.StoryFactionName(from);
        string recipient = world.StoryFactionName(document);
        ColorParagraph(roadOrderOrigin, from == document ? $"缴获自 {holder}"
            : $"缴获自 {holder} · 原收报方 {recipient}", onPaper: true);
        ColorParagraph(roadOrderCheckpoint, $"{file.Checkpoint} · {recipient}", onPaper: true);
        roadOrderReference.Text = file.Reference;
        roadOrderTime.Text = $"收报  {file.Time}";
        ColorParagraph(roadOrderDirective, file.Directive, onPaper: true);
        if (orders.ReadCount == 0)
            ColorParagraph(roadOrderRadioText, $"指挥官，刚从{holder}缴到这份调度令。我还以为路是他们自己封的。\n您看，转发栏写的是雾脊。原件收好，回头让周砚对一下。");
        roadOrderNext.Text = orders.UnreadCount > 1 ? "下一份  Enter" : world.Won ? "合上文件  Enter" : "返回战场  Enter";
    }

    private void UpdateRoadOrderPanel()
    {
        if (HasRoadOrders && roadOrderPhase == RoadOrderPhase.Hidden)
        {
            ChangeRoadOrderPhase(RoadOrderPhase.Arriving);
            CancelDrag();
            rightHeld = false;
            FillRoadOrder();
        }
        roadOrderOverlay.Visible = roadOrderPhase != RoadOrderPhase.Hidden;
        if (!roadOrderOverlay.Visible) return;

        float progress = roadOrderPhase switch
        {
            RoadOrderPhase.Arriving => Mathf.SmoothStep(0, 1, (roadOrderElapsed / OrderArrival - .18f) / .82f),
            RoadOrderPhase.TurningOut => 1 - Mathf.SmoothStep(0, 1, roadOrderElapsed / OrderTurnOut),
            RoadOrderPhase.TurningIn => Mathf.SmoothStep(0, 1, roadOrderElapsed / OrderTurnIn),
            RoadOrderPhase.Closing => 1 - Mathf.SmoothStep(0, 1, roadOrderElapsed / OrderClose),
            _ => 1
        };
        float scrim = roadOrderPhase is RoadOrderPhase.Arriving or RoadOrderPhase.Closing ? progress : 1;
        roadOrderOverlay.Color = new Color("#101517", .70f * scrim);
        var center = (GetViewport().GetVisibleRect().Size - OrderPaperSize) * .5f;
        var offset = roadOrderPhase == RoadOrderPhase.TurningOut ? new Vector2(-88, -12)
            : roadOrderPhase == RoadOrderPhase.TurningIn ? new Vector2(66, 14) : new Vector2(0, 36);
        roadOrderPaper.Position = center + offset * (1 - progress);
        roadOrderPaper.Rotation = (roadOrderPhase == RoadOrderPhase.TurningOut ? -.07f : .045f) * (1 - progress);
        roadOrderPaper.Scale = Vector2.One * (.965f + .035f * progress);
        roadOrderPaper.Modulate = new Color(1, 1, 1, progress);
        roadOrderRadio.Visible = world.BasinOperation.Orders.ReadCount == 0;
        if (roadOrderRadio.Visible)
        {
            Position(roadOrderRadio, roadOrderPaper.Position.X, Math.Max(6, roadOrderPaper.Position.Y - 91), OrderPaperSize.X, 82);
            roadOrderRadio.Modulate = roadOrderPaper.Modulate;
        }
        roadOrderNext.Disabled = roadOrderPhase != RoadOrderPhase.Ready;
    }

    private void ChangeRoadOrderPhase(RoadOrderPhase phase)
    {
        roadOrderPhase = phase;
        roadOrderElapsed = 0;
        if (phase is RoadOrderPhase.Hidden or RoadOrderPhase.Ready) stepper.Reset();
    }

    private void AdvanceRoadOrder(float elapsed)
    {
        UpdateRoadOrderPanel();
        if (roadOrderPhase == RoadOrderPhase.Arriving && focused && !paused)
        {
            float pace = 1 - Math.Clamp((roadOrderElapsed + elapsed * .5f) / OrderArrival, 0, 1);
            stepper.Advance(elapsed, MatchTempo.DefaultRate * multiplier * pace * pace, step =>
            {
                if (world.Won || world.Lost) world.TickVisuals(step);
                else world.Tick(step);
            });
            view.QueueRedraw();
        }
        roadOrderElapsed += elapsed;
        switch (roadOrderPhase)
        {
            case RoadOrderPhase.Arriving when roadOrderElapsed >= OrderArrival:
            case RoadOrderPhase.TurningIn when roadOrderElapsed >= OrderTurnIn:
                ChangeRoadOrderPhase(RoadOrderPhase.Ready);
                break;
            case RoadOrderPhase.TurningOut when roadOrderElapsed >= OrderTurnOut:
                world.BasinOperation.Orders.Acknowledge();
                FillRoadOrder();
                ChangeRoadOrderPhase(RoadOrderPhase.TurningIn);
                break;
            case RoadOrderPhase.Closing when roadOrderElapsed >= OrderClose:
                world.BasinOperation.Orders.Acknowledge();
                ChangeRoadOrderPhase(RoadOrderPhase.Hidden);
                break;
        }
        UpdateRoadOrderPanel();
    }

    private void AcknowledgeRoadOrder()
    {
        if (roadOrderPhase != RoadOrderPhase.Ready) return;
        ChangeRoadOrderPhase(world.BasinOperation.Orders.UnreadCount > 1 ? RoadOrderPhase.TurningOut : RoadOrderPhase.Closing);
        UpdateRoadOrderPanel();
    }
}
