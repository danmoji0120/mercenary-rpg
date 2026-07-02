using System.Collections.Generic;
using Godot;

public partial class CraftingPanel : Control
{
    private const float PanelWidth = 320.0f;
    private const float PanelTopMargin = 72.0f;
    private const float PanelRightReservedWidth = 220.0f;
    private const float PanelRightMargin = 12.0f;

    private BaseBuildManager? _buildManager;
    private CraftingManager? _craftingManager;
    private PanelContainer? _panel;
    private Label? _facilityLabel;
    private VBoxContainer? _recipeList;
    private Label? _statusLabel;
    private Vector2I _facilityOriginCell;
    private TileBuildType _facilityType = TileBuildType.None;
    private float _refreshTimer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildPanel();
        HidePanel();
    }

    public override void _Process(double delta)
    {
        UpdatePanelLayout();

        if (!Visible)
        {
            return;
        }

        _refreshTimer -= (float)delta;
        if (_refreshTimer <= 0.0f)
        {
            _refreshTimer = 0.75f;
            RefreshRecipes();
        }
    }

    public void SetManagers(BaseBuildManager? buildManager, CraftingManager? craftingManager)
    {
        _buildManager = buildManager;
        _craftingManager = craftingManager;
        RefreshRecipes();
    }

    public void ShowFacility(TileBuildType facilityType, Vector2I facilityOriginCell)
    {
        _facilityType = facilityType;
        _facilityOriginCell = facilityOriginCell;
        Visible = true;

        if (_panel != null)
        {
            _panel.Visible = true;
        }

        SetStatus("-");
        RefreshRecipes();
        UpdatePanelLayout();
    }

    public void HidePanel()
    {
        Visible = false;
        _facilityType = TileBuildType.None;
        _facilityOriginCell = Vector2I.Zero;
        SetStatus("-");

        if (_panel != null)
        {
            _panel.Visible = false;
        }
    }

    private void BuildPanel()
    {
        _panel = new PanelContainer
        {
            Name = "CraftingPanelContainer",
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(PanelWidth, 180.0f)
        };
        StyleBoxFlat style = new()
        {
            BgColor = new Color(0.10f, 0.11f, 0.12f, 0.94f),
            BorderColor = new Color(0.76f, 0.60f, 0.34f, 0.82f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10.0f,
            ContentMarginTop = 10.0f,
            ContentMarginRight = 10.0f,
            ContentMarginBottom = 10.0f
        };
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        VBoxContainer content = new()
        {
            Name = "CraftingPanelContent",
            MouseFilter = MouseFilterEnum.Ignore
        };
        content.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(content);

        content.AddChild(CreateLabel("\uC81C\uC791", 16, true));

        _facilityLabel = CreateLabel("-", 12, false);
        content.AddChild(_facilityLabel);

        content.AddChild(new HSeparator
        {
            MouseFilter = MouseFilterEnum.Ignore
        });

        _recipeList = new VBoxContainer
        {
            Name = "CraftingRecipeList",
            MouseFilter = MouseFilterEnum.Ignore
        };
        _recipeList.AddThemeConstantOverride("separation", 6);
        content.AddChild(_recipeList);

        _statusLabel = CreateLabel("-", 11, false);
        _statusLabel.Modulate = new Color(0.95f, 0.86f, 0.58f, 1.0f);
        content.AddChild(_statusLabel);
    }

    private void RefreshRecipes()
    {
        if (_facilityLabel != null)
        {
            string facilityName = _facilityType == TileBuildType.None
                ? "-"
                : BaseBuildManager.GetBuildDisplayName(_facilityType);
            _facilityLabel.Text = $"\uC81C\uC791\uB300: {facilityName} ({_facilityOriginCell.X}, {_facilityOriginCell.Y})";
        }

        if (_recipeList == null)
        {
            return;
        }

        foreach (Node child in _recipeList.GetChildren())
        {
            child.QueueFree();
        }

        if (_facilityType == TileBuildType.None)
        {
            return;
        }

        if (_craftingManager == null)
        {
            _recipeList.AddChild(CreateLabel("\uC81C\uC791 \uAD00\uB9AC\uC790 \uC5C6\uC74C", 12, false));
            SetStatus("\uC81C\uC791 \uAD00\uB9AC\uC790\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C");
            return;
        }

        IReadOnlyList<CraftRecipeEntry> recipes = CraftRecipeDatabase.GetRecipesForFacility(_facilityType);
        int activeRecipeCount = 0;

        foreach (CraftRecipeEntry recipe in recipes)
        {
            if (!recipe.IsEnabled)
            {
                continue;
            }

            activeRecipeCount++;
            AddRecipeRow(recipe);
        }

        if (activeRecipeCount <= 0)
        {
            _recipeList.AddChild(CreateLabel("\uC0AC\uC6A9 \uAC00\uB2A5\uD55C \uB808\uC2DC\uD53C \uC5C6\uC74C", 12, false));
        }
    }

    private void AddRecipeRow(CraftRecipeEntry recipe)
    {
        if (_recipeList == null)
        {
            return;
        }

        PanelContainer rowPanel = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        StyleBoxFlat rowStyle = new()
        {
            BgColor = new Color(0.18f, 0.18f, 0.18f, 0.92f),
            BorderColor = new Color(0.05f, 0.05f, 0.05f, 0.55f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8.0f,
            ContentMarginTop = 6.0f,
            ContentMarginRight = 8.0f,
            ContentMarginBottom = 6.0f
        };
        rowPanel.AddThemeStyleboxOverride("panel", rowStyle);
        _recipeList.AddChild(rowPanel);

        VBoxContainer row = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 4);
        rowPanel.AddChild(row);

        row.AddChild(CreateLabel(recipe.DisplayName, 13, true));
        row.AddChild(CreateLabel($"\uC785\uB825: {FormatAmounts(recipe.Inputs)}", 11, false));
        row.AddChild(CreateLabel($"\uCD9C\uB825: {FormatAmounts(recipe.Outputs)}", 11, false));
        row.AddChild(CreateLabel($"\uC791\uC5C5\uB7C9: {recipe.RequiredWork:0.0}", 11, false));

        Button reserveButton = new()
        {
            Text = "\uC81C\uC791 \uC608\uC57D",
            CustomMinimumSize = new Vector2(112.0f, 28.0f),
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop
        };
        reserveButton.AddThemeFontSizeOverride("font_size", 12);

        bool hasExistingJob = _craftingManager != null && _craftingManager.TryGetJobAtFacility(_facilityOriginCell, out _);
        reserveButton.Disabled = hasExistingJob;
        reserveButton.Pressed += () => TryReserveRecipe(recipe.RecipeId);
        row.AddChild(reserveButton);

        if (hasExistingJob)
        {
            row.AddChild(CreateLabel("\uC774\uBBF8 \uC791\uC5C5\uC774 \uC788\uC74C", 10, false));
        }
    }

    private void TryReserveRecipe(string recipeId)
    {
        if (_craftingManager == null)
        {
            SetStatus("\uC81C\uC791 \uAD00\uB9AC\uC790 \uC5C6\uC74C");
            return;
        }

        if (_craftingManager.TryGetJobAtFacility(_facilityOriginCell, out _))
        {
            SetStatus("\uC774\uBBF8 \uC791\uC5C5\uC774 \uC788\uC74C");
            RefreshRecipes();
            return;
        }

        if (_craftingManager.TryCreateJob(recipeId, _facilityOriginCell, out _))
        {
            SetStatus("\uC608\uC57D \uC644\uB8CC");
            RefreshRecipes();
            return;
        }

        SetStatus("\uC608\uC57D \uC2E4\uD328");
        RefreshRecipes();
    }

    private void SetStatus(string text)
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = text;
        }
    }

    private void UpdatePanelLayout()
    {
        if (_panel == null)
        {
            return;
        }

        Vector2 viewportSize = GetViewportRect().Size;
        float x = Mathf.Max(12.0f, viewportSize.X - PanelRightReservedWidth - PanelRightMargin - PanelWidth);
        float maxHeight = Mathf.Max(180.0f, viewportSize.Y - PanelTopMargin - 170.0f);
        _panel.Position = new Vector2(x, PanelTopMargin);
        _panel.Size = new Vector2(PanelWidth, Mathf.Min(360.0f, maxHeight));
    }

    private static Label CreateLabel(string text, int fontSize, bool highlighted)
    {
        Label label = new()
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);

        if (highlighted)
        {
            label.Modulate = new Color(1.0f, 0.88f, 0.58f, 1.0f);
        }

        return label;
    }

    private static string FormatAmounts(IReadOnlyDictionary<BaseResourceType, int> amounts)
    {
        List<string> parts = new();

        foreach (KeyValuePair<BaseResourceType, int> pair in amounts)
        {
            if (pair.Value > 0)
            {
                parts.Add($"{BaseBuildManager.GetResourceDisplayName(pair.Key)} x{pair.Value}");
            }
        }

        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }
}
