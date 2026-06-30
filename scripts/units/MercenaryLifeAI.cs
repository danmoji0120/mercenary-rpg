using System.Collections.Generic;
using Godot;

public partial class MercenaryLifeAI : Node
{
    [Export]
    public float WaitTimeAtPoint { get; set; } = 2.0f;

    [Export]
    public bool DebugLifeFacilityChoice { get; set; } = false;

    [Export]
    public bool DebugLifeNeeds { get; set; } = false;

    [Export]
    public float MaxSleepNeed { get; set; } = 100.0f;

    [Export]
    public float SleepNeed { get; set; } = 100.0f;

    [Export]
    public float SleepDecayPerSecond { get; set; } = 1.0f;

    [Export]
    public float SleepRecoverPerSecond { get; set; } = 12.0f;

    [Export]
    public float SleepUrgentThreshold { get; set; } = 35.0f;

    [Export]
    public float SleepCriticalThreshold { get; set; } = 15.0f;

    [Export]
    public float MaxHungerNeed { get; set; } = 100.0f;

    [Export]
    public float HungerNeed { get; set; } = 100.0f;

    [Export]
    public float HungerDecayPerSecond { get; set; } = 0.75f;

    [Export]
    public float HungerRecoverPerSecond { get; set; } = 18.0f;

    [Export]
    public float HungerUrgentThreshold { get; set; } = 40.0f;

    [Export]
    public float HungerCriticalThreshold { get; set; } = 20.0f;

    [Export]
    public float StarvationDelaySeconds { get; set; } = 10.0f;

    [Export]
    public float PatrolContinueChance { get; set; } = 0.6f;

    [Export]
    public float PatrolDetectionRadius { get; set; } = 180.0f;

    [Export]
    public float PatrolDetectionInterval { get; set; } = 0.5f;

    [Export]
    public bool DebugPatrolDetection { get; set; } = false;

    [Export]
    public float GatherChance { get; set; } = 0.2f;

    [Export]
    public float GatherWorkSeconds { get; set; } = 3.0f;

    [Export]
    public int GatherAmountPerWork { get; set; } = 5;

    [Export]
    public bool DebugGathering { get; set; } = false;

    [Export]
    public int CarryCapacity { get; set; } = 10;

    [Export]
    public float HaulChance { get; set; } = 0.45f;

    [Export]
    public bool DebugHauling { get; set; } = false;

    [Export]
    public float PlantWorkSeconds { get; set; } = 2.5f;

    [Export]
    public float HarvestCropWorkSeconds { get; set; } = 3.0f;

    [Export]
    public float FarmWorkChance { get; set; } = 0.35f;

    [Export]
    public bool DebugFarming { get; set; } = false;

    [Export]
    public int DesiredWoodAmount { get; set; } = 80;

    [Export]
    public int DesiredStoneAmount { get; set; } = 60;

    [Export]
    public int DesiredMetalAmount { get; set; } = 30;

    [Export]
    public float GatherBaseScore { get; set; } = 1.0f;

    [Export]
    public float GatherNeedWeight { get; set; } = 6.0f;

    [Export]
    public float GatherDistancePenalty { get; set; } = 0.08f;

    public string CurrentLifeAction { get; private set; } = "Idle";
    public float CurrentSleepNeed => SleepNeed;
    public float CurrentMaxSleepNeed => MaxSleepNeed;
    public float CurrentHungerNeed => HungerNeed;
    public float CurrentMaxHungerNeed => MaxHungerNeed;
    public string CurrentLifeActionName => CurrentLifeAction;
    public bool IsSleepUrgent => SleepNeed <= SleepUrgentThreshold;
    public bool IsSleepCritical => SleepNeed <= SleepCriticalThreshold;
    public bool IsHungerUrgent => HungerNeed <= HungerUrgentThreshold;
    public bool IsHungerCritical => HungerNeed <= HungerCriticalThreshold;
    public float CurrentStarvingSeconds => _starvingSeconds;
    public float CurrentStarvationDelaySeconds => StarvationDelaySeconds;
    public bool IsStarving => _isStarving;
    public bool IsPatrolling => _isPatrolling;
    public bool HasSpottedEnemy => _hasSpottedEnemy;
    public Node2D? SpottedEnemy => _spottedEnemy;
    public float CurrentPatrolDetectionRadius => PatrolDetectionRadius;
    public bool IsGathering => _isGathering;
    public string CurrentGatherTargetLabel => _currentGatherTargetLabel;
    public float CurrentGatherWorkRemaining => _gatherWorkTimer;
    public float CurrentGatherWorkSeconds => GatherWorkSeconds;
    public bool IsCarryingResource => _isCarryingResource;
    public string CarryingResourceLabel => _isCarryingResource ? $"{_carriedResourceType} {_carriedResourceAmount}" : "-";
    public string HaulingTargetLabel => GetHaulingTargetLabel();
    public string FarmingTargetLabel => GetFarmingTargetLabel();
    public float CurrentFarmWorkRemaining => IsPlantAction() ? _plantWorkTimer : _harvestCropWorkTimer;
    public float CurrentFarmWorkSeconds => IsPlantAction() ? PlantWorkSeconds : HarvestCropWorkSeconds;
    public bool HasGatherReservation
    {
        get
        {
            ResourceNode? targetResourceNode = _targetResourceNode;
            return IsSafeResourceNodeReference(targetResourceNode)
                && targetResourceNode!.IsReservedBy(this);
        }
    }
    public bool HasConsumedFoodForCurrentEat => _hasConsumedFoodForCurrentEat;
    public bool HasReservedFacility => _reservedFacilityCell.HasValue;
    public bool HasOccupiedFacility => _occupiedFacilityCell.HasValue;
    public FacilityType CurrentReservedFacilityType
    {
        get
        {
            if (!_reservedFacilityCell.HasValue || _reservedBuildManager == null)
            {
                return FacilityType.None;
            }

            return _reservedBuildManager.GetFacilityTypeAt(_reservedFacilityCell.Value);
        }
    }

    public FacilityType CurrentOccupiedFacilityType
    {
        get
        {
            if (!_occupiedFacilityCell.HasValue || _reservedBuildManager == null)
            {
                return FacilityType.None;
            }

            return _reservedBuildManager.GetFacilityTypeAt(_occupiedFacilityCell.Value);
        }
    }

    private readonly List<Node2D> _lifePoints = new();
    private readonly RandomNumberGenerator _random = new();
    private Node2D? _targetPoint;
    private float _waitTimer;
    private bool _hasPathToTarget;
    private bool _usingFacilityTarget;
    private FacilityInfo _targetFacility;
    private Vector2I? _reservedFacilityCell;
    private Vector2I? _occupiedFacilityCell;
    private BaseBuildManager? _reservedBuildManager;
    private MercenaryController? _reservedMercenary;
    private bool _hasConsumedFoodForCurrentEat;
    private bool _hasReportedNoFoodForCurrentEat;
    private float _starvingSeconds;
    private bool _isStarving;
    private Vector2I? _lastPatrolFacilityCell;
    private bool _isPatrolling;
    private bool _hasSpottedEnemy;
    private Node2D? _spottedEnemy;
    private float _patrolDetectionTimer;
    private ResourceNode? _targetResourceNode;
    private string _currentGatherTargetLabel = "-";
    private bool _isGathering;
    private float _gatherWorkTimer;
    private bool _hasReportedGatherFailure;
    private ResourcePile? _targetHaulPile;
    private string _targetHaulPileLabel = "-";
    private Vector2I? _targetStorageCell;
    private Vector2I? _targetStorageAccessCell;
    private BaseResourceType _carriedResourceType = BaseResourceType.Wood;
    private int _carriedResourceAmount;
    private bool _isCarryingResource;
    private bool _isHaulingToStorage;
    private Vector2I? _targetPlantCell;
    private CropPlant? _targetHarvestCrop;
    private string _targetHarvestCropLabel = "-";
    private float _plantWorkTimer;
    private float _harvestCropWorkTimer;

