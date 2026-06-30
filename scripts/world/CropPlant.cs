using Godot;

public partial class CropPlant : Node2D
{
    [Export]
    public float GrowSeconds { get; set; } = 60.0f;

    [Export]
    public int HarvestFoodAmount { get; set; } = 8;

    public Vector2I Cell { get; private set; }
    public float Growth { get; private set; }
    public bool IsMature => Growth >= 1.0f;
    public bool IsReservedForHarvest => _reservedByHarvest != null;
    public bool IsRemoving => _isRemoving;

    private Label? _label;
    private MercenaryLifeAI? _reservedByHarvest;
    private bool _isRemoving;

    public override void _Ready()
    {
        Visible = true;
        ZIndex = 3;
        AddToGroup("crop_plants");

        _label = new Label
        {
            Name = "CropLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-42.0f, -34.0f),
            Size = new Vector2(84.0f, 22.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _label.AddThemeFontSizeOverride("font_size", 12);
        AddChild(_label);
        UpdateLabel();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_isRemoving || IsMature)
        {
            return;
        }

        float growSeconds = Mathf.Max(1.0f, GrowSeconds);
        Growth = Mathf.Clamp(Growth + (float)delta / growSeconds, 0.0f, 1.0f);
        UpdateLabel();
        QueueRedraw();
    }

    public override void _Draw()
    {
        Color stemColor = IsMature
            ? new Color(0.95f, 0.78f, 0.24f, 0.96f)
            : new Color(0.22f, 0.74f, 0.32f, 0.88f);
        float size = Mathf.Lerp(5.0f, 14.0f, Growth);

        DrawCircle(Vector2.Zero, size, stemColor);
        DrawArc(Vector2.Zero, size + 4.0f, 0.0f, Mathf.Tau, 32, new Color(0.04f, 0.18f, 0.06f, 0.78f), 2.0f);
        DrawLine(new Vector2(-size, size * 0.35f), new Vector2(size, -size * 0.35f), stemColor.Lightened(0.18f), 2.0f);
        DrawLine(new Vector2(-size, -size * 0.35f), new Vector2(size, size * 0.35f), stemColor.Lightened(0.18f), 2.0f);

        if (IsMature)
        {
            DrawArc(Vector2.Zero, size + 8.0f, 0.0f, Mathf.Tau, 36, new Color(1.0f, 0.92f, 0.25f, 0.95f), 2.0f);
        }

        if (IsReservedForHarvest)
        {
            DrawArc(Vector2.Zero, size + 12.0f, 0.0f, Mathf.Tau, 36, new Color(1.0f, 0.84f, 0.25f, 0.95f), 2.0f);
        }
    }

    public void Initialize(Vector2I cell)
    {
        Cell = cell;
        Growth = 0.0f;
        UpdateLabel();
        QueueRedraw();
    }

    public bool TryReserveHarvest(MercenaryLifeAI lifeAI)
    {
        PruneInvalidReservation();

        if (lifeAI == null || _isRemoving || !IsMature)
        {
            return false;
        }

        if (_reservedByHarvest == lifeAI)
        {
            return true;
        }

        if (_reservedByHarvest != null)
        {
            return false;
        }

        _reservedByHarvest = lifeAI;
        UpdateLabel();
        QueueRedraw();
        return true;
    }

    public bool IsReservedBy(MercenaryLifeAI lifeAI)
    {
        PruneInvalidReservation();
        return _reservedByHarvest == lifeAI;
    }

    public void ReleaseHarvestReservation(MercenaryLifeAI lifeAI)
    {
        PruneInvalidReservation();

        if (_reservedByHarvest != lifeAI)
        {
            return;
        }

        _reservedByHarvest = null;
        UpdateLabel();
        QueueRedraw();
    }

    public void ForceReleaseHarvestReservation()
    {
        if (_reservedByHarvest == null)
        {
            return;
        }

        _reservedByHarvest = null;
        UpdateLabel();
        QueueRedraw();
    }

    public int Harvest()
    {
        if (_isRemoving || !IsMature)
        {
            return 0;
        }

        int foodAmount = Mathf.Max(0, HarvestFoodAmount);
        TryRemoveAfterHarvest();
        return foodAmount;
    }

    public bool TryRemoveAfterHarvest()
    {
        if (_isRemoving)
        {
            return true;
        }

        _isRemoving = true;
        ForceReleaseHarvestReservation();
        RemoveFromGroup("crop_plants");
        QueueFree();
        return true;
    }

    public string GetDisplayName()
    {
        return IsMature ? "Crop Ready" : $"Crop {Mathf.RoundToInt(Growth * 100.0f)}%";
    }

    private void UpdateLabel()
    {
        if (_label == null)
        {
            return;
        }

        string reservationMarker = IsReservedForHarvest ? " R" : "";
        _label.Text = IsMature ? $"Ready{reservationMarker}" : $"{Mathf.RoundToInt(Growth * 100.0f)}%";
    }

    private void PruneInvalidReservation()
    {
        if (_reservedByHarvest != null && !GodotObject.IsInstanceValid(_reservedByHarvest))
        {
            _reservedByHarvest = null;
        }
    }
}
