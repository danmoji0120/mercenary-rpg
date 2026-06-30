using System.Collections.Generic;
using Godot;

public enum MercenaryControlMode
{
    Life,
    Rallying,
    Squad,
    Downed,
    Dead
}

public enum MercenaryOrderState
{
    None,
    LifeMoving,
    LifeWaiting,
    RallyMoving,
    SquadIdle,
    SquadMoving,
    SquadHolding,
    SquadDefending
}

public partial class MercenaryController : Node2D
{
    private const float PathFeedbackDurationSeconds = 2.0f;

    [Export]
    public float MoveSpeed { get; set; } = 180.0f;

    [Export]
    public float SelectionRadius { get; set; } = 18.0f;

    [Export]
    public float DefenseDetectRadius { get; set; } = 160.0f;

    [Export]
    public int DefenseAttackDamage { get; set; } = 1;

    [Export]
    public double DefenseAttackInterval { get; set; } = 1.0;

    [Export]
    public bool DebugPathRevalidation { get; set; } = false;

    public string MercenaryName { get; private set; } = "Test Mercenary";
    public MercenaryControlMode ControlMode { get; private set; } = MercenaryControlMode.Life;
    public MercenaryOrderState OrderState { get; private set; } = MercenaryOrderState.None;
    public Area2D? SelectionArea { get; private set; }
    public string PathFeedbackMessage => _pathFeedbackTimer > 0.0f ? _pathFeedbackMessage : "";
    public bool HasPathFeedback => _pathFeedbackTimer > 0.0f;

    private bool _isSelected;
    private bool _hasMoveTarget;
    private Vector2 _moveTarget;
    private readonly Queue<Vector2> _pathTargets = new();
    private Vector2I? _pathGoalCell;
    private Marker2D? _rallyPoint;
    private bool _hasRallyTarget;
    private Vector2 _rallyTarget;
    private string? _defenseTargetName;
    private EnemyDummyController? _defenseTarget;
    private double _defenseAttackCooldown;
    private string _pathFeedbackMessage = "";
    private float _pathFeedbackTimer;
    private Label? _nameLabel;
    private MercenaryLifeAI? _lifeAI;
    private BaseBuildManager? _baseBuildManager;

    public override void _Ready()
    {
        _baseBuildManager = GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");

        _lifeAI = new MercenaryLifeAI
        {
            Name = "LifeAI"
        };
        AddChild(_lifeAI);

        SelectionArea = new Area2D
        {
            Name = "SelectionArea",
            CollisionLayer = 1,
            CollisionMask = 0,
            Monitoring = false,
            Monitorable = true
        };

        CollisionShape2D selectionShape = new CollisionShape2D
        {
            Name = "SelectionShape",
            Shape = new CircleShape2D
            {
                Radius = SelectionRadius
            }
        };

        SelectionArea.AddChild(selectionShape);
        AddChild(SelectionArea);

        _nameLabel = new Label
        {
            Text = GetDisplayName(),
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-80.0f, -44.0f),
            Size = new Vector2(160.0f, 24.0f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        AddChild(_nameLabel);
        UpdateNameLabel();
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        UpdatePathFeedbackTimer(delta);

        if (ControlMode == MercenaryControlMode.Life)
        {
            SetOrderState(_lifeAI?.UpdateLifeAI(this, delta) ?? MercenaryOrderState.None);
            UpdateNameLabel();
            return;
        }

        if (ControlMode == MercenaryControlMode.Rallying)
        {
            UpdateRallying(delta);
            return;
        }

        if (!_hasMoveTarget)
        {
            if (ControlMode != MercenaryControlMode.Squad)
            {
                SetOrderState(MercenaryOrderState.None);
            }
            else if (OrderState != MercenaryOrderState.SquadHolding && OrderState != MercenaryOrderState.SquadDefending)
            {
                SetOrderState(MercenaryOrderState.SquadIdle);
            }

            UpdateDefenseTarget();
            UpdateDefenseAttack(delta);
            UpdateNameLabel();
            return;
        }

        UpdatePathMovement(delta, MercenaryOrderState.SquadIdle, MercenaryOrderState.SquadMoving);
    }

    public override void _Draw()
    {
        Color bodyColor = _isSelected
            ? new Color(0.95f, 0.82f, 0.22f)
            : new Color(0.36f, 0.58f, 0.86f);

        Color outlineColor = _isSelected
            ? new Color(1.0f, 0.95f, 0.48f)
            : new Color(0.1f, 0.16f, 0.25f);

        if (_isSelected)
        {
            DrawCircle(Vector2.Zero, SelectionRadius + 7.0f, new Color(1.0f, 0.88f, 0.22f, 0.24f));
            DrawArc(Vector2.Zero, SelectionRadius + 7.0f, 0.0f, Mathf.Tau, 48, outlineColor, 2.5f);
        }

        DrawCircle(Vector2.Zero, SelectionRadius, bodyColor);
        DrawArc(Vector2.Zero, SelectionRadius, 0.0f, Mathf.Tau, 40, outlineColor, 2.0f);
        DrawRect(new Rect2(-8.0f, -8.0f, 16.0f, 16.0f), new Color(0.18f, 0.25f, 0.35f));
    }

    public void Initialize(string mercenaryName, Vector2 spawnPosition, float moveSpeed)
    {
        MercenaryName = mercenaryName;
        GlobalPosition = spawnPosition;
        MoveSpeed = moveSpeed;
        ControlMode = MercenaryControlMode.Life;
        OrderState = MercenaryOrderState.None;

        UpdateNameLabel();
    }

    public void SetLifePoints(Node2D lifePointLayer)
    {
        _lifeAI?.SetLifePoints(GetLifePointNodes(lifePointLayer));
        UpdateNameLabel();
    }

    public void SetRallyPoint(Marker2D rallyPoint)
    {
        _rallyPoint = rallyPoint;
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected)
        {
            return;
        }

        _isSelected = selected;
        QueueRedraw();
    }