    private readonly struct GatherResourceCandidate
    {
        public GatherResourceCandidate(ResourceNode resourceNode, float score, int pathLength, float distanceSquared)
        {
            ResourceNode = resourceNode;
            Score = score;
            PathLength = pathLength;
            DistanceSquared = distanceSquared;
        }

        public ResourceNode ResourceNode { get; }
        public float Score { get; }
        public int PathLength { get; }
        public float DistanceSquared { get; }
    }

    public override void _Ready()
    {
        _random.Randomize();
    }

    public void SetLifePoints(IEnumerable<Node2D> lifePoints)
    {
        // TODO: Replace temporary LifePoint markers with BaseBuildManager facility queries.
        _lifePoints.Clear();
        _lifePoints.AddRange(lifePoints);

        if (_lifePoints.Count > 0)
        {
            PickNextLifePoint();
        }
    }

    public void StopLifeAI(MercenaryController? mercenary = null)
    {
        ReleaseCurrentFacilityUse();
        _targetPoint = null;
        _waitTimer = 0.0f;
        _hasPathToTarget = false;
        _usingFacilityTarget = false;
        ResetCurrentEatFood();
        ResetGatherState();
        ResetHaulState(true, mercenary);
        ResetPlantState();
        ResetHarvestCropState();
        _isPatrolling = false;
        ClearPatrolDetection();
        CurrentLifeAction = "Idle";
    }

    public void ResumeLifeAI()
    {
        if (_lifePoints.Count == 0)
        {
            CurrentLifeAction = "Idle";
            return;
        }

        PickNextLifePoint();
    }

    public MercenaryOrderState UpdateLifeAI(MercenaryController mercenary, double delta)
    {
        UpdateSleepDecay(delta);
        UpdateHungerDecay(delta);
        UpdateStarvation(delta);

        if (_lifePoints.Count == 0)
        {
            CurrentLifeAction = "Idle";
            ClearPatrolDetection();
            return MercenaryOrderState.None;
        }

        if (_targetPoint == null)
        {
            PickNextLifePoint();
            return MercenaryOrderState.LifeMoving;
        }

        UpdatePatrolDetection(mercenary, delta);

        Vector2 targetPosition = GetFallbackTargetPosition(mercenary);

        if (!_hasPathToTarget)
        {
            BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

            if (buildManager != null)
            {
                bool isHaulAction = IsHaulAction();
                bool isGatherAction = IsGatherAction();
                bool isPlantAction = IsPlantAction();
                bool isHarvestCropAction = IsHarvestCropAction();
                bool hasHaulPath = isHaulAction && TryStartHaulPath(mercenary, buildManager);
                bool hasPlantPath = !isHaulAction && isPlantAction && TryStartPlantPath(mercenary, buildManager);
                bool hasHarvestCropPath = !isHaulAction && !isPlantAction && isHarvestCropAction && TryStartHarvestCropPath(mercenary, buildManager);
                bool hasGatherPath = !isHaulAction && !isPlantAction && !isHarvestCropAction && isGatherAction && TryStartGatherPath(mercenary, buildManager);
                bool hasFacilityPath = !isHaulAction && !isPlantAction && !isHarvestCropAction && !isGatherAction && TryStartFacilityPath(mercenary, buildManager);
                bool hasFallbackPath = hasHaulPath
                    || hasPlantPath
                    || hasHarvestCropPath
                    || hasGatherPath
                    || hasFacilityPath
                    || (!isHaulAction && !isPlantAction && !isHarvestCropAction && !isGatherAction && mercenary.TryMoveToWorldWithPath(targetPosition, buildManager));

        if (!hasFallbackPath)
                {
                    _waitTimer += (float)delta;

                    if (_waitTimer >= WaitTimeAtPoint)
                    {
                        PickNextLifePoint();
                    }

                    return MercenaryOrderState.LifeWaiting;
                }

                if (!hasFacilityPath)
                {
                    ReleaseCurrentFacilityUse();
                    _usingFacilityTarget = false;
                    _isPatrolling = false;
                    ClearPatrolDetection();
                }

                _hasPathToTarget = true;
            }
            else
            {
                _hasPathToTarget = true;
            }
        }

        bool arrived = mercenary.GetBaseBuildManager() != null
            ? mercenary.UpdateLifePathMovement(delta)
            : UpdateDirectLifeMovement(mercenary, targetPosition, delta);

        if (!arrived)
        {
            return MercenaryOrderState.LifeMoving;
        }

        if (IsHaulAction())
        {
            if (UpdateHaulAtArrival(mercenary))
            {
                PickNextLifePoint();
            }

            return MercenaryOrderState.LifeMoving;
        }

        if (IsPlantAction())
        {
            if (UpdatePlantWork(mercenary, delta))
            {
                PickNextLifePoint();
                return MercenaryOrderState.LifeMoving;
            }

            return MercenaryOrderState.LifeWaiting;
        }

        if (IsHarvestCropAction())
        {
            if (UpdateHarvestCropWork(mercenary, delta))
            {
                PickNextLifePoint();
                return MercenaryOrderState.LifeMoving;
            }

            return MercenaryOrderState.LifeWaiting;
        }

        if (IsGatherAction())
        {
            if (UpdateGatherWork(mercenary, delta))
            {
                PickNextLifePoint();
                return MercenaryOrderState.LifeMoving;
            }

            return MercenaryOrderState.LifeWaiting;
        }

        if (_usingFacilityTarget && !_occupiedFacilityCell.HasValue && !TryOccupyReservedFacility(mercenary))
        {
            ReleaseCurrentFacilityUse();
            PickNextLifePoint();
            return MercenaryOrderState.LifeMoving;
        }

        if (UpdateHungerRecovery(mercenary, delta, out bool canRecoverHunger))
        {
            ReleaseCurrentFacilityUse();
            PickNextLifePoint();
            return MercenaryOrderState.LifeMoving;
        }

        if (IsEatingAtPoint())
        {
            if (canRecoverHunger)
            {
                return MercenaryOrderState.LifeWaiting;
            }

            _waitTimer += (float)delta;

            if (_waitTimer >= WaitTimeAtPoint)
            {
                ResetCurrentEatFood();
                PickNextLifePoint();
                return MercenaryOrderState.LifeMoving;
            }

            return MercenaryOrderState.LifeWaiting;
        }

        if (UpdateSleepRecovery(delta))
        {
            ReleaseCurrentFacilityUse();
            PickNextLifePoint();
            return MercenaryOrderState.LifeMoving;
        }

        if (IsSleepingInBed())
        {
            return MercenaryOrderState.LifeWaiting;
        }

        _waitTimer += (float)delta;

        if (_waitTimer >= WaitTimeAtPoint)
        {
            ReleaseCurrentFacilityUse();
            PickNextPatrolPointOrLifePoint();
            return MercenaryOrderState.LifeMoving;
        }

        return MercenaryOrderState.LifeWaiting;
    }

