using Godot;

public partial class ResourcePile : Node2D
{
    public BaseResourceType ResourceType { get; private set; } = BaseResourceType.Wood;
    public int Amount { get; private set; }
    public Vector2I Cell { get; private set; }
    public bool IsEmpty => Amount <= 0;
    public bool IsReservedForHaul => _reservedBy != null;
    public bool IsRemoving => _isRemoving;

    private Label? _label;
    private MercenaryLifeAI? _reservedBy;
    private bool _isRemoving;

    public override void _Ready()
    {
        Visible = true;
        ZIndex = 4;
        AddToGroup("resource_piles");

        _label = new Label
        {
            Name = "PileLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-42.0f, -34.0f),
            Size = new Vector2(84.0f, 22.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _label.AddThemeFontSizeOverride("font_size", 13);
        AddChild(_label);
        UpdateLabel();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color bodyColor = ResourceType switch
        {
            BaseResourceType.Food => new Color(0.78f, 0.66f, 0.28f, 0.92f),
            BaseResourceType.Wood => new Color(0.48f, 0.32f, 0.14f, 0.9f),
            BaseResourceType.Stone => new Color(0.52f, 0.54f, 0.56f, 0.9f),
            BaseResourceType.Metal => new Color(0.34f, 0.46f, 0.54f, 0.9f),
            BaseResourceType.IronOre => new Color(0.45f, 0.39f, 0.34f, 0.92f),
            BaseResourceType.Coal => new Color(0.12f, 0.12f, 0.12f, 0.92f),
            BaseResourceType.Herb => new Color(0.32f, 0.72f, 0.36f, 0.92f),
            BaseResourceType.Plank => new Color(0.62f, 0.42f, 0.20f, 0.9f),
            BaseResourceType.Brick => new Color(0.62f, 0.24f, 0.16f, 0.9f),
            BaseResourceType.IronIngot => new Color(0.42f, 0.46f, 0.50f, 0.92f),
            BaseResourceType.SimpleMeal => new Color(0.86f, 0.56f, 0.24f, 0.92f),
            BaseResourceType.Medicine => new Color(0.72f, 0.92f, 0.82f, 0.92f),
            _ => new Color(0.8f, 0.8f, 0.8f, 0.9f)
        };

        DrawCircle(Vector2.Zero, 12.0f, bodyColor);
        DrawCircle(new Vector2(-8.0f, 5.0f), 8.0f, bodyColor.Darkened(0.12f));
        DrawCircle(new Vector2(8.0f, 5.0f), 8.0f, bodyColor.Lightened(0.08f));
        DrawArc(Vector2.Zero, 15.0f, 0.0f, Mathf.Tau, 32, new Color(0.06f, 0.06f, 0.06f, 0.82f), 2.0f);

        if (IsReservedForHaul)
        {
            DrawArc(Vector2.Zero, 20.0f, 0.0f, Mathf.Tau, 36, new Color(1.0f, 0.84f, 0.25f, 0.95f), 2.0f);
        }
    }

    public void Initialize(BaseResourceType type, Vector2I cell, int amount)
    {
        ResourceType = type;
        Cell = cell;
        Amount = Mathf.Max(0, amount);
        UpdateLabel();
        QueueRedraw();
    }

    public int TakeAmount(int requestedAmount)
    {
        if (requestedAmount <= 0 || IsEmpty || _isRemoving)
        {
            return 0;
        }

        int takenAmount = Mathf.Min(requestedAmount, Amount);
        Amount = Mathf.Max(0, Amount - takenAmount);

        if (IsEmpty)
        {
            TryRemoveIfEmpty();
        }
        else
        {
            UpdateLabel();
            QueueRedraw();
        }

        return takenAmount;
    }

    public bool ValidateLogisticsState(out string warning)
    {
        warning = "";

        if (!System.Enum.IsDefined(typeof(BaseResourceType), ResourceType))
        {
            warning = $"Resource pile has invalid type at {Cell}";
            return false;
        }

        if (Amount < 0)
        {
            Amount = 0;
            TryRemoveIfEmpty();
            warning = $"Resource pile negative amount clamped at {Cell}";
            return false;
        }

        if (Amount == 0 && !_isRemoving)
        {
            TryRemoveIfEmpty();
            warning = $"Empty resource pile removed at {Cell}";
            return false;
        }

        PruneInvalidReservation();
        return true;
    }

    public void AddAmount(int amount)
    {
        if (amount <= 0 || _isRemoving)
        {
            return;
        }

        Amount += amount;
        UpdateLabel();
        QueueRedraw();
    }

    public string GetDisplayName()
    {
        return $"{BaseBuildManager.GetResourceDisplayName(ResourceType)} Pile [{Amount}]";
    }

    public bool TryRemoveIfEmpty()
    {
        if (_isRemoving)
        {
            return true;
        }

        if (!IsEmpty)
        {
            return false;
        }

        _isRemoving = true;
        ForceReleaseHaulReservation();
        RemoveFromGroup("resource_piles");
        QueueFree();
        return true;
    }

    public bool IsReservedBy(MercenaryLifeAI lifeAI)
    {
        PruneInvalidReservation();
        return _reservedBy == lifeAI;
    }

    public bool TryReserveHaul(MercenaryLifeAI lifeAI)
    {
        PruneInvalidReservation();

        if (lifeAI == null || _isRemoving || IsEmpty)
        {
            return false;
        }

        if (_reservedBy == lifeAI)
        {
            return true;
        }

        if (_reservedBy != null)
        {
            return false;
        }

        _reservedBy = lifeAI;
        UpdateLabel();
        QueueRedraw();
        return true;
    }

    public void ReleaseHaulReservation(MercenaryLifeAI lifeAI)
    {
        PruneInvalidReservation();

        if (_reservedBy != lifeAI)
        {
            return;
        }

        _reservedBy = null;
        UpdateLabel();
        QueueRedraw();
    }

    public void ForceReleaseHaulReservation()
    {
        if (_reservedBy == null)
        {
            return;
        }

        _reservedBy = null;
        UpdateLabel();
        QueueRedraw();
    }

    private void UpdateLabel()
    {
        if (_label == null)
        {
            return;
        }

        string marker = BaseBuildManager.GetResourceMarker(ResourceType);

        string reservationMarker = IsReservedForHaul ? " H" : "";
        _label.Text = $"{marker} {Amount}{reservationMarker}";
    }

    private void PruneInvalidReservation()
    {
        if (_reservedBy != null && !GodotObject.IsInstanceValid(_reservedBy))
        {
            _reservedBy = null;
        }
    }
}
