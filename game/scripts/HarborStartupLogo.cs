using Godot;

public partial class HarborGame
{
    private CanvasLayer startupLogo;

    private async void ShowStartupLogo()
    {
        const double initialBlackSeconds = 0.6;
        startupLogo = new CanvasLayer { Name = "StartupLogo", Layer = 100 };
        AddChild(startupLogo);

        var background = new ColorRect { Color = Colors.Black };
        startupLogo.AddChild(background);
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var mark = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/wenxin-startup.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Modulate = new Color(1, 1, 1, 0)
        };
        background.AddChild(mark);
        mark.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var animation = startupLogo.CreateTween();
        animation.TweenInterval(initialBlackSeconds);
        animation.TweenProperty(mark, "modulate:a", 1f, 5.0 / 6.0)
            .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.InOut);
        animation.TweenInterval(1.0);
        animation.TweenProperty(mark, "modulate:a", 0f, 2.0 / 3.0);
        animation.TweenInterval(0.25);

        // Give the logo a visible head start before building controls on the main thread.
        var tree = GetTree();
        await ToSignal(tree.CreateTimer(initialBlackSeconds + 0.18), SceneTreeTimer.SignalName.Timeout);
        MakeFrontInterface();
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        MakeBattleInterface();
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        ResetChapter(false);

        if (animation.IsRunning()) await ToSignal(animation, Tween.SignalName.Finished);
        mark.QueueFree();

        var reveal = startupLogo.CreateTween();
        reveal.TweenProperty(background, "color:a", 0f, 0.55)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        await ToSignal(reveal, Tween.SignalName.Finished);
        startupLogo.QueueFree();
        startupLogo = null;
    }
}
