using Godot;

namespace WorldV2;

public partial class WorldV2LoadingOverlay : Control
{
    private Label? _label;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);

        ColorRect background = new()
        {
            Name = "Background",
            Color = new Color(0.0f, 0.0f, 0.0f, 0.72f),
            MouseFilter = MouseFilterEnum.Stop
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        CenterContainer center = new()
        {
            Name = "Center",
            MouseFilter = MouseFilterEnum.Ignore
        };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        _label = new Label
        {
            Name = "Text",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.Off,
            Text = "World Loading..."
        };
        _label.AddThemeFontSizeOverride("font_size", 24);
        center.AddChild(_label);
    }

    public void Refresh(WorldStreamManagerV2? streamManager)
    {
        if (streamManager == null)
        {
            Visible = true;
            SetText("World Loading...\nstream manager missing");
            return;
        }

        bool loading = !streamManager.IsInitialLoadingComplete;
        Visible = loading;
        if (!loading)
        {
            return;
        }

        SetText(
            $"World Loading...\n" +
            $"{streamManager.InitialLoadingLoadedChunks}/{streamManager.InitialLoadingTargetChunks} chunks\n" +
            $"{streamManager.InitialLoadingProgress:P0}");
    }

    private void SetText(string text)
    {
        if (_label != null)
        {
            _label.Text = text;
        }
    }
}
