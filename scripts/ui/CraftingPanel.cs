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
    private Label? _jobStatusLabel;
    private Button? _cancelJobButton;
    private Label? _cancelReasonLabel;
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
            _refreshTimer = 0.45f;
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

        _jobStatusLabel = CreateLabel("-", 11, false);
        content.AddChild(_jobStatusLabel);

        HBoxContainer cancelRow = new()
        {
            Name = "CraftingCancelRow",
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        cancelRow.AddThemeConstantOverride("separation", 6);
        content.AddChild(cancelRow);

        _cancelJobButton = new Button
        {
            Text = "\uC791\uC5C5 \uCDE8\uC18C",
            CustomMinimumSize = new Vector2(84.0f, 26.0f),
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop,
            Visible = false
        };
        _cancelJobButton.AddThemeFontSizeOverride("font_size", 11);
        _cancelJobButton.Pressed += TryCancelActiveJob;
        cancelRow.AddChild(_cancelJobButton);

        _cancelReasonLabel = CreateLabel("", 10, false);
        _cancelReasonLabel.CustomMinimumSize = new Vector2(170.0f, 0.0f);
        _cancelReasonLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _cancelReasonLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _cancelReasonLabel.ClipText = true;
        _cancelReasonLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _cancelReasonLabel.Visible = false;
        cancelRow.AddChild(_cancelReasonLabel);

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

        UpdateJobStatusLabel();

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
        row.AddChild(CreateLabel($"\uC785\uB825: {FormatInputAmounts(recipe.Inputs)}", 11, false));
        row.AddChild(CreateLabel($"\uACB0\uACFC: {FormatAmounts(recipe.Outputs)}", 11, false));
        row.AddChild(CreateLabel($"\uC791\uC5C5\uB7C9: {recipe.RequiredWork:0.0}", 11, false));

        CraftJob? activeJob = null;
        bool hasExistingJob = _craftingManager != null && _craftingManager.TryGetJobAtFacility(_facilityOriginCell, out activeJob);
        if (hasExistingJob && activeJob != null && activeJob.RecipeId == recipe.RecipeId)
        {
            row.AddChild(CreateLabel($"\uC0C1\uD0DC: {FormatJobState(activeJob)}", 11, false));
        }
        else if (!hasExistingJob && HasMissingInput(recipe))
        {
            row.AddChild(CreateLabel("\uC7AC\uB8CC \uBD80\uC871: \uB300\uAE30 \uC608\uC57D \uAC00\uB2A5", 10, false));
        }

        Button reserveButton = new()
        {
            Text = "\uC81C\uC791 \uC608\uC57D",
            CustomMinimumSize = new Vector2(112.0f, 28.0f),
            FocusMode = FocusModeEnum.None,
            MouseFilter = MouseFilterEnum.Stop
        };
        reserveButton.AddThemeFontSizeOverride("font_size", 12);

        reserveButton.Disabled = _craftingManager == null || hasExistingJob;
        reserveButton.Pressed += () => TryReserveRecipe(recipe.RecipeId);
        row.AddChild(reserveButton);

        if (hasExistingJob)
        {
            row.AddChild(CreateLabel("\uC608\uC57D \uBD88\uAC00: \uC774\uBBF8 \uC791\uC5C5\uC774 \uC788\uC74C", 10, false));
        }
        else if (_craftingManager == null)
        {
            row.AddChild(CreateLabel("\uC608\uC57D \uBD88\uAC00: \uC81C\uC791 \uAD00\uB9AC\uC790 \uC5C6\uC74C", 10, false));
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

        bool hasMissingMaterials = CraftRecipeDatabase.TryGet(recipeId, out CraftRecipeEntry recipe)
            && HasMissingInput(recipe);

        if (_craftingManager.TryCreateJob(recipeId, _facilityOriginCell, out _))
        {
            SetStatus(hasMissingMaterials
                ? "\uC7AC\uB8CC \uBD80\uC871: \uC791\uC5C5\uC740 \uB300\uAE30 \uC0C1\uD0DC\uB85C \uC608\uC57D\uB428"
                : "\uC608\uC57D \uC644\uB8CC");
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

    private void UpdateJobStatusLabel()
    {
        if (_jobStatusLabel == null)
        {
            return;
        }

        if (_facilityType == TileBuildType.None)
        {
            _jobStatusLabel.Text = "\uD604\uC7AC \uC791\uC5C5: -";
            UpdateCancelControls(null);
            return;
        }

        if (_craftingManager == null)
        {
            _jobStatusLabel.Text = "\uD604\uC7AC \uC791\uC5C5: \uC81C\uC791 \uAD00\uB9AC\uC790 \uC5C6\uC74C";
            UpdateCancelControls(null);
            return;
        }

        if (!_craftingManager.TryGetJobAtFacility(_facilityOriginCell, out CraftJob? job) || job == null)
        {
            _jobStatusLabel.Text = "\uD604\uC7AC \uC791\uC5C5: \uC5C6\uC74C";
            UpdateCancelControls(null);
            return;
        }

        string recipeName = CraftRecipeDatabase.TryGet(job.RecipeId, out CraftRecipeEntry recipe)
            ? recipe.DisplayName
            : job.RecipeId;
        _jobStatusLabel.Text = $"\uD604\uC7AC \uC791\uC5C5: {recipeName} / {FormatJobState(job)}";
        UpdateCancelControls(job);
    }

    private void UpdateCancelControls(CraftJob? job)
    {
        if (_cancelJobButton == null || _cancelReasonLabel == null)
        {
            return;
        }

        if (job == null)
        {
            _cancelJobButton.Visible = false;
            _cancelReasonLabel.Visible = false;
            _cancelReasonLabel.Text = "";
            return;
        }

        bool canCancel = CanSafeCancelWithoutResourceLoss(job, out string reason);
        _cancelJobButton.Visible = true;
        _cancelJobButton.Disabled = !canCancel;
        _cancelReasonLabel.Visible = true;
        _cancelReasonLabel.Text = canCancel ? "\uCDE8\uC18C \uAC00\uB2A5" : reason;
    }

    private void TryCancelActiveJob()
    {
        if (_craftingManager == null)
        {
            SetStatus("\uC81C\uC791 \uAD00\uB9AC\uC790 \uC5C6\uC74C");
            return;
        }

        if (!_craftingManager.TryGetJobAtFacility(_facilityOriginCell, out CraftJob? job) || job == null)
        {
            SetStatus("\uCDE8\uC18C\uD560 \uC791\uC5C5 \uC5C6\uC74C");
            RefreshRecipes();
            return;
        }

        if (!CanSafeCancelWithoutResourceLoss(job, out string reason))
        {
            SetStatus(reason);
            RefreshRecipes();
            return;
        }

        if (_craftingManager.CancelJob(job))
        {
            SetStatus("\uC791\uC5C5 \uCDE8\uC18C\uB428");
            RefreshRecipes();
            return;
        }

        SetStatus("\uC791\uC5C5 \uCDE8\uC18C \uC2E4\uD328");
        RefreshRecipes();
    }

    private static bool CanSafeCancelWithoutResourceLoss(CraftJob job, out string reason)
    {
        if (job == null || job.IsCompleted || job.IsCancelled)
        {
            reason = "\uCDE8\uC18C\uD560 \uC791\uC5C5 \uC5C6\uC74C";
            return false;
        }

        if (job.ProducedOutputs.Count > 0)
        {
            reason = "\uCD9C\uB825 \uB300\uAE30 \uC911 \uCDE8\uC18C \uBD88\uAC00";
            return false;
        }

        if (HasDeliveredInputs(job))
        {
            reason = "\uC7AC\uB8CC \uD22C\uC785 \uD6C4 \uCDE8\uC18C \uBD88\uAC00";
            return false;
        }

        if (job.State == CraftJobState.WaitingForMaterials)
        {
            reason = string.Empty;
            return true;
        }

        reason = job.State switch
        {
            CraftJobState.ReadyToCraft => "\uC7AC\uB8CC \uD22C\uC785 \uD6C4 \uCDE8\uC18C \uBD88\uAC00",
            CraftJobState.Crafting => "\uC81C\uC791 \uC911 \uCDE8\uC18C \uBD88\uAC00",
            CraftJobState.OutputReady => "\uCD9C\uB825 \uB300\uAE30 \uC911 \uCDE8\uC18C \uBD88\uAC00",
            _ => "\uCDE8\uC18C \uBD88\uAC00"
        };
        return false;
    }

    private static bool HasDeliveredInputs(CraftJob job)
    {
        foreach (KeyValuePair<BaseResourceType, int> input in job.InputsDelivered)
        {
            if (input.Value > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string FormatJobState(CraftJob job)
    {
        return job.State switch
        {
            CraftJobState.Crafting => $"Crafting {(int)(job.GetProgressRatio() * 100.0f)}%",
            CraftJobState.WaitingForMaterials => "WaitingForMaterials",
            CraftJobState.ReadyToCraft => "ReadyToCraft",
            CraftJobState.OutputReady => "OutputReady",
            CraftJobState.Completed => "Completed",
            CraftJobState.Cancelled => "Cancelled",
            _ => job.State.ToString()
        };
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

    private string FormatInputAmounts(IReadOnlyDictionary<BaseResourceType, int> amounts)
    {
        List<string> parts = new();

        foreach (KeyValuePair<BaseResourceType, int> pair in amounts)
        {
            if (pair.Value > 0)
            {
                int storedAmount = _buildManager?.GetTotalStoredAmount(pair.Key) ?? 0;
                parts.Add($"{BaseBuildManager.GetResourceDisplayName(pair.Key)} x{pair.Value} / \uBCF4\uC720 {storedAmount}");
            }
        }

        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }

    private bool HasMissingInput(CraftRecipeEntry recipe)
    {
        if (_buildManager == null)
        {
            return false;
        }

        foreach (KeyValuePair<BaseResourceType, int> input in recipe.Inputs)
        {
            if (input.Value > 0 && _buildManager.GetTotalStoredAmount(input.Key) < input.Value)
            {
                return true;
            }
        }

        return false;
    }
}
