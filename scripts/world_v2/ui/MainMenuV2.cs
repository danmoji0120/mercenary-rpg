using Godot;

namespace WorldV2;

public partial class MainMenuV2 : Control
{
    private const string WorldSetupScenePath = "res://scenes/world_v2/WorldSetup.tscn";

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildUi();
    }

    private void BuildUi()
    {
        ColorRect background = new()
        {
            Color = new Color(0.05f, 0.07f, 0.06f, 1.0f)
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        VBoxContainer panel = new()
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -160.0f,
            OffsetTop = -160.0f,
            OffsetRight = 160.0f,
            OffsetBottom = 160.0f,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        AddChild(panel);

        Label title = new()
        {
            Text = "Mercenary RPG",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(320.0f, 48.0f)
        };
        panel.AddChild(title);

        AddButton(panel, "New Game", OnNewGamePressed);
        AddButton(panel, "Continue", () => GD.Print("Continue is not implemented yet."));
        AddButton(panel, "Settings", () => GD.Print("Settings is not implemented yet."));
        AddButton(panel, "Quit", () => GetTree().Quit());
    }

    private static void AddButton(VBoxContainer parent, string text, System.Action callback)
    {
        Button button = new()
        {
            Text = text,
            CustomMinimumSize = new Vector2(320.0f, 42.0f)
        };
        button.Pressed += callback;
        parent.AddChild(button);
    }

    private void OnNewGamePressed()
    {
        GetTree().ChangeSceneToFile(WorldSetupScenePath);
    }
}
