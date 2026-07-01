using System.Collections.Generic;
using Godot;

public sealed class MercenaryInventory
{
    public List<MercenaryInventoryStack> Stacks { get; } = new();

    public float MaxWeight { get; set; } = 25.0f;
    public float UsedWeight => GetUsedWeight();
    public float FreeWeight => GetFreeWeight();

    public int GetAmount(BaseResourceType type)
    {
        MercenaryInventoryStack? stack = FindStack(type);
        return stack?.Amount ?? 0;
    }

    public float GetUsedWeight()
    {
        float usedWeight = 0.0f;

        foreach (MercenaryInventoryStack stack in Stacks)
        {
            if (stack.Amount <= 0)
            {
                continue;
            }

            usedWeight += stack.Amount * BaseBuildManager.GetResourceUnitWeight(stack.ResourceType);
        }

        return usedWeight;
    }

    public float GetFreeWeight()
    {
        return Mathf.Max(0.0f, MaxWeight - GetUsedWeight());
    }

    public bool CanAdd(BaseResourceType type, int amount)
    {
        return amount > 0 && GetMaxAddableAmount(type) >= amount;
    }

    public int GetMaxAddableAmount(BaseResourceType type)
    {
        int unitWeight = Mathf.Max(1, BaseBuildManager.GetResourceUnitWeight(type));
        return Mathf.Max(0, Mathf.FloorToInt(GetFreeWeight() / unitWeight));
    }

    public bool TryAdd(BaseResourceType type, int amount, out int addedAmount)
    {
        addedAmount = 0;

        if (amount <= 0)
        {
            return false;
        }

        int maxAddable = GetMaxAddableAmount(type);
        addedAmount = Mathf.Min(amount, maxAddable);

        if (addedAmount <= 0)
        {
            return false;
        }

        MercenaryInventoryStack? stack = FindStack(type);

        if (stack == null)
        {
            Stacks.Add(new MercenaryInventoryStack(type, addedAmount));
        }
        else
        {
            stack.Amount += addedAmount;
        }

        return true;
    }

    public bool TryRemove(BaseResourceType type, int amount, out int removedAmount)
    {
        removedAmount = 0;

        if (amount <= 0)
        {
            return false;
        }

        MercenaryInventoryStack? stack = FindStack(type);

        if (stack == null || stack.Amount <= 0)
        {
            return false;
        }

        removedAmount = Mathf.Min(amount, stack.Amount);
        stack.Amount -= removedAmount;

        if (stack.Amount <= 0)
        {
            Stacks.Remove(stack);
        }

        return removedAmount > 0;
    }

    public void Clear()
    {
        Stacks.Clear();
    }

    public bool IsEmpty()
    {
        foreach (MercenaryInventoryStack stack in Stacks)
        {
            if (stack.Amount > 0)
            {
                return false;
            }
        }

        return true;
    }

    public bool Validate(out string warning)
    {
        warning = "";
        List<string> warnings = new();

        for (int i = Stacks.Count - 1; i >= 0; i--)
        {
            MercenaryInventoryStack stack = Stacks[i];

            if (stack.Amount < 0)
            {
                warnings.Add("Inventory negative stack");
                stack.Amount = 0;
            }

            if (stack.Amount == 0)
            {
                Stacks.RemoveAt(i);
            }
        }

        if (GetUsedWeight() > MaxWeight + 0.01f)
        {
            warnings.Add("Inventory overweight");
        }

        warning = string.Join(" / ", warnings);
        return warnings.Count == 0;
    }

    public string GetContentsSummary()
    {
        List<string> parts = new();

        foreach (MercenaryInventoryStack stack in Stacks)
        {
            if (stack.Amount <= 0)
            {
                continue;
            }

            parts.Add($"{BaseBuildManager.GetResourceDisplayName(stack.ResourceType)} x{stack.Amount}");
        }

        return parts.Count == 0 ? "\uBE44\uC5B4 \uC788\uC74C" : string.Join(", ", parts);
    }

    private MercenaryInventoryStack? FindStack(BaseResourceType type)
    {
        foreach (MercenaryInventoryStack stack in Stacks)
        {
            if (stack.ResourceType == type)
            {
                return stack;
            }
        }

        return null;
    }
}
