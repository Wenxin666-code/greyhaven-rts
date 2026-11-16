using Godot;

public partial class HarborGame
{
    private CanvasLayer exitConfirmation;
    private bool exitFadeStarted;

    private void RequestExitGame()
    {
        if (exitConfirmation != null) return;

        exitConfirmation = new CanvasLayer { Name = "ExitConfirmation", Layer = 110 };
        AddChild(exitConfirmation);
        var shade = new ColorRect { Color = new Color(Ink, 171 / 255f), MouseFilter = Control.MouseFilterEnum.Stop };
        shade.Theme = ui?.Theme ?? new Theme { DefaultFont = GD.Load<Font>("res://assets/fonts/GreyhavenUI.ttf") };
        exitConfirmation.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        bool showSaveNotice = started && !Finished && chapterSavesEnabled;
        float noticeHeight = showSaveNotice ? 32 : 0;
        float halfHeight = (216 + noticeHeight) / 2;
        var dialog = Strip(shade, "#1f302f", "#a2b398");
        dialog.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        dialog.OffsetLeft = -245;
        dialog.OffsetRight = 245;
        dialog.OffsetTop = -halfHeight;
        dialog.OffsetBottom = halfHeight;
        var title = LabelAt(dialog, "退出游戏？", 30, 20, 429, 50, 35);
        if (uiBold != null) title.AddThemeFontOverride("font", uiBold);
        if (showSaveNotice)
            LabelAt(dialog, "当前战局将保存，下次可以继续。", 32, 73, 426, 27, 17).Modulate = Dim;
        Accent(dialog, 32, 85 + noticeHeight, 426, 1, new Color("#5d7265"));

        var cancel = AddButton(dialog, "取消", 32, 112 + noticeHeight, 200, 45, CancelExitGame);
        var confirm = AddButton(dialog, "退出游戏", 258, 112 + noticeHeight, 200, 45, ConfirmExitGame);
        LabelAt(dialog, "Esc", 37, 165 + noticeHeight, 140, 23, 13).Modulate = Dim;
        var focus = new StyleBoxEmpty();
        foreach (var button in new[] { cancel, confirm })
        {
            button.AddThemeStyleboxOverride("focus", focus);
            button.AddThemeColorOverride("font_focus_color", Ink);
            button.FocusEntered += () => SetLit(button, true);
            button.FocusExited += () => SetLit(button, false);
        }
        cancel.FocusMode = confirm.FocusMode = Control.FocusModeEnum.All;
        cancel.FocusNext = cancel.FocusPrevious = cancel.FocusNeighborRight = confirm.GetPath();
        confirm.FocusNext = confirm.FocusPrevious = confirm.FocusNeighborLeft = cancel.GetPath();
        cancel.GrabFocus();

        CancelDrag();
        rightHeld = false;
        edgeArmed = false;
        edgeDwell = 0;
        stepper.Reset();
        if (pointerHelp != null) UpdatePointer(0);
    }

    private void CancelExitGame()
    {
        exitConfirmation.QueueFree();
        exitConfirmation = null;
    }

    private void ConfirmExitGame()
    {
        if (!SaveBeforeLeavingChapter())
        {
            CancelExitGame();
            return;
        }

        exitFadeStarted = true;
        var fade = new ColorRect { Color = new Color(Colors.Black, 0) };
        exitConfirmation.AddChild(fade);
        fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var animation = exitConfirmation.CreateTween();
        animation.TweenProperty(fade, "color:a", 1f, .3);
        animation.TweenInterval(.03);
        animation.TweenCallback(Callable.From(() => GetTree().Quit()));
    }
}
