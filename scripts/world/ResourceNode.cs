using Godot;

public partial class ResourceNode : Node2D
{
	[Export]
	public BaseResourceType ResourceType { get; set; } = BaseResourceType.Wood;

	[Export]
	public int Amount { get; set; } = 50;

	[Export]
	public int MaxAmount { get; set; } = 50;

	[Export]
	public Vector2I Cell { get; set; }

	[Export]
	public bool AutoRemoveOnDepletion { get; set; } = true;

	[Export]
	public bool DebugAutoRemove { get; set; } = false;

	public bool IsDepleted => Amount <= 0;
	public bool CanBeRemoved => IsDepleted;
	public bool IsRemoving => _isRemoving;
	public bool IsHarvestDesignated => _isHarvestDesignated;
	public bool CanBeHarvestDesignated => !IsDepleted && !IsRemoving && Amount > 0;
	public bool IsReserved
	{
		get
		{
			PruneInvalidReservation();
			return _reservedBy != null;
		}
	}

	private Label? _label;
	private MercenaryLifeAI? _reservedBy;
	private bool _isRemoving;
	private bool _isHarvestDesignated;

	public override void _Ready()
	{
		Visible = true;
		ZIndex = 5;
		AddToGroup("resource_nodes");

		_label = new Label
		{
			Name = "ResourceLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			Position = new Vector2(-52.0f, -56.0f),
			Size = new Vector2(104.0f, 24.0f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};

		_label.AddThemeFontSizeOverride("font_size", 14);
		AddChild(_label);
		UpdateLabel();
		QueueRedraw();
	}

	public override void _Draw()
	{
		Color bodyColor = GetBodyColor();
		Color outlineColor = IsDepleted
			? new Color(0.08f, 0.08f, 0.08f, 0.55f)
			: new Color(0.08f, 0.08f, 0.08f, 0.9f);

		DrawSetTransform(Vector2.Zero, 0.0f, new Vector2(1.55f, 1.55f));

		if (ResourceType == BaseResourceType.Wood)
		{
			DrawRect(new Rect2(-4.0f, -3.0f, 8.0f, 20.0f), IsDepleted ? new Color(0.18f, 0.12f, 0.08f, 0.45f) : new Color(0.38f, 0.22f, 0.1f));
			DrawCircle(new Vector2(0.0f, -8.0f), 17.0f, bodyColor);
			DrawArc(new Vector2(0.0f, -8.0f), 17.0f, 0.0f, Mathf.Tau, 32, outlineColor, 2.0f);
		}
		else if (ResourceType == BaseResourceType.Stone)
		{
			Vector2[] points =
			{
				new Vector2(-18.0f, 8.0f),
				new Vector2(-12.0f, -10.0f),
				new Vector2(4.0f, -17.0f),
				new Vector2(18.0f, -4.0f),
				new Vector2(14.0f, 13.0f),
				new Vector2(-4.0f, 17.0f)
			};

			DrawColoredPolygon(points, bodyColor);
			DrawPolyline(points, outlineColor, 2.0f, true);
		}
		else if (ResourceType == BaseResourceType.Metal)
		{
			Vector2[] points =
			{
				new Vector2(0.0f, -20.0f),
				new Vector2(17.0f, -6.0f),
				new Vector2(11.0f, 16.0f),
				new Vector2(-11.0f, 16.0f),
				new Vector2(-17.0f, -6.0f)
			};

			DrawColoredPolygon(points, bodyColor);
			DrawPolyline(points, outlineColor, 2.0f, true);
			DrawLine(new Vector2(-7.0f, -8.0f), new Vector2(8.0f, 9.0f), new Color(0.76f, 0.86f, 0.92f, IsDepleted ? 0.35f : 0.95f), 2.0f);
		}

		if (IsDepleted)
		{
			DrawLine(new Vector2(-18.0f, -18.0f), new Vector2(18.0f, 18.0f), new Color(0.95f, 0.12f, 0.08f, 0.85f), 3.0f);
			DrawLine(new Vector2(18.0f, -18.0f), new Vector2(-18.0f, 18.0f), new Color(0.95f, 0.12f, 0.08f, 0.85f), 3.0f);
		}

		if (IsReserved)
		{
			DrawArc(Vector2.Zero, 24.0f, 0.0f, Mathf.Tau, 40, new Color(1.0f, 0.84f, 0.25f, 0.95f), 2.0f);
		}

		if (IsHarvestDesignated)
		{
			DrawArc(Vector2.Zero, 30.0f, 0.0f, Mathf.Tau, 48, new Color(0.18f, 0.78f, 1.0f, 0.95f), 3.0f);
		}

		DrawSetTransform(Vector2.Zero, 0.0f, Vector2.One);
	}

	public void Initialize(BaseResourceType resourceType, Vector2I cell, int amount)
	{
		ForceReleaseReservation();
		ResourceType = resourceType;
		Cell = cell;
		MaxAmount = Mathf.Max(1, amount);
		Amount = Mathf.Clamp(amount, 0, MaxAmount);
		UpdateLabel();
		QueueRedraw();
	}

	public string GetDisplayName()
	{
		return $"{ResourceType} Node [{Amount}/{MaxAmount}]";
	}

	public int Harvest(int requestedAmount)
	{
		if (requestedAmount <= 0 || IsDepleted)
		{
			return 0;
		}

		int harvestedAmount = Mathf.Min(requestedAmount, Amount);
		Amount = Mathf.Max(0, Amount - harvestedAmount);

		if (IsDepleted)
		{
			ClearHarvestDesignation();

			if (AutoRemoveOnDepletion)
			{
				if (DebugAutoRemove)
				{
					GD.Print($"ResourceNode depleted, auto removing: {ResourceType} cell={Cell}");
				}

				TryRemoveDepleted();
				return harvestedAmount;
			}

			ForceReleaseReservation();
		}

		UpdateLabel();
		QueueRedraw();
		return harvestedAmount;
	}

	public bool TryRemoveDepleted()
	{
		if (_isRemoving)
		{
			return true;
		}

		if (!CanBeRemoved)
		{
			return false;
		}

		_isRemoving = true;
		ClearHarvestDesignation();
		ForceReleaseReservation();
		RemoveFromGroup("resource_nodes");

		if (DebugAutoRemove)
		{
			GD.Print($"ResourceNode auto removed: {ResourceType} cell={Cell}");
		}

		QueueFree();
		return true;
	}

	public bool TrySetHarvestDesignated(bool designated)
	{
		if (!designated)
		{
			ClearHarvestDesignation();
			return true;
		}

		if (!CanBeHarvestDesignated)
		{
			return false;
		}

		if (_isHarvestDesignated)
		{
			return true;
		}

		_isHarvestDesignated = true;
		UpdateLabel();
		QueueRedraw();
		return true;
	}

	public void ClearHarvestDesignation()
	{
		if (!_isHarvestDesignated)
		{
			return;
		}

		_isHarvestDesignated = false;
		UpdateLabel();
		QueueRedraw();
	}

	public bool IsReservedBy(MercenaryLifeAI lifeAI)
	{
		PruneInvalidReservation();
		return _reservedBy == lifeAI;
	}

	public bool TryReserve(MercenaryLifeAI lifeAI)
	{
		PruneInvalidReservation();

		if (lifeAI == null || _isRemoving || IsDepleted || Amount <= 0)
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

	public void ReleaseReservation(MercenaryLifeAI lifeAI)
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

	public void ForceReleaseReservation()
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

		string marker = ResourceType switch
		{
			BaseResourceType.Wood => "W",
			BaseResourceType.Stone => "S",
			BaseResourceType.Metal => "M",
			_ => "?"
		};

		string designationMarker = IsHarvestDesignated ? " D" : "";
		string reservationMarker = IsReserved ? " R" : "";
		_label.Text = $"{marker} {Amount}{designationMarker}{reservationMarker}";
	}

	private void PruneInvalidReservation()
	{
		if (_reservedBy != null && !GodotObject.IsInstanceValid(_reservedBy))
		{
			_reservedBy = null;
		}
	}

	private Color GetBodyColor()
	{
		float alpha = IsDepleted ? 0.35f : 0.92f;

		return ResourceType switch
		{
			BaseResourceType.Wood => new Color(0.18f, 0.62f, 0.24f, alpha),
			BaseResourceType.Stone => new Color(0.52f, 0.54f, 0.56f, alpha),
			BaseResourceType.Metal => new Color(0.34f, 0.42f, 0.48f, alpha),
			_ => new Color(0.8f, 0.8f, 0.8f, alpha)
		};
	}

	// TODO: Revisit respawn, drops, hauling, tool quality, skills, or manual clear commands if the resource loop needs them.
}
