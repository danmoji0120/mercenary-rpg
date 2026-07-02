using System.Collections.Generic;
using Godot;

public partial class SelectedMercenaryHud : Control
{
    private const float HudLeftMargin = 12.0f;
    private const float HudRightMargin = 12.0f;
    private const float HudBottomMargin = 12.0f;
    private const float HudRightReservedWidth = 240.0f;
    private const float HudMaxHeight = 156.0f;
    private const float HudMinHeight = 124.0f;
    private const float HudMinWidth = 520.0f;
    private const int InventoryVisibleItemLines = 2;

    [Export]
    public bool ShowDebugStatusText { get; set; } = false;

    private Label? _statusLabel;
    private PanelContainer? _rimHudPanel;
    private Label? _identityNameLabel;
    private Label? _identityMetaLabel;
    private Label? _identityStateLabel;
    private Label? _workLabel;
    private Label? _inventoryLabel;
    private Label? _equipmentLabel;
    private Label? _needsLabel;
    private Label? _statsLabel;
    private Label? _warningLabel;
    private BaseBuildManager? _buildManager;
    private BaseAlertState? _baseAlertState;
    private EquipmentInventoryManager? _equipmentInventoryManager;
    private MercenaryEquipmentLoadoutManager? _mercenaryEquipmentLoadoutManager;
    private MercenaryController? _displayedMercenary;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _buildManager = GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");
        _baseAlertState = GetTree().CurrentScene?.GetNodeOrNull<BaseAlertState>("BaseAlertState");
        _equipmentInventoryManager = GetTree().CurrentScene?.GetNodeOrNull<EquipmentInventoryManager>("EquipmentInventoryManager");
        _mercenaryEquipmentLoadoutManager = GetTree().CurrentScene?.GetNodeOrNull<MercenaryEquipmentLoadoutManager>("MercenaryEquipmentLoadoutManager");

        _statusLabel = new Label
        {
            Name = "StatusLabel",
            Text = "Selected: 0",
            Position = new Vector2(12.0f, 12.0f),
            Size = new Vector2(1040.0f, 390.0f),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = ShowDebugStatusText
        };

        AddChild(_statusLabel);

