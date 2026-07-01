using System.Collections.Generic;
using Godot;

public sealed class ConstructionSite
{
    private readonly Dictionary<BaseResourceType, ConstructionRequirement> _requirements = new();
    private MercenaryController? _reservedDeliveryWorker;
    private MercenaryController? _reservedBuildWorker;

    public int SiteId { get; set; }
    public TileBuildType TargetBuildType { get; set; }
    public BuildMaterialType MaterialType { get; set; } = BuildMaterialType.Basic;
    public Vector2I OriginCell { get; set; }
    public Vector2I Size { get; set; } = Vector2I.One;
    public List<Vector2I> OccupiedCells { get; } = new();
    public IReadOnlyDictionary<BaseResourceType, ConstructionRequirement> Requirements => _requirements;
    public float BuildProgress { get; private set; }
    public float RequiredWork { get; set; } = 5.0f;
    public ConstructionSiteState State { get; private set; } = ConstructionSiteState.WaitingForMaterials;
    public string DisplayName { get; set; } = "Construction Site";
    public MercenaryController? ReservedDeliveryWorker => IsValidWorker(_reservedDeliveryWorker) ? _reservedDeliveryWorker : null;
    public MercenaryController? ReservedBuildWorker => IsValidWorker(_reservedBuildWorker) ? _reservedBuildWorker : null;
    public bool HasAllMaterials => GetFirstMissingRequirement() == null;
    public bool IsCompleted => State == ConstructionSiteState.Completed;
    public bool IsCancelled => State == ConstructionSiteState.Cancelled;

    public void SetRequirements(IReadOnlyDictionary<BaseResourceType, int> costs)
    {
        _requirements.Clear();

        foreach (KeyValuePair<BaseResourceType, int> cost in costs)
        {
            if (cost.Value > 0)
            {
                _requirements[cost.Key] = new ConstructionRequirement(cost.Key, cost.Value);
            }
        }

        RefreshState();
    }

    public ConstructionRequirement? GetFirstMissingRequirement()
    {
        foreach (ConstructionRequirement requirement in _requirements.Values)
        {
            if (!requirement.IsSatisfied)
            {
                return requirement;
            }
        }

        return null;
    }

    public int GetDeliveredAmount(BaseResourceType type)
    {
        return _requirements.TryGetValue(type, out ConstructionRequirement? requirement)
            ? requirement.DeliveredAmount
            : 0;
    }

    public int GetRequiredAmount(BaseResourceType type)
    {
        return _requirements.TryGetValue(type, out ConstructionRequirement? requirement)
            ? requirement.RequiredAmount
            : 0;
    }

    public int GetRemainingAmount(BaseResourceType type)
    {
        return _requirements.TryGetValue(type, out ConstructionRequirement? requirement)
            ? requirement.RemainingAmount
            : 0;
    }

    public int AcceptMaterial(BaseResourceType type, int amount)
    {
        if (amount <= 0 || !_requirements.TryGetValue(type, out ConstructionRequirement? requirement))
        {
            return 0;
        }

        int acceptedAmount = Mathf.Min(amount, requirement.RemainingAmount);
        requirement.DeliveredAmount += acceptedAmount;
        RefreshState();
        return acceptedAmount;
    }

    public bool TryReserveMaterialDelivery(MercenaryController worker)
    {
        PruneReservations();

        if (_reservedDeliveryWorker != null && _reservedDeliveryWorker != worker)
        {
            return false;
        }

        _reservedDeliveryWorker = worker;
        return true;
    }

    public void ReleaseMaterialDelivery(MercenaryController worker)
    {
        if (_reservedDeliveryWorker == worker || !IsValidWorker(_reservedDeliveryWorker))
        {
            _reservedDeliveryWorker = null;
        }
    }

    public bool TryReserveBuildWork(MercenaryController worker)
    {
        PruneReservations();

        if (!HasAllMaterials)
        {
            return false;
        }

        if (_reservedBuildWorker != null && _reservedBuildWorker != worker)
        {
            return false;
        }

        _reservedBuildWorker = worker;
        State = ConstructionSiteState.Building;
        return true;
    }

    public void ReleaseBuildWork(MercenaryController worker)
    {
        if (_reservedBuildWorker == worker || !IsValidWorker(_reservedBuildWorker))
        {
            _reservedBuildWorker = null;
        }

        RefreshState();
    }

    public void AddBuildProgress(float amount)
    {
        if (amount <= 0.0f || IsCompleted || IsCancelled)
        {
            return;
        }

        BuildProgress = Mathf.Min(RequiredWork, BuildProgress + amount);

        if (BuildProgress >= RequiredWork)
        {
            State = ConstructionSiteState.Completed;
        }
    }

    public void Cancel()
    {
        State = ConstructionSiteState.Cancelled;
        _reservedDeliveryWorker = null;
        _reservedBuildWorker = null;
    }

    public void Complete()
    {
        State = ConstructionSiteState.Completed;
        BuildProgress = RequiredWork;
        _reservedDeliveryWorker = null;
        _reservedBuildWorker = null;
    }

    public float GetProgressRatio()
    {
        return RequiredWork <= 0.0f ? 1.0f : Mathf.Clamp(BuildProgress / RequiredWork, 0.0f, 1.0f);
    }

    public int GetDeliveredTotal()
    {
        int total = 0;

        foreach (ConstructionRequirement requirement in _requirements.Values)
        {
            total += requirement.DeliveredAmount;
        }

        return total;
    }

    public int GetRequiredTotal()
    {
        int total = 0;

        foreach (ConstructionRequirement requirement in _requirements.Values)
        {
            total += requirement.RequiredAmount;
        }

        return total;
    }

    public void PruneReservations()
    {
        if (!IsValidWorker(_reservedDeliveryWorker))
        {
            _reservedDeliveryWorker = null;
        }

        if (!IsValidWorker(_reservedBuildWorker))
        {
            _reservedBuildWorker = null;
        }
    }

    private void RefreshState()
    {
        if (IsCompleted || IsCancelled || State == ConstructionSiteState.Building)
        {
            return;
        }

        State = HasAllMaterials ? ConstructionSiteState.ReadyToBuild : ConstructionSiteState.WaitingForMaterials;
    }

    private static bool IsValidWorker(MercenaryController? worker)
    {
        return worker != null
            && GodotObject.IsInstanceValid(worker)
            && !worker.IsQueuedForDeletion();
    }
}