    public void MoveTo(Vector2 worldPosition)
    {
        if (ControlMode != MercenaryControlMode.Squad)
        {
            return;
        }

        _defenseTargetName = null;
        _defenseTarget = null;
        SetPathTargets(new[] { SnapWorldToGridCenter(worldPosition) });
        _pathGoalCell = _baseBuildManager?.WorldToCell(worldPosition);
        SetOrderState(MercenaryOrderState.SquadMoving);
    }

    public void MoveAlongWorldPath(IReadOnlyList<Vector2> worldTargets)
    {
        if (ControlMode != MercenaryControlMode.Squad || worldTargets.Count == 0)
        {
            return;
        }

        _defenseTargetName = null;
        _defenseTarget = null;
        SetPathTargets(worldTargets);
        _pathGoalCell = _baseBuildManager != null && worldTargets.Count > 0
            ? _baseBuildManager.WorldToCell(worldTargets[^1])
            : null;
        SetOrderState(MercenaryOrderState.SquadMoving);
    }

    public bool TryMoveToWorldWithPath(Vector2 goalWorld, BaseBuildManager buildManager)
    {
        return TryMoveToCell(buildManager.WorldToCell(goalWorld), buildManager);
    }

    public bool TryMoveToCell(Vector2I goalCell, BaseBuildManager buildManager)
    {
        if (!buildManager.IsCellInWorld(goalCell) || buildManager.IsCellBlocked(goalCell))
        {
            return false;
        }

        Vector2I startCell = buildManager.WorldToCell(GlobalPosition);

        if (startCell == goalCell)
        {
            ClearPath();
            _pathGoalCell = null;
            GlobalPosition = buildManager.CellToWorldCenter(goalCell);
            return true;
        }

        List<Vector2I> pathCells = GridPathfinder.FindPath(startCell, goalCell, buildManager);

        if (pathCells.Count == 0)
        {
            return false;
        }

        List<Vector2> worldPath = new();

        foreach (Vector2I pathCell in pathCells)
        {
            worldPath.Add(buildManager.CellToWorldCenter(pathCell));
        }

        SetPathTargets(worldPath);
        _pathGoalCell = goalCell;
        return true;
    }

    public bool HasActivePath()
    {
        return _hasMoveTarget;
    }

    public bool UpdateLifePathMovement(double delta)
    {
        return UpdatePathMovement(delta, MercenaryOrderState.LifeWaiting, MercenaryOrderState.LifeMoving);
    }

    public void RevalidateCurrentPath()
    {
        if (_baseBuildManager == null || !_hasMoveTarget)
        {
            return;
        }

        EnsurePathIsTraversable(_baseBuildManager);
    }

    public BaseBuildManager? GetBaseBuildManager()
    {
        return _baseBuildManager;
    }

    public bool TryGetLifeAI(out MercenaryLifeAI? lifeAI)
    {
        lifeAI = _lifeAI;
        return lifeAI != null;
    }

