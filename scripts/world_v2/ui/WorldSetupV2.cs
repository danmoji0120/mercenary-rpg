using Godot;

namespace WorldV2;

public partial class WorldSetupV2 : Control
{
    private const string MainMenuScenePath = "res://scenes/world_v2/MainMenu.tscn";
    private const string LoadingScenePath = "res://scenes/world_v2/WorldLoadingScene.tscn";

    private OptionButton? _sizeOptions;
    private OptionButton? _planOptions;
    private LineEdit? _seedEdit;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildUi();
        RandomizeSeed();
    }

    private void BuildUi()
    {
        ColorRect background = new()
        {
            Color = new Color(0.04f, 0.06f, 0.055f, 1.0f)
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        VBoxContainer panel = new()
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -240.0f,
            OffsetTop = -220.0f,
            OffsetRight = 240.0f,
            OffsetBottom = 220.0f
        };
        AddChild(panel);

        panel.AddChild(new Label { Text = "New Game Setup", HorizontalAlignment = HorizontalAlignment.Center });

        _sizeOptions = AddOptions(panel, "Map Size", new[] { "Small 2048 x 2048", "Medium 4096 x 4096", "Large 8192 x 8192" });
        _planOptions = AddOptions(panel, "Plan Version", new[] { "V3", "V2" });

        panel.AddChild(new Label { Text = "Seed" });
        HBoxContainer seedRow = new();
        panel.AddChild(seedRow);
        _seedEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        seedRow.AddChild(_seedEdit);
        Button randomButton = new()
        {
            Text = "Random Seed"
        };
        randomButton.Pressed += RandomizeSeed;
        seedRow.AddChild(randomButton);

        Button startButton = new()
        {
            Text = "Start Generation",
            CustomMinimumSize = new Vector2(480.0f, 42.0f)
        };
        startButton.Pressed += StartGeneration;
        panel.AddChild(startButton);

        Button backButton = new()
        {
            Text = "Back",
            CustomMinimumSize = new Vector2(480.0f, 38.0f)
        };
        backButton.Pressed += () => GetTree().ChangeSceneToFile(MainMenuScenePath);
        panel.AddChild(backButton);
    }

    private static OptionButton AddOptions(VBoxContainer panel, string label, string[] items)
    {
        panel.AddChild(new Label { Text = label });
        OptionButton optionButton = new()
        {
            CustomMinimumSize = new Vector2(480.0f, 36.0f)
        };
        for (int i = 0; i < items.Length; i++)
        {
            optionButton.AddItem(items[i], i);
        }

        optionButton.Select(0);
        panel.AddChild(optionButton);
        return optionButton;
    }

    private void RandomizeSeed()
    {
        RandomNumberGenerator random = new();
        random.Randomize();
        _seedEdit ??= new LineEdit();
        _seedEdit.Text = ((int)(random.Randi() & 0x7fffffffu)).ToString();
    }

    private void StartGeneration()
    {
        int seed = 20260707;
        if (_seedEdit != null && int.TryParse(_seedEdit.Text, out int parsedSeed))
        {
            seed = parsedSeed;
        }

        WorldMapSizePresetV2 sizePreset = _sizeOptions?.Selected switch
        {
            1 => WorldMapSizePresetV2.Medium,
            2 => WorldMapSizePresetV2.Large,
            _ => WorldMapSizePresetV2.Small
        };
        WorldMapSizeDefinitionV2 size = WorldMapSizeDefinitionV2.FromPreset(sizePreset);
        WorldPlanVersionV2 planVersion = _planOptions?.Selected == 1 ? WorldPlanVersionV2.V2 : WorldPlanVersionV2.V3;
        Vector2I? startCell = planVersion == WorldPlanVersionV2.V2 ? new Vector2I(64, 64) : null;
        WorldGenerationRequestV2 request = new(sizePreset, seed, planVersion, startCell);
        WorldGenerationSessionV2.SetPendingRequest(request);
        GetTree().ChangeSceneToFile(LoadingScenePath);
    }
}