    public void PickNextLifePoint()
    {
        ReleaseCurrentFacilityUse();
        ResetCurrentEatFood();
        ResetGatherState();
        ResetPlantState();
        ResetHarvestCropState();

        if (_isCarryingResource)
        {
            _targetPoint = null;
            CurrentLifeAction = "Haul";
            _waitTimer = 0.0f;
            _hasPathToTarget = false;
            _usingFacilityTarget = false;
            _isPatrolling = false;
            ClearPatrolDetection();
            return;
        }

        if (_lifePoints.Count == 0)
        {
            _targetPoint = null;
            CurrentLifeAction = "Idle";
            _isPatrolling = false;
            ClearPatrolDetection();
            return;
        }

        Node2D? previousPoint = _targetPoint;
        Node2D? priorityPoint = GetPriorityNeedPoint();
        BaseBuildManager? buildManager = GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");
        MercenaryController? mercenary = GetParent() as MercenaryController;

        if (priorityPoint != null)
        {
            _targetPoint = priorityPoint;
        }
        else if (buildManager != null
            && mercenary != null
            && _random.Randf() <= HaulChance
            && buildManager.FindNearestHaulablePile(buildManager.WorldToCell(mercenary.GlobalPosition), this) != null)
        {
            _targetPoint = previousPoint ?? _lifePoints[0];
            CurrentLifeAction = "Haul";
            _waitTimer = 0.0f;
            _hasPathToTarget = false;
            _usingFacilityTarget = false;
            _isPatrolling = false;
            ClearPatrolDetection();
            return;
        }
        else if (buildManager != null
            && mercenary != null
            && buildManager.FindNearestHarvestableCrop(buildManager.WorldToCell(mercenary.GlobalPosition), this) != null)
        {
            _targetPoint = previousPoint ?? _lifePoints[0];
            CurrentLifeAction = "HarvestCrop";
            _waitTimer = 0.0f;
            _hasPathToTarget = false;
            _usingFacilityTarget = false;
            _isPatrolling = false;
            ClearPatrolDetection();
            return;
        }
        else if (buildManager != null
            && mercenary != null
            && _random.Randf() <= FarmWorkChance
            && buildManager.TryFindNearestPlantableFarmCell(buildManager.WorldToCell(mercenary.GlobalPosition), out _))
        {
            _targetPoint = previousPoint ?? _lifePoints[0];
            CurrentLifeAction = "Plant";
            _waitTimer = 0.0f;
            _hasPathToTarget = false;
            _usingFacilityTarget = false;
            _isPatrolling = false;
            ClearPatrolDetection();
            return;
        }
        else if (_random.Randf() <= GatherChance)
        {
            _targetPoint = previousPoint ?? _lifePoints[0];
            CurrentLifeAction = "Gather";
            _waitTimer = 0.0f;
            _hasPathToTarget = false;
            _usingFacilityTarget = false;
            _isPatrolling = false;
            ClearPatrolDetection();
            return;
        }
        else if (_lifePoints.Count == 1)
        {
            _targetPoint = _lifePoints[0];
        }
        else
        {
            do
            {
                _targetPoint = _lifePoints[_random.RandiRange(0, _lifePoints.Count - 1)];
            }
            while (_targetPoint == previousPoint);
        }

        _waitTimer = 0.0f;
        _hasPathToTarget = false;
        _usingFacilityTarget = false;
        CurrentLifeAction = _targetPoint == null ? "Idle" : GetActionName(_targetPoint.Name);
        _isPatrolling = CurrentLifeAction == "Guard";

        if (!_isPatrolling)
        {
            ClearPatrolDetection();
        }
    }

    private void PickNextPatrolPointOrLifePoint()
    {
        if (CurrentLifeAction != "Guard" || _random.Randf() > PatrolContinueChance)
        {
            PickNextLifePoint();
            return;
        }

        Node2D? guardPoint = GetLifePointByAction("Guard");

        if (guardPoint == null)
        {
            PickNextLifePoint();
            return;
        }

        ReleaseCurrentFacilityUse();
        ResetCurrentEatFood();
        ResetGatherState();
        ResetPlantState();
        ResetHarvestCropState();
        _targetPoint = guardPoint;
        _waitTimer = 0.0f;
        _hasPathToTarget = false;
        _usingFacilityTarget = false;
        CurrentLifeAction = "Guard";
        _isPatrolling = true;
    }

    private bool TryStartFacilityPath(MercenaryController mercenary, BaseBuildManager buildManager)
    {
        FacilityType facilityType = GetPreferredFacilityType(CurrentLifeAction);

        if (facilityType == FacilityType.None)
        {
            return false;
        }

        if (facilityType == FacilityType.GuardPost)
        {
            return TryStartPatrolPath(mercenary, buildManager);
        }

        if (!buildManager.TryGetNearestAvailableFacility(mercenary.GlobalPosition, facilityType, mercenary, out FacilityInfo facility))
        {
            return false;
        }

        if (!buildManager.TryReserveFacility(facility.Cell, mercenary))
        {
            return false;
        }

        if (!mercenary.TryMoveToCell(facility.Cell, buildManager))
        {
            buildManager.ReleaseFacilityReservation(facility.Cell, mercenary);
            return false;
        }

        _targetFacility = facility;
        _reservedFacilityCell = facility.Cell;
        _reservedBuildManager = buildManager;
        _reservedMercenary = mercenary;
        _usingFacilityTarget = true;

        // TODO: Add facility occupancy, personal bed assignment, and guard post assignment.
        if (DebugLifeFacilityChoice)
        {
            GD.Print($"{mercenary.MercenaryName} life facility: {facility.FacilityType} at {facility.Cell}");
        }

        return true;
    }

    private bool TryStartPatrolPath(MercenaryController mercenary, BaseBuildManager buildManager)
    {
        if (!TryPickPatrolFacility(mercenary, buildManager, true, out FacilityInfo facility)
            && !TryPickPatrolFacility(mercenary, buildManager, false, out facility))
        {
            _isPatrolling = false;
            ClearPatrolDetection();
            return false;
        }

        if (!buildManager.TryReserveFacility(facility.Cell, mercenary))
        {
            _isPatrolling = false;
            ClearPatrolDetection();
            return false;
        }

        if (!mercenary.TryMoveToCell(facility.Cell, buildManager))
        {
            buildManager.ReleaseFacilityReservation(facility.Cell, mercenary);
            _isPatrolling = false;
            ClearPatrolDetection();
            return false;
        }

        _targetFacility = facility;
        _reservedFacilityCell = facility.Cell;
        _reservedBuildManager = buildManager;
        _reservedMercenary = mercenary;
        _usingFacilityTarget = true;
        _isPatrolling = true;

        if (DebugLifeFacilityChoice)
        {
            GD.Print($"{mercenary.MercenaryName} patrol point: {facility.Cell}");
        }

        return true;
    }