    public void StopSquadCommand()
    {
        if (ControlMode != MercenaryControlMode.Squad)
        {
            return;
        }

        _hasMoveTarget = false;
        ClearPath();
        _pathGoalCell = null;
        _moveTarget = GlobalPosition;
        _defenseTargetName = null;
        _defenseTarget = null;
        SetOrderState(MercenaryOrderState.SquadHolding);
    }

    public void SetSquadDefending()
    {
        if (ControlMode != MercenaryControlMode.Squad)
        {
            return;
        }

        _hasMoveTarget = false;
        ClearPath();
        _pathGoalCell = null;
        _moveTarget = GlobalPosition;
        SetOrderState(MercenaryOrderState.SquadDefending);
        UpdateDefenseTarget();
    }

    public void SetControlMode(MercenaryControlMode controlMode)
    {
        if (ControlMode == controlMode)
        {
            return;
        }

        ControlMode = controlMode;

        if (ControlMode != MercenaryControlMode.Squad)
        {
            ClearPath();
            _pathGoalCell = null;
        }

        if (ControlMode == MercenaryControlMode.Life)
        {
            _hasRallyTarget = false;
            _lifeAI?.ResumeLifeAI();
            SetOrderState(MercenaryOrderState.LifeMoving);
        }
        else
        {
            _lifeAI?.StopLifeAI(this);
            _baseBuildManager?.ReleaseFacilityUseFor(this);
            SetOrderState(ControlMode == MercenaryControlMode.Squad ? MercenaryOrderState.SquadIdle : MercenaryOrderState.None);
        }

        UpdateNameLabel();
        QueueRedraw();
    }

    public void EnterSquad()
    {
        SetControlMode(MercenaryControlMode.Squad);
    }

    public void ReturnToLife()
    {
        SetControlMode(MercenaryControlMode.Life);
    }

    public void StartRallying()
    {
        if (_rallyPoint == null)
        {
            EnterSquad();
            return;
        }

        SetControlMode(MercenaryControlMode.Rallying);
        if (StartRallyPath(_rallyPoint.GlobalPosition))
        {
            SetOrderState(MercenaryOrderState.RallyMoving);
        }
    }

    public void StartRallying(Vector2 rallyTarget)
    {
        SetControlMode(MercenaryControlMode.Rallying);
        if (StartRallyPath(rallyTarget))
        {
            SetOrderState(MercenaryOrderState.RallyMoving);
        }
    }

    public Vector2 SnapWorldToGridCenter(Vector2 worldPosition)
    {
        return _baseBuildManager?.SnapWorldToCellCenter(worldPosition) ?? worldPosition;
    }

    public void ToggleRallyState(Vector2 rallyTarget)
    {
        if (ControlMode == MercenaryControlMode.Life)
        {
            StartRallying(rallyTarget);
        }
        else if (ControlMode == MercenaryControlMode.Rallying || ControlMode == MercenaryControlMode.Squad)
        {
            ReturnToLife();
        }
    }

    private void UpdateNameLabel()
    {
        if (_nameLabel != null)
        {
            _nameLabel.Text = GetDisplayName();
        }
    }

    private string GetDisplayName()
    {
        string displayName = $"{MercenaryName} [{ControlMode}: {OrderState}]";

        if (OrderState == MercenaryOrderState.SquadDefending && !string.IsNullOrEmpty(_defenseTargetName))
        {
            displayName += $" Target: {_defenseTargetName}";
        }

        return displayName;
    }

    private void UpdateDefenseTarget()
    {
        if (ControlMode != MercenaryControlMode.Squad || OrderState != MercenaryOrderState.SquadDefending)
        {
            _defenseTargetName = null;
            _defenseTarget = null;
            return;
        }

        EnemyDummyController? closestEnemy = null;
        float closestDistance = DefenseDetectRadius;

        foreach (Node node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not EnemyDummyController enemy)
            {
                continue;
            }

            if (enemy.IsDefeated)
            {
                continue;
            }

            float distance = GlobalPosition.DistanceTo(enemy.GlobalPosition);

            if (distance <= closestDistance)
            {
                closestEnemy = enemy;
                closestDistance = distance;
            }
        }

