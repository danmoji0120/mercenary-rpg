using System.Collections.Generic;
using Godot;

public partial class SelectedMercenaryHud : Control
{
    private Label? _statusLabel;
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
            MouseFilter = MouseFilterEnum.Ignore
        };

        AddChild(_statusLabel);
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
        }

        summary += BuildResourceSummary();

        if (_statusLabel != null)
        {
            _statusLabel.Text = summary;
        }
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
