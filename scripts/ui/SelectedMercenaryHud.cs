using System.Collections.Generic;
using Godot;

public partial class SelectedMercenaryHud : Control
{
    [Export]
    public bool ShowDebugStatusText { get; set; } = false;

    private Label? _statusLabel;
    private Label? _profileLabel;
    private BaseBuildManager? _buildManager;
    private BaseAlertState? _baseAlertState;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _buildManager = GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");
        _baseAlertState = GetTree().CurrentScene?.GetNodeOrNull<BaseAlertState>("BaseAlertState");

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

        _profileLabel = new Label
        {
            Name = "MercenaryProfileLabel",
            Text = "",
            Position = new Vector2(220.0f, 12.0f),
            Size = new Vector2(460.0f, 320.0f),
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _profileLabel.AddThemeFontSizeOverride("font_size", 13);
        _profileLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.0f, 0.0f, 0.82f));
        _profileLabel.AddThemeConstantOverride("outline_size", 2);
        AddChild(_profileLabel);
    }

    public void SetDebugStatusVisible(bool visible)
    {
        ShowDebugStatusText = visible;

        if (_statusLabel != null)
        {
            _statusLabel.Visible = visible;
        }
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
        else if (_profileLabel != null)
        {
            _profileLabel.Visible = false;
        }

        summary += BuildResourceSummary();

        if (_statusLabel != null)
        {
            _statusLabel.Text = summary;
        }
    }

    private void ShowProfileSummary(MercenaryController mercenary)
    {
        if (_profileLabel == null)
        {
            return;
        }

        MercenaryProfile profile = mercenary.Profile;
        MercenaryStats stats = profile.Stats;
        MercenaryCondition condition = profile.Condition;
        int hunger = Mathf.RoundToInt(condition.Hunger);
        int sleepiness = Mathf.RoundToInt(condition.Sleepiness);
        int mood = Mathf.RoundToInt(condition.Mood);
        int stress = Mathf.RoundToInt(condition.Stress);
        int hygiene = Mathf.RoundToInt(condition.Hygiene);
        int injurySeverity = Mathf.RoundToInt(condition.InjurySeverity);

        string traits = profile.Traits.Count == 0 ? "-" : string.Join(", ", profile.Traits);
        string useProgressLabel = mercenary.GetCurrentUseProgressLabel();
        string useProgressText = string.IsNullOrWhiteSpace(useProgressLabel)
            ? ""
            : $"\n\uC9C4\uD589: {useProgressLabel}";
        string roomEffectLabel = mercenary.GetCurrentRoomEffectLabel();
        string roomEffectText = string.IsNullOrWhiteSpace(roomEffectLabel)
            ? ""
            : $"\n\uBC29 \uD6A8\uACFC: {roomEffectLabel}";
        string inventoryContents = mercenary.GetInventoryContentsSummary();
        string logisticsWarning = mercenary.GetLogisticsValidationWarning();
        string logisticsWarningText = string.IsNullOrWhiteSpace(logisticsWarning)
            ? ""
            : $"\n\uACBD\uACE0: {logisticsWarning}";
        _profileLabel.Text =
            $"{profile.DisplayName} [{profile.Rank}]\n"
            + $"{profile.Race} / {profile.PrimaryRole}"
            + (profile.SecondaryRole == profile.PrimaryRole ? "" : $" / {profile.SecondaryRole}")
            + $"\nHP {condition.Health}/{stats.MaxHealth}"
            + $"\n\uC0C1\uD0DC \uC694\uC57D: {mercenary.GetConditionStatusSummary()}"
            + $"\n\uAE30\uBD84 {mood} / \uC2A4\uD2B8\uB808\uC2A4 {stress} / \uC704\uC0DD {hygiene}"
            + $"\n\uBC30\uACE0\uD514 {hunger} / \uC878\uB9BC {sleepiness} / \uBD80\uC0C1 {injurySeverity}"
            + $"\n\uC6B4\uBC18 \uAC00\uB2A5 \uBB34\uAC8C {stats.MaxCarryWeight}"
            + $"\n\uAC00\uBC29: {mercenary.GetInventoryWeightSummary()}"
            + $"\n\uAC00\uBC29 \uB0B4\uC6A9: {inventoryContents}"
            + logisticsWarningText
            + $"\n\n\uD604\uC7AC \uC791\uC5C5: {mercenary.GetCurrentWorkLabel()}"
            + $"\n\uBAA9\uD45C: {mercenary.GetCurrentWorkTargetLabel()}"
            + $"\n\uC0C1\uD0DC: {mercenary.GetCurrentWorkStateLabel()}"
            + $"\n\uC774\uC720: {mercenary.GetCurrentWorkDecisionReason()}"
            + useProgressText
            + roomEffectText
            + $"\nSTR {stats.Strength} DEX {stats.Dexterity} END {stats.Endurance} INT {stats.Intelligence} WIL {stats.Willpower}"
            + $"\n\uC219\uB828 C{stats.CombatSkill} H{stats.HaulingSkill} F{stats.FarmingSkill} Cr{stats.CraftingSkill} M{stats.MedicalSkill} Co{stats.CookingSkill}"
            + $"\n\uC791\uC5C5: {profile.GetWorkSettings().GetCompactSummary()}"
            + $"\n\uD2B9\uC131: {traits}"
            + (string.IsNullOrWhiteSpace(profile.ShortDescription) ? "" : $"\n{profile.ShortDescription}");
        _profileLabel.Visible = true;
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