        _defenseTarget = closestEnemy;
        _defenseTargetName = closestEnemy?.EnemyName;
    }

    private bool UpdatePathMovement(double delta, MercenaryOrderState completedState, MercenaryOrderState movingState)
    {
        if (!_hasMoveTarget)
        {
            SetOrderState(completedState);
            return true;
        }

        if (_baseBuildManager != null && !EnsurePathIsTraversable(_baseBuildManager))
        {
            return true;
        }

        Vector2 toTarget = _moveTarget - GlobalPosition;
        float distance = toTarget.Length();
        float step = MoveSpeed * (float)delta;

        if (distance <= step)
        {
            GlobalPosition = _moveTarget;

            if (_pathTargets.Count > 0)
            {
                _moveTarget = _pathTargets.Dequeue();
                _hasMoveTarget = true;
                SetOrderState(movingState);
                return false;
            }

            _hasMoveTarget = false;
            _pathGoalCell = null;
            SetOrderState(completedState);
            return true;
        }

        SetOrderState(movingState);
        GlobalPosition += toTarget.Normalized() * step;
        return false;
    }

    private void SetPathTargets(IEnumerable<Vector2> worldTargets)
    {
        _pathTargets.Clear();
        _hasMoveTarget = false;

        foreach (Vector2 worldTarget in worldTargets)
        {
            Vector2 snappedTarget = SnapWorldToGridCenter(worldTarget);

            if (!_hasMoveTarget)
            {
                _moveTarget = snappedTarget;
                _hasMoveTarget = true;
            }
            else
            {
                _pathTargets.Enqueue(snappedTarget);
            }
        }
    }

    private void ClearPath()
    {
        _hasMoveTarget = false;
        _pathTargets.Clear();
        _pathGoalCell = null;
    }

    private bool EnsurePathIsTraversable(BaseBuildManager buildManager)
    {
        if (!_hasMoveTarget)
        {
            return true;
        }

        if (!IsCurrentPathBlocked(buildManager))
        {
            return true;
        }

        if (DebugPathRevalidation)
        {
            GD.Print($"{MercenaryName} path blocked, revalidating");
        }

        if (TryRepathToCurrentGoal(buildManager))
        {
            ShowPathFeedback("Path updated");

            if (DebugPathRevalidation)
            {
                GD.Print($"{MercenaryName} path revalidated");
            }

            return true;
        }

        ShowPathFeedback(GetPathRevalidationFailureMessage());

        if (DebugPathRevalidation)
        {
            GD.Print($"{MercenaryName} path revalidation failed");
        }

        HandlePathRevalidationFailed();
        return false;
    }

    private bool IsCurrentPathBlocked(BaseBuildManager buildManager)
    {
        if (buildManager.IsCellBlocked(buildManager.WorldToCell(_moveTarget)))
        {
            return true;
        }

        foreach (Vector2 pathTarget in _pathTargets)
        {
            if (buildManager.IsCellBlocked(buildManager.WorldToCell(pathTarget)))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryRepathToCurrentGoal(BaseBuildManager buildManager)
    {
        Vector2I goalCell = _pathGoalCell ?? buildManager.WorldToCell(_moveTarget);

        if (!buildManager.IsCellInWorld(goalCell) || buildManager.IsCellBlocked(goalCell))
        {
            return false;
        }

        Vector2I startCell = buildManager.WorldToCell(GlobalPosition);

        if (startCell == goalCell)
        {
            GlobalPosition = buildManager.CellToWorldCenter(goalCell);
            ClearPath();
            _pathGoalCell = null;
            return true;
        }

        List<Vector2I> pathCells = GridPathfinder.FindPath(startCell, goalCell, buildManager);

        if (pathCells.Count == 0)
        {
            return false;
        }

        List<Vector2> worldPath = new();

        foreach (Vector2I pathCell in pathCells)
        {
            worldPath.Add(buildManager.CellToWorldCenter(pathCell));
        }

        SetPathTargets(worldPath);
        _pathGoalCell = goalCell;
        return true;
    }

    private void HandlePathRevalidationFailed()
    {
        ClearPath();
        _pathGoalCell = null;

        if (ControlMode == MercenaryControlMode.Life)
        {
            _lifeAI?.PickNextLifePoint();
            SetOrderState(MercenaryOrderState.LifeWaiting);
        }
        else if (ControlMode == MercenaryControlMode.Rallying)
        {
            _hasRallyTarget = false;
            ReturnToLife();
        }
        else if (ControlMode == MercenaryControlMode.Squad)
        {
            SetOrderState(MercenaryOrderState.SquadIdle);
        }
    }

    private string GetPathRevalidationFailureMessage()
    {
        if (ControlMode == MercenaryControlMode.Squad)
        {
            return "Path failed: stopped";
        }

        if (ControlMode == MercenaryControlMode.Rallying)
        {
            return "Path failed: rally canceled";
        }

        if (ControlMode == MercenaryControlMode.Life)
        {
            if (_lifeAI?.CurrentLifeActionName == "Gather")
            {
                return "Path failed: gather canceled";
            }

            return "Path failed: choosing new task";
        }

        return "Path failed";
    }

    private void ShowPathFeedback(string message)
    {
        _pathFeedbackMessage = message;
        _pathFeedbackTimer = PathFeedbackDurationSeconds;
    }

    private void ClearPathFeedback()
    {
        _pathFeedbackMessage = "";
        _pathFeedbackTimer = 0.0f;
    }

    private void UpdatePathFeedbackTimer(double delta)
    {
        if (_pathFeedbackTimer <= 0.0f)
        {
            return;
        }

        _pathFeedbackTimer = Mathf.Max(0.0f, _pathFeedbackTimer - (float)delta);

        if (_pathFeedbackTimer <= 0.0f)
        {
            ClearPathFeedback();
        }
    }

    private void UpdateDefenseAttack(double delta)
    {
        if (ControlMode != MercenaryControlMode.Squad || OrderState != MercenaryOrderState.SquadDefending)
        {
            _defenseAttackCooldown = 0.0;
            return;
        }

        if (_defenseTarget == null || _defenseTarget.IsDefeated)
        {
            UpdateDefenseTarget();
        }

        if (_defenseTarget == null || _defenseTarget.IsDefeated)
        {
            return;
        }

        _defenseAttackCooldown -= delta;

        if (_defenseAttackCooldown > 0.0)
        {
            return;
        }

        SpawnAttackDebugEffect(GlobalPosition, _defenseTarget.GlobalPosition);
        SpawnDamageNumberEffect(_defenseTarget.GlobalPosition, DefenseAttackDamage);
        _defenseTarget.TakeDamage(DefenseAttackDamage);
        _defenseAttackCooldown = DefenseAttackInterval;

        if (_defenseTarget.IsDefeated)
        {
            UpdateDefenseTarget();
        }
    }

    private void SpawnAttackDebugEffect(Vector2 start, Vector2 end)
    {
        Node? effectLayer = GetTree().CurrentScene?.GetNodeOrNull("EffectLayer");

        if (effectLayer == null)
        {
            return;
        }

        AttackDebugEffect effect = new AttackDebugEffect();
        effectLayer.AddChild(effect);
        effect.Setup(start, end);
    }

    private void SpawnDamageNumberEffect(Vector2 position, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        Node? effectLayer = GetTree().CurrentScene?.GetNodeOrNull("EffectLayer");

        if (effectLayer == null)
        {
            return;
        }

        DamageNumberEffect effect = new DamageNumberEffect();
        effectLayer.AddChild(effect);
        effect.Setup(position, damage);
    }

    private void UpdateRallying(double delta)
    {
        if (!_hasRallyTarget)
        {
            if (_rallyPoint == null)
            {
                SetControlMode(MercenaryControlMode.Squad);
                return;
            }

            if (!StartRallyPath(_rallyPoint.GlobalPosition))
            {
                return;
            }
        }

        if (UpdatePathMovement(delta, MercenaryOrderState.RallyMoving, MercenaryOrderState.RallyMoving))
        {
            GlobalPosition = _rallyTarget;
            _hasRallyTarget = false;
            EnterSquad();
            return;
        }
    }

    private bool StartRallyPath(Vector2 rallyTarget)
    {
        _rallyTarget = SnapWorldToGridCenter(rallyTarget);
        _hasRallyTarget = true;

        if (_baseBuildManager == null)
        {
            SetPathTargets(new[] { _rallyTarget });
            return true;
        }

        if (TryMoveToWorldWithPath(_rallyTarget, _baseBuildManager))
        {
            return true;
        }

        _hasRallyTarget = false;
        ReturnToLife();
        return false;
    }

    private void SetOrderState(MercenaryOrderState orderState)
    {
        if (OrderState == orderState)
        {
            return;
        }

        OrderState = orderState;
        UpdateNameLabel();
    }

    private static IEnumerable<Node2D> GetLifePointNodes(Node2D lifePointLayer)
    {
        foreach (Node child in lifePointLayer.GetChildren())
        {
            if (child is Node2D point)
            {
                yield return point;
            }
        }
    }
}