        BuildRimHudPanel();
    }

    public override void _Process(double delta)
    {
        UpdateRimHudPanelLayout();
        RefreshEquipmentHudText();
    }

    public void SetDebugStatusVisible(bool visible)
    {
        ShowDebugStatusText = visible;

        if (_statusLabel != null)
        {
            _statusLabel.Visible = visible;
        }
    }

    private void BuildRimHudPanel()
    {
        _rimHudPanel = new PanelContainer
        {
            Name = "SelectedMercenaryRimHud",
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false
        };
        _rimHudPanel.AnchorLeft = 0.0f;
        _rimHudPanel.AnchorRight = 0.0f;
        _rimHudPanel.AnchorTop = 0.0f;
        _rimHudPanel.AnchorBottom = 0.0f;

        StyleBoxFlat panelStyle = new()
        {
            BgColor = new Color(0.05f, 0.06f, 0.075f, 0.88f),
            BorderColor = new Color(0.42f, 0.48f, 0.55f, 0.72f)
        };
        panelStyle.SetBorderWidthAll(1);
        panelStyle.SetCornerRadiusAll(4);
        _rimHudPanel.AddThemeStyleboxOverride("panel", panelStyle);

        MarginContainer margin = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        _rimHudPanel.AddChild(margin);

        HBoxContainer row = new()
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        row.AddThemeConstantOverride("separation", 6);
        margin.AddChild(row);

        VBoxContainer identitySection = CreateHudSection("\uC6A9\uBCD1", 145.0f);
        _identityNameLabel = CreateHudLabel(14, new Color(0.96f, 0.91f, 0.72f));
        _identityMetaLabel = CreateHudLabel();
        _identityStateLabel = CreateHudLabel();
        identitySection.AddChild(_identityNameLabel);
        identitySection.AddChild(_identityMetaLabel);
        identitySection.AddChild(_identityStateLabel);
        row.AddChild(identitySection);

        VBoxContainer workSection = CreateHudSection("\uD604\uC7AC \uC791\uC5C5", 185.0f);
        _workLabel = CreateHudLabel();
        workSection.AddChild(_workLabel);
        row.AddChild(workSection);

        VBoxContainer inventorySection = CreateHudSection("\uC18C\uC9C0\uD488", 130.0f);
        _inventoryLabel = CreateHudLabel();
        inventorySection.AddChild(_inventoryLabel);
        row.AddChild(inventorySection);

        VBoxContainer equipmentSection = CreateHudSection("\uC7A5\uBE44", 150.0f);
        _equipmentLabel = CreateHudLabel(10);
        equipmentSection.AddChild(_equipmentLabel);
        row.AddChild(equipmentSection);

        VBoxContainer needsSection = CreateHudSection("\uC695\uAD6C / \uC0C1\uD0DC", 170.0f);
        _needsLabel = CreateHudLabel();
        needsSection.AddChild(_needsLabel);
        row.AddChild(needsSection);

        VBoxContainer statsSection = CreateHudSection("\uC8FC\uC694 \uB2A5\uB825\uCE58", 180.0f);
        _statsLabel = CreateHudLabel();
        _warningLabel = CreateHudLabel(10, new Color(1.0f, 0.68f, 0.48f));
        statsSection.AddChild(_statsLabel);
        statsSection.AddChild(_warningLabel);
        row.AddChild(statsSection);

        AddChild(_rimHudPanel);
        UpdateRimHudPanelLayout();
    }

    private void UpdateRimHudPanelLayout()
    {
        if (_rimHudPanel == null)
        {
            return;
        }

        Vector2 viewportSize = GetViewportRect().Size;
        float availableWidth = viewportSize.X - HudLeftMargin - HudRightReservedWidth - HudRightMargin;
        float fallbackWidth = Mathf.Max(0.0f, viewportSize.X - HudLeftMargin - HudRightMargin);
        float panelWidth = availableWidth >= HudMinWidth ? availableWidth : fallbackWidth;
        panelWidth = Mathf.Max(280.0f, panelWidth);
        panelWidth = Mathf.Min(panelWidth, fallbackWidth);

        float maxHeightForViewport = Mathf.Max(80.0f, viewportSize.Y - HudBottomMargin * 2.0f);
        float panelHeight = Mathf.Clamp(HudMaxHeight, 80.0f, maxHeightForViewport);

        if (panelHeight < HudMinHeight && maxHeightForViewport >= HudMinHeight)
        {
            panelHeight = HudMinHeight;
        }

        float maxY = Mathf.Max(0.0f, viewportSize.Y - panelHeight);
        float panelY = Mathf.Clamp(viewportSize.Y - panelHeight - HudBottomMargin, 0.0f, maxY);
        _rimHudPanel.Position = new Vector2(HudLeftMargin, panelY);
        _rimHudPanel.Size = new Vector2(panelWidth, panelHeight);
        _rimHudPanel.CustomMinimumSize = new Vector2(280.0f, 80.0f);
    }

    private static VBoxContainer CreateHudSection(string title, float width)
    {
        VBoxContainer section = new()
        {
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(width, 0.0f),
            ClipContents = true
        };
        section.SizeFlagsHorizontal = SizeFlags.Fill;
        section.AddThemeConstantOverride("separation", 2);

        Label titleLabel = CreateHudLabel(10, new Color(0.58f, 0.68f, 0.78f));
        titleLabel.Text = title;
        titleLabel.Text = titleLabel.Text.ToUpperInvariant();
        section.AddChild(titleLabel);

        return section;
    }

    private static Label CreateHudLabel(int fontSize = 11, Color? color = null)
    {
        Label label = new()
        {
            Text = "-",
            MouseFilter = MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.Off,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeColorOverride("font_color", color ?? new Color(0.86f, 0.89f, 0.9f));
        label.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.72f));
        label.AddThemeConstantOverride("outline_size", 1);
        return label;
    }

    public void UpdateSelectionSummary(IEnumerable<MercenaryController> selectedMercenaries)
    {
        int selectedCount = 0;
        int lifeCount = 0;
        int rallyingCount = 0;
        int squadCount = 0;
        int downedCount = 0;
        int deadCount = 0;
        int noneOrderCount = 0;
        int lifeMovingCount = 0;
        int lifeWaitingCount = 0;
        int rallyMovingCount = 0;
        int squadIdleCount = 0;
        int squadMovingCount = 0;
        int squadHoldingCount = 0;
        int squadDefendingCount = 0;
        MercenaryController? singleSelectedMercenary = null;

        foreach (MercenaryController mercenary in selectedMercenaries)
        {
            if (selectedCount == 0)
            {
                singleSelectedMercenary = mercenary;
            }

            selectedCount++;

            if (mercenary.ControlMode == MercenaryControlMode.Life)
            {
                lifeCount++;
            }
            else if (mercenary.ControlMode == MercenaryControlMode.Rallying)
            {
                rallyingCount++;
            }
            else if (mercenary.ControlMode == MercenaryControlMode.Squad)
            {
                squadCount++;
            }
            else if (mercenary.ControlMode == MercenaryControlMode.Downed)
            {
                downedCount++;
            }
            else if (mercenary.ControlMode == MercenaryControlMode.Dead)
            {
                deadCount++;
            }

            if (mercenary.OrderState == MercenaryOrderState.None)
            {
                noneOrderCount++;
            }
            else if (mercenary.OrderState == MercenaryOrderState.LifeMoving)
            {
                lifeMovingCount++;
            }
            else if (mercenary.OrderState == MercenaryOrderState.LifeWaiting)
            {
                lifeWaitingCount++;
            }
            else if (mercenary.OrderState == MercenaryOrderState.RallyMoving)
            {
                rallyMovingCount++;
            }
            else if (mercenary.OrderState == MercenaryOrderState.SquadIdle)
            {
                squadIdleCount++;
            }
            else if (mercenary.OrderState == MercenaryOrderState.SquadMoving)
            {
                squadMovingCount++;
            }
            else if (mercenary.OrderState == MercenaryOrderState.SquadHolding)
            {
                squadHoldingCount++;
            }
            else if (mercenary.OrderState == MercenaryOrderState.SquadDefending)
            {
                squadDefendingCount++;
            }
        }

        string summary = selectedCount == 0
            ? "Selected: 0"
            : $"Selected: {selectedCount} | Life {lifeCount} / Rallying {rallyingCount} / Squad {squadCount} / Downed {downedCount} / Dead {deadCount}";

        summary += BuildBaseAlertSummary();

        if (selectedCount > 0)
        {
            summary += $"\nOrders: LifeMoving {lifeMovingCount} / LifeWaiting {lifeWaitingCount} / RallyMoving {rallyMovingCount} / SquadIdle {squadIdleCount} / SquadMoving {squadMovingCount} / SquadHolding {squadHoldingCount} / SquadDefending {squadDefendingCount} / None {noneOrderCount}";
        }

        if (selectedCount == 1 && singleSelectedMercenary != null)
        {
            summary += BuildNeedSummary(singleSelectedMercenary);
            summary += BuildPathFeedbackSummary(singleSelectedMercenary);
            ShowProfileSummary(singleSelectedMercenary);
        }
        else
        {
            HideProfileSummary();
        }

        summary += BuildResourceSummary();

        if (_statusLabel != null)
        {
            _statusLabel.Text = summary;
        }
    }

    private void ShowProfileSummary(MercenaryController mercenary)
    {
        if (_rimHudPanel == null)
        {
            return;
        }

        MercenaryProfile profile = mercenary.Profile;
        MercenaryStats stats = profile.Stats;
        MercenaryCondition condition = profile.Condition;
        string secondaryRole = profile.SecondaryRole == profile.PrimaryRole ? "" : $" / {profile.SecondaryRole}";
        string progressLabel = mercenary.GetCurrentUseProgressLabel();
        string roomEffectLabel = mercenary.GetCurrentRoomEffectLabel();
        string logisticsWarning = mercenary.GetLogisticsValidationWarning();

        SetLabelText(_identityNameLabel, TruncateText($"{profile.DisplayName} [{profile.Rank}]", 24));
        SetLabelText(_identityMetaLabel, TruncateText($"{profile.Race} / {profile.PrimaryRole}{secondaryRole}", 30));
        SetLabelText(
            _identityStateLabel,
            TruncateText($"\uD604\uC7AC: {mercenary.GetCurrentWorkSummary()}", 30));

        List<string> workLines = new()
        {
            $"\uC791\uC5C5: {mercenary.GetCurrentWorkLabel()}",
            $"\uBAA9\uD45C: {mercenary.GetCurrentWorkTargetLabel()}",
            $"\uC0C1\uD0DC: {mercenary.GetCurrentWorkStateLabel()}"
        };

        if (!string.IsNullOrWhiteSpace(progressLabel))
        {
            workLines.Add($"\uC9C4\uD589: {progressLabel}");
        }
        else if (!string.IsNullOrWhiteSpace(roomEffectLabel))
        {
            workLines.Add($"\uBC29 \uD6A8\uACFC: {roomEffectLabel}");
        }
        else
        {
            workLines.Add($"\uC774\uC720: {mercenary.GetCurrentWorkDecisionReason()}");
        }

        SetLabelText(_workLabel, BuildLimitedLines(workLines, 4, 34));
        SetLabelText(_inventoryLabel, BuildInventoryHudText(mercenary));
        SetLabelText(_equipmentLabel, BuildEquipmentHudText(mercenary));
        SetLabelText(
            _needsLabel,
            BuildLimitedLines(
                new[]
                {
                    $"HP {condition.Health}/{stats.MaxHealth}",
                    $"\uBC30\uACE0\uD514 {Mathf.RoundToInt(condition.Hunger)} / \uD53C\uB85C {Mathf.RoundToInt(condition.Sleepiness)}",
                    $"\uAE30\uBD84 {Mathf.RoundToInt(condition.Mood)} / \uC2A4\uD2B8\uB808\uC2A4 {Mathf.RoundToInt(condition.Stress)}",
                    $"\uC704\uC0DD {Mathf.RoundToInt(condition.Hygiene)} / \uBD80\uC0C1 {Mathf.RoundToInt(condition.InjurySeverity)}",
                    $"\uC694\uC57D: {mercenary.GetConditionStatusSummary()}"
                },
                5,
                32));

        SetLabelText(
            _statsLabel,
            BuildLimitedLines(
                new[]
                {
                    $"\uC804\uD22C {stats.CombatSkill}  \uC6B4\uBC18 {stats.HaulingSkill}",
                    $"\uC81C\uC791 {stats.CraftingSkill}  \uB18D\uC0AC {stats.FarmingSkill}",
                    $"\uC758\uB8CC {stats.MedicalSkill}  \uC694\uB9AC {stats.CookingSkill}",
                    $"STR {stats.Strength}  DEX {stats.Dexterity}  END {stats.Endurance}",
                    $"INT {stats.Intelligence}  WIL {stats.Willpower}  \uC6B4\uBC18 {stats.MaxCarryWeight}"
                },
                5,
                34));

        SetLabelText(
            _warningLabel,
            string.IsNullOrWhiteSpace(logisticsWarning)
                ? ""
                : BuildLimitedLines(new[] { $"\uACBD\uACE0: {logisticsWarning}" }, 1, 38));

        UpdateRimHudPanelLayout();
        _displayedMercenary = mercenary;
        _rimHudPanel.Visible = true;
    }

    private void HideProfileSummary()
    {
        if (_rimHudPanel != null)
        {
            _rimHudPanel.Visible = false;
        }

        _displayedMercenary = null;
    }

    private static void SetLabelText(Label? label, string text)
    {
        if (label == null)
        {
            return;
        }

        label.Text = GetNonEmptyText(text, "-");
    }

    private static string BuildInventoryHudText(MercenaryController mercenary)
    {
        List<string> lines = new()
        {
            $"\uAC00\uBC29: {mercenary.GetInventoryWeightSummary()}"
        };
        int hiddenCount = 0;
        int shownCount = 0;

        foreach (MercenaryInventoryStack stack in mercenary.Inventory.Stacks)
        {
            if (stack.Amount <= 0)
            {
                continue;
            }

            if (shownCount < InventoryVisibleItemLines)
            {
                lines.Add($"{BaseBuildManager.GetResourceDisplayName(stack.ResourceType)} x{stack.Amount}");
                shownCount++;
            }
            else
            {
                hiddenCount++;
            }
        }

        if (shownCount == 0)
        {
            lines.Add("\uBE44\uC5B4 \uC788\uC74C");
        }
        else if (hiddenCount > 0)
        {
            lines.Add($"\uC678 {hiddenCount}\uAC1C");
        }

        return BuildLimitedLines(lines, 4, 28);
    }

    private string BuildEquipmentHudText(MercenaryController mercenary)
    {
        MercenaryEquipmentLoadoutManager? loadoutManager = GetMercenaryEquipmentLoadoutManager();
        MercenaryEquipmentLoadout? loadout = null;

        if (loadoutManager != null)
        {
            loadoutManager.TryGetLoadout(GetMercenaryEquipmentId(mercenary), out loadout);
        }

        List<string> lines = new()
        {
            $"{GetEquipmentSlotDisplayName(EquipmentSlotType.Weapon)}: {GetEquipmentSlotText(loadout, EquipmentSlotType.Weapon)}",
            $"{GetEquipmentSlotDisplayName(EquipmentSlotType.Armor)}: {GetEquipmentSlotText(loadout, EquipmentSlotType.Armor)}",
            $"{GetEquipmentSlotDisplayName(EquipmentSlotType.Shield)}: {GetEquipmentSlotText(loadout, EquipmentSlotType.Shield)}",
            $"{GetEquipmentSlotDisplayName(EquipmentSlotType.Accessory)}: {GetEquipmentSlotText(loadout, EquipmentSlotType.Accessory)}",
            $"{GetEquipmentSlotDisplayName(EquipmentSlotType.Tool)}: {GetEquipmentSlotText(loadout, EquipmentSlotType.Tool)}"
        };

        return BuildLimitedLines(lines, 5, 22);
    }

    private string GetEquipmentSlotText(MercenaryEquipmentLoadout? loadout, EquipmentSlotType slot)
    {
        if (loadout == null
            || !loadout.TryGetEquipped(slot, out string? instanceId)
            || string.IsNullOrWhiteSpace(instanceId))
        {
            return "\uC5C6\uC74C";
        }

        EquipmentInventoryManager? equipmentInventoryManager = GetEquipmentInventoryManager();
        if (equipmentInventoryManager == null
            || !equipmentInventoryManager.TryGetEquipment(instanceId, out EquipmentInstance? instance)
            || instance == null)
        {
            return "\uB204\uB77D\uB41C \uC7A5\uBE44";
        }

        if (EquipmentDefinitionDatabase.TryGet(instance.DefinitionId, out EquipmentDefinitionEntry? definition))
        {
            return definition.DisplayName;
        }

        return string.IsNullOrWhiteSpace(instance.DefinitionId)
            ? "\uC54C \uC218 \uC5C6\uB294 \uC7A5\uBE44"
            : instance.DefinitionId;
    }

    private void RefreshEquipmentHudText()
    {
        if (_rimHudPanel == null
            || !_rimHudPanel.Visible
            || _equipmentLabel == null
            || _displayedMercenary == null
            || !GodotObject.IsInstanceValid(_displayedMercenary)
            || _displayedMercenary.IsQueuedForDeletion())
        {
            return;
        }

        SetLabelText(_equipmentLabel, BuildEquipmentHudText(_displayedMercenary));
    }

    private EquipmentInventoryManager? GetEquipmentInventoryManager()
    {
        _equipmentInventoryManager ??= GetTree().CurrentScene?.GetNodeOrNull<EquipmentInventoryManager>("EquipmentInventoryManager");
        return _equipmentInventoryManager;
    }

    private MercenaryEquipmentLoadoutManager? GetMercenaryEquipmentLoadoutManager()
    {
        _mercenaryEquipmentLoadoutManager ??= GetTree().CurrentScene?.GetNodeOrNull<MercenaryEquipmentLoadoutManager>("MercenaryEquipmentLoadoutManager");
        return _mercenaryEquipmentLoadoutManager;
    }

    private static string GetMercenaryEquipmentId(MercenaryController mercenary)
    {
        // Fallback is a runtime-only id used only when a profile id is missing.
        return string.IsNullOrWhiteSpace(mercenary.Profile.MercenaryId)
            ? mercenary.GetInstanceId().ToString()
            : mercenary.Profile.MercenaryId;
    }

    private static string GetEquipmentSlotDisplayName(EquipmentSlotType slotType)
    {
        return slotType switch
        {
            EquipmentSlotType.Weapon => "\uBB34\uAE30",
            EquipmentSlotType.Armor => "\uBC29\uC5B4\uAD6C",
            EquipmentSlotType.Shield => "\uBC29\uD328",
            EquipmentSlotType.Accessory => "\uC7A5\uC2E0\uAD6C",
            EquipmentSlotType.Tool => "\uB3C4\uAD6C",
            _ => slotType.ToString()
        };
    }

    private static string BuildLimitedLines(IEnumerable<string> lines, int maxLines, int maxCharactersPerLine)
    {
        List<string> result = new();

        foreach (string line in lines)
        {
            if (result.Count >= maxLines)
            {
                break;
            }

            string compactLine = TruncateText(line, maxCharactersPerLine);

            if (!string.IsNullOrWhiteSpace(compactLine))
            {
                result.Add(compactLine);
            }
        }

        return result.Count == 0 ? "-" : string.Join("\n", result);
    }

    private static string TruncateText(string? text, int maxCharacters)
    {
        string value = GetNonEmptyText(text, "-").Replace('\n', ' ').Trim();

        if (maxCharacters <= 3 || value.Length <= maxCharacters)
        {
            return value;
        }

        return value[..(maxCharacters - 3)] + "...";
    }

    private static string GetNonEmptyText(string? text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static string BuildNeedSummary(MercenaryController mercenary)
    {
        if (!mercenary.TryGetLifeAI(out MercenaryLifeAI? lifeAI) || lifeAI == null)
        {
            return "\nNeed: -";
        }

        int currentSleep = Mathf.RoundToInt(lifeAI.CurrentSleepNeed);
        int maxSleep = Mathf.RoundToInt(lifeAI.CurrentMaxSleepNeed);
        int currentHunger = Mathf.RoundToInt(lifeAI.CurrentHungerNeed);
        int maxHunger = Mathf.RoundToInt(lifeAI.CurrentMaxHungerNeed);
        string sleepStatus = "";
        string hungerStatus = "";

        if (lifeAI.IsSleepCritical)
        {
            sleepStatus = " CRITICAL";
        }
        else if (lifeAI.IsSleepUrgent)
        {
            sleepStatus = " LOW";
        }

        if (lifeAI.IsHungerCritical)
        {
            hungerStatus = " CRITICAL";
        }
        else if (lifeAI.IsHungerUrgent)
        {
            hungerStatus = " LOW";
        }

        string starvationSummary = BuildStarvationSummary(lifeAI);
        string dutySummary = lifeAI.IsPatrolling ? "\nDuty: Patrolling" : "";
        string detectionSummary = lifeAI.IsPatrolling ? $"\nDetection: {GetPatrolDetectionText(lifeAI)}" : "";
        string gatherSummary = lifeAI.CurrentLifeActionName == "Gather" ? $"\nGathering: {GetGatherText(lifeAI)}" : "";
        string haulSummary = lifeAI.CurrentLifeActionName == "Haul" ? $"\nHauling: {lifeAI.HaulingTargetLabel}\nCarrying: {lifeAI.CarryingResourceLabel}" : "";
        string farmingSummary = lifeAI.CurrentLifeActionName == "Plant" || lifeAI.CurrentLifeActionName == "HarvestCrop"
            ? $"\nFarming: {lifeAI.FarmingTargetLabel} | Work {lifeAI.CurrentFarmWorkRemaining:0.0} / {lifeAI.CurrentFarmWorkSeconds:0.0}"
            : "";
        return $"\nNeed\nSleep: {currentSleep} / {maxSleep}{sleepStatus}\nHunger: {currentHunger} / {maxHunger}{hungerStatus}{starvationSummary}\nAction: {GetDisplayLifeAction(lifeAI)}{dutySummary}{detectionSummary}{gatherSummary}{haulSummary}{farmingSummary}\nFacility: {GetFacilityUseText(lifeAI)}\nEat Food: {GetEatFoodText(lifeAI)}";
    }

    private static string GetFacilityUseText(MercenaryLifeAI lifeAI)
    {
        if (lifeAI.HasOccupiedFacility)
        {
            return $"{lifeAI.CurrentOccupiedFacilityType} Occupied";
        }

        if (lifeAI.HasReservedFacility)
        {
            return $"{lifeAI.CurrentReservedFacilityType} Reserved";
        }

        return "None";
    }

    private static string BuildPathFeedbackSummary(MercenaryController mercenary)
    {
        return mercenary.HasPathFeedback
            ? $"\nPath: {mercenary.PathFeedbackMessage}"
            : "";
    }

    private static string GetDisplayLifeAction(MercenaryLifeAI lifeAI)
    {
        return lifeAI.CurrentLifeActionName switch
        {
            "Guard" => "Patrol",
            "HarvestCrop" => "Harvest Crop",
            _ => lifeAI.CurrentLifeActionName
        };
    }

    private static string GetPatrolDetectionText(MercenaryLifeAI lifeAI)
    {
        if (!lifeAI.HasSpottedEnemy)
        {
            return "Clear";
        }

        string enemyName = lifeAI.GetSpottedEnemyName();
        return string.IsNullOrEmpty(enemyName) ? "Enemy Spotted" : $"Enemy Spotted: {enemyName}";
    }

    private static string GetGatherText(MercenaryLifeAI lifeAI)
    {
        string reservationText = lifeAI.HasGatherReservation ? " Reserved" : "";

        if (!lifeAI.IsGathering)
        {
            return $"{lifeAI.CurrentGatherTargetLabel}{reservationText}";
        }

        return $"{lifeAI.CurrentGatherTargetLabel}{reservationText} | Work {lifeAI.CurrentGatherWorkRemaining:0.0} / {lifeAI.CurrentGatherWorkSeconds:0.0}";
    }

    private string BuildResourceSummary()
    {
        BaseBuildManager? buildManager = GetBuildManager();

        if (buildManager == null)
        {
            return "\nResources\nFood: -\nWood: -\nStone: -\nMetal: -";
        }

        string statusLabel = buildManager.GetFoodStatusLabel();
        string foodLine = string.IsNullOrEmpty(statusLabel)
            ? $"Food: {buildManager.GetResourceAmount(BaseResourceType.Food)}"
            : $"Food: {buildManager.GetResourceAmount(BaseResourceType.Food)} {statusLabel}";

        return $"\nResources\n{foodLine}\nWood: {buildManager.GetResourceAmount(BaseResourceType.Wood)}\nStone: {buildManager.GetResourceAmount(BaseResourceType.Stone)}\nMetal: {buildManager.GetResourceAmount(BaseResourceType.Metal)}";
    }

    private string BuildBaseAlertSummary()
    {
        BaseAlertState? baseAlertState = GetBaseAlertState();

        if (baseAlertState == null)
        {
            return "\nBase Alert: -";
        }

        if (!baseAlertState.EnemySpotted)
        {
            return "\nBase Alert: Clear";
        }

        return $"\nBase Alert: {baseAlertState.AlertLabel}\nSpotters: {baseAlertState.SpotterCount}\nSpotted Enemies: {baseAlertState.SpottedEnemyCount}";
    }

    private BaseBuildManager? GetBuildManager()
    {
        _buildManager ??= GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");
        return _buildManager;
    }

    private BaseAlertState? GetBaseAlertState()
    {
        _baseAlertState ??= GetTree().CurrentScene?.GetNodeOrNull<BaseAlertState>("BaseAlertState");
        return _baseAlertState;
    }

    private static string GetEatFoodText(MercenaryLifeAI lifeAI)
    {
        if (lifeAI.CurrentLifeActionName != "Eat")
        {
            return "-";
        }

        BaseBuildManager? buildManager = lifeAI.GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");

        if (buildManager != null && buildManager.IsFoodEmpty && !lifeAI.HasConsumedFoodForCurrentEat)
        {
            return "No Food";
        }

        return lifeAI.HasConsumedFoodForCurrentEat ? "Consumed" : "Needed";
    }

    private static string BuildStarvationSummary(MercenaryLifeAI lifeAI)
    {
        if (lifeAI.CurrentStarvingSeconds <= 0.0f)
        {
            return "";
        }

        string summary = $"\nStarvation: {lifeAI.CurrentStarvingSeconds:0.0} / {lifeAI.CurrentStarvationDelaySeconds:0.0}";

        if (lifeAI.IsStarving)
        {
            summary += "\nStatus: Starving";
        }

        return summary;
    }
}
