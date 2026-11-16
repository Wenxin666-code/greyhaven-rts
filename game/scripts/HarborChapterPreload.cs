using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Point = System.Drawing.PointF;

public partial class HarborGame
{
    private bool useChapterPreload;
    private CanvasLayer chapterLoading;

    private void EnterChapter(Action initialize, bool preloadOpening = false)
    {
        if (!useChapterPreload) initialize();
        else if (chapterLoading == null) PreloadChapter(initialize, preloadOpening);
    }

    private async void PreloadChapter(Action initialize, bool preloadOpening)
    {
        var progress = ShowChapterLoadingScreen();

        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            initialize();
            if (!started) return;

            // Let the actual renderer populate its cache while the opening remains at time zero.
            Point entryCamera = view.CameraPosition;
            var cameras = new List<Point>(3);
            if (preloadOpening && sequence.Opening)
            {
                Point command = CommandCamera();
                Point firstPan = sequence.OpeningPanTarget;
                if (command != entryCamera && command != firstPan)
                    cameras.Add(command);
                if (firstPan != entryCamera)
                    cameras.Add(firstPan);
            }
            cameras.Add(entryCamera);

            progress.MaxValue = cameras.Count + 1;
            progress.Value = 1;
            foreach (Point camera in cameras)
            {
                await DrawChapterPreload(camera);
                progress.Value++;
            }
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }
        finally
        {
            chapterLoading.QueueFree();
            chapterLoading = null;
        }
    }

    private ProgressBar ShowChapterLoadingScreen()
    {
        chapterLoading = new CanvasLayer { Name = "ChapterLoading", Layer = 100 };
        AddChild(chapterLoading);

        var front = ui.GetNode<TextureRect>("Front");
        var backdrop = new TextureRect
        {
            Name = "Backdrop",
            Texture = front.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = front.StretchMode
        };
        chapterLoading.AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shade = Gradient(backdrop, new Color(Ink, 0.05f), new Color(Ink, 0.87f), vertical: true);
        shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var status = new VBoxContainer { Theme = ui.Theme };
        backdrop.AddChild(status);
        status.SetAnchorAndOffset(Side.Right, 0.7f, 0);
        status.SetAnchorAndOffset(Side.Left, 0.3f, 0);
        status.SetAnchorAndOffset(Side.Bottom, 1, -64);
        status.SetAnchorAndOffset(Side.Top, 1, -112);
        status.AddThemeConstantOverride("separation", 11);

        var label = new Label { Text = "正在准备关卡…", HorizontalAlignment = HorizontalAlignment.Center };
        status.AddChild(label);
        label.AddThemeFontSizeOverride("font_size", 20);

        var progress = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, 10) };
        var track = new StyleBoxFlat { BgColor = new Color(Ink, 0.95f), BorderColor = new Color(Pale, 0.42f) };
        track.SetBorderWidthAll(1);
        progress.AddThemeStyleboxOverride("background", track);
        progress.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = Gold });
        status.AddChild(progress);
        return progress;
    }

    private async Task DrawChapterPreload(Point camera)
    {
        view.Center(camera);
        view.QueueRedraw();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }
}
