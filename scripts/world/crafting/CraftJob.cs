using System.Collections.Generic;
using Godot;

public sealed class CraftJob
{
    private readonly Dictionary<BaseResourceType, int> _requiredInputs = new();
    private readonly Dictionary<BaseResourceType, int> _inputsDelivered = new();
    private readonly Dictionary<BaseResourceType, int> _producedOutputs = new();
    private MercenaryController? _reservedWorker;
    private MercenaryController? _reservedDeliveryWorker;

    public int JobId { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public Vector2I FacilityCell { get; set; }
    public CraftJobState State { get; private set; } = CraftJobState.WaitingForMaterials;
    public IReadOnlyDictionary<BaseResourceType, int> InputsDelivered => _inputsDelivered;
    public IReadOnlyDictionary<BaseResourceType, int> ProducedOutputs => _producedOutputs;
    public float WorkProgress { get; private set; }
    public float RequiredWork { get; set; } = 1.0f;
    public MercenaryController? ReservedWorker => IsValidWorker(_reservedWorker) ? _reservedWorker : null;
    public MercenaryController? ReservedDeliveryWorker => IsValidWorker(_reservedDeliveryWorker) ? _reservedDeliveryWorker : null;
    public bool IsCompleted => State == CraftJobState.Completed;
    public bool IsCancelled => State == CraftJobState.Cancelled;
    public bool HasAllMaterials => GetFirstMissingInput() == null;

    public void SetRequirements(CraftRecipeEntry recipe)
    {
        _requiredInputs.Clear();
        _inputsDelivered.Clear();
        _producedOutputs.Clear();

        if (recipe != null)
        {
            RecipeId = recipe.RecipeId;
            RequiredWork = recipe.RequiredWork;

            foreach (KeyValuePair<BaseResourceType, int> input in recipe.Inputs)
            {
                if (input.Value > 0)
                {
                    _requiredInputs[input.Key] = input.Value;
                    _inputsDelivered[input.Key] = 0;
                }
            }
        }

        WorkProgress = 0.0f;
        RefreshState();
    }

    public int GetDeliveredAmount(BaseResourceType type)
    {
        return _inputsDelivered.TryGetValue(type, out int deliveredAmount) ? deliveredAmount : 0;
    }

    public int GetRequiredAmount(BaseResourceType type)
    {
        return _requiredInputs.TryGetValue(type, out int requiredAmount) ? requiredAmount : 0;
    }

    public int GetRemainingAmount(BaseResourceType type)
    {
        int remainingAmount = GetRequiredAmount(type) - GetDeliveredAmount(type);
        return Mathf.Max(0, remainingAmount);
    }

    public int AcceptMaterial(BaseResourceType type, int amount)
    {
        if (amount <= 0 || IsCompleted || IsCancelled || !_requiredInputs.ContainsKey(type))
        {
            return 0;
        }

        int acceptedAmount = Mathf.Min(amount, GetRemainingAmount(type));
        if (acceptedAmount <= 0)
        {
            return 0;
        }

        _inputsDelivered[type] = GetDeliveredAmount(type) + acceptedAmount;
        RefreshState();
        return acceptedAmount;
    }

    public void AddWorkProgress(float amount)
    {
        if (amount <= 0.0f || IsCompleted || IsCancelled || !HasAllMaterials)
        {
            return;
        }

        if (State == CraftJobState.ReadyToCraft)
        {
            State = CraftJobState.Crafting;
        }

        if (State != CraftJobState.Crafting)
        {
            return;
        }

        WorkProgress = Mathf.Min(RequiredWork, WorkProgress + amount);

        if (WorkProgress >= RequiredWork)
        {
            State = CraftJobState.OutputReady;
        }
    }

    public bool TryReserveDelivery(MercenaryController worker)
    {
        PruneReservations();

        if (IsCompleted || IsCancelled || HasAllMaterials)
        {
            return false;
        }

        if (_reservedDeliveryWorker != null && _reservedDeliveryWorker != worker)
        {
            return false;
        }

        _reservedDeliveryWorker = worker;
        return true;
    }

    public void ReleaseDelivery(MercenaryController worker)
    {
        if (_reservedDeliveryWorker == worker || !IsValidWorker(_reservedDeliveryWorker))
        {
            _reservedDeliveryWorker = null;
        }
    }

    public bool TryReserveCrafting(MercenaryController worker)
    {
        PruneReservations();

        if (IsCompleted || IsCancelled || !HasAllMaterials)
        {
            return false;
        }

        if (_reservedWorker != null && _reservedWorker != worker)
        {
            return false;
        }

        _reservedWorker = worker;
        State = CraftJobState.Crafting;
        return true;
    }

    public void ReleaseCrafting(MercenaryController worker)
    {
        if (_reservedWorker == worker || !IsValidWorker(_reservedWorker))
        {
            _reservedWorker = null;
        }

        RefreshState();
    }

    public void Cancel()
    {
        State = CraftJobState.Cancelled;
        _reservedWorker = null;
        _reservedDeliveryWorker = null;
    }

    public void Complete()
    {
        State = CraftJobState.Completed;
        WorkProgress = RequiredWork;
        _reservedWorker = null;
        _reservedDeliveryWorker = null;
    }

    public float GetProgressRatio()
    {
        return RequiredWork <= 0.0f ? 1.0f : Mathf.Clamp(WorkProgress / RequiredWork, 0.0f, 1.0f);
    }

    public void PruneReservations()
    {
        if (!IsValidWorker(_reservedWorker))
        {
            _reservedWorker = null;
        }

        if (!IsValidWorker(_reservedDeliveryWorker))
        {
            _reservedDeliveryWorker = null;
        }
    }

    private BaseResourceType? GetFirstMissingInput()
    {
        foreach (KeyValuePair<BaseResourceType, int> input in _requiredInputs)
        {
            if (GetDeliveredAmount(input.Key) < input.Value)
            {
                return input.Key;
            }
        }

        return null;
    }

    private void RefreshState()
    {
        if (IsCompleted || IsCancelled || State == CraftJobState.Crafting || State == CraftJobState.OutputReady)
        {
            return;
        }

        State = HasAllMaterials ? CraftJobState.ReadyToCraft : CraftJobState.WaitingForMaterials;
    }

    private static bool IsValidWorker(MercenaryController? worker)
    {
        return worker != null
            && GodotObject.IsInstanceValid(worker)
            && !worker.IsQueuedForDeletion();
    }
}