    private bool TryPickPatrolFacility(MercenaryController mercenary, BaseBuildManager buildManager, bool excludeLastPatrolCell, out FacilityInfo facility)
    {
        facility = default;
        List<FacilityInfo> candidates = new();
        Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

        foreach (FacilityInfo candidate in buildManager.GetFacilitiesByType(FacilityType.GuardPost))
        {
            if (excludeLastPatrolCell && _lastPatrolFacilityCell.HasValue && candidate.Cell == _lastPatrolFacilityCell.Value)
            {
                continue;
            }

            bool reservedByOther = candidate.IsReserved && !buildManager.IsFacilityReservedBy(candidate.Cell, mercenary);
            bool occupiedByOther = candidate.IsOccupied && !buildManager.IsFacilityOccupiedBy(candidate.Cell, mercenary);

            if (!candidate.IsUsable || reservedByOther || occupiedByOther)
            {
                continue;
            }

            List<Vector2I> path = GridPathfinder.FindPath(startCell, candidate.Cell, buildManager);

            if (startCell != candidate.Cell && path.Count == 0)
            {
                continue;
            }

            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        facility = candidates[_random.RandiRange(0, candidates.Count - 1)];
        return true;
    }

    private bool TryStartPlantPath(MercenaryController mercenary, BaseBuildManager buildManager)
    {
        Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

        if (!_targetPlantCell.HasValue)
        {
            if (!buildManager.TryFindNearestPlantableFarmCell(startCell, out Vector2I plantCell))
            {
                return false;
            }

            _targetPlantCell = plantCell;
        }

        if (!mercenary.TryMoveToCell(_targetPlantCell.Value, buildManager))
        {
            ResetPlantState();
            return false;
        }

        if (DebugFarming)
        {
            GD.Print($"{mercenary.MercenaryName} plant target selected at {_targetPlantCell.Value}");
        }

        return true;
    }

    private bool TryStartHarvestCropPath(MercenaryController mercenary, BaseBuildManager buildManager)
    {
        CropPlant? cropPlant = _targetHarvestCrop;

        if (!IsValidHarvestCrop(cropPlant))
        {
            cropPlant = buildManager.FindNearestHarvestableCrop(buildManager.WorldToCell(mercenary.GlobalPosition), this);

            if (!IsValidHarvestCrop(cropPlant))
            {
                return false;
            }

            if (!cropPlant!.TryReserveHarvest(this))
            {
                return false;
            }

            _targetHarvestCrop = cropPlant;
            _targetHarvestCropLabel = cropPlant.GetDisplayName();
        }

        CropPlant safeCropPlant = cropPlant!;
        Vector2I cropCell = safeCropPlant.Cell;

        if (!mercenary.TryMoveToCell(cropCell, buildManager))
        {
            safeCropPlant.ReleaseHarvestReservation(this);
            ResetHarvestCropState();
            return false;
        }

        _targetPoint = safeCropPlant;

        if (DebugFarming)
        {
            GD.Print($"{mercenary.MercenaryName} harvest crop target selected at {cropCell}");
        }

        return true;
    }

    private bool TryStartGatherPath(MercenaryController mercenary, BaseBuildManager buildManager)
    {
        if (!TryPickPreferredResourceNode(mercenary, buildManager, out ResourceNode? resourceNode) || resourceNode == null)
        {
            if (DebugGathering && !_hasReportedGatherFailure)
            {
                GD.Print($"{mercenary.MercenaryName} gather failed: no reachable resource node");
            }

            _hasReportedGatherFailure = true;
            return false;
        }

        string resourceNodeLabel = resourceNode.GetDisplayName();
        Vector2I resourceNodeCell = resourceNode.Cell;

        if (!mercenary.TryMoveToCell(resourceNodeCell, buildManager))
        {
            resourceNode.ReleaseReservation(this);

            if (DebugGathering && !_hasReportedGatherFailure)
            {
                GD.Print($"{mercenary.MercenaryName} gather failed: no path to {resourceNodeLabel}");
            }

            _hasReportedGatherFailure = true;
            return false;
        }

        _targetResourceNode = resourceNode;
        _targetPoint = resourceNode;
        _currentGatherTargetLabel = resourceNodeLabel;
        _hasReportedGatherFailure = false;

        if (DebugGathering)
        {
            GD.Print($"{mercenary.MercenaryName} gather target selected: {resourceNodeLabel} at {resourceNodeCell}");
        }

        return true;
    }

    private bool TryPickPreferredResourceNode(MercenaryController mercenary, BaseBuildManager buildManager, out ResourceNode? resourceNode)
    {
        resourceNode = null;
        List<GatherResourceCandidate> candidates = new();
        Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

        CollectGatherCandidates(mercenary, buildManager, startCell, true, candidates);
        bool triedDesignatedCandidates = candidates.Count > 0;

        if (candidates.Count == 0)
        {
            CollectGatherCandidates(mercenary, buildManager, startCell, false, candidates);
        }

        if (TryReserveBestGatherCandidate(candidates, out resourceNode))
        {
            return true;
        }

        if (triedDesignatedCandidates)
        {
            candidates.Clear();
            CollectGatherCandidates(mercenary, buildManager, startCell, false, candidates);
            return TryReserveBestGatherCandidate(candidates, out resourceNode);
        }

        return false;
    }

    private bool TryReserveBestGatherCandidate(List<GatherResourceCandidate> candidates, out ResourceNode? resourceNode)
    {
        resourceNode = null;

        if (candidates.Count == 0)
        {
            return false;
        }

        candidates.Sort((left, right) =>
        {
            int scoreComparison = right.Score.CompareTo(left.Score);

            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            int pathComparison = left.PathLength.CompareTo(right.PathLength);

            if (pathComparison != 0)
            {
                return pathComparison;
            }

            return left.DistanceSquared.CompareTo(right.DistanceSquared);
        });

        foreach (GatherResourceCandidate candidate in candidates)
        {
            if (!IsGatherableResourceNode(candidate.ResourceNode))
            {
                continue;
            }

            if (!candidate.ResourceNode.TryReserve(this))
            {
                if (DebugGathering)
                {
                    GD.Print($"{Name} gather reservation failed: {candidate.ResourceNode.GetDisplayName()}");
                }

                continue;
            }

            resourceNode = candidate.ResourceNode;

            if (DebugGathering)
            {
                string designationText = resourceNode.IsHarvestDesignated ? " designated" : "";
                GD.Print($"{Name} gather selected{designationText} {resourceNode.GetDisplayName()} cell={resourceNode.Cell} score={candidate.Score:0.00}");
                GD.Print($"{Name} gather reserved: {resourceNode.GetDisplayName()}");
            }

            return true;
        }

        return false;
    }

    private void CollectGatherCandidates(
        MercenaryController mercenary,
        BaseBuildManager buildManager,
        Vector2I startCell,
        bool designatedOnly,
        List<GatherResourceCandidate> candidates)
    {
        foreach (Node node in GetTree().GetNodesInGroup("resource_nodes"))
        {
            if (node is not ResourceNode candidate || !IsGatherableResourceNode(candidate))
            {
                continue;
            }

            if (designatedOnly && !candidate.IsHarvestDesignated)
            {
                continue;
            }

            if (candidate.IsReserved && !candidate.IsReservedBy(this))
            {
                continue;
            }

            if (!buildManager.IsCellInWorld(candidate.Cell))
            {
                continue;
            }

            List<Vector2I> path = GridPathfinder.FindPath(startCell, candidate.Cell, buildManager);

            if (startCell != candidate.Cell && path.Count == 0)
            {
                continue;
            }

            int pathLength = startCell == candidate.Cell ? 0 : path.Count;
            float distanceSquared = mercenary.GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
            float score = CalculateGatherCandidateScore(candidate, buildManager, pathLength);
            candidates.Add(new GatherResourceCandidate(candidate, score, pathLength, distanceSquared));

            if (DebugGathering)
            {
                string designationText = candidate.IsHarvestDesignated ? " designated" : "";
                GD.Print($"{Name} gather candidate{designationText} {candidate.GetDisplayName()} cell={candidate.Cell} amount={candidate.Amount} need={GetResourceNeedScore(candidate.ResourceType, buildManager):0.00} path={pathLength} score={score:0.00}");
            }
        }
    }

    private float CalculateGatherCandidateScore(ResourceNode resourceNode, BaseBuildManager buildManager, int pathLength)
    {
        float needScore = GetResourceNeedScore(resourceNode.ResourceType, buildManager);
        float baseWeight = GetResourceBaseWeight(resourceNode.ResourceType);
        float urgentBonus = GetResourceUrgentBonus(resourceNode.ResourceType, buildManager);
        float distancePenalty = Mathf.Max(0, pathLength) * GatherDistancePenalty;
        return GatherBaseScore + baseWeight + urgentBonus + (needScore * GatherNeedWeight) - distancePenalty;
    }

    private float GetResourceNeedScore(BaseResourceType resourceType, BaseBuildManager? buildManager)
    {
        if (buildManager == null)
        {
            return 0.0f;
        }

        int desiredAmount = GetDesiredResourceAmount(resourceType);

        if (desiredAmount <= 0)
        {
            return 0.0f;
        }

        int currentAmount = buildManager.GetResourceAmount(resourceType);
        int deficit = desiredAmount - currentAmount;

        if (deficit <= 0)
        {
            return 0.0f;
        }

        return deficit / (float)desiredAmount;
    }

    private float GetResourceUrgentBonus(BaseResourceType resourceType, BaseBuildManager buildManager)
    {
        int desiredAmount = GetDesiredResourceAmount(resourceType);

        if (desiredAmount <= 0)
        {
            return 0.0f;
        }

        int currentAmount = buildManager.GetResourceAmount(resourceType);

        if (currentAmount <= 0)
        {
            return 3.0f;
        }

        return currentAmount < desiredAmount * 0.25f ? 1.5f : 0.0f;
    }

    private int GetDesiredResourceAmount(BaseResourceType resourceType)
    {
        return resourceType switch
        {
            BaseResourceType.Wood => DesiredWoodAmount,
            BaseResourceType.Stone => DesiredStoneAmount,
            BaseResourceType.Metal => DesiredMetalAmount,
            _ => 0
        };
    }

    private static float GetResourceBaseWeight(BaseResourceType resourceType)
    {
        return resourceType switch
        {
            BaseResourceType.Wood => 1.0f,
            BaseResourceType.Stone => 1.1f,
            BaseResourceType.Metal => 1.3f,
            _ => 0.0f
        };
    }

    private bool UpdatePlantWork(MercenaryController mercenary, double delta)
    {
        BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

        if (buildManager == null || !_targetPlantCell.HasValue)
        {
            ResetPlantState();
            return true;
        }

        Vector2I plantCell = _targetPlantCell.Value;

        if (!buildManager.IsFarmZoneCell(plantCell))
        {
            ResetPlantState();
            return true;
        }

        _plantWorkTimer += (float)delta;

        if (_plantWorkTimer < PlantWorkSeconds)
        {
            return false;
        }

        bool planted = buildManager.TrySpawnCropPlantAt(plantCell);

        if (DebugFarming)
        {
            GD.Print(planted
                ? $"{mercenary.MercenaryName} planted crop at {plantCell}"
                : $"{mercenary.MercenaryName} plant canceled at {plantCell}");
        }

        ResetPlantState();
        return true;
    }

    private bool UpdateHarvestCropWork(MercenaryController mercenary, double delta)
    {
        BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();
        CropPlant? cropPlant = _targetHarvestCrop;

        if (buildManager == null || !IsValidHarvestCrop(cropPlant))
        {
            ResetHarvestCropState();
            return true;
        }

        CropPlant safeCropPlant = cropPlant!;
        Vector2I cropCell = safeCropPlant.Cell;
        string cropLabel = safeCropPlant.GetDisplayName();

        _harvestCropWorkTimer += (float)delta;

        if (_harvestCropWorkTimer < HarvestCropWorkSeconds)
        {
            return false;
        }

        int foodAmount = safeCropPlant.Harvest();

        if (foodAmount > 0)
        {
            buildManager.TrySpawnOrMergeResourcePile(BaseResourceType.Food, cropCell, foodAmount);
        }

        if (DebugFarming)
        {
            GD.Print($"{mercenary.MercenaryName} harvested {cropLabel}: Food x{foodAmount} at {cropCell}");
        }

        ResetHarvestCropState();
        return true;
    }

    private bool UpdateGatherWork(MercenaryController mercenary, double delta)
    {
        ResourceNode? targetResourceNode = _targetResourceNode;

        if (!IsSafeResourceNodeReference(targetResourceNode))
        {
            ResetGatherState();
            return true;
        }

        ResourceNode safeTargetResourceNode = targetResourceNode!;

        if (!IsGatherableResourceNode(safeTargetResourceNode) || !safeTargetResourceNode.IsReservedBy(this))
        {
            if (DebugGathering)
            {
                GD.Print($"{mercenary.MercenaryName} gather skipped: target unavailable");
            }

            ResetGatherState();
            return true;
        }

        if (!_isGathering)
        {
            _isGathering = true;
            _gatherWorkTimer = GatherWorkSeconds;
        }

        _gatherWorkTimer -= (float)delta;

        if (_gatherWorkTimer > 0.0f)
        {
            return false;
        }

        ResourceNode resourceNode = safeTargetResourceNode;
        BaseResourceType resourceType = resourceNode.ResourceType;
        string resourceNodeLabel = resourceNode.GetDisplayName();
        Vector2I resourceNodeCell = resourceNode.Cell;
        BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();
        int harvestedAmount = resourceNode.Harvest(GatherAmountPerWork);

        if (harvestedAmount > 0 && buildManager != null)
        {
            buildManager.TrySpawnOrMergeResourcePile(resourceType, resourceNodeCell, harvestedAmount);

            if (DebugGathering)
            {
                GD.Print($"{mercenary.MercenaryName} gather completed: {resourceType} pile +{harvestedAmount} from {resourceNodeLabel}");
            }
        }
        else if (DebugGathering)
        {
            GD.Print($"{mercenary.MercenaryName} gather skipped: depleted");
        }

        ResetGatherState();
        return true;
    }

    private bool TryStartHaulPath(MercenaryController mercenary, BaseBuildManager buildManager)
    {
        Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

        if (_isCarryingResource)
        {
            if (!_targetStorageCell.HasValue
                || !_targetStorageAccessCell.HasValue
                || buildManager.GetStorageFreeSpace(_targetStorageCell.Value, _carriedResourceType) <= 0)
            {
                if (!buildManager.TryFindNearestStorageAccessWithSpace(startCell, _carriedResourceType, out Vector2I deliveryStorageCell, out Vector2I deliveryAccessCell))
                {
                    DropCarriedResourceAtCurrentCell(mercenary);
                    ResetHaulState(false);
                    return false;
                }

                _targetStorageCell = deliveryStorageCell;
                _targetStorageAccessCell = deliveryAccessCell;
            }

            _isHaulingToStorage = true;

            if (mercenary.TryMoveToCell(_targetStorageAccessCell.Value, buildManager))
            {
                return true;
            }

            if (DebugHauling)
            {
                GD.Print($"{mercenary.MercenaryName} haul delivery path failed, dropping {_carriedResourceType} x{_carriedResourceAmount}");
            }

            DropCarriedResourceAtCurrentCell(mercenary);
            ResetHaulState(false);
            return false;
        }

        ResourcePile? pile = TryPickReachableHaulPile(startCell, buildManager, out Vector2I storageCell, out Vector2I storageAccessCell);

        if (!IsValidResourcePile(pile))
        {
            return false;
        }

        ResourcePile safePile = pile!;

        if (!safePile.TryReserveHaul(this))
        {
            return false;
        }

        if (!mercenary.TryMoveToCell(safePile.Cell, buildManager))
        {
            safePile.ReleaseHaulReservation(this);
            return false;
        }

        _targetHaulPile = safePile;
        _targetHaulPileLabel = safePile.GetDisplayName();
        _targetPoint = safePile;
        _targetStorageCell = storageCell;
        _targetStorageAccessCell = storageAccessCell;
        _isHaulingToStorage = false;

        if (DebugHauling)
        {
            GD.Print($"{mercenary.MercenaryName} haul target selected: {_targetHaulPileLabel} at {safePile.Cell}");
        }

        return true;
    }

    private ResourcePile? TryPickReachableHaulPile(Vector2I startCell, BaseBuildManager buildManager, out Vector2I storageCell, out Vector2I storageAccessCell)
    {
        storageCell = default;
        storageAccessCell = default;
        ResourcePile? bestPile = null;
        int bestPathLength = int.MaxValue;

        foreach (ResourcePile pile in buildManager.GetAllResourcePiles())
        {
            if (!IsValidResourcePile(pile)
                || (pile.IsReservedForHaul && !pile.IsReservedBy(this)))
            {
                continue;
            }

            if (!buildManager.TryFindNearestStorageAccessWithSpace(pile.Cell, pile.ResourceType, out Vector2I candidateStorageCell, out Vector2I candidateAccessCell))
            {
                continue;
            }

            List<Vector2I> pathToPile = GridPathfinder.FindPath(startCell, pile.Cell, buildManager);

            if (startCell != pile.Cell && pathToPile.Count == 0)
            {
                continue;
            }

            List<Vector2I> pathToStorage = GridPathfinder.FindPath(pile.Cell, candidateAccessCell, buildManager);

            if (pile.Cell != candidateAccessCell && pathToStorage.Count == 0)
            {
                continue;
            }

            int pathLength = (startCell == pile.Cell ? 0 : pathToPile.Count)
                + (pile.Cell == candidateAccessCell ? 0 : pathToStorage.Count);

            if (pathLength >= bestPathLength)
            {
                continue;
            }

            bestPathLength = pathLength;
            bestPile = pile;
            storageCell = candidateStorageCell;
            storageAccessCell = candidateAccessCell;
        }

        return bestPile;
    }

    private bool UpdateHaulAtArrival(MercenaryController mercenary)
    {
        BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

        if (buildManager == null)
        {
            ResetHaulState(true, mercenary);
            return true;
        }

        if (!_isCarryingResource)
        {
            ResourcePile? pile = _targetHaulPile;

            if (!IsValidResourcePile(pile))
            {
                ResetHaulState(false);
                return true;
            }

            ResourcePile safePile = pile!;

            if (!safePile.IsReservedBy(this))
            {
                ResetHaulState(false);
                return true;
            }

            BaseResourceType pileType = safePile.ResourceType;
            safePile.ReleaseHaulReservation(this);
            int pickedUpAmount = safePile.TakeAmount(CarryCapacity);
            _targetHaulPile = null;
            _targetPoint = null;

            if (pickedUpAmount <= 0)
            {
                ResetHaulState(false);
                return true;
            }

            _carriedResourceType = pileType;
            _carriedResourceAmount = pickedUpAmount;
            _isCarryingResource = true;
            Vector2I currentCell = buildManager.WorldToCell(mercenary.GlobalPosition);

            if (!buildManager.TryFindNearestStorageAccessWithSpace(currentCell, pileType, out Vector2I storageCell, out Vector2I accessCell))
            {
                DropCarriedResourceAtCurrentCell(mercenary);
                ResetHaulState(false);
                return true;
            }

            _targetStorageCell = storageCell;
            _targetStorageAccessCell = accessCell;
            _hasPathToTarget = false;

            if (DebugHauling)
            {
                GD.Print($"{mercenary.MercenaryName} picked up {pileType} x{pickedUpAmount}, delivering to Storage {storageCell} via {accessCell}");
            }

            return false;
        }

        Vector2I deliveryCell = buildManager.WorldToCell(mercenary.GlobalPosition);

        if (!_targetStorageCell.HasValue
            || !_targetStorageAccessCell.HasValue
            || buildManager.GetStorageFreeSpace(_targetStorageCell.Value, _carriedResourceType) <= 0)
        {
            if (!buildManager.TryFindNearestStorageAccessWithSpace(deliveryCell, _carriedResourceType, out Vector2I storageCell, out Vector2I accessCell))
            {
                DropCarriedResourceAtCurrentCell(mercenary);
                ResetHaulState(false);
                return true;
            }

            _targetStorageCell = storageCell;
            _targetStorageAccessCell = accessCell;
            _hasPathToTarget = false;
            return false;
        }

        if (deliveryCell != _targetStorageAccessCell.Value
            && GetManhattanDistance(deliveryCell, _targetStorageCell.Value) > 1)
        {
            DropCarriedResourceAtCurrentCell(mercenary);
            ResetHaulState(false);
            return true;
        }

        buildManager.TryAddResourceToStorage(_targetStorageCell.Value, _carriedResourceType, _carriedResourceAmount, out int storedAmount, out int leftoverAmount);

        if (DebugHauling && storedAmount > 0)
        {
            GD.Print($"{mercenary.MercenaryName} stored {_carriedResourceType} x{storedAmount} at Storage {_targetStorageCell.Value}");
        }

        _carriedResourceAmount = leftoverAmount;

        if (_carriedResourceAmount > 0)
        {
            DropCarriedResourceAtCurrentCell(mercenary);
        }

        ResetHaulState(false);
        return true;
    }

    private static bool IsGatherableResourceNode(ResourceNode resourceNode)
    {
        return IsValidResourceNode(resourceNode)
            && (resourceNode.ResourceType == BaseResourceType.Wood
                || resourceNode.ResourceType == BaseResourceType.Stone
                || resourceNode.ResourceType == BaseResourceType.Metal);
    }

    private static bool IsValidResourceNode(ResourceNode? resourceNode)
    {
        if (!IsSafeResourceNodeReference(resourceNode))
        {
            return false;
        }

        ResourceNode safeResourceNode = resourceNode!;
        return !safeResourceNode.IsDepleted
            && safeResourceNode.Amount > 0;
    }

    private static bool IsSafeResourceNodeReference(ResourceNode? resourceNode)
    {
        return resourceNode != null
            && GodotObject.IsInstanceValid(resourceNode)
            && !resourceNode.IsQueuedForDeletion()
            && !resourceNode.IsRemoving;
    }

    private static bool IsSafeNodeReference(Node? node)
    {
        return node != null
            && GodotObject.IsInstanceValid(node)
            && !node.IsQueuedForDeletion();
    }

    private static bool IsValidResourcePile(ResourcePile? pile)
    {
        if (!IsSafeResourcePileReference(pile))
        {
            return false;
        }

        return !pile!.IsEmpty;
    }

    private static bool IsSafeResourcePileReference(ResourcePile? pile)
    {
        return pile != null
            && GodotObject.IsInstanceValid(pile)
            && !pile.IsQueuedForDeletion()
            && !pile.IsRemoving;
    }

    private static bool IsValidHarvestCrop(CropPlant? cropPlant)
    {
        if (!IsSafeCropPlantReference(cropPlant))
        {
            return false;
        }

        return cropPlant!.IsMature;
    }

    private static bool IsSafeCropPlantReference(CropPlant? cropPlant)
    {
        return cropPlant != null
            && GodotObject.IsInstanceValid(cropPlant)
            && !cropPlant.IsQueuedForDeletion()
            && !cropPlant.IsRemoving;
    }

    private void UpdatePatrolDetection(MercenaryController mercenary, double delta)
    {
        if (!_isPatrolling)
        {
            ClearPatrolDetection();
            return;
        }

        _patrolDetectionTimer -= (float)delta;

        if (_patrolDetectionTimer > 0.0f && IsCurrentSpottedEnemyStillValid(mercenary))
        {
            return;
        }

        _patrolDetectionTimer = Mathf.Max(0.05f, PatrolDetectionInterval);

        Node2D? closestEnemy = null;
        float closestDistanceSquared = PatrolDetectionRadius * PatrolDetectionRadius;

        foreach (Node node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Node2D enemy || !GodotObject.IsInstanceValid(enemy))
            {
                continue;
            }

            if (enemy is EnemyDummyController enemyDummy && enemyDummy.IsDefeated)
            {
                continue;
            }

            float distanceSquared = mercenary.GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);

            if (distanceSquared > closestDistanceSquared)
            {
                continue;
            }

            closestDistanceSquared = distanceSquared;
            closestEnemy = enemy;
        }

