using System.Collections.Generic;
using Godot;

public sealed class CraftJob
{
    private readonly Dictionary<BaseResourceType, int> _requiredInputs = new();
    private readonly Dictionary<BaseResourceType, int> _inputsDelivered = new();
    private readonly Dictionary<BaseResourceType, int> _requiredOutputs = new();
    private readonly Dictionary<BaseResourceType, int> _producedOutputs = new();
    private MercenaryController? _reservedWorker;
    private MercenaryController? _reservedDeliveryWorker;
    private MercenaryController? _reservedOutputWorker;

    public int JobId { get; set; }
    public string RecipeId { get; set; } = string.Empty;
    public Vector2I FacilityCell { get; set; }
    public CraftJobState State { get; private set; } = CraftJobState.WaitingForMaterials;
    public IReadOnlyDictionary<BaseResourceType, int> RequiredInputs => _requiredInputs;
    public IReadOnlyDictionary<BaseResourceType, int> InputsDelivered => _inputsDelivered;
    public IReadOnlyDictionary<BaseResourceType, int> RequiredOutputs => _requiredOutputs;
    public IReadOnlyDictionary<BaseResourceType, int> ProducedOutputs => _producedOutputs;
    public float WorkProgress { get; private set; }
    public float RequiredWork { get; set; } = 1.0f;
    public MercenaryController? ReservedWorker => IsValidWorker(_reservedWorker) ? _reservedWorker : null;
    public MercenaryController? ReservedDeliveryWorker => IsValidWorker(_reservedDeliveryWorker) ? _reservedDeliveryWorker : null;
    public MercenaryController? ReservedOutputWorker => IsValidWorker(_reservedOutputWorker) ? _reservedOutputWorker : null;
    public bool IsCompleted => State == CraftJobState.Completed;
    public bool IsCancelled => State == CraftJobState.Cancelled;
    public bool HasAllMaterials => GetFirstMissingInput() == null;
    public bool HasProducedOutputs => _producedOutputs.Count > 0;

    public void SetRequirements(CraftRecipeEntry recipe)
    {
        _requiredInputs.Clear();
        _inputsDelivered.Clear();
        _requiredOutputs.Clear();
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

            foreach (KeyValuePair<BaseResourceType, int> output in recipe.Outputs)
            {
                if (output.Value > 0)
                {
                    _requiredOutputs[output.Key] = output.Value;
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
        if (amount <= 0
            || State != CraftJobState.WaitingForMaterials
            || IsCompleted
            || IsCancelled
            || !_requiredInputs.ContainsKey(type))
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
            TryFinalizeOutputs();
        }
    }

    public bool TryFinalizeOutputs()
    {
        if (IsCompleted || IsCancelled || State != CraftJobState.OutputReady || _producedOutputs.Count > 0)
        {
            return false;
        }

        foreach (KeyValuePair<BaseResourceType, int> output in _requiredOutputs)
        {
            if (output.Value > 0)
            {
                _producedOutputs[output.Key] = output.Value;
            }
        }

        return _producedOutputs.Count > 0;
    }

    public int TakeProducedOutput(BaseResourceType type, int amount)
    {
        if (amount <= 0 || IsCompleted || IsCancelled || State != CraftJobState.OutputReady)
        {
            return 0;
        }

        if (!_producedOutputs.TryGetValue(type, out int availableAmount) || availableAmount <= 0)
        {
            return 0;
        }

        int takenAmount = Mathf.Min(amount, availableAmount);
        int remainingAmount = availableAmount - takenAmount;

        if (remainingAmount > 0)
        {
            _producedOutputs[type] = remainingAmount;
        }
        else
        {
            _producedOutputs.Remove(type);
        }

        TryMarkCompletedIfOutputsEmpty();
        return takenAmount;
    }

    public Dictionary<BaseResourceType, int> TakeAllProducedOutputs()
    {
        Dictionary<BaseResourceType, int> outputs = new();

        if (IsCompleted || IsCancelled || State != CraftJobState.OutputReady)
        {
            return outputs;
        }

        foreach (KeyValuePair<BaseResourceType, int> output in _producedOutputs)
        {
            if (output.Value > 0)
            {
                outputs[output.Key] = output.Value;
            }
        }

        _producedOutputs.Clear();
        TryMarkCompletedIfOutputsEmpty();
        return outputs;
    }

    public bool TryMarkCompletedIfOutputsEmpty()
    {
        if (IsCompleted || IsCancelled || State != CraftJobState.OutputReady || _producedOutputs.Count > 0)
        {
            return false;
        }

        Complete();
        return true;
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

    public bool TryReserveOutputPickup(MercenaryController worker)
    {
        PruneReservations();

        if (IsCompleted || IsCancelled || State != CraftJobState.OutputReady || !HasProducedOutputs)
        {
            return false;
        }

        if (_reservedOutputWorker != null && _reservedOutputWorker != worker)
        {
            return false;
        }

        _reservedOutputWorker = worker;
        return true;
    }

    public void ReleaseOutputPickup(MercenaryController worker)
    {
        if (_reservedOutputWorker == worker || !IsValidWorker(_reservedOutputWorker))
        {
            _reservedOutputWorker = null;
        }
    }

    public bool TryReserveCrafting(MercenaryController worker)
    {
        PruneReservations();

        if (IsCompleted || IsCancelled || State == CraftJobState.OutputReady || !HasAllMaterials)
        {
            return false;
        }

        if (_reservedWorker != null && _reservedWorker != worker)
        {
            return false;
        }

        _reservedWorker = worker;
        return true;
    }

    public void ReleaseCrafting(MercenaryController worker)
    {
        if (_reservedWorker == worker || !IsValidWorker(_reservedWorker))
        {
            _reservedWorker = null;
        }

        if (_reservedWorker == null && State == CraftJobState.Crafting && WorkProgress < RequiredWork)
        {
            State = CraftJobState.ReadyToCraft;
        }

        RefreshState();
    }

    public void Cancel()
    {
        State = CraftJobState.Cancelled;
        _reservedWorker = null;
        _reservedDeliveryWorker = null;
        _reservedOutputWorker = null;
    }

    public void Complete()
    {
        State = CraftJobState.Completed;
        WorkProgress = RequiredWork;
        _reservedWorker = null;
        _reservedDeliveryWorker = null;
        _reservedOutputWorker = null;
    }

    public float GetProgressRatio()
    {
        return RequiredWork <= 0.0f ? 1.0f : Mathf.Clamp(WorkProgress / RequiredWork, 0.0f, 1.0f);
    }

    public void PruneReservations()
    {
        bool clearedCraftWorker = false;

        if (!IsValidWorker(_reservedWorker))
        {
            _reservedWorker = null;
            clearedCraftWorker = true;
        }

        if (!IsValidWorker(_reservedDeliveryWorker))
        {
            _reservedDeliveryWorker = null;
        }

        if (!IsValidWorker(_reservedOutputWorker))
        {
            _reservedOutputWorker = null;
        }

        if (clearedCraftWorker && State == CraftJobState.Crafting && WorkProgress < RequiredWork)
        {
            State = CraftJobState.ReadyToCraft;
        }
    }

    public BaseResourceType? GetFirstMissingInput()
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
