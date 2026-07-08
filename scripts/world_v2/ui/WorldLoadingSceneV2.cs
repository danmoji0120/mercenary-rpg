using Godot;

namespace WorldV2;

public partial class WorldLoadingSceneV2 : Control
{
    private const string WorldScenePath = "res://scenes/world_v2/WorldV2Root.tscn";

    private Label? _titleLabel;
    private Label? _detailLabel;
    private Label? _progressLabel;
    private WorldGenerationRequestV2? _request;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        _request = WorldGenerationSessionV2.PendingRequest ?? WorldGenerationRequestV2.CreateDevDefault(20260707);
        BuildUi();
        Refresh("Preparing loading screen...");
        CallDeferred(MethodName.BeginLoadingDeferred);
    }

    private void BuildUi()
    {
        ColorRect background = new()
        {
            Color = new Color(0.02f, 0.025f, 0.025f, 1.0f)
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        VBoxContainer panel = new()
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -300.0f,
            OffsetTop = -140.0f,
            OffsetRight = 300.0f,
            OffsetBottom = 140.0f,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        AddChild(panel);

        _titleLabel = new Label
        {
            Text = "Generating World...",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(600.0f, 42.0f)
        };
        panel.AddChild(_titleLabel);

        _detailLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(600.0f, 84.0f)
        };
        panel.AddChild(_detailLabel);

        _progressLabel = new Label
        {
            Text = "Waiting...",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(600.0f, 38.0f)
        };
        panel.AddChild(_progressLabel);
    }

    private async void BeginLoadingDeferred()
    {
        Refresh("Showing loading screen...");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Refresh("Building world plan metadata...");
        if (_request?.PlanVersion == WorldPlanVersionV2.V3)
        {
            FlatlandWorldPlanV3 plan = new();
            plan.Initialize(_request, WorldGenerationSettingsV2.Default);
            plan.BuildPlan();
        }

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Refresh("Opening WorldV2...");
        GetTree().ChangeSceneToFile(WorldScenePath);
    }

    private void Refresh(string progress)
    {
        if (_request == null)
        {
            return;
        }

        if (_detailLabel != null)
        {
            _detailLabel.Text = $"Size: {_request.MapSizePreset} ({_request.MapSize.WidthCells} x {_request.MapSize.HeightCells} cells)\nSeed: {_request.Seed}\nPlan: {_request.PlanVersion}";
        }

        if (_progressLabel != null)
        {
            _progressLabel.Text = progress;
        }
    }
}