        SetSpottedEnemy(closestEnemy);
        // TODO: Promote patrol detection into BaseAlertState, global spotted counts, raid alert HUD, auto rally prompts, and manual rally hints.
        // TODO: Add an optional F4/debug radius overlay once patrol detection needs spatial tuning.
    }

    private bool IsCurrentSpottedEnemyStillValid(MercenaryController mercenary)
    {
        if (!_hasSpottedEnemy || _spottedEnemy == null || !GodotObject.IsInstanceValid(_spottedEnemy))
        {
            return false;
        }

        if (_spottedEnemy is EnemyDummyController enemyDummy && enemyDummy.IsDefeated)
        {
            return false;
        }

        return mercenary.GlobalPosition.DistanceSquaredTo(_spottedEnemy.GlobalPosition) <= PatrolDetectionRadius * PatrolDetectionRadius;
    }

    private void SetSpottedEnemy(Node2D? enemy)
    {
        if (enemy == _spottedEnemy && _hasSpottedEnemy == (enemy != null))
        {
            return;
        }

        Node2D? previousEnemy = _spottedEnemy;
        _spottedEnemy = enemy;
        _hasSpottedEnemy = enemy != null;

        if (!DebugPatrolDetection)
        {
            return;
        }

        if (_hasSpottedEnemy)
        {
            GD.Print($"Enemy spotted: {GetSpottedEnemyName()}");
        }
        else if (previousEnemy != null)
        {
            GD.Print("Enemy lost");
        }
    }

    private void ClearPatrolDetection()
    {
        _patrolDetectionTimer = 0.0f;
        SetSpottedEnemy(null);
    }

    public string GetSpottedEnemyName()
    {
        if (!_hasSpottedEnemy || _spottedEnemy == null || !GodotObject.IsInstanceValid(_spottedEnemy))
        {
            return "";
        }

        return _spottedEnemy is EnemyDummyController enemyDummy
            ? enemyDummy.EnemyName
            : _spottedEnemy.Name.ToString();
    }

    private Vector2 GetFallbackTargetPosition(MercenaryController mercenary)
    {
        if (IsHaulAction())
        {
            BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

            if (_isCarryingResource && _targetStorageAccessCell.HasValue && buildManager != null)
            {
                return buildManager.CellToWorldCenter(_targetStorageAccessCell.Value);
            }

            ResourcePile? targetHaulPile = _targetHaulPile;

            if (IsSafeResourcePileReference(targetHaulPile))
            {
                return mercenary.SnapWorldToGridCenter(targetHaulPile!.GlobalPosition);
            }

            ResetHaulState(false);
            return mercenary.GlobalPosition;
        }

        if (IsPlantAction())
        {
            BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

            if (_targetPlantCell.HasValue && buildManager != null)
            {
                return buildManager.CellToWorldCenter(_targetPlantCell.Value);
            }

            ResetPlantState();
            return mercenary.GlobalPosition;
        }

        if (IsHarvestCropAction())
        {
            CropPlant? targetHarvestCrop = _targetHarvestCrop;

            if (IsSafeCropPlantReference(targetHarvestCrop))
            {
                return mercenary.SnapWorldToGridCenter(targetHarvestCrop!.GlobalPosition);
            }

            ResetHarvestCropState();
            return mercenary.GlobalPosition;
        }

        if (IsGatherAction())
        {
            ResourceNode? targetResourceNode = _targetResourceNode;

            if (IsSafeResourceNodeReference(targetResourceNode))
            {
                return mercenary.SnapWorldToGridCenter(targetResourceNode!.GlobalPosition);
            }

            ResetGatherState();
            return mercenary.GlobalPosition;
        }

        if (_usingFacilityTarget)
        {
            return _targetFacility.WorldPosition;
        }

        Node2D? targetPoint = _targetPoint;
        return IsSafeNodeReference(targetPoint)
            ? mercenary.SnapWorldToGridCenter(targetPoint!.GlobalPosition)
            : mercenary.GlobalPosition;
    }

    private static FacilityType GetPreferredFacilityType(string lifeAction)
    {
        return lifeAction switch
        {
            "Sleep" => FacilityType.Bed,
            "Guard" => FacilityType.GuardPost,
            _ => FacilityType.None
        };
    }

    private void UpdateSleepDecay(double delta)
    {
        SleepNeed = Mathf.Clamp(SleepNeed - SleepDecayPerSecond * (float)delta, 0.0f, MaxSleepNeed);
    }

    private void UpdateHungerDecay(double delta)
    {
        HungerNeed = Mathf.Clamp(HungerNeed - HungerDecayPerSecond * (float)delta, 0.0f, MaxHungerNeed);
    }

    private void UpdateStarvation(double delta)
    {
        if (HungerNeed <= 0.0f)
        {
            _starvingSeconds += (float)delta;
        }
        else
        {
            if (_isStarving && DebugLifeNeeds)
            {
                GD.Print("Starving cleared");
            }

            _starvingSeconds = 0.0f;
            _isStarving = false;
            return;
        }

        bool nextIsStarving = _starvingSeconds >= StarvationDelaySeconds;

        if (nextIsStarving && !_isStarving && DebugLifeNeeds)
        {
            GD.Print("Starving started");
        }

        _isStarving = nextIsStarving;
        // TODO: Add HP damage over time, Downed transition, Death, Mood penalty, Work speed penalty, Emergency food seeking, and Alert log.
    }

    private bool UpdateSleepRecovery(double delta)
    {
        if (!IsSleepingInBed())
        {
            return false;
        }

        SleepNeed = Mathf.Clamp(SleepNeed + SleepRecoverPerSecond * (float)delta, 0.0f, MaxSleepNeed);

        if (SleepNeed < MaxSleepNeed * 0.95f)
        {
            return false;
        }

        if (DebugLifeNeeds)
        {
            GD.Print($"SleepNeed recovered ({SleepNeed:0.0}), ending Sleep");
        }

        return true;
    }

    private bool UpdateHungerRecovery(MercenaryController mercenary, double delta, out bool canRecoverHunger)
    {
        canRecoverHunger = false;

        if (!IsEatingAtPoint())
        {
            return false;
        }

        if (!TryConsumeFoodForCurrentEat(mercenary))
        {
            return false;
        }

        canRecoverHunger = true;
        HungerNeed = Mathf.Clamp(HungerNeed + HungerRecoverPerSecond * (float)delta, 0.0f, MaxHungerNeed);

        if (HungerNeed < MaxHungerNeed * 0.95f)
        {
            return false;
        }

        if (DebugLifeNeeds)
        {
            GD.Print($"HungerNeed recovered ({HungerNeed:0.0}), ending Eat");
        }

        return true;
    }

    private bool TryConsumeFoodForCurrentEat(MercenaryController mercenary)
    {
        if (_hasConsumedFoodForCurrentEat)
        {
            return true;
        }

        BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

        if (buildManager == null || !buildManager.TryConsumeFood())
        {
            if (DebugLifeNeeds && !_hasReportedNoFoodForCurrentEat)
            {
                GD.Print($"{mercenary.MercenaryName} has no food for Eat");
            }

            _hasReportedNoFoodForCurrentEat = true;
            return false;
        }

        _hasConsumedFoodForCurrentEat = true;
        _hasReportedNoFoodForCurrentEat = false;

        if (DebugLifeNeeds)
        {
            GD.Print($"{mercenary.MercenaryName} consumed food for Eat. Food left: {buildManager.GetFoodCount()}");
        }

        return true;
    }

    private bool IsSleepingInBed()
    {
        return CurrentLifeAction == "Sleep" && IsUsingFacility(FacilityType.Bed);
    }

    private bool IsEatingAtPoint()
    {
        return CurrentLifeAction == "Eat";
    }

    private bool IsGatherAction()
    {
        return CurrentLifeAction == "Gather";
    }

    private bool IsHaulAction()
    {
        return CurrentLifeAction == "Haul";
    }

    private bool IsPlantAction()
    {
        return CurrentLifeAction == "Plant";
    }

    private bool IsHarvestCropAction()
    {
        return CurrentLifeAction == "HarvestCrop";
    }

    private bool IsUsingFacility(FacilityType facilityType)
    {
        return CurrentOccupiedFacilityType == facilityType;
    }

    private Node2D? GetPriorityNeedPoint()
    {
        Node2D? sleepPoint = GetLifePointByAction("Sleep");
        Node2D? eatPoint = GetLifePointByAction("Eat");

        if (sleepPoint != null && eatPoint != null && IsSleepCritical && IsHungerCritical)
        {
            float sleepRatio = MaxSleepNeed <= 0.0f ? 1.0f : SleepNeed / MaxSleepNeed;
            float hungerRatio = MaxHungerNeed <= 0.0f ? 1.0f : HungerNeed / MaxHungerNeed;
            return hungerRatio <= sleepRatio
                ? ChooseNeedPoint(eatPoint, $"HungerNeed critical ({HungerNeed:0.0}), choosing Eat")
                : ChooseNeedPoint(sleepPoint, $"SleepNeed critical ({SleepNeed:0.0}), choosing Sleep");
        }

        if (eatPoint != null && IsHungerCritical)
        {
            return ChooseNeedPoint(eatPoint, $"HungerNeed critical ({HungerNeed:0.0}), choosing Eat");
        }

        if (sleepPoint != null && IsSleepCritical)
        {
            return ChooseNeedPoint(sleepPoint, $"SleepNeed critical ({SleepNeed:0.0}), choosing Sleep");
        }

        if (eatPoint != null && IsHungerUrgent && _random.Randf() <= 0.75f)
        {
            return ChooseNeedPoint(eatPoint, $"HungerNeed low ({HungerNeed:0.0}), choosing Eat");
        }

        if (sleepPoint != null && IsSleepUrgent && _random.Randf() <= 0.75f)
        {
            return ChooseNeedPoint(sleepPoint, $"SleepNeed low ({SleepNeed:0.0}), choosing Sleep");
        }

        return null;
    }

    private Node2D ChooseNeedPoint(Node2D point, string debugMessage)
    {
        if (DebugLifeNeeds)
        {
            GD.Print(debugMessage);
        }

        return point;
    }

    private void ResetCurrentEatFood()
    {
        _hasConsumedFoodForCurrentEat = false;
        _hasReportedNoFoodForCurrentEat = false;
    }

    private void ResetGatherState()
    {
        ResourceNode? targetResourceNode = _targetResourceNode;

        if (IsSafeResourceNodeReference(targetResourceNode))
        {
            ResourceNode safeTargetResourceNode = targetResourceNode!;

            if (DebugGathering && safeTargetResourceNode.IsReservedBy(this))
            {
                GD.Print($"{Name} gather reservation released: {safeTargetResourceNode.GetDisplayName()}");
            }

            safeTargetResourceNode.ReleaseReservation(this);
        }

        _targetResourceNode = null;
        if (CurrentLifeAction == "Gather")
        {
            _targetPoint = null;
        }

        _currentGatherTargetLabel = "-";
        _isGathering = false;
        _gatherWorkTimer = 0.0f;
        _hasReportedGatherFailure = false;
    }

    private void ResetPlantState()
    {
        _targetPlantCell = null;
        _plantWorkTimer = 0.0f;

        if (CurrentLifeAction == "Plant")
        {
            _targetPoint = null;
        }
    }

    private void ResetHarvestCropState()
    {
        CropPlant? targetHarvestCrop = _targetHarvestCrop;

        if (IsSafeCropPlantReference(targetHarvestCrop))
        {
            targetHarvestCrop!.ReleaseHarvestReservation(this);
        }

        _targetHarvestCrop = null;
        _targetHarvestCropLabel = "-";
        _harvestCropWorkTimer = 0.0f;

        if (CurrentLifeAction == "HarvestCrop")
        {
            _targetPoint = null;
        }
    }

    private void ResetHaulState(bool dropCarriedResource, MercenaryController? mercenary = null)
    {
        ResourcePile? targetHaulPile = _targetHaulPile;

        if (IsSafeResourcePileReference(targetHaulPile))
        {
            targetHaulPile!.ReleaseHaulReservation(this);
        }

        if (dropCarriedResource && mercenary != null)
        {
            DropCarriedResourceAtCurrentCell(mercenary);
        }

        _targetHaulPile = null;
        _targetHaulPileLabel = "-";
        _targetStorageCell = null;
        _targetStorageAccessCell = null;
        _isCarryingResource = false;
        _isHaulingToStorage = false;
        _carriedResourceAmount = 0;

        if (CurrentLifeAction == "Haul")
        {
            _targetPoint = null;
        }
    }

    private void DropCarriedResourceAtCurrentCell(MercenaryController mercenary)
    {
        if (!_isCarryingResource || _carriedResourceAmount <= 0)
        {
            return;
        }

        BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

        if (buildManager == null)
        {
            return;
        }

        Vector2I currentCell = buildManager.WorldToCell(mercenary.GlobalPosition);
        buildManager.TrySpawnOrMergeResourcePile(_carriedResourceType, currentCell, _carriedResourceAmount);

        if (DebugHauling)
        {
            GD.Print($"{mercenary.MercenaryName} dropped {_carriedResourceType} x{_carriedResourceAmount} at {currentCell}");
        }
    }

    private string GetHaulingTargetLabel()
    {
        if (_isCarryingResource)
        {
            return _targetStorageCell.HasValue
                ? $"Delivering to Storage {_targetStorageCell.Value}"
                : "Delivering to Storage";
        }

        return _targetHaulPileLabel == "-" ? "Moving to pile" : $"Moving to {_targetHaulPileLabel}";
    }

    private string GetFarmingTargetLabel()
    {
        if (IsPlantAction())
        {
            return _targetPlantCell.HasValue ? $"Planting at {_targetPlantCell.Value}" : "Planting crop";
        }

        if (IsHarvestCropAction())
        {
            return _targetHarvestCropLabel == "-" ? "Harvesting crop" : $"Harvesting {_targetHarvestCropLabel}";
        }

        return "-";
    }

    private static int GetManhattanDistance(Vector2I first, Vector2I second)
    {
        return Mathf.Abs(first.X - second.X) + Mathf.Abs(first.Y - second.Y);
    }

    private Node2D? GetLifePointByAction(string actionName)
    {
        foreach (Node2D lifePoint in _lifePoints)
        {
            if (GetActionName(lifePoint.Name) == actionName)
            {
                return lifePoint;
            }
        }

        return null;
    }

    private bool TryOccupyReservedFacility(MercenaryController mercenary)
    {
        if (!_reservedFacilityCell.HasValue || _reservedBuildManager == null)
        {
            return true;
        }

        if (!_reservedBuildManager.TryOccupyFacility(_reservedFacilityCell.Value, mercenary))
        {
            if (DebugLifeFacilityChoice)
            {
                GD.Print($"{mercenary.MercenaryName} failed to occupy facility at {_reservedFacilityCell.Value}");
            }

            return false;
        }

        _occupiedFacilityCell = _reservedFacilityCell;
        _reservedFacilityCell = null;

        if (CurrentLifeAction == "Guard" && CurrentOccupiedFacilityType == FacilityType.GuardPost)
        {
            _lastPatrolFacilityCell = _occupiedFacilityCell;
            _isPatrolling = true;
        }

        if (DebugLifeFacilityChoice)
        {
            GD.Print($"{mercenary.MercenaryName} occupied facility at {_occupiedFacilityCell.Value}");
        }

        return true;
    }

    private void ReleaseCurrentFacilityReservation()
    {
        if (_reservedFacilityCell.HasValue && _reservedBuildManager != null && _reservedMercenary != null)
        {
            _reservedBuildManager.ReleaseFacilityReservation(_reservedFacilityCell.Value, _reservedMercenary);
        }

        _reservedFacilityCell = null;
    }

    private void ReleaseCurrentFacilityOccupancy()
    {
        if (_occupiedFacilityCell.HasValue && _reservedBuildManager != null && _reservedMercenary != null)
        {
            _reservedBuildManager.ReleaseFacilityOccupancy(_occupiedFacilityCell.Value, _reservedMercenary);
        }

        _occupiedFacilityCell = null;
    }

    private void ReleaseCurrentFacilityUse()
    {
        ReleaseCurrentFacilityReservation();
        ReleaseCurrentFacilityOccupancy();
        _reservedBuildManager = null;
        _reservedMercenary = null;
    }

    private static bool UpdateDirectLifeMovement(MercenaryController mercenary, Vector2 targetPosition, double delta)
    {
        Vector2 toTarget = targetPosition - mercenary.GlobalPosition;
        float distance = toTarget.Length();
        float step = mercenary.MoveSpeed * (float)delta;

        if (distance <= step)
        {
            mercenary.GlobalPosition = targetPosition;
            return true;
        }

        mercenary.GlobalPosition += toTarget.Normalized() * step;
        return false;
    }

    private static string GetActionName(StringName pointName)
    {
        string name = pointName.ToString();
        return name.EndsWith("Point") ? name[..^5] : name;
    }
}
