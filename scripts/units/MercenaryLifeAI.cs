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
	public bool EnableConditionTick { get; set; } = true;

	[Export]
	public float HygieneDecayRate { get; set; } = 0.01f;

	[Export]
	public float WorkStressRate { get; set; } = 0.015f;

	[Export]
	public float RestStressRecoveryRate { get; set; } = 0.025f;

	[Export]
	public float MoodChangeRate { get; set; } = 0.01f;

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
	public float EatDurationSeconds { get; set; } = 4.0f;

	[Export]
	public float EatHungerRecover { get; set; } = 45.0f;

	[Export]
	public float EatMoodRecover { get; set; } = 5.0f;

	[Export]
	public float EatStressRecover { get; set; } = 3.0f;

	[Export]
	public float SleepTargetSleepiness { get; set; } = 20.0f;

	[Export]
	public float SleepStressRecoverPerSecond { get; set; } = 1.0f;

	[Export]
	public float SleepMoodRecoverPerSecond { get; set; } = 0.5f;

	[Export]
	public float RelaxStressRecoverPerSecond { get; set; } = 4.0f;

	[Export]
	public float RelaxMoodRecoverPerSecond { get; set; } = 1.0f;

	[Export]
	public float RelaxMinSeconds { get; set; } = 5.0f;

	[Export]
	public float RelaxEndStressThreshold { get; set; } = 30.0f;

	[Export]
	public float RelaxEndMoodThreshold { get; set; } = 55.0f;

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
	public bool IsDepositingToStorage => _isDepositingToStorage;
	public Vector2I? CurrentStorageInteractionCell => _reservedStorageInteractionCell;
	public bool IsConstructionInteraction => IsConstructionAction();
	public string CarryingResourceLabel => _isCarryingResource ? $"{_carriedResourceType} {_carriedResourceAmount}" : "-";
	public string HaulingTargetLabel => GetHaulingTargetLabel();
	public string FarmingTargetLabel => GetFarmingTargetLabel();
	public MercenaryWorkType CurrentWorkType { get; private set; } = MercenaryWorkType.Rest;
	public string CurrentWorkLabel { get; private set; } = "\uB300\uAE30";
	public string CurrentTargetLabel { get; private set; } = "\uC5C6\uC74C";
	public string CurrentDecisionReason { get; private set; } = "\uAC00\uB2A5\uD55C \uC791\uC5C5 \uC5C6\uC74C";
	public string CurrentStateLabel { get; private set; } = "\uD560 \uC77C \uC5C6\uC74C";
	public string CurrentWorkSummary => CurrentTargetLabel == "\uC5C6\uC74C"
		? CurrentWorkLabel
		: $"{CurrentWorkLabel} / {CurrentTargetLabel}";
	public float CurrentUseProgress { get; private set; }
	public float CurrentUseDuration { get; private set; }
	public string CurrentUseProgressLabel { get; private set; } = "";
	public string CurrentRoomEffectLabel { get; private set; } = "";
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
	private Vector2I? _targetFurnitureOriginCell;
	private Vector2I? _targetFurnitureAccessCell;
	private Vector2I? _reservedFurnitureOriginCell;
	private TileBuildType _targetFurnitureType = TileBuildType.None;
	private FurnitureUseType? _targetFurnitureUseType;
	private FurnitureUseType? _reservedFurnitureUseType;
	private float _eatWorkTimer;
	private float _relaxWorkTimer;
	private BaseResourceType _carriedResourceType = BaseResourceType.Wood;
	private int _carriedResourceAmount;
	private bool _isCarryingResource;
	private bool _isHaulingToStorage;
	private bool _isDepositingToStorage;
	private bool _preserveCarriedResourceOnDepositFailure;
	private float _storageInteractionTimer;
	private float _storageInteractionDuration;
	private Vector2I? _reservedStorageInteractionCell;
	private ConstructionSite? _targetConstructionSite;
	private BaseResourceType _constructionMaterialType = BaseResourceType.Wood;
	private int _constructionMaterialAmount;
	private Vector2I? _constructionStorageCell;
	private Vector2I? _constructionStorageAccessCell;
	private Vector2I? _constructionSiteAccessCell;
	private float _constructionInteractionTimer;
	private float _constructionInteractionDuration;
	private CraftJob? _targetCraftJob;
	private BaseResourceType _craftMaterialType = BaseResourceType.Wood;
	private int _craftMaterialAmount;
	private Vector2I? _craftStorageCell;
	private Vector2I? _craftStorageAccessCell;
	private Vector2I? _craftFacilityAccessCell;
	private float _craftInteractionTimer;
	private float _craftInteractionDuration;
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

	private readonly struct RoomUseBonus
	{
		public RoomUseBonus(
			bool hasEffect,
			RoomType roomType,
			string effectLabel,
			float hungerRecoverMultiplier,
			float sleepRecoverMultiplier,
			float stressRecoverMultiplier,
			float moodRecoverMultiplier,
			float flatMoodRecoverBonus,
			float flatStressRecoverBonus)
		{
			HasEffect = hasEffect;
			RoomType = roomType;
			EffectLabel = effectLabel;
			HungerRecoverMultiplier = hungerRecoverMultiplier;
			SleepRecoverMultiplier = sleepRecoverMultiplier;
			StressRecoverMultiplier = stressRecoverMultiplier;
			MoodRecoverMultiplier = moodRecoverMultiplier;
			FlatMoodRecoverBonus = flatMoodRecoverBonus;
			FlatStressRecoverBonus = flatStressRecoverBonus;
		}

		public bool HasEffect { get; }
		public RoomType RoomType { get; }
		public string EffectLabel { get; }
		public float HungerRecoverMultiplier { get; }
		public float SleepRecoverMultiplier { get; }
		public float StressRecoverMultiplier { get; }
		public float MoodRecoverMultiplier { get; }
		public float FlatMoodRecoverBonus { get; }
		public float FlatStressRecoverBonus { get; }

		public static RoomUseBonus None => new(false, RoomType.None, "", 1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f);
	}

	private bool CanContinueWithoutTargetPoint()
	{
		return IsFurnitureUseAction()
			|| IsHaulAction()
			|| IsConstructionAction()
			|| IsCraftAction()
			|| IsPlantAction()
			|| IsHarvestCropAction()
			|| IsGatherAction();
	}

	public override void _Ready()
	{
		_random.Randomize();
	}

	public override void _ExitTree()
	{
		ReleaseReservedFurniture(GetParent() as MercenaryController);
	}

	public void SetLifePoints(IEnumerable<Node2D> lifePoints)
	{
		// Eat/Sleep/Relax are furniture-based; legacy need markers remain only as non-recovery fallbacks.
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
		ResetFurnitureUseState(mercenary);
		ResetGatherState();
		ResetHaulState(true, mercenary);
		ResetCraftState(mercenary, false);
		ResetPlantState();
		ResetHarvestCropState();
		_isPatrolling = false;
		ClearPatrolDetection();
		CurrentLifeAction = "Idle";
		SetIdleWorkStatus("\uC218\uB3D9 \uC815\uC9C0");
	}

	public void ResumeLifeAI()
	{
		if (_lifePoints.Count == 0)
		{
			CurrentLifeAction = "Idle";
			SetIdleWorkStatus("\uAC00\uB2A5\uD55C \uC791\uC5C5 \uC5C6\uC74C");
			return;
		}

		PickNextLifePoint();
	}

	public MercenaryOrderState UpdateLifeAI(MercenaryController mercenary, double delta)
	{
		UpdateSleepDecay(delta);
		UpdateHungerDecay(delta);
		UpdateConditionTick(mercenary, delta);
		UpdateStarvation(delta);

		if (_targetPoint == null && !CanContinueWithoutTargetPoint())
		 {
			 PickNextLifePoint();
			 if (CurrentLifeAction == "Idle")
			 {
				 return MercenaryOrderState.None;
			 }

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
				bool isFurnitureUseAction = IsFurnitureUseAction();
				bool isConstructionAction = IsConstructionAction();
				bool isCraftAction = IsCraftAction();
				bool hasFurnitureUsePath = isFurnitureUseAction && TryStartFurnitureUsePath(mercenary, buildManager);
				bool hasHaulPath = !isFurnitureUseAction && isHaulAction && TryStartHaulPath(mercenary, buildManager);
				bool hasConstructionPath = !isFurnitureUseAction && !isHaulAction && isConstructionAction && TryStartConstructionPath(mercenary, buildManager);
				bool hasCraftPath = !isFurnitureUseAction && !isHaulAction && !isConstructionAction && isCraftAction && TryStartCraftPath(mercenary, buildManager);
				bool hasPlantPath = !isFurnitureUseAction && !isHaulAction && !isConstructionAction && !isCraftAction && isPlantAction && TryStartPlantPath(mercenary, buildManager);
				bool hasHarvestCropPath = !isFurnitureUseAction && !isHaulAction && !isConstructionAction && !isCraftAction && !isPlantAction && isHarvestCropAction && TryStartHarvestCropPath(mercenary, buildManager);
				bool hasGatherPath = !isFurnitureUseAction && !isHaulAction && !isConstructionAction && !isCraftAction && !isPlantAction && !isHarvestCropAction && isGatherAction && TryStartGatherPath(mercenary, buildManager);
				bool hasFacilityPath = !isFurnitureUseAction && !isHaulAction && !isConstructionAction && !isCraftAction && !isPlantAction && !isHarvestCropAction && !isGatherAction && TryStartFacilityPath(mercenary, buildManager);
				bool hasFallbackPath = hasHaulPath
					|| hasFurnitureUsePath
					|| hasConstructionPath
					|| hasCraftPath
					|| hasPlantPath
					|| hasHarvestCropPath
					|| hasGatherPath
					|| hasFacilityPath
					|| (!isFurnitureUseAction && !isHaulAction && !isConstructionAction && !isCraftAction && !isPlantAction && !isHarvestCropAction && !isGatherAction && mercenary.TryMoveToWorldWithPath(targetPosition, buildManager));

		if (!hasFallbackPath)
				{
					_waitTimer += (float)delta;

					if (_waitTimer >= WaitTimeAtPoint)
					{
						PickNextLifePoint();
					}

					return MercenaryOrderState.LifeWaiting;
				}

				if (!hasFacilityPath && !hasFurnitureUsePath)
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

		if (IsFurnitureUseAction())
		{
			if (UpdateFurnitureUseAtArrival(mercenary, delta))
			{
				PickNextLifePoint();
				return MercenaryOrderState.LifeMoving;
			}

			return MercenaryOrderState.LifeWaiting;
		}

		if (IsHaulAction())
		{
			if (UpdateHaulAtArrival(mercenary, delta))
			{
				PickNextLifePoint();
				return MercenaryOrderState.LifeMoving;
			}

			return MercenaryOrderState.LifeWaiting;
		}

		if (IsConstructionAction())
		{
			if (UpdateConstructionAtArrival(mercenary, delta))
			{
				PickNextLifePoint();
				return MercenaryOrderState.LifeMoving;
			}

			return MercenaryOrderState.LifeWaiting;
		}

		if (IsCraftAction())
		{
			if (UpdateCraftAtArrival(mercenary, delta))
			{
				PickNextLifePoint();
				return MercenaryOrderState.LifeMoving;
			}

			return MercenaryOrderState.LifeWaiting;
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
		MercenaryController? mercenary = GetParent() as MercenaryController;
		BaseBuildManager? buildManager = GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");
		ReleaseCurrentFacilityUse();
		ResetCurrentEatFood();
		ResetFurnitureUseState(mercenary);
		ResetGatherState();
		ResetConstructionState(mercenary, false);
		ResetCraftState(mercenary, false);
		ResetPlantState();
		ResetHarvestCropState();

		if (_isCarryingResource)
		{
			_targetPoint = null;
			CurrentLifeAction = "Haul";
			SetCurrentWorkStatus(MercenaryWorkType.Haul, "\uCC3D\uACE0", "\uC790\uC6D0 \uBC30\uC1A1 \uC911", "\uC774\uBBF8 \uC6B4\uBC18 \uC911\uC774\uB77C \uBC30\uC1A1 \uACC4\uC18D");
			_waitTimer = 0.0f;
			_hasPathToTarget = false;
			_usingFacilityTarget = false;
			_isPatrolling = false;
			ClearPatrolDetection();
			return;
		}

		if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Haul)
			&& TrySelectInventoryDepositWork(mercenary, buildManager))
		{
			return;
		}

		List<Node2D> selectableLifePoints = GetSelectableLifePoints();

		Node2D? previousPoint = _targetPoint;
		Node2D? priorityPoint = GetPriorityNeedPoint();
		float haulChance = GetAdjustedWorkChance(mercenary, MercenaryWorkType.Haul, HaulChance);
		float farmChance = GetAdjustedWorkChance(mercenary, MercenaryWorkType.Farm, FarmWorkChance);
		float gatherChance = GetAdjustedWorkChance(mercenary, MercenaryWorkType.Gather, GatherChance);

		if (buildManager != null
			&& mercenary != null
			&& TrySelectNeedFurnitureAction(mercenary, buildManager, previousPoint))
		{
			return;
		}

		if (priorityPoint != null)
		{
			_targetPoint = priorityPoint;
		}
		else if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Haul)
			&& _random.Randf() <= haulChance
			&& buildManager.FindNearestHaulablePile(buildManager.WorldToCell(mercenary.GlobalPosition), this) != null)
		{
			_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
				?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
			CurrentLifeAction = "Haul";
			SetCurrentWorkStatus(MercenaryWorkType.Haul, "\uC790\uC6D0 \uB354\uBBF8", "\uC790\uC6D0 \uC6B4\uBC18 \uC900\uBE44", GetWorkDecisionReason(mercenary, MercenaryWorkType.Haul));
			_waitTimer = 0.0f;
			_hasPathToTarget = false;
			_usingFacilityTarget = false;
			_isPatrolling = false;
			ClearPatrolDetection();
			return;
		}
		else if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Build)
			&& TrySelectConstructionBuildWork(mercenary, buildManager, previousPoint, selectableLifePoints))
		{
			return;
		}
		else if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Haul)
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Build)
			&& TrySelectConstructionMaterialDelivery(mercenary, buildManager, previousPoint, selectableLifePoints))
		{
			return;
		}
		else if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Craft)
			&& TrySelectCraftOutputPickup(mercenary, buildManager, previousPoint, selectableLifePoints))
		{
			return;
		}
		else if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Craft)
			&& TrySelectCraftWork(mercenary, buildManager, previousPoint, selectableLifePoints))
		{
			return;
		}
		else if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Craft)
			&& TrySelectCraftMaterialDelivery(mercenary, buildManager, previousPoint, selectableLifePoints))
		{
			return;
		}
		else if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Farm)
			&& buildManager.FindNearestHarvestableCrop(buildManager.WorldToCell(mercenary.GlobalPosition), this) != null)
		{
			_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
				?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
			CurrentLifeAction = "HarvestCrop";
			SetCurrentWorkStatus(MercenaryWorkType.Farm, "\uC218\uD655", "\uB18D\uC9C0 \uC791\uC5C5 \uC911", GetWorkDecisionReason(mercenary, MercenaryWorkType.Farm));
			_waitTimer = 0.0f;
			_hasPathToTarget = false;
			_usingFacilityTarget = false;
			_isPatrolling = false;
			ClearPatrolDetection();
			return;
		}
		else if (buildManager != null
			&& mercenary != null
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Farm)
			&& _random.Randf() <= farmChance
			&& buildManager.TryFindNearestPlantableFarmCell(buildManager.WorldToCell(mercenary.GlobalPosition), out Vector2I plantCell))
		{
			_targetPlantCell = plantCell;
			_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
				?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
			CurrentLifeAction = "Plant";
			SetCurrentWorkStatus(MercenaryWorkType.Farm, "\uC2EC\uAE30", "\uB18D\uC9C0\uB85C \uC774\uB3D9 \uC911", GetWorkDecisionReason(mercenary, MercenaryWorkType.Farm));
			_waitTimer = 0.0f;
			_hasPathToTarget = false;
			_usingFacilityTarget = false;
			_isPatrolling = false;
			ClearPatrolDetection();
			return;
		}
		else if (IsWorkEnabled(mercenary, MercenaryWorkType.Gather) && _random.Randf() <= gatherChance)
		{
			_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
				?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
			CurrentLifeAction = "Gather";
			SetCurrentWorkStatus(MercenaryWorkType.Gather, "\uC790\uC6D0 \uB178\uB4DC", "\uC790\uC6D0 \uCC44\uC9D1 \uC900\uBE44", GetWorkDecisionReason(mercenary, MercenaryWorkType.Gather));
			_waitTimer = 0.0f;
			_hasPathToTarget = false;
			_usingFacilityTarget = false;
			_isPatrolling = false;
			ClearPatrolDetection();
			return;
		}
		else if (selectableLifePoints.Count == 0)
		{
			_targetPoint = null;
			CurrentLifeAction = "Idle";
			SetIdleWorkStatus("\uC0DD\uD65C \uC9C0\uC810 \uC5C6\uC74C");
			_isPatrolling = false;
			ClearPatrolDetection();
			return;
		}
		else if (selectableLifePoints.Count == 1)
		{
			_targetPoint = selectableLifePoints[0];
		}
		else
		{
			do
			{
				_targetPoint = selectableLifePoints[_random.RandiRange(0, selectableLifePoints.Count - 1)];
			}
			while (_targetPoint == previousPoint);
		}

		_waitTimer = 0.0f;
		_hasPathToTarget = false;
		_usingFacilityTarget = false;
		CurrentLifeAction = _targetPoint == null ? "Idle" : GetActionName(_targetPoint.Name);
		_isPatrolling = CurrentLifeAction == "Guard";

		if (_isPatrolling && !IsWorkEnabled(mercenary, MercenaryWorkType.Guard))
		{
			_targetPoint = null;
			CurrentLifeAction = "Idle";
			_isPatrolling = false;
		}

		if (_isPatrolling)
		{
			SetCurrentWorkStatus(MercenaryWorkType.Guard, "\uC21C\uCC30 \uC9C0\uC810", "\uC21C\uCC30 \uC911", GetWorkDecisionReason(mercenary, MercenaryWorkType.Guard));
		}
		else if (CurrentLifeAction == "Idle")
		{
			SetIdleWorkStatus("\uAC00\uB2A5\uD55C \uC791\uC5C5 \uC5C6\uC74C");
		}
		else
		{
			SetCurrentWorkStatus(MercenaryWorkType.Rest, CurrentLifeAction, "\uC0DD\uD65C \uD589\uB3D9 \uC911", "\uD544\uC694/\uC0DD\uD65C \uD589\uB3D9 \uC120\uD0DD");
		}

		if (!_isPatrolling)
		{
			ClearPatrolDetection();
		}
	}

	private static bool IsWorkEnabled(MercenaryController? mercenary, MercenaryWorkType workType)
	{
		return mercenary?.Profile.GetWorkSettings().IsEnabled(workType) ?? true;
	}

	private static float GetAdjustedWorkChance(MercenaryController? mercenary, MercenaryWorkType workType, float baseChance)
	{
		float multiplier = mercenary?.Profile.GetWorkSettings().GetChanceMultiplier(workType) ?? 1.0f;
		return Mathf.Clamp(baseChance * multiplier, 0.0f, 1.0f);
	}

	private bool TrySelectNeedFurnitureAction(MercenaryController mercenary, BaseBuildManager buildManager, Node2D? previousPoint)
	{
		MercenaryCondition condition = mercenary.Profile.Condition;
		Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

		if (condition.Hunger >= 85.0f && TrySelectEatFurnitureAction(mercenary, buildManager, startCell, previousPoint, "\uBC30\uACE0\uD514 \uB9E4\uC6B0 \uB192\uC74C"))
		{
			return true;
		}

		if (condition.Sleepiness >= 90.0f && TrySelectSleepFurnitureAction(mercenary, buildManager, startCell, previousPoint, "\uC878\uB9BC \uB9E4\uC6B0 \uB192\uC74C"))
		{
			return true;
		}

		if (condition.Hunger >= 65.0f && TrySelectEatFurnitureAction(mercenary, buildManager, startCell, previousPoint, "\uBC30\uACE0\uD514 \uB192\uC74C"))
		{
			return true;
		}

		if (condition.Sleepiness >= 70.0f && TrySelectSleepFurnitureAction(mercenary, buildManager, startCell, previousPoint, "\uC878\uB9BC \uB192\uC74C"))
		{
			return true;
		}

		if ((condition.Stress >= 70.0f || condition.Mood <= 30.0f)
			&& IsWorkEnabled(mercenary, MercenaryWorkType.Rest)
			&& TrySelectRelaxFurnitureAction(mercenary, buildManager, startCell, previousPoint, condition.Stress >= 70.0f ? "\uC2A4\uD2B8\uB808\uC2A4 \uB192\uC74C" : "\uAE30\uBD84 \uB0AE\uC74C"))
		{
			return true;
		}

		return false;
	}

	private bool TrySelectEatFurnitureAction(MercenaryController mercenary, BaseBuildManager buildManager, Vector2I startCell, Node2D? previousPoint, string reason)
	{
		if (buildManager.GetResourceAmount(BaseResourceType.Food) <= 0)
		{
			SetIdleWorkStatus("\uC2DD\uB7C9 \uC5C6\uC74C");
			return false;
		}

		if (!buildManager.TryFindNearestUsableFurnitureAccess(startCell, FurnitureUseType.Eat, mercenary, out Vector2I originCell, out Vector2I accessCell, out TileBuildType furnitureType))
		{
			SetIdleWorkStatus("\uC2DD\uC0AC \uAC00\uAD6C \uC5C6\uC74C");
			return false;
		}

		SelectFurnitureUseAction("EatFurniture", FurnitureUseType.Eat, originCell, accessCell, furnitureType, previousPoint);
		SetRestWorkStatusLabel("\uC2DD\uC0AC", GetFurnitureDisplayName(furnitureType), "\uC2DD\uC0AC\uD558\uB7EC \uC774\uB3D9 \uC911", reason);
		return true;
	}

	private bool TrySelectConstructionBuildWork(MercenaryController mercenary, BaseBuildManager buildManager, Node2D? previousPoint, List<Node2D> selectableLifePoints)
	{
		Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

		if (!buildManager.TryFindConstructionBuildWork(startCell, mercenary, out ConstructionSite site, out Vector2I accessCell))
		{
			return false;
		}

		if (!buildManager.TryReserveBuildWork(site, mercenary))
		{
			return false;
		}

		_targetConstructionSite = site;
		_constructionSiteAccessCell = accessCell;
		_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
			?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
		CurrentLifeAction = "ConstructionBuild";
		_waitTimer = 0.0f;
		_hasPathToTarget = false;
		_usingFacilityTarget = false;
		_isPatrolling = false;
		ClearPatrolDetection();
		SetCurrentWorkStatus(MercenaryWorkType.Build, site.DisplayName, "\uAC74\uC124 \uC900\uBE44", "\uC7AC\uB8CC \uC900\uBE44 \uC644\uB8CC");
		return true;
	}

	private bool TrySelectConstructionMaterialDelivery(MercenaryController mercenary, BaseBuildManager buildManager, Node2D? previousPoint, List<Node2D> selectableLifePoints)
	{
		Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

		if (!buildManager.TryFindConstructionMaterialDelivery(
			startCell,
			mercenary,
			out ConstructionSite site,
			out BaseResourceType resourceType,
			out int amount,
			out Vector2I storageCell,
			out Vector2I storageAccessCell,
			out Vector2I siteAccessCell))
		{
			return false;
		}

		if (!buildManager.TryReserveSiteMaterialDelivery(site, mercenary))
		{
			return false;
		}

		_targetConstructionSite = site;
		_constructionMaterialType = resourceType;
		_constructionMaterialAmount = amount;
		_constructionStorageCell = storageCell;
		_constructionStorageAccessCell = storageAccessCell;
		_constructionSiteAccessCell = siteAccessCell;
		_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
			?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
		CurrentLifeAction = "ConstructionWithdraw";
		_waitTimer = 0.0f;
		_hasPathToTarget = false;
		_usingFacilityTarget = false;
		_isPatrolling = false;
		ClearPatrolDetection();
		SetCurrentWorkStatus(MercenaryWorkType.Build, $"{BaseBuildManager.GetResourceDisplayName(resourceType)} x{amount} \u2192 {site.DisplayName}", "\uCC3D\uACE0\uB85C \uC774\uB3D9 \uC911", "\uAC74\uC124 \uC608\uC815\uC9C0 \uC7AC\uB8CC \uBD80\uC871");
		return true;
	}

	private bool TrySelectInventoryDepositWork(MercenaryController mercenary, BaseBuildManager buildManager)
	{
		if (_isCarryingResource || mercenary.Inventory.IsEmpty())
		{
			return false;
		}

		Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

		foreach (MercenaryInventoryStack stack in mercenary.Inventory.Stacks)
		{
			if (stack.Amount <= 0 || !ResourceDefinitionDatabase.IsStoredResource(stack.ResourceType))
			{
				continue;
			}

			if (!buildManager.TryFindNearestStorageAccessWithSpace(startCell, stack.ResourceType, out Vector2I storageCell, out Vector2I accessCell))
			{
				continue;
			}

			_carriedResourceType = stack.ResourceType;
			_carriedResourceAmount = stack.Amount;
			_isCarryingResource = true;
			_isHaulingToStorage = true;
			_preserveCarriedResourceOnDepositFailure = true;
			_targetStorageCell = storageCell;
			_targetStorageAccessCell = accessCell;
			_targetPoint = null;
			CurrentLifeAction = "Haul";
			_waitTimer = 0.0f;
			_hasPathToTarget = false;
			_usingFacilityTarget = false;
			_isPatrolling = false;
			ClearPatrolDetection();
			SetCurrentWorkStatus(
				MercenaryWorkType.Haul,
				BaseBuildManager.GetResourceDisplayName(stack.ResourceType),
				"\uC790\uC6D0 \uBC30\uC1A1 \uC911",
				"\uAC00\uBC29 \uC790\uC6D0 \uC785\uACE0");
			return true;
		}

		return false;
	}

	private bool TrySelectCraftOutputPickup(MercenaryController mercenary, BaseBuildManager buildManager, Node2D? previousPoint, List<Node2D> selectableLifePoints)
	{
		if (_isCarryingResource)
		{
			return false;
		}

		CraftingManager? craftingManager = GetCraftingManager();

		if (craftingManager == null)
		{
			return false;
		}

		Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);
		CraftJob? selectedJob = null;
		Vector2I selectedFacilityAccessCell = default;
		int bestPathLength = int.MaxValue;

		foreach (CraftJob job in craftingManager.GetOutputReadyJobs())
		{
			job.PruneReservations();

			if (job.ReservedOutputWorker is { } reservedWorker && reservedWorker != mercenary)
			{
				continue;
			}

			if (!craftingManager.TryGetRecipeForJob(job, out CraftRecipeEntry recipe))
			{
				continue;
			}

			if (!TryValidateCraftFacility(buildManager, job, recipe, out Vector2I facilityOriginCell))
			{
				continue;
			}

			if (!CanCarryAllCraftOutputs(mercenary, job))
			{
				continue;
			}

			if (!buildManager.TryFindObjectAccessCell(startCell, facilityOriginCell, out Vector2I facilityAccessCell, out int pathLength))
			{
				continue;
			}

			if (pathLength >= bestPathLength)
			{
				continue;
			}

			bestPathLength = pathLength;
			selectedJob = job;
			selectedFacilityAccessCell = facilityAccessCell;
		}

		if (selectedJob == null)
		{
			return false;
		}

		if (!selectedJob.TryReserveOutputPickup(mercenary))
		{
			return false;
		}

		_targetCraftJob = selectedJob;
		_craftStorageCell = null;
		_craftStorageAccessCell = null;
		_craftFacilityAccessCell = selectedFacilityAccessCell;
		_craftInteractionTimer = 0.0f;
		_craftInteractionDuration = 0.0f;
		_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
			?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
		CurrentLifeAction = "CraftPickupOutput";
		_waitTimer = 0.0f;
		_hasPathToTarget = false;
		_usingFacilityTarget = false;
		_isPatrolling = false;
		ClearPatrolDetection();
		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			GetCraftOutputLabel(selectedJob),
			"Moving to workbench",
			"Craft output ready");
		return true;
	}

	private bool TrySelectCraftWork(MercenaryController mercenary, BaseBuildManager buildManager, Node2D? previousPoint, List<Node2D> selectableLifePoints)
	{
		CraftingManager? craftingManager = GetCraftingManager();

		if (craftingManager == null)
		{
			return false;
		}

		Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);
		CraftJob? selectedJob = null;
		Vector2I selectedFacilityAccessCell = default;
		int bestPathLength = int.MaxValue;

		foreach (CraftJob job in craftingManager.GetReadyToCraftJobs())
		{
			job.PruneReservations();

			if (job.ReservedWorker is { } reservedWorker && reservedWorker != mercenary)
			{
				continue;
			}

			if (!craftingManager.TryGetRecipeForJob(job, out CraftRecipeEntry recipe))
			{
				continue;
			}

			if (!TryValidateCraftFacility(buildManager, job, recipe, out Vector2I facilityOriginCell))
			{
				craftingManager.CancelJob(job);
				continue;
			}

			if (!buildManager.TryFindObjectAccessCell(startCell, facilityOriginCell, out Vector2I facilityAccessCell, out int pathLength))
			{
				continue;
			}

			if (pathLength >= bestPathLength)
			{
				continue;
			}

			bestPathLength = pathLength;
			selectedJob = job;
			selectedFacilityAccessCell = facilityAccessCell;
		}

		if (selectedJob == null)
		{
			return false;
		}

		if (!selectedJob.TryReserveCrafting(mercenary))
		{
			return false;
		}

		_targetCraftJob = selectedJob;
		_craftStorageCell = null;
		_craftStorageAccessCell = null;
		_craftFacilityAccessCell = selectedFacilityAccessCell;
		_craftInteractionTimer = 0.0f;
		_craftInteractionDuration = 0.0f;
		_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
			?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
		CurrentLifeAction = "CraftWork";
		_waitTimer = 0.0f;
		_hasPathToTarget = false;
		_usingFacilityTarget = false;
		_isPatrolling = false;
		ClearPatrolDetection();
		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			selectedJob.RecipeId,
			"Moving to workbench",
			"Craft materials ready");
		return true;
	}

	private bool TrySelectCraftMaterialDelivery(MercenaryController mercenary, BaseBuildManager buildManager, Node2D? previousPoint, List<Node2D> selectableLifePoints)
	{
		CraftingManager? craftingManager = GetCraftingManager();

		if (craftingManager == null)
		{
			return false;
		}

		Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);
		CraftJob? selectedJob = null;
		BaseResourceType selectedResourceType = BaseResourceType.Wood;
		int selectedAmount = 0;
		Vector2I selectedStorageCell = default;
		Vector2I selectedStorageAccessCell = default;
		Vector2I selectedFacilityAccessCell = default;
		int bestPathLength = int.MaxValue;

		foreach (CraftJob job in craftingManager.GetMaterialDeliveryJobs())
		{
			job.PruneReservations();

			if (job.ReservedDeliveryWorker is { } reservedWorker && reservedWorker != mercenary)
			{
				continue;
			}

			if (!craftingManager.TryGetRecipeForJob(job, out CraftRecipeEntry recipe))
			{
				continue;
			}

			if (!TryValidateCraftFacility(buildManager, job, recipe, out Vector2I facilityOriginCell))
			{
				craftingManager.CancelJob(job);
				continue;
			}

			foreach (KeyValuePair<BaseResourceType, int> input in recipe.Inputs)
			{
				int remainingAmount = job.GetRemainingAmount(input.Key);
				int maxAddableAmount = mercenary.Inventory.GetMaxAddableAmount(input.Key);
				int amount = Mathf.Min(remainingAmount, maxAddableAmount);

				if (amount <= 0)
				{
					continue;
				}

				if (!buildManager.TryFindNearestStorageAccessWithResource(startCell, input.Key, out Vector2I storageCell, out Vector2I storageAccessCell))
				{
					continue;
				}

				if (!buildManager.TryFindObjectAccessCell(storageAccessCell, facilityOriginCell, out Vector2I facilityAccessCell, out int facilityPathLength))
				{
					continue;
				}

				List<Vector2I> storagePath = GridPathfinder.FindPath(startCell, storageAccessCell, buildManager);

				if (startCell != storageAccessCell && storagePath.Count == 0)
				{
					continue;
				}

				int storagePathLength = startCell == storageAccessCell ? 0 : storagePath.Count;
				int pathLength = storagePathLength + facilityPathLength;

				if (pathLength >= bestPathLength)
				{
					continue;
				}

				bestPathLength = pathLength;
				selectedJob = job;
				selectedResourceType = input.Key;
				selectedAmount = amount;
				selectedStorageCell = storageCell;
				selectedStorageAccessCell = storageAccessCell;
				selectedFacilityAccessCell = facilityAccessCell;
			}
		}

		if (selectedJob == null || selectedAmount <= 0)
		{
			return false;
		}

		if (!selectedJob.TryReserveDelivery(mercenary))
		{
			return false;
		}

		_targetCraftJob = selectedJob;
		_craftMaterialType = selectedResourceType;
		_craftMaterialAmount = selectedAmount;
		_craftStorageCell = selectedStorageCell;
		_craftStorageAccessCell = selectedStorageAccessCell;
		_craftFacilityAccessCell = selectedFacilityAccessCell;
		_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
			?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
		CurrentLifeAction = "CraftWithdraw";
		_waitTimer = 0.0f;
		_hasPathToTarget = false;
		_usingFacilityTarget = false;
		_isPatrolling = false;
		ClearPatrolDetection();
		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			$"{BaseBuildManager.GetResourceDisplayName(selectedResourceType)} x{selectedAmount} \u2192 {selectedJob.RecipeId}",
			"Moving to storage",
			"Craft job needs materials");
		return true;
	}

	private bool TrySelectSleepFurnitureAction(MercenaryController mercenary, BaseBuildManager buildManager, Vector2I startCell, Node2D? previousPoint, string reason)
	{
		if (!buildManager.TryFindNearestUsableFurnitureAccess(startCell, FurnitureUseType.Sleep, mercenary, out Vector2I originCell, out Vector2I accessCell, out TileBuildType furnitureType))
		{
			SetIdleWorkStatus("\uCE68\uB300 \uC5C6\uC74C");
			return false;
		}

		SelectFurnitureUseAction("SleepFurniture", FurnitureUseType.Sleep, originCell, accessCell, furnitureType, previousPoint);
		SetRestWorkStatusLabel("\uC218\uBA74", GetFurnitureDisplayName(furnitureType), "\uCE68\uB300\uB85C \uC774\uB3D9 \uC911", reason);
		return true;
	}

	private bool TrySelectRelaxFurnitureAction(MercenaryController mercenary, BaseBuildManager buildManager, Vector2I startCell, Node2D? previousPoint, string reason)
	{
		if (!buildManager.TryFindNearestUsableFurnitureAccess(startCell, FurnitureUseType.Relax, mercenary, out Vector2I originCell, out Vector2I accessCell, out TileBuildType furnitureType))
		{
			SetIdleWorkStatus("\uD734\uC2DD \uAC00\uAD6C \uC5C6\uC74C");
			return false;
		}

		SelectFurnitureUseAction("RelaxFurniture", FurnitureUseType.Relax, originCell, accessCell, furnitureType, previousPoint);
		SetRestWorkStatusLabel("\uD734\uC2DD", GetFurnitureDisplayName(furnitureType), "\uD734\uC2DD \uAC00\uAD6C\uB85C \uC774\uB3D9 \uC911", reason);
		return true;
	}

	private void SelectFurnitureUseAction(string actionName, FurnitureUseType useType, Vector2I originCell, Vector2I accessCell, TileBuildType furnitureType, Node2D? previousPoint)
	{
		_targetFurnitureOriginCell = originCell;
		_targetFurnitureAccessCell = accessCell;
		_targetFurnitureType = furnitureType;
		_targetFurnitureUseType = useType;
		List<Node2D> selectableLifePoints = GetSelectableLifePoints();
		_targetPoint = GetSafePreviousLifePoint(previousPoint, selectableLifePoints)
			?? (selectableLifePoints.Count > 0 ? selectableLifePoints[0] : null);
		CurrentLifeAction = actionName;
		_waitTimer = 0.0f;
		_eatWorkTimer = 0.0f;
		_relaxWorkTimer = 0.0f;
		ClearUseProgress();
		_hasPathToTarget = false;
		_usingFacilityTarget = false;
		_isPatrolling = false;
		ClearPatrolDetection();
	}

	private void SetCurrentWorkStatus(MercenaryWorkType workType, string targetLabel, string stateLabel, string decisionReason)
	{
		if (!IsFurnitureUseAction())
		{
			ClearUseProgress();
			ClearCurrentRoomEffect();
		}

		CurrentWorkType = workType;
		CurrentWorkLabel = workType == MercenaryWorkType.Rest
			? "\uB300\uAE30"
			: MercenaryWorkSettings.GetWorkTypeDisplayName(workType);
		CurrentTargetLabel = string.IsNullOrWhiteSpace(targetLabel) ? "\uC5C6\uC74C" : targetLabel;
		CurrentStateLabel = string.IsNullOrWhiteSpace(stateLabel) ? "\uD560 \uC77C \uC5C6\uC74C" : stateLabel;
		CurrentDecisionReason = string.IsNullOrWhiteSpace(decisionReason) ? "\uAC00\uB2A5\uD55C \uC791\uC5C5 \uC5C6\uC74C" : decisionReason;
	}

	private void SetRestWorkStatusLabel(string workLabel, string targetLabel, string stateLabel, string decisionReason)
	{
		CurrentWorkType = MercenaryWorkType.Rest;
		CurrentWorkLabel = string.IsNullOrWhiteSpace(workLabel) ? "\uD734\uC2DD" : workLabel;
		CurrentTargetLabel = string.IsNullOrWhiteSpace(targetLabel) ? "\uC5C6\uC74C" : targetLabel;
		CurrentStateLabel = string.IsNullOrWhiteSpace(stateLabel) ? "\uD560 \uC77C \uC5C6\uC74C" : stateLabel;
		CurrentDecisionReason = string.IsNullOrWhiteSpace(decisionReason) ? "\uAC00\uB2A5\uD55C \uC791\uC5C5 \uC5C6\uC74C" : decisionReason;
	}

	private void SetStorageDepositWorkStatus(string targetLabel, string stateLabel, string decisionReason)
	{
		CurrentWorkType = MercenaryWorkType.Haul;
		CurrentWorkLabel = "\uC785\uACE0";
		CurrentTargetLabel = string.IsNullOrWhiteSpace(targetLabel) ? "\uCC3D\uACE0" : targetLabel;
		CurrentStateLabel = string.IsNullOrWhiteSpace(stateLabel) ? "\uCC3D\uACE0\uC5D0 \uB123\uB294 \uC911" : stateLabel;
		CurrentDecisionReason = string.IsNullOrWhiteSpace(decisionReason) ? "\uC790\uC6D0 \uC785\uACE0" : decisionReason;
	}

	private void SetIdleWorkStatus(string reason)
	{
		ClearUseProgress();
		ClearCurrentRoomEffect();
		SetCurrentWorkStatus(MercenaryWorkType.Rest, "\uC5C6\uC74C", "\uD560 \uC77C \uC5C6\uC74C", reason);
	}

	private void SetUseProgress(float progress, float duration, string label)
	{
		CurrentUseProgress = Mathf.Max(0.0f, progress);
		CurrentUseDuration = Mathf.Max(0.0f, duration);
		CurrentUseProgressLabel = string.IsNullOrWhiteSpace(label) ? "" : label;
	}

	private void ClearUseProgress()
	{
		CurrentUseProgress = 0.0f;
		CurrentUseDuration = 0.0f;
		CurrentUseProgressLabel = "";
	}

	private void SetCurrentRoomEffect(RoomUseBonus bonus)
	{
		CurrentRoomEffectLabel = bonus.HasEffect ? bonus.EffectLabel : "";
	}

	private void ClearCurrentRoomEffect()
	{
		CurrentRoomEffectLabel = "";
	}

	public string ValidateLogisticsState(MercenaryController mercenary, bool verboseLogs)
	{
		List<string> warnings = new();

		if (!_isCarryingResource && _carriedResourceAmount != 0)
		{
			warnings.Add("Carry flag off but carried amount not zero");
			_carriedResourceAmount = 0;
		}

		if (_isCarryingResource)
		{
			int inventoryAmount = mercenary.Inventory.GetAmount(_carriedResourceType);

			if (inventoryAmount <= 0 && _carriedResourceAmount > 0)
			{
				if (mercenary.Inventory.TryAdd(_carriedResourceType, _carriedResourceAmount, out int addedAmount)
					&& addedAmount > 0)
				{
					inventoryAmount = mercenary.Inventory.GetAmount(_carriedResourceType);
					warnings.Add("Carry state synced into inventory");
				}
			}

			if (inventoryAmount <= 0)
			{
				warnings.Add("Carrying but inventory is empty");
				_isCarryingResource = false;
				_isHaulingToStorage = false;
				_carriedResourceAmount = 0;
			}
			else if (_carriedResourceAmount != inventoryAmount)
			{
				warnings.Add("Carry amount synced from inventory");
				_carriedResourceAmount = inventoryAmount;
			}
		}

		if (_isHaulingToStorage && mercenary.Inventory.IsEmpty())
		{
			warnings.Add("Haul delivery had empty inventory");
			ResetHaulState(false);
		}

		if (_isDepositingToStorage)
		{
			BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

			if (mercenary.Inventory.IsEmpty())
			{
				warnings.Add("Storage deposit had empty inventory");
				ResetHaulState(false);
			}
			else if (!_reservedStorageInteractionCell.HasValue || buildManager == null)
			{
				warnings.Add("Storage deposit missing reservation");
				ResetHaulState(false);
			}
			else if (buildManager.IsStorageInteractionReservedByOther(_reservedStorageInteractionCell.Value, mercenary))
			{
				warnings.Add("Storage deposit reservation owner mismatch");
				ResetHaulState(false);
			}
		}

		if (IsFurnitureUseAction()
			&& (!_targetFurnitureOriginCell.HasValue || !_targetFurnitureUseType.HasValue))
		{
			warnings.Add("Furniture action missing reservation target");
			ResetFurnitureUseState(mercenary);
		}

		if (IsFurnitureUseAction()
			&& _hasPathToTarget
			&& !_reservedFurnitureOriginCell.HasValue)
		{
			warnings.Add("Furniture action missing active reservation");
			ResetFurnitureUseState(mercenary);
		}

		if (!IsFurnitureUseAction() && !string.IsNullOrWhiteSpace(CurrentRoomEffectLabel))
		{
			warnings.Add("Stale room effect label cleared");
			ClearCurrentRoomEffect();
		}

		if (IsConstructionAction())
		{
			ConstructionSite? site = _targetConstructionSite;

			if (site == null || site.IsCancelled)
			{
				warnings.Add("Construction action had invalid site");
				ResetConstructionState(mercenary, true);
			}
			else if (site.IsCompleted)
			{
				ResetConstructionState(mercenary, false);
			}
			else if (IsConstructionWithdrawAction()
				&& (!_constructionStorageCell.HasValue
					|| !_reservedStorageInteractionCell.HasValue && _constructionInteractionTimer > 0.0f))
			{
				warnings.Add("Construction withdraw missing storage reservation");
				ResetConstructionState(mercenary, true);
			}
			else if (IsConstructionDeliverAction()
				&& mercenary.Inventory.GetAmount(_constructionMaterialType) <= 0)
			{
				warnings.Add("Construction delivery had empty inventory");
				ResetConstructionState(mercenary, false);
			}
			else if (IsConstructionBuildAction()
				&& !site.HasAllMaterials)
			{
				warnings.Add("Construction build started without materials");
				ResetConstructionState(mercenary, false);
			}
		}

		if (IsCraftAction())
		{
			CraftJob? job = _targetCraftJob;
			BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

			if (job == null)
			{
				warnings.Add("Craft action missing job");
				ResetCraftState(mercenary, false);
			}
			else if (job.IsCancelled || job.IsCompleted)
			{
				ResetCraftState(mercenary, false);
			}
			else if (job.State == CraftJobState.OutputReady && !IsCraftPickupOutputAction())
			{
				ResetCraftState(mercenary, false);
			}
			else if (buildManager != null && !TryValidateCraftFacility(buildManager, job, out _))
			{
				if (IsCraftPickupOutputAction())
				{
					warnings.Add("Craft output pickup had invalid facility");
				}
				else
				{
					warnings.Add("Craft action had invalid facility");
					job.Cancel();
				}

				ResetCraftState(mercenary, false);
			}
			else if (IsCraftPickupOutputAction()
				&& !job.HasProducedOutputs)
			{
				ResetCraftState(mercenary, false);
			}
			else if (IsCraftPickupOutputAction()
				&& job.ReservedOutputWorker is { } reservedOutputWorker
				&& reservedOutputWorker != mercenary)
			{
				warnings.Add("Craft output reservation owner mismatch");
				ResetCraftState(mercenary, false);
			}
			else if (IsCraftWorkAction()
				&& !job.HasAllMaterials)
			{
				warnings.Add("Craft work started without materials");
				ResetCraftState(mercenary, false);
			}
			else if (IsCraftWorkAction()
				&& job.ReservedWorker is { } reservedWorker
				&& reservedWorker != mercenary)
			{
				warnings.Add("Craft work reservation owner mismatch");
				ResetCraftState(mercenary, false);
			}
			else if (IsCraftWithdrawAction()
				&& (!_craftStorageCell.HasValue
					|| !_reservedStorageInteractionCell.HasValue && _craftInteractionTimer > 0.0f))
			{
				warnings.Add("Craft withdraw missing storage reservation");
				ResetCraftState(mercenary, false);
			}
			else if (IsCraftDeliverAction()
				&& mercenary.Inventory.GetAmount(_craftMaterialType) <= 0)
			{
				warnings.Add("Craft delivery had empty inventory");
				ResetCraftState(mercenary, false);
			}
		}

		string warning = string.Join(" / ", warnings);

		if (!string.IsNullOrWhiteSpace(warning) && verboseLogs)
		{
			GD.PushWarning($"Logistics validation warning for {mercenary.MercenaryName}: {warning}");
		}

		return warning;
	}

	private void RunLogisticsValidation(MercenaryController mercenary)
	{
		BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

		if (buildManager?.EnableLogisticsValidation != true)
		{
			return;
		}

		mercenary.ValidateLogisticsState(buildManager.EnableVerboseLogisticsLogs);
	}

	private static string GetWorkDecisionReason(MercenaryController? mercenary, MercenaryWorkType workType)
	{
		MercenaryWorkPriority priority = mercenary?.Profile.GetWorkSettings().GetPriority(workType) ?? MercenaryWorkPriority.Normal;
		return $"{MercenaryWorkSettings.GetWorkTypeDisplayName(workType)} \uC6B0\uC120\uC21C\uC704 {MercenaryWorkSettings.GetPriorityDisplayName(priority)}";
	}

	private List<Node2D> GetSelectableLifePoints()
	{
		List<Node2D> selectableLifePoints = new();

		foreach (Node2D lifePoint in _lifePoints)
		{
			if (!IsSafeNodeReference(lifePoint))
			{
				continue;
			}

			if (IsTemporaryNeedLifeAction(GetActionName(lifePoint.Name)))
			{
				continue;
			}

			selectableLifePoints.Add(lifePoint);
		}

		return selectableLifePoints;
	}

	private static Node2D? GetSafePreviousLifePoint(Node2D? previousPoint, List<Node2D> selectableLifePoints)
	{
		if (!IsSafeNodeReference(previousPoint))
		{
			return null;
		}

		return selectableLifePoints.Contains(previousPoint!) ? previousPoint : null;
	}

	private static bool IsTemporaryNeedLifeAction(string actionName)
	{
		return actionName == "Eat" || actionName == "Sleep" || actionName == "Rest";
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
		SetCurrentWorkStatus(MercenaryWorkType.Guard, "\uC21C\uCC30 \uC9C0\uC810", "\uC21C\uCC30 \uC911", "\uACBD\uBE44 \uACC4\uC18D");
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
		if (!IsWorkEnabled(mercenary, MercenaryWorkType.Guard))
		{
			_isPatrolling = false;
			ClearPatrolDetection();
			return false;
		}

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

		SetCurrentWorkStatus(MercenaryWorkType.Guard, "\uC21C\uCC30 \uC9C0\uC810", "\uC21C\uCC30 \uC911", GetWorkDecisionReason(mercenary, MercenaryWorkType.Guard));
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

	private bool TryStartConstructionPath(MercenaryController mercenary, BaseBuildManager buildManager)
	{
		ConstructionSite? site = _targetConstructionSite;

		if (site == null || site.IsCancelled || site.IsCompleted)
		{
			ResetConstructionState(mercenary, true);
			return false;
		}

		Vector2I targetCell;
		string stateLabel;

		if (IsConstructionWithdrawAction())
		{
			if (!_constructionStorageCell.HasValue || !_constructionStorageAccessCell.HasValue)
			{
				ResetConstructionState(mercenary, true);
				return false;
			}

			targetCell = _constructionStorageAccessCell.Value;
			stateLabel = "\uCC3D\uACE0\uB85C \uC774\uB3D9 \uC911";
		}
		else
		{
			if (!_constructionSiteAccessCell.HasValue)
			{
				ResetConstructionState(mercenary, true);
				return false;
			}

			targetCell = _constructionSiteAccessCell.Value;
			stateLabel = IsConstructionBuildAction()
				? "\uAC74\uC124 \uC704\uCE58\uB85C \uC774\uB3D9 \uC911"
				: "\uD604\uC7A5\uC73C\uB85C \uC774\uB3D9 \uC911";
		}

		if (!mercenary.TryMoveToCell(targetCell, buildManager))
		{
			ResetConstructionState(mercenary, true);
			return false;
		}

		SetCurrentWorkStatus(
			MercenaryWorkType.Build,
			GetConstructionTargetLabel(site),
			stateLabel,
			IsConstructionBuildAction() ? "\uC7AC\uB8CC \uC900\uBE44 \uC644\uB8CC" : "\uAC74\uC124 \uC608\uC815\uC9C0 \uC7AC\uB8CC \uBD80\uC871");
		return true;
	}

	private bool TryStartCraftPath(MercenaryController mercenary, BaseBuildManager buildManager)
	{
		CraftJob? job = _targetCraftJob;

		if (job == null || job.IsCancelled || job.IsCompleted)
		{
			ResetCraftState(mercenary, false);
			return false;
		}

		if (!TryValidateCraftFacility(buildManager, job, out _))
		{
			if (!IsCraftPickupOutputAction())
			{
				job.Cancel();
			}

			ResetCraftState(mercenary, false);
			return false;
		}

		Vector2I targetCell;
		string stateLabel;

		if (IsCraftWithdrawAction())
		{
			if (job.HasAllMaterials)
			{
				ResetCraftState(mercenary, false);
				return false;
			}

			if (!_craftStorageCell.HasValue || !_craftStorageAccessCell.HasValue)
			{
				ResetCraftState(mercenary, false);
				return false;
			}

			targetCell = _craftStorageAccessCell.Value;
			stateLabel = "Moving to storage";
		}
		else if (IsCraftDeliverAction())
		{
			if (job.HasAllMaterials)
			{
				ResetCraftState(mercenary, false);
				return false;
			}

			if (!_craftFacilityAccessCell.HasValue)
			{
				ResetCraftState(mercenary, false);
				return false;
			}

			targetCell = _craftFacilityAccessCell.Value;
			stateLabel = "Moving to workbench";
		}
		else if (IsCraftWorkAction())
		{
			if (job.State == CraftJobState.OutputReady || !job.HasAllMaterials || !_craftFacilityAccessCell.HasValue)
			{
				ResetCraftState(mercenary, false);
				return false;
			}

			targetCell = _craftFacilityAccessCell.Value;
			stateLabel = "Moving to workbench";
		}
		else if (IsCraftPickupOutputAction())
		{
			if (job.State != CraftJobState.OutputReady || !job.HasProducedOutputs || !_craftFacilityAccessCell.HasValue)
			{
				ResetCraftState(mercenary, false);
				return false;
			}

			targetCell = _craftFacilityAccessCell.Value;
			stateLabel = "Moving to workbench";
		}
		else
		{
			ResetCraftState(mercenary, false);
			return false;
		}

		if (!mercenary.TryMoveToCell(targetCell, buildManager))
		{
			ResetCraftState(mercenary, false);
			return false;
		}

		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			IsCraftPickupOutputAction() ? GetCraftOutputLabel(job) : GetCraftTargetLabel(job),
			stateLabel,
			IsCraftPickupOutputAction() ? "Craft output ready" : IsCraftWorkAction() ? "Craft materials ready" : "Craft material delivery");
		return true;
	}

	private bool UpdateConstructionAtArrival(MercenaryController mercenary, double delta)
	{
		BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

		if (buildManager == null)
		{
			ResetConstructionState(mercenary, true);
			return true;
		}

		if (IsConstructionWithdrawAction())
		{
			return UpdateConstructionWithdrawWork(mercenary, buildManager, delta);
		}

		if (IsConstructionDeliverAction())
		{
			return UpdateConstructionDeliverWork(mercenary, buildManager, delta);
		}

		if (IsConstructionBuildAction())
		{
			return UpdateConstructionBuildWork(mercenary, buildManager, delta);
		}

		ResetConstructionState(mercenary, true);
		return true;
	}

	private bool UpdateCraftAtArrival(MercenaryController mercenary, double delta)
	{
		BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

		if (buildManager == null)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (IsCraftWithdrawAction())
		{
			return UpdateCraftWithdrawWork(mercenary, buildManager, delta);
		}

		if (IsCraftDeliverAction())
		{
			return UpdateCraftDeliverWork(mercenary, buildManager, delta);
		}

		if (IsCraftWorkAction())
		{
			return UpdateCraftWork(mercenary, buildManager, delta);
		}

		if (IsCraftPickupOutputAction())
		{
			return UpdateCraftOutputPickup(mercenary, buildManager);
		}

		ResetCraftState(mercenary, false);
		return true;
	}

	private bool UpdateCraftWithdrawWork(MercenaryController mercenary, BaseBuildManager buildManager, double delta)
	{
		CraftJob? job = _targetCraftJob;

		if (job == null
			|| job.IsCancelled
			|| job.IsCompleted
			|| job.HasAllMaterials
			|| !_craftStorageCell.HasValue
			|| _craftMaterialAmount <= 0)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (!TryValidateCraftFacility(buildManager, job, out _))
		{
			job.Cancel();
			ResetCraftState(mercenary, false);
			return true;
		}

		if (buildManager.GetStorageAvailableAmount(_craftStorageCell.Value, _craftMaterialType) <= 0)
		{
			ResetCraftState(mercenary, false);
			SetIdleWorkStatus("Craft material missing");
			return true;
		}

		if (buildManager.IsStorageInteractionReservedByOther(_craftStorageCell.Value, mercenary))
		{
			ResetCraftState(mercenary, false);
			SetIdleWorkStatus("Storage busy");
			return true;
		}

		if (!_reservedStorageInteractionCell.HasValue)
		{
			_reservedStorageInteractionCell = _craftStorageCell.Value;
			_craftInteractionTimer = 0.0f;

			if (!buildManager.TryReserveStorageInteraction(_craftStorageCell.Value, mercenary))
			{
				_reservedStorageInteractionCell = null;
				ResetCraftState(mercenary, false);
				SetIdleWorkStatus("Storage busy");
				return true;
			}

			_craftInteractionDuration = buildManager.GetStorageWithdrawDuration(_craftStorageCell.Value, _craftMaterialType, _craftMaterialAmount);
		}

		_craftInteractionTimer += (float)delta;
		SetUseProgress(_craftInteractionTimer, _craftInteractionDuration, $"Withdraw {_craftInteractionTimer:0.0}s / {_craftInteractionDuration:0.0}s");
		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			$"{BaseBuildManager.GetResourceDisplayName(_craftMaterialType)} x{_craftMaterialAmount}",
			"Withdrawing craft material",
			$"{job.RecipeId} material withdraw");

		if (_craftInteractionTimer < _craftInteractionDuration)
		{
			return false;
		}

		int requestAmount = Mathf.Min(_craftMaterialAmount, mercenary.Inventory.GetMaxAddableAmount(_craftMaterialType));

		if (requestAmount <= 0
			|| !buildManager.TryRemoveResourceFromStorage(_craftStorageCell.Value, _craftMaterialType, requestAmount, out int removedAmount)
			|| removedAmount <= 0)
		{
			ReleaseStorageInteractionReservation(mercenary);
			ResetCraftState(mercenary, false);
			SetIdleWorkStatus("Craft material withdraw failed");
			return true;
		}

		if (!mercenary.Inventory.TryAdd(_craftMaterialType, removedAmount, out int addedAmount) || addedAmount <= 0)
		{
			buildManager.TryAddResourceToStorage(_craftStorageCell.Value, _craftMaterialType, removedAmount, out _, out _);
			ReleaseStorageInteractionReservation(mercenary);
			ResetCraftState(mercenary, false);
			SetIdleWorkStatus("Inventory full");
			return true;
		}

		if (addedAmount < removedAmount)
		{
			buildManager.TryAddResourceToStorage(_craftStorageCell.Value, _craftMaterialType, removedAmount - addedAmount, out _, out _);
		}

		_craftMaterialAmount = addedAmount;
		_carriedResourceType = _craftMaterialType;
		_carriedResourceAmount = mercenary.Inventory.GetAmount(_craftMaterialType);
		_isCarryingResource = _carriedResourceAmount > 0;
		_isHaulingToStorage = false;
		ReleaseStorageInteractionReservation(mercenary);
		CurrentLifeAction = "CraftDeliver";
		_craftInteractionTimer = 0.0f;
		_craftInteractionDuration = 0.0f;
		_hasPathToTarget = false;
		ClearUseProgress();
		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			$"{BaseBuildManager.GetResourceDisplayName(_craftMaterialType)} x{_craftMaterialAmount} \u2192 {job.RecipeId}",
			"Moving to workbench",
			"Craft material delivery");
		RunLogisticsValidation(mercenary);
		return false;
	}

	private bool UpdateCraftDeliverWork(MercenaryController mercenary, BaseBuildManager buildManager, double delta)
	{
		CraftJob? job = _targetCraftJob;

		if (job == null || job.IsCancelled || job.IsCompleted)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (job.HasAllMaterials)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (!TryValidateCraftFacility(buildManager, job, out _))
		{
			job.Cancel();
			ResetCraftState(mercenary, false);
			return true;
		}

		int carriedAmount = mercenary.Inventory.GetAmount(_craftMaterialType);

		if (carriedAmount <= 0)
		{
			_isCarryingResource = false;
			_carriedResourceAmount = 0;
			ResetCraftState(mercenary, false);
			return true;
		}

		int amountToDeliver = Mathf.Min(_craftMaterialAmount, carriedAmount);
		amountToDeliver = Mathf.Min(amountToDeliver, job.GetRemainingAmount(_craftMaterialType));

		if (amountToDeliver <= 0)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (_craftInteractionDuration <= 0.0f)
		{
			int deliveryWeight = amountToDeliver * BaseBuildManager.GetResourceUnitWeight(_craftMaterialType);
			_craftInteractionDuration = Mathf.Clamp(0.5f + deliveryWeight * 0.015f, 0.5f, 3.0f);
			_craftInteractionTimer = 0.0f;
		}

		_craftInteractionTimer += (float)delta;
		SetUseProgress(_craftInteractionTimer, _craftInteractionDuration, $"Deliver {_craftInteractionTimer:0.0}s / {_craftInteractionDuration:0.0}s");
		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			$"{BaseBuildManager.GetResourceDisplayName(_craftMaterialType)} x{amountToDeliver} \u2192 {job.RecipeId}",
			"Delivering craft material",
			"Craft job needs materials");

		if (_craftInteractionTimer < _craftInteractionDuration)
		{
			return false;
		}

		int acceptedAmount = job.AcceptMaterial(_craftMaterialType, amountToDeliver);

		if (acceptedAmount <= 0)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		mercenary.Inventory.TryRemove(_craftMaterialType, acceptedAmount, out _);
		int remainingAmount = mercenary.Inventory.GetAmount(_craftMaterialType);
		_carriedResourceAmount = remainingAmount;
		_isCarryingResource = remainingAmount > 0;
		_isHaulingToStorage = remainingAmount > 0;
		ResetCraftState(mercenary, false);
		RunLogisticsValidation(mercenary);
		return true;
	}

	private bool UpdateCraftWork(MercenaryController mercenary, BaseBuildManager buildManager, double delta)
	{
		CraftJob? job = _targetCraftJob;

		if (job == null || job.IsCancelled || job.IsCompleted)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (job.State == CraftJobState.OutputReady)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (!job.HasAllMaterials)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (!TryValidateCraftFacility(buildManager, job, out _))
		{
			job.Cancel();
			ResetCraftState(mercenary, false);
			return true;
		}

		float craftSpeed = GetCraftWorkSpeed(mercenary);
		job.AddWorkProgress((float)delta * craftSpeed);
		SetUseProgress(job.WorkProgress, job.RequiredWork, $"Craft {(int)(job.GetProgressRatio() * 100.0f)}%");
		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			job.RecipeId,
			"Crafting",
			"Craft materials ready");

		if (job.State != CraftJobState.OutputReady)
		{
			return false;
		}

		ResetCraftState(mercenary, false);
		RunLogisticsValidation(mercenary);
		return true;
	}

	private bool UpdateCraftOutputPickup(MercenaryController mercenary, BaseBuildManager buildManager)
	{
		CraftJob? job = _targetCraftJob;

		if (job == null || job.IsCancelled || job.IsCompleted)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (job.State != CraftJobState.OutputReady || !job.HasProducedOutputs)
		{
			ResetCraftState(mercenary, false);
			return true;
		}

		if (!TryValidateCraftFacility(buildManager, job, out _))
		{
			ResetCraftState(mercenary, false);
			SetIdleWorkStatus("Craft output inaccessible");
			return true;
		}

		if (!CanCarryAllCraftOutputs(mercenary, job))
		{
			ResetCraftState(mercenary, false);
			SetIdleWorkStatus("Inventory full");
			return true;
		}

		Dictionary<BaseResourceType, int> outputs = job.TakeAllProducedOutputs();

		foreach (KeyValuePair<BaseResourceType, int> output in outputs)
		{
			if (output.Value <= 0)
			{
				continue;
			}

			mercenary.Inventory.TryAdd(output.Key, output.Value, out _);
		}

		SetCurrentWorkStatus(
			MercenaryWorkType.Craft,
			GetCraftOutputLabel(outputs),
			"Picked up craft output",
			"Craft output ready");
		ResetCraftState(mercenary, false);

		bool startedDeposit = TryGetFirstStoredOutput(outputs, mercenary, out _, out _)
			&& TrySelectInventoryDepositWork(mercenary, buildManager);
		RunLogisticsValidation(mercenary);
		return !startedDeposit;
	}

	private bool UpdateConstructionWithdrawWork(MercenaryController mercenary, BaseBuildManager buildManager, double delta)
	{
		ConstructionSite? site = _targetConstructionSite;

		if (site == null
			|| site.IsCancelled
			|| site.IsCompleted
			|| !_constructionStorageCell.HasValue
			|| _constructionMaterialAmount <= 0)
		{
			ResetConstructionState(mercenary, true);
			return true;
		}

		if (buildManager.GetStorageAvailableAmount(_constructionStorageCell.Value, _constructionMaterialType) <= 0)
		{
			ResetConstructionState(mercenary, false);
			SetIdleWorkStatus("\uAC74\uC124 \uC7AC\uB8CC \uC5C6\uC74C");
			return true;
		}

		if (buildManager.IsStorageInteractionReservedByOther(_constructionStorageCell.Value, mercenary))
		{
			ResetConstructionState(mercenary, false);
			SetIdleWorkStatus("\uCC3D\uACE0 \uC0AC\uC6A9 \uC911");
			return true;
		}

		if (!_reservedStorageInteractionCell.HasValue)
		{
			_reservedStorageInteractionCell = _constructionStorageCell.Value;
			_constructionInteractionTimer = 0.0f;

			if (!buildManager.TryReserveStorageInteraction(_constructionStorageCell.Value, mercenary))
			{
				_reservedStorageInteractionCell = null;
				ResetConstructionState(mercenary, false);
				SetIdleWorkStatus("\uCC3D\uACE0 \uC0AC\uC6A9 \uC911");
				return true;
			}

			_constructionInteractionDuration = buildManager.GetStorageWithdrawDuration(_constructionStorageCell.Value, _constructionMaterialType, _constructionMaterialAmount);
		}

		_constructionInteractionTimer += (float)delta;
		SetUseProgress(_constructionInteractionTimer, _constructionInteractionDuration, $"\uCD9C\uACE0 {_constructionInteractionTimer:0.0}s / {_constructionInteractionDuration:0.0}s");
		SetCurrentWorkStatus(
			MercenaryWorkType.Build,
			$"{BaseBuildManager.GetResourceDisplayName(_constructionMaterialType)} x{_constructionMaterialAmount}",
			"\uCC3D\uACE0\uC5D0\uC11C \uAEBC\uB0B4\uB294 \uC911",
			$"{site.DisplayName} \uC7AC\uB8CC \uCD9C\uACE0");

		if (_constructionInteractionTimer < _constructionInteractionDuration)
		{
			return false;
		}

		int requestAmount = Mathf.Min(_constructionMaterialAmount, mercenary.Inventory.GetMaxAddableAmount(_constructionMaterialType));

		if (requestAmount <= 0
			|| !buildManager.TryRemoveResourceFromStorage(_constructionStorageCell.Value, _constructionMaterialType, requestAmount, out int removedAmount)
			|| removedAmount <= 0)
		{
			ReleaseStorageInteractionReservation(mercenary);
			ResetConstructionState(mercenary, false);
			SetIdleWorkStatus("\uAC74\uC124 \uC7AC\uB8CC \uCD9C\uACE0 \uC2E4\uD328");
			return true;
		}

		if (!mercenary.Inventory.TryAdd(_constructionMaterialType, removedAmount, out int addedAmount) || addedAmount <= 0)
		{
			Vector2I currentCell = buildManager.WorldToCell(mercenary.GlobalPosition);
			buildManager.TrySpawnOrMergeResourcePile(_constructionMaterialType, currentCell, removedAmount);
			ReleaseStorageInteractionReservation(mercenary);
			ResetConstructionState(mercenary, false);
			SetIdleWorkStatus("\uAC00\uBC29 \uBB34\uAC8C \uBD80\uC871");
			return true;
		}

		if (addedAmount < removedAmount)
		{
			Vector2I currentCell = buildManager.WorldToCell(mercenary.GlobalPosition);
			buildManager.TrySpawnOrMergeResourcePile(_constructionMaterialType, currentCell, removedAmount - addedAmount);
		}

		_constructionMaterialAmount = addedAmount;
		ReleaseStorageInteractionReservation(mercenary);
		CurrentLifeAction = "ConstructionDeliver";
		_constructionInteractionTimer = 0.0f;
		_constructionInteractionDuration = 0.0f;
		_hasPathToTarget = false;
		ClearUseProgress();
		SetCurrentWorkStatus(
			MercenaryWorkType.Build,
			$"{BaseBuildManager.GetResourceDisplayName(_constructionMaterialType)} x{_constructionMaterialAmount} \u2192 {site.DisplayName}",
			"\uD604\uC7A5\uC73C\uB85C \uC774\uB3D9 \uC911",
			"\uAC74\uC124 \uC7AC\uB8CC \uC6B4\uBC18");
		RunLogisticsValidation(mercenary);
		return false;
	}

	private bool UpdateConstructionDeliverWork(MercenaryController mercenary, BaseBuildManager buildManager, double delta)
	{
		ConstructionSite? site = _targetConstructionSite;

		if (site == null || site.IsCancelled || site.IsCompleted)
		{
			ResetConstructionState(mercenary, true);
			return true;
		}

		int carriedAmount = mercenary.Inventory.GetAmount(_constructionMaterialType);

		if (carriedAmount <= 0)
		{
			ResetConstructionState(mercenary, false);
			return true;
		}

		int amountToDeliver = Mathf.Min(_constructionMaterialAmount, carriedAmount);

		if (_constructionInteractionDuration <= 0.0f)
		{
			int deliveryWeight = amountToDeliver * BaseBuildManager.GetResourceUnitWeight(_constructionMaterialType);
			_constructionInteractionDuration = Mathf.Clamp(0.5f + deliveryWeight * 0.015f, 0.5f, 3.0f);
			_constructionInteractionTimer = 0.0f;
		}

		_constructionInteractionTimer += (float)delta;
		SetUseProgress(_constructionInteractionTimer, _constructionInteractionDuration, $"\uD22C\uC785 {_constructionInteractionTimer:0.0}s / {_constructionInteractionDuration:0.0}s");
		SetCurrentWorkStatus(
			MercenaryWorkType.Build,
			$"{BaseBuildManager.GetResourceDisplayName(_constructionMaterialType)} x{amountToDeliver} \u2192 {site.DisplayName}",
			"\uC7AC\uB8CC \uD22C\uC785 \uC911",
			"\uAC74\uC124 \uC608\uC815\uC9C0 \uC7AC\uB8CC \uBD80\uC871");

		if (_constructionInteractionTimer < _constructionInteractionDuration)
		{
			return false;
		}

		if (!buildManager.TryDeliverMaterial(site, _constructionMaterialType, amountToDeliver, out int acceptedAmount) || acceptedAmount <= 0)
		{
			ResetConstructionState(mercenary, true);
			return true;
		}

		mercenary.Inventory.TryRemove(_constructionMaterialType, acceptedAmount, out _);

		int leftoverAmount = mercenary.Inventory.GetAmount(_constructionMaterialType);

		if (leftoverAmount > 0)
		{
			Vector2I currentCell = buildManager.WorldToCell(mercenary.GlobalPosition);
			buildManager.TrySpawnOrMergeResourcePile(_constructionMaterialType, currentCell, leftoverAmount);
			mercenary.Inventory.TryRemove(_constructionMaterialType, leftoverAmount, out _);
		}

		ResetConstructionState(mercenary, false);
		RunLogisticsValidation(mercenary);
		return true;
	}

	private bool UpdateConstructionBuildWork(MercenaryController mercenary, BaseBuildManager buildManager, double delta)
	{
		ConstructionSite? site = _targetConstructionSite;

		if (site == null || site.IsCancelled || site.IsCompleted || !site.HasAllMaterials)
		{
			ResetConstructionState(mercenary, false);
			return true;
		}

		float buildSpeed = GetConstructionBuildSpeed(mercenary);
		bool completed = buildManager.AddBuildProgress(site, mercenary, (float)delta * buildSpeed);
		SetUseProgress(site.BuildProgress, site.RequiredWork, $"\uAC74\uC124 {(int)(site.GetProgressRatio() * 100.0f)}%");
		SetCurrentWorkStatus(
			MercenaryWorkType.Build,
			site.DisplayName,
			"\uAC74\uC124 \uC911",
			"\uC7AC\uB8CC \uC900\uBE44 \uC644\uB8CC");

		if (!completed)
		{
			return false;
		}

		ResetConstructionState(mercenary, false);
		RunLogisticsValidation(mercenary);
		return true;
	}

	private static float GetConstructionBuildSpeed(MercenaryController mercenary)
	{
		MercenaryStats stats = mercenary.Profile.Stats;
		float speed = 1.0f + stats.Strength * 0.02f + stats.CraftingSkill * 0.03f;
		MercenaryWorkPriority buildPriority = mercenary.Profile.GetWorkSettings().GetPriority(MercenaryWorkType.Build);

		if (buildPriority == MercenaryWorkPriority.High)
		{
			speed *= 1.15f;
		}
		else if (buildPriority == MercenaryWorkPriority.Low)
		{
			speed *= 0.85f;
		}

		return Mathf.Clamp(speed, 0.5f, 2.0f);
	}

	private static float GetCraftWorkSpeed(MercenaryController mercenary)
	{
		MercenaryWorkPriority craftPriority = mercenary.Profile.GetWorkSettings().GetPriority(MercenaryWorkType.Craft);
		float speed = 1.0f;

		if (craftPriority == MercenaryWorkPriority.High)
		{
			speed *= 1.1f;
		}
		else if (craftPriority == MercenaryWorkPriority.Low)
		{
			speed *= 0.9f;
		}

		return Mathf.Clamp(speed, 0.5f, 1.5f);
	}

	private static string GetConstructionTargetLabel(ConstructionSite site)
	{
		return site.HasAllMaterials
			? site.DisplayName
			: $"{site.DisplayName} {site.GetDeliveredTotal()}/{site.GetRequiredTotal()}";
	}

	private CraftingManager? GetCraftingManager()
	{
		return GetTree().CurrentScene?.GetNodeOrNull<CraftingManager>("CraftingManager");
	}

	private static string GetCraftTargetLabel(CraftJob job)
	{
		BaseResourceType? missingInput = job.GetFirstMissingInput();

		if (!missingInput.HasValue)
		{
			return job.RecipeId;
		}

		BaseResourceType resourceType = missingInput.Value;
		return $"{job.RecipeId} {job.GetDeliveredAmount(resourceType)}/{job.GetRequiredAmount(resourceType)} {resourceType}";
	}

	private static string GetCraftOutputLabel(CraftJob job)
	{
		return GetCraftOutputLabel(job.ProducedOutputs);
	}

	private static string GetCraftOutputLabel(IReadOnlyDictionary<BaseResourceType, int> outputs)
	{
		List<string> parts = new();

		foreach (KeyValuePair<BaseResourceType, int> output in outputs)
		{
			if (output.Value > 0)
			{
				parts.Add($"{BaseBuildManager.GetResourceDisplayName(output.Key)} x{output.Value}");
			}
		}

		return parts.Count == 0 ? "Craft output" : string.Join(", ", parts);
	}

	private static bool CanCarryAllCraftOutputs(MercenaryController mercenary, CraftJob job)
	{
		float totalWeight = 0.0f;

		foreach (KeyValuePair<BaseResourceType, int> output in job.ProducedOutputs)
		{
			if (output.Value <= 0)
			{
				continue;
			}

			totalWeight += output.Value * BaseBuildManager.GetResourceUnitWeight(output.Key);
		}

		return totalWeight > 0.0f && mercenary.Inventory.GetFreeWeight() + 0.01f >= totalWeight;
	}

	private static bool TryGetFirstStoredOutput(IReadOnlyDictionary<BaseResourceType, int> outputs, MercenaryController mercenary, out BaseResourceType resourceType, out int amount)
	{
		resourceType = BaseResourceType.Wood;
		amount = 0;

		foreach (KeyValuePair<BaseResourceType, int> output in outputs)
		{
			if (output.Value <= 0 || !ResourceDefinitionDatabase.IsStoredResource(output.Key))
			{
				continue;
			}

			int inventoryAmount = mercenary.Inventory.GetAmount(output.Key);

			if (inventoryAmount <= 0)
			{
				continue;
			}

			resourceType = output.Key;
			amount = inventoryAmount;
			return true;
		}

		return false;
	}

	private static bool TryValidateCraftFacility(BaseBuildManager buildManager, CraftJob job, out Vector2I facilityOriginCell)
	{
		facilityOriginCell = job.FacilityCell;

		if (!CraftRecipeDatabase.TryGet(job.RecipeId, out CraftRecipeEntry recipe))
		{
			return false;
		}

		return TryValidateCraftFacility(buildManager, job, recipe, out facilityOriginCell);
	}

	private static bool TryValidateCraftFacility(BaseBuildManager buildManager, CraftJob job, CraftRecipeEntry recipe, out Vector2I facilityOriginCell)
	{
		facilityOriginCell = job.FacilityCell;

		if (!buildManager.TryGetObjectInfoAtCell(job.FacilityCell, out TileBuildType objectType, out Vector2I originCell, out _))
		{
			return false;
		}

		if (objectType != recipe.RequiredFacilityType)
		{
			return false;
		}

		facilityOriginCell = originCell;
		return true;
	}

	private bool TryStartPlantPath(MercenaryController mercenary, BaseBuildManager buildManager)
	{
		if (!IsWorkEnabled(mercenary, MercenaryWorkType.Farm))
		{
			ResetPlantState();
			return false;
		}

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

		SetCurrentWorkStatus(MercenaryWorkType.Farm, $"\uC2EC\uAE30 {_targetPlantCell.Value}", "\uB18D\uC9C0\uB85C \uC774\uB3D9 \uC911", GetWorkDecisionReason(mercenary, MercenaryWorkType.Farm));
		return true;
	}

	private bool TryStartFurnitureUsePath(MercenaryController mercenary, BaseBuildManager buildManager)
	{
		if (!_targetFurnitureUseType.HasValue)
		{
			ResetFurnitureUseState(mercenary);
			return false;
		}

		Vector2I startCell = buildManager.WorldToCell(mercenary.GlobalPosition);

		if (!_targetFurnitureOriginCell.HasValue
			|| !_targetFurnitureAccessCell.HasValue
			|| _targetFurnitureType == TileBuildType.None
			|| buildManager.IsFurnitureReservedByOther(_targetFurnitureOriginCell.Value, mercenary))
		{
			if (!buildManager.TryFindNearestUsableFurnitureAccess(startCell, _targetFurnitureUseType.Value, mercenary, out Vector2I originCell, out Vector2I accessCell, out TileBuildType furnitureType))
			{
				ResetFurnitureUseState(mercenary);
				return false;
			}

			_targetFurnitureOriginCell = originCell;
			_targetFurnitureAccessCell = accessCell;
			_targetFurnitureType = furnitureType;
		}

		ReleaseReservedFurniture(mercenary);

		if (!buildManager.TryReserveFurniture(_targetFurnitureOriginCell.Value, mercenary))
		{
			SetIdleWorkStatus("\uAC00\uAD6C \uC608\uC57D\uB428");
			ResetFurnitureUseState(mercenary);
			return false;
		}

		_reservedFurnitureOriginCell = _targetFurnitureOriginCell;
		_reservedFurnitureUseType = _targetFurnitureUseType;

		if (!mercenary.TryMoveToCell(_targetFurnitureAccessCell.Value, buildManager))
		{
			SetIdleWorkStatus("\uAC00\uAD6C \uC811\uADFC \uCE78 \uC5C6\uC74C");
			ResetFurnitureUseState(mercenary);
			return false;
		}

		string targetLabel = GetFurnitureDisplayName(_targetFurnitureType);

		if (IsEatFurnitureAction())
		{
			SetRestWorkStatusLabel("\uC2DD\uC0AC", targetLabel, "\uC2DD\uC0AC\uD558\uB7EC \uC774\uB3D9 \uC911", "\uBC30\uACE0\uD514 \uB192\uC74C");
		}
		else if (IsSleepFurnitureAction())
		{
			SetRestWorkStatusLabel("\uC218\uBA74", targetLabel, "\uCE68\uB300\uB85C \uC774\uB3D9 \uC911", "\uC878\uB9BC \uB192\uC74C");
		}
		else if (IsRelaxFurnitureAction())
		{
			SetRestWorkStatusLabel("\uD734\uC2DD", targetLabel, "\uD734\uC2DD \uAC00\uAD6C\uB85C \uC774\uB3D9 \uC911", "\uC2A4\uD2B8\uB808\uC2A4/\uAE30\uBD84 \uAD00\uB9AC");
		}

		return true;
	}

	private bool TryStartHarvestCropPath(MercenaryController mercenary, BaseBuildManager buildManager)
	{
		if (!IsWorkEnabled(mercenary, MercenaryWorkType.Farm))
		{
			ResetHarvestCropState();
			return false;
		}

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

		SetCurrentWorkStatus(MercenaryWorkType.Farm, _targetHarvestCropLabel, "\uC791\uBB3C \uC218\uD655 \uC911", GetWorkDecisionReason(mercenary, MercenaryWorkType.Farm));
		return true;
	}

	private bool TryStartGatherPath(MercenaryController mercenary, BaseBuildManager buildManager)
	{
		if (!IsWorkEnabled(mercenary, MercenaryWorkType.Gather))
		{
			ResetGatherState();
			return false;
		}

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

		SetCurrentWorkStatus(MercenaryWorkType.Gather, resourceNodeLabel, "\uC790\uC6D0 \uCC44\uC9D1 \uC911", GetWorkDecisionReason(mercenary, MercenaryWorkType.Gather));
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

		if (!_isCarryingResource && !IsWorkEnabled(mercenary, MercenaryWorkType.Haul))
		{
			ResetHaulState(false);
			return false;
		}

		if (_isCarryingResource)
		{
			if (!_targetStorageCell.HasValue
				|| !_targetStorageAccessCell.HasValue
				|| buildManager.GetStorageFreeSpace(_targetStorageCell.Value, _carriedResourceType) <= 0)
			{
				if (!buildManager.TryFindNearestStorageAccessWithSpace(startCell, _carriedResourceType, out Vector2I deliveryStorageCell, out Vector2I deliveryAccessCell))
				{
					if (_preserveCarriedResourceOnDepositFailure)
					{
						AbortCarriedResourceDepositWithoutDrop("Storage unavailable");
						return false;
					}

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
				SetCurrentWorkStatus(MercenaryWorkType.Haul, "\uCC3D\uACE0", "\uC790\uC6D0 \uBC30\uC1A1 \uC911", "\uC774\uBBF8 \uC6B4\uBC18 \uC911\uC774\uB77C \uBC30\uC1A1 \uACC4\uC18D");
				return true;
			}

			if (DebugHauling)
			{
				GD.Print($"{mercenary.MercenaryName} haul delivery path failed, dropping {_carriedResourceType} x{_carriedResourceAmount}");
			}

			if (_preserveCarriedResourceOnDepositFailure)
			{
				AbortCarriedResourceDepositWithoutDrop("Storage path failed");
				return false;
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

		SetCurrentWorkStatus(MercenaryWorkType.Haul, _targetHaulPileLabel, "\uC790\uC6D0 \uB354\uBBF8\uB85C \uC774\uB3D9 \uC911", GetWorkDecisionReason(mercenary, MercenaryWorkType.Haul));
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

	private bool UpdateHaulAtArrival(MercenaryController mercenary, double delta)
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
			int pickupRequestAmount = mercenary.Inventory.GetMaxAddableAmount(pileType);

			if (pickupRequestAmount <= 0)
			{
				safePile.ReleaseHaulReservation(this);
				ResetHaulState(false);
				SetIdleWorkStatus("\uAC00\uBC29 \uBB34\uAC8C \uBD80\uC871");
				return true;
			}

			safePile.ReleaseHaulReservation(this);
			int pickedUpAmount = safePile.TakeAmount(pickupRequestAmount);
			_targetHaulPile = null;
			_targetPoint = null;

			if (pickedUpAmount <= 0)
			{
				ResetHaulState(false);
				return true;
			}

			if (!mercenary.Inventory.TryAdd(pileType, pickedUpAmount, out int addedAmount) || addedAmount <= 0)
			{
				Vector2I returnCell = buildManager.WorldToCell(mercenary.GlobalPosition);
				buildManager.TrySpawnOrMergeResourcePile(pileType, returnCell, pickedUpAmount);
				ResetHaulState(false);
				SetIdleWorkStatus("\uAC00\uBC29 \uBB34\uAC8C \uBD80\uC871");
				return true;
			}

			if (addedAmount < pickedUpAmount)
			{
				Vector2I leftoverCell = buildManager.WorldToCell(mercenary.GlobalPosition);
				buildManager.TrySpawnOrMergeResourcePile(pileType, leftoverCell, pickedUpAmount - addedAmount);
			}

			_carriedResourceType = pileType;
			_carriedResourceAmount = mercenary.Inventory.GetAmount(pileType);
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
			_isHaulingToStorage = true;
			_hasPathToTarget = false;
			SetCurrentWorkStatus(MercenaryWorkType.Haul, $"{pileType} x{_carriedResourceAmount} \u2192 \uCC3D\uACE0", "\uC790\uC6D0 \uBC30\uC1A1 \uC911", "\uC774\uBBF8 \uC6B4\uBC18 \uC911\uC774\uB77C \uBC30\uC1A1 \uACC4\uC18D");

			if (DebugHauling)
			{
				GD.Print($"{mercenary.MercenaryName} picked up {pileType} x{_carriedResourceAmount}, delivering to Storage {storageCell} via {accessCell}");
			}

			RunLogisticsValidation(mercenary);
			return false;
		}

		Vector2I deliveryCell = buildManager.WorldToCell(mercenary.GlobalPosition);

		if (!_targetStorageCell.HasValue
			|| !_targetStorageAccessCell.HasValue
			|| buildManager.GetStorageFreeSpace(_targetStorageCell.Value, _carriedResourceType) <= 0)
		{
			if (!buildManager.TryFindNearestStorageAccessWithSpace(deliveryCell, _carriedResourceType, out Vector2I storageCell, out Vector2I accessCell))
			{
				if (_preserveCarriedResourceOnDepositFailure)
				{
					AbortCarriedResourceDepositWithoutDrop("Storage unavailable");
					return true;
				}

				DropCarriedResourceAtCurrentCell(mercenary);
				ResetHaulState(false);
				return true;
			}

			_targetStorageCell = storageCell;
			_targetStorageAccessCell = accessCell;
			_hasPathToTarget = false;
			SetCurrentWorkStatus(MercenaryWorkType.Haul, "\uCC3D\uACE0", "\uC790\uC6D0 \uBC30\uC1A1 \uC911", "\uC800\uC7A5 \uACF5\uAC04 \uC7AC\uD0D0\uC0C9");
			return false;
		}

		if (deliveryCell != _targetStorageAccessCell.Value
			&& GetManhattanDistance(deliveryCell, _targetStorageCell.Value) > 1)
		{
			if (_preserveCarriedResourceOnDepositFailure)
			{
				AbortCarriedResourceDepositWithoutDrop("Storage path failed");
				return true;
			}

			DropCarriedResourceAtCurrentCell(mercenary);
			ResetHaulState(false);
			return true;
		}

		return UpdateStorageDepositWork(mercenary, buildManager, delta);
	}

	private bool UpdateStorageDepositWork(MercenaryController mercenary, BaseBuildManager buildManager, double delta)
	{
		if (!_targetStorageCell.HasValue)
		{
			ResetHaulState(false);
			return true;
		}

		if (buildManager.IsStorageInteractionReservedByOther(_targetStorageCell.Value, mercenary))
		{
			if (_preserveCarriedResourceOnDepositFailure)
			{
				AbortCarriedResourceDepositWithoutDrop("Storage busy");
				return true;
			}

			DropCarriedResourceAtCurrentCell(mercenary);
			ResetHaulState(false);
			return true;
		}

		int carriedAmount = mercenary.Inventory.GetAmount(_carriedResourceType);

		if (carriedAmount <= 0)
		{
			_carriedResourceAmount = 0;
			ResetHaulState(false);
			return true;
		}

		_carriedResourceAmount = carriedAmount;

		if (!_isDepositingToStorage)
		{
			_isDepositingToStorage = true;
			_reservedStorageInteractionCell = _targetStorageCell.Value;
			_storageInteractionTimer = 0.0f;

			if (!buildManager.TryReserveStorageInteraction(_targetStorageCell.Value, mercenary))
			{
				_isDepositingToStorage = false;
				_reservedStorageInteractionCell = null;

				if (_preserveCarriedResourceOnDepositFailure)
				{
					AbortCarriedResourceDepositWithoutDrop("Storage busy");
					return true;
				}

				DropCarriedResourceAtCurrentCell(mercenary);
				ResetHaulState(false);
				return true;
			}

			_storageInteractionDuration = buildManager.GetStorageDepositDuration(_targetStorageCell.Value, _carriedResourceType, _carriedResourceAmount);
		}

		_storageInteractionTimer += (float)delta;
		SetUseProgress(_storageInteractionTimer, _storageInteractionDuration, $"\uC785\uACE0 {_storageInteractionTimer:0.0}s / {_storageInteractionDuration:0.0}s");
		SetStorageDepositWorkStatus(
			buildManager.GetStorageDisplayName(_targetStorageCell.Value),
			"\uCC3D\uACE0\uC5D0 \uB123\uB294 \uC911",
			$"{BaseBuildManager.GetResourceDisplayName(_carriedResourceType)} x{_carriedResourceAmount} \uC785\uACE0");

		if (_storageInteractionTimer < _storageInteractionDuration)
		{
			return false;
		}

		Vector2I storageCell = _targetStorageCell.Value;
		BaseResourceType depositType = _carriedResourceType;
		int requestedAmount = _carriedResourceAmount;
		buildManager.TryAddResourceToStorage(storageCell, depositType, requestedAmount, out int storedAmount, out _);

		if (DebugHauling && storedAmount > 0)
		{
			GD.Print($"{mercenary.MercenaryName} stored {depositType} x{storedAmount} at Storage {storageCell}");
		}

		if (storedAmount > 0)
		{
			mercenary.Inventory.TryRemove(depositType, storedAmount, out _);
		}

		int remainingAmount = mercenary.Inventory.GetAmount(depositType);
		_carriedResourceType = depositType;
		_carriedResourceAmount = remainingAmount;
		_isCarryingResource = remainingAmount > 0;
		_isHaulingToStorage = remainingAmount > 0;
		_isDepositingToStorage = false;
		_storageInteractionTimer = 0.0f;
		_storageInteractionDuration = 0.0f;
		ClearUseProgress();

		if (DebugHauling)
		{
			GD.Print(remainingAmount > 0
				? $"{mercenary.MercenaryName} deposit completed with leftover: {depositType} accepted {storedAmount}, remaining {remainingAmount}"
				: $"{mercenary.MercenaryName} deposit completed: {depositType} accepted {storedAmount}, remaining 0");
		}

		ReleaseStorageInteractionReservation(mercenary);

		if (remainingAmount > 0)
		{
			if (_preserveCarriedResourceOnDepositFailure)
			{
				AbortCarriedResourceDepositWithoutDrop("Storage full");
				RunLogisticsValidation(mercenary);
				return true;
			}

			DropCarriedResourceAtCurrentCell(mercenary);
		}

		ResetHaulState(false);
		RunLogisticsValidation(mercenary);
		return true;
	}

	private static bool IsGatherableResourceNode(ResourceNode resourceNode)
	{
		return IsValidResourceNode(resourceNode)
			&& ResourceDefinitionDatabase.IsStoredResource(resourceNode.ResourceType);
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

		if (IsConstructionAction())
		{
			BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

			if (buildManager != null)
			{
				if (IsConstructionWithdrawAction() && _constructionStorageAccessCell.HasValue)
				{
					return buildManager.CellToWorldCenter(_constructionStorageAccessCell.Value);
				}

				if (_constructionSiteAccessCell.HasValue)
				{
					return buildManager.CellToWorldCenter(_constructionSiteAccessCell.Value);
				}
			}

			ResetConstructionState(mercenary, true);
			return mercenary.GlobalPosition;
		}

		if (IsCraftAction())
		{
			BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

			if (buildManager != null)
			{
				if (IsCraftWithdrawAction() && _craftStorageAccessCell.HasValue)
				{
					return buildManager.CellToWorldCenter(_craftStorageAccessCell.Value);
				}

				if (_craftFacilityAccessCell.HasValue)
				{
					return buildManager.CellToWorldCenter(_craftFacilityAccessCell.Value);
				}
			}

			ResetCraftState(mercenary, false);
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

		if (IsFurnitureUseAction() && _targetFurnitureAccessCell.HasValue && mercenary.GetBaseBuildManager() != null)
		{
			return mercenary.SnapWorldToGridCenter(mercenary.GetBaseBuildManager()!.CellToWorldCenter(_targetFurnitureAccessCell.Value));
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

	private void UpdateConditionTick(MercenaryController mercenary, double delta)
	{
		MercenaryCondition condition = mercenary.Profile.Condition;

		if (!EnableConditionTick)
		{
			SyncConditionNeeds(condition);
			ClampCondition(mercenary, condition);
			return;
		}

		float seconds = (float)delta;
		float hungerExtra = 0.0f;
		float sleepinessExtra = 0.0f;
		float stressDelta = 0.0f;
		float hygieneLoss = HygieneDecayRate;
		float moodDirectDelta = 0.0f;

		switch (CurrentWorkType)
		{
			case MercenaryWorkType.Haul:
				hungerExtra += 0.02f;
				sleepinessExtra += 0.015f;
				stressDelta += WorkStressRate;
				hygieneLoss += 0.01f;
				break;
			case MercenaryWorkType.Farm:
				hungerExtra += 0.015f;
				sleepinessExtra += 0.01f;
				hygieneLoss += 0.015f;
				break;
			case MercenaryWorkType.Gather:
				hungerExtra += 0.025f;
				sleepinessExtra += 0.02f;
				stressDelta += WorkStressRate;
				hygieneLoss += 0.015f;
				break;
			case MercenaryWorkType.Guard:
				hungerExtra += 0.01f;
				sleepinessExtra += 0.02f;
				stressDelta += 0.01f;
				break;
			case MercenaryWorkType.Build:
				hungerExtra += 0.025f;
				sleepinessExtra += 0.02f;
				stressDelta += WorkStressRate;
				hygieneLoss += 0.01f;
				break;
			case MercenaryWorkType.Craft:
				sleepinessExtra += 0.01f;
				stressDelta += 0.012f;
				break;
			case MercenaryWorkType.Medical:
				stressDelta += 0.018f;
				break;
			case MercenaryWorkType.Rest:
				stressDelta -= RestStressRecoveryRate;
				moodDirectDelta += MoodChangeRate;
				break;
		}

		HungerNeed = Mathf.Clamp(HungerNeed - hungerExtra * seconds, 0.0f, MaxHungerNeed);
		SleepNeed = Mathf.Clamp(SleepNeed - sleepinessExtra * seconds, 0.0f, MaxSleepNeed);
		SyncConditionNeeds(condition);

		condition.Stress = Mathf.Clamp(condition.Stress + stressDelta * seconds, 0.0f, 100.0f);
		condition.Hygiene = Mathf.Clamp(condition.Hygiene - hygieneLoss * seconds, 0.0f, 100.0f);
		condition.Mood = Mathf.Clamp(condition.Mood + CalculateMoodDelta(condition, moodDirectDelta) * seconds, 0.0f, 100.0f);
		ClampCondition(mercenary, condition);
	}

	private void SyncConditionNeeds(MercenaryCondition condition)
	{
		condition.Hunger = MaxHungerNeed <= 0.0f
			? 0.0f
			: Mathf.Clamp((MaxHungerNeed - HungerNeed) / MaxHungerNeed * 100.0f, 0.0f, 100.0f);
		condition.Sleepiness = MaxSleepNeed <= 0.0f
			? 0.0f
			: Mathf.Clamp((MaxSleepNeed - SleepNeed) / MaxSleepNeed * 100.0f, 0.0f, 100.0f);
	}

	private void ApplyHungerRecover(MercenaryCondition condition, float recoverAmount)
	{
		float scaledRecover = MaxHungerNeed <= 0.0f ? recoverAmount : recoverAmount / 100.0f * MaxHungerNeed;
		HungerNeed = Mathf.Clamp(HungerNeed + scaledRecover, 0.0f, MaxHungerNeed);
		SyncConditionNeeds(condition);
	}

	private void ApplySleepRecover(MercenaryCondition condition, float recoverAmount)
	{
		float scaledRecover = MaxSleepNeed <= 0.0f ? recoverAmount : recoverAmount / 100.0f * MaxSleepNeed;
		SleepNeed = Mathf.Clamp(SleepNeed + scaledRecover, 0.0f, MaxSleepNeed);
		SyncConditionNeeds(condition);
	}

	private float CalculateMoodDelta(MercenaryCondition condition, float directDelta)
	{
		float moodDelta = directDelta;
		int badConditionCount = 0;

		if (condition.Hunger > 70.0f)
		{
			badConditionCount++;
		}

		if (condition.Sleepiness > 70.0f)
		{
			badConditionCount++;
		}

		if (condition.Stress > 60.0f)
		{
			badConditionCount++;
		}

		if (condition.Hygiene < 30.0f)
		{
			badConditionCount++;
		}

		if (condition.InjurySeverity > 0.0f)
		{
			badConditionCount++;
		}

		if (badConditionCount > 0)
		{
			return moodDelta - MoodChangeRate * badConditionCount;
		}

		if (condition.Hunger < 40.0f
			&& condition.Sleepiness < 40.0f
			&& condition.Stress < 30.0f
			&& condition.Hygiene > 60.0f)
		{
			moodDelta += MoodChangeRate;
		}

		return moodDelta;
	}

	private static void ClampCondition(MercenaryController mercenary, MercenaryCondition condition)
	{
		int maxHealth = Mathf.Max(1, mercenary.Profile.Stats.MaxHealth);
		condition.Health = Mathf.Clamp(condition.Health, 0, maxHealth);
		condition.Hunger = Mathf.Clamp(condition.Hunger, 0.0f, 100.0f);
		condition.Sleepiness = Mathf.Clamp(condition.Sleepiness, 0.0f, 100.0f);
		condition.Mood = Mathf.Clamp(condition.Mood, 0.0f, 100.0f);
		condition.Stress = Mathf.Clamp(condition.Stress, 0.0f, 100.0f);
		condition.Hygiene = Mathf.Clamp(condition.Hygiene, 0.0f, 100.0f);
		condition.InjurySeverity = Mathf.Clamp(condition.InjurySeverity, 0.0f, 100.0f);
	}

	private static float GetSleepFurnitureRecoverMultiplier(TileBuildType furnitureType)
	{
		return furnitureType switch
		{
			TileBuildType.ImprovisedBed => 0.8f,
			TileBuildType.LuxuryBed => 1.3f,
			_ => 1.0f
		};
	}

	private static string GetFurnitureDisplayName(TileBuildType furnitureType)
	{
		return furnitureType switch
		{
			TileBuildType.ImprovisedBed => "\uAE09\uC870 \uCE68\uB300",
			TileBuildType.Bed => "\uAE30\uBCF8 \uCE68\uB300",
			TileBuildType.LuxuryBed => "\uACE0\uAE09 \uCE68\uB300",
			TileBuildType.Chair => "\uC758\uC790",
			TileBuildType.SmallDiningTable => "\uC791\uC740 \uC2DD\uD0C1",
			TileBuildType.LongDiningTable => "\uAE34 \uC2DD\uD0C1",
			TileBuildType.ServingCounter => "\uBC30\uC2DD\uB300",
			TileBuildType.KitchenCounter => "\uC870\uB9AC\uB300",
			TileBuildType.Hearth => "\uD654\uB355",
			TileBuildType.IngredientCrate => "\uC2DD\uC7AC\uB8CC \uC0C1\uC790",
			TileBuildType.Storage => "\uCC3D\uACE0",
			TileBuildType.SmallChest => "\uC791\uC740 \uC0C1\uC790",
			TileBuildType.LargeStorage => "\uB300\uD615 \uCC3D\uACE0",
			TileBuildType.SmallDesk => "\uC791\uC740 \uCC45\uC0C1",
			TileBuildType.SmallRug => "\uC791\uC740 \uAE54\uAC1C",
			TileBuildType.LargeRug => "\uD070 \uAE54\uAC1C",
			TileBuildType.Lamp => "\uB7A8\uD504",
			TileBuildType.PlantPot => "\uD654\uBD84",
			TileBuildType.WallBanner => "\uBCBD\uAC78\uC774 \uAE43\uBC1C",
			TileBuildType.TrophyDisplay => "\uC804\uB9AC\uD488 \uC7A5\uC2DD\uB300",
			_ => furnitureType.ToString()
		};
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
		return false;
	}

	private bool IsEatingAtPoint()
	{
		return false;
	}

	private bool IsGatherAction()
	{
		return CurrentLifeAction == "Gather";
	}

	private bool IsHaulAction()
	{
		return CurrentLifeAction == "Haul";
	}

	private bool IsConstructionWithdrawAction()
	{
		return CurrentLifeAction == "ConstructionWithdraw";
	}

	private bool IsConstructionDeliverAction()
	{
		return CurrentLifeAction == "ConstructionDeliver";
	}

	private bool IsConstructionBuildAction()
	{
		return CurrentLifeAction == "ConstructionBuild";
	}

	private bool IsConstructionAction()
	{
		return IsConstructionWithdrawAction() || IsConstructionDeliverAction() || IsConstructionBuildAction();
	}

	private bool IsCraftWithdrawAction()
	{
		return CurrentLifeAction == "CraftWithdraw";
	}

	private bool IsCraftDeliverAction()
	{
		return CurrentLifeAction == "CraftDeliver";
	}

	private bool IsCraftWorkAction()
	{
		return CurrentLifeAction == "CraftWork";
	}

	private bool IsCraftPickupOutputAction()
	{
		return CurrentLifeAction == "CraftPickupOutput";
	}

	private bool IsCraftAction()
	{
		return IsCraftWithdrawAction() || IsCraftDeliverAction() || IsCraftWorkAction() || IsCraftPickupOutputAction();
	}

	private bool IsPlantAction()
	{
		return CurrentLifeAction == "Plant";
	}

	private bool IsHarvestCropAction()
	{
		return CurrentLifeAction == "HarvestCrop";
	}

	private bool IsEatFurnitureAction()
	{
		return CurrentLifeAction == "EatFurniture";
	}

	private bool IsSleepFurnitureAction()
	{
		return CurrentLifeAction == "SleepFurniture";
	}

	private bool IsRelaxFurnitureAction()
	{
		return CurrentLifeAction == "RelaxFurniture";
	}

	private bool IsFurnitureUseAction()
	{
		return IsEatFurnitureAction() || IsSleepFurnitureAction() || IsRelaxFurnitureAction();
	}

	private bool IsUsingFacility(FacilityType facilityType)
	{
		return CurrentOccupiedFacilityType == facilityType;
	}

	private Node2D? GetPriorityNeedPoint()
	{
		// Need recovery now requires real furniture; legacy Eat/Sleep LifePoints are not recovery targets.
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

	private void ResetFurnitureUseState(MercenaryController? mercenary)
	{
		bool wasFurnitureUseAction = IsFurnitureUseAction();
		ReleaseReservedFurniture(mercenary);

		_targetFurnitureOriginCell = null;
		_targetFurnitureAccessCell = null;
		_targetFurnitureType = TileBuildType.None;
		_targetFurnitureUseType = null;
		_eatWorkTimer = 0.0f;
		_relaxWorkTimer = 0.0f;
		ClearUseProgress();
		ClearCurrentRoomEffect();

		if (wasFurnitureUseAction)
		{
			_targetPoint = null;
			CurrentLifeAction = "Idle";
		}
	}

	private void ReleaseReservedFurniture(MercenaryController? mercenary)
	{
		mercenary ??= GetParent() as MercenaryController;

		if (!_reservedFurnitureOriginCell.HasValue || mercenary == null)
		{
			_reservedFurnitureOriginCell = null;
			_reservedFurnitureUseType = null;
			return;
		}

		BaseBuildManager? buildManager = mercenary.GetBaseBuildManager()
			?? GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");
		buildManager?.ReleaseFurnitureReservation(_reservedFurnitureOriginCell.Value, mercenary);
		_reservedFurnitureOriginCell = null;
		_reservedFurnitureUseType = null;
	}

	private bool UpdateFurnitureUseAtArrival(MercenaryController mercenary, double delta)
	{
		BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

		if (buildManager == null
			|| !_targetFurnitureOriginCell.HasValue
			|| !_targetFurnitureUseType.HasValue
			|| !buildManager.TryGetObjectInfoAtCell(_targetFurnitureOriginCell.Value, out TileBuildType objectType, out Vector2I originCell, out _)
			|| originCell != _targetFurnitureOriginCell.Value
			|| objectType != _targetFurnitureType)
		{
			SetIdleWorkStatus("\uC0AC\uC6A9 \uAC00\uAD6C \uC5C6\uC74C");
			ResetFurnitureUseState(mercenary);
			return true;
		}

		return _targetFurnitureUseType.Value switch
		{
			FurnitureUseType.Eat => UpdateEatFurnitureWork(mercenary, buildManager, delta),
			FurnitureUseType.Sleep => UpdateSleepFurnitureWork(mercenary, delta),
			FurnitureUseType.Relax => UpdateRelaxFurnitureWork(mercenary, delta),
			_ => true
		};
	}

	private bool UpdateEatFurnitureWork(MercenaryController mercenary, BaseBuildManager buildManager, double delta)
	{
		if (buildManager.GetResourceAmount(BaseResourceType.Food) <= 0)
		{
			SetIdleWorkStatus("\uC2DD\uB7C9 \uC5C6\uC74C");
			ResetFurnitureUseState(mercenary);
			return true;
		}

		RoomUseBonus roomBonus = GetRoomUseBonus(mercenary, FurnitureUseType.Eat);
		SetCurrentRoomEffect(roomBonus);
		SetRestWorkStatusLabel(
			"\uC2DD\uC0AC",
			GetFurnitureDisplayName(_targetFurnitureType),
			roomBonus.HasEffect ? $"\uC2DD\uC0AC \uC911, {roomBonus.EffectLabel} \uC801\uC6A9" : "\uC2DD\uC0AC \uC911",
			roomBonus.HasEffect ? $"\uBC30\uACE0\uD514 \uB192\uC74C / {roomBonus.EffectLabel}" : "\uBC30\uACE0\uD514 \uB192\uC74C");
		_eatWorkTimer += (float)delta;
		SetUseProgress(
			Mathf.Min(_eatWorkTimer, EatDurationSeconds),
			EatDurationSeconds,
			$"\uC2DD\uC0AC {Mathf.Min(_eatWorkTimer, EatDurationSeconds):0.0}s / {EatDurationSeconds:0.0}s");

		if (_eatWorkTimer < EatDurationSeconds)
		{
			return false;
		}

		if (!buildManager.TryConsumeResource(BaseResourceType.Food, 1))
		{
			SetIdleWorkStatus("\uC2DD\uB7C9 \uC5C6\uC74C");
			ResetFurnitureUseState(mercenary);
			return true;
		}

		MercenaryCondition condition = mercenary.Profile.Condition;
		ApplyHungerRecover(condition, EatHungerRecover * roomBonus.HungerRecoverMultiplier);
		condition.Mood = Mathf.Clamp(condition.Mood + EatMoodRecover + roomBonus.FlatMoodRecoverBonus, 0.0f, 100.0f);
		condition.Stress = Mathf.Clamp(condition.Stress - EatStressRecover - roomBonus.FlatStressRecoverBonus, 0.0f, 100.0f);
		ResetFurnitureUseState(mercenary);
		return true;
	}

	private bool UpdateSleepFurnitureWork(MercenaryController mercenary, double delta)
	{
		MercenaryCondition condition = mercenary.Profile.Condition;
		RoomUseBonus roomBonus = GetRoomUseBonus(mercenary, FurnitureUseType.Sleep);
		SetCurrentRoomEffect(roomBonus);
		float multiplier = Mathf.Min(2.0f, GetSleepFurnitureRecoverMultiplier(_targetFurnitureType) * roomBonus.SleepRecoverMultiplier);
		SetRestWorkStatusLabel(
			"\uC218\uBA74",
			GetFurnitureDisplayName(_targetFurnitureType),
			roomBonus.HasEffect ? $"\uC218\uBA74 \uC911, {roomBonus.EffectLabel} \uC801\uC6A9" : "\uC218\uBA74 \uC911",
			roomBonus.HasEffect ? $"\uC878\uB9BC \uB192\uC74C / {roomBonus.EffectLabel}" : "\uC878\uB9BC \uB192\uC74C");
		ApplySleepRecover(condition, SleepRecoverPerSecond * multiplier * (float)delta);
		condition.Stress = Mathf.Clamp(condition.Stress - (SleepStressRecoverPerSecond + roomBonus.FlatStressRecoverBonus) * (float)delta, 0.0f, 100.0f);
		condition.Mood = Mathf.Clamp(condition.Mood + (SleepMoodRecoverPerSecond + roomBonus.FlatMoodRecoverBonus) * (float)delta, 0.0f, 100.0f);
		SetUseProgress(
			Mathf.Clamp(100.0f - condition.Sleepiness, 0.0f, 100.0f),
			100.0f - SleepTargetSleepiness,
			$"\uC878\uB9BC {condition.Sleepiness:0} \u2192 {SleepTargetSleepiness:0}");

		if (condition.Sleepiness <= SleepTargetSleepiness || condition.Hunger >= 90.0f)
		{
			ResetFurnitureUseState(mercenary);
			return true;
		}

		return false;
	}

	private bool UpdateRelaxFurnitureWork(MercenaryController mercenary, double delta)
	{
		MercenaryCondition condition = mercenary.Profile.Condition;
		RoomUseBonus roomBonus = GetRoomUseBonus(mercenary, FurnitureUseType.Relax);
		SetCurrentRoomEffect(roomBonus);
		string relaxReason = condition.Stress >= 70.0f ? "\uC2A4\uD2B8\uB808\uC2A4 \uB192\uC74C" : "\uAE30\uBD84 \uB0AE\uC74C";
		SetRestWorkStatusLabel(
			"\uD734\uC2DD",
			GetFurnitureDisplayName(_targetFurnitureType),
			roomBonus.HasEffect ? $"\uD734\uC2DD \uC911, {roomBonus.EffectLabel} \uC801\uC6A9" : "\uD734\uC2DD \uC911",
			roomBonus.HasEffect ? $"{relaxReason} / {roomBonus.EffectLabel}" : relaxReason);
		_relaxWorkTimer += (float)delta;
		condition.Stress = Mathf.Clamp(condition.Stress - RelaxStressRecoverPerSecond * roomBonus.StressRecoverMultiplier * (float)delta, 0.0f, 100.0f);
		condition.Mood = Mathf.Clamp(condition.Mood + RelaxMoodRecoverPerSecond * roomBonus.MoodRecoverMultiplier * (float)delta, 0.0f, 100.0f);
		SetUseProgress(
			_relaxWorkTimer,
			RelaxMinSeconds,
			$"\uC2A4\uD2B8\uB808\uC2A4 {condition.Stress:0} / \uAE30\uBD84 {condition.Mood:0}");

		if (_relaxWorkTimer >= RelaxMinSeconds
			&& condition.Stress <= RelaxEndStressThreshold
			&& condition.Mood >= RelaxEndMoodThreshold)
		{
			ResetFurnitureUseState(mercenary);
			return true;
		}

		return false;
	}

	private RoomUseBonus GetRoomUseBonus(MercenaryController mercenary, FurnitureUseType useType)
	{
		BaseRoomManager? roomManager = GetTree().CurrentScene?.GetNodeOrNull<BaseRoomManager>("EffectLayer/BaseRoomManager")
			?? GetTree().CurrentScene?.GetNodeOrNull<BaseRoomManager>("BaseRoomManager");

		if (roomManager == null)
		{
			return RoomUseBonus.None;
		}

		if (_targetFurnitureOriginCell.HasValue)
		{
			BaseRoom? originRoom = roomManager.GetRoomAtCell(_targetFurnitureOriginCell.Value);

			if (originRoom != null)
			{
				return originRoom.IsValid ? CreateRoomUseBonus(useType, originRoom) : RoomUseBonus.None;
			}
		}

		if (_targetFurnitureAccessCell.HasValue)
		{
			BaseRoom? accessRoom = roomManager.GetRoomAtCell(_targetFurnitureAccessCell.Value);

			if (accessRoom != null)
			{
				return accessRoom.IsValid ? CreateRoomUseBonus(useType, accessRoom) : RoomUseBonus.None;
			}
		}

		if (mercenary.GetBaseBuildManager() is BaseBuildManager buildManager)
		{
			BaseRoom? currentRoom = roomManager.GetRoomAtCell(buildManager.WorldToCell(mercenary.GlobalPosition));

			if (currentRoom != null)
			{
				return currentRoom.IsValid ? CreateRoomUseBonus(useType, currentRoom) : RoomUseBonus.None;
			}
		}

		return RoomUseBonus.None;
	}

	private static RoomUseBonus CreateRoomUseBonus(FurnitureUseType useType, BaseRoom room)
	{
		float quality = Mathf.Clamp(room.QualityScore, 0, 5);

		if (useType == FurnitureUseType.Eat)
		{
			if (room.RoomType == RoomType.DiningRoom)
			{
				float qualityBonus = quality * 0.05f;
				return new RoomUseBonus(
					true,
					room.RoomType,
					$"\uC2DD\uB2F9 \u2605{room.QualityScore}",
					Mathf.Min(1.35f, 1.10f + qualityBonus),
					1.0f,
					1.0f,
					1.0f,
					2.0f,
					1.0f);
			}

			if (room.RoomType == RoomType.Kitchen)
			{
				return new RoomUseBonus(
					true,
					room.RoomType,
					$"\uC8FC\uBC29 \u2605{room.QualityScore}",
					1.0f,
					1.0f,
					1.0f,
					1.0f,
					1.0f,
					0.0f);
			}
		}

		if (useType == FurnitureUseType.Sleep)
		{
			if (room.RoomType == RoomType.PrivateRoom)
			{
				float qualityBonus = quality * 0.03f;
				return new RoomUseBonus(
					true,
					room.RoomType,
					$"\uAC1C\uC778\uC2E4 \u2605{room.QualityScore}",
					1.0f,
					Mathf.Min(1.40f, 1.15f + qualityBonus),
					1.0f,
					1.0f,
					0.2f,
					0.5f);
			}

			if (room.RoomType == RoomType.Dormitory)
			{
				float qualityBonus = quality * 0.03f;
				return new RoomUseBonus(
					true,
					room.RoomType,
					$"\uACF5\uC6A9 \uCE68\uC2E4 \u2605{room.QualityScore}",
					1.0f,
					Mathf.Min(1.30f, 1.08f + qualityBonus),
					1.0f,
					1.0f,
					0.1f,
					0.2f);
			}
		}

		if (useType == FurnitureUseType.Relax && room.RoomType == RoomType.Lounge)
		{
			float qualityBonus = quality * 0.05f;
			return new RoomUseBonus(
				true,
				room.RoomType,
				$"\uD734\uAC8C\uC2E4 \u2605{room.QualityScore}",
				1.0f,
				1.0f,
				Mathf.Min(1.55f, 1.25f + qualityBonus),
				Mathf.Min(1.50f, 1.20f + qualityBonus),
				0.0f,
				0.0f);
		}

		return RoomUseBonus.None;
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

	private void ResetConstructionState(MercenaryController? mercenary, bool dropMaterials)
	{
		bool wasConstructionAction = IsConstructionAction();
		ConstructionSite? site = _targetConstructionSite;

		if (mercenary != null && site != null)
		{
			BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();
			buildManager?.ReleaseSiteMaterialDelivery(site, mercenary);
			buildManager?.ReleaseBuildWork(site, mercenary);
		}

		if (dropMaterials && mercenary != null)
		{
			DropConstructionMaterialAtCurrentCell(mercenary);
		}

		ReleaseStorageInteractionReservation(mercenary);
		_targetConstructionSite = null;
		_constructionMaterialType = BaseResourceType.Wood;
		_constructionMaterialAmount = 0;
		_constructionStorageCell = null;
		_constructionStorageAccessCell = null;
		_constructionSiteAccessCell = null;
		_constructionInteractionTimer = 0.0f;
		_constructionInteractionDuration = 0.0f;
		ClearUseProgress();

		if (wasConstructionAction)
		{
			_targetPoint = null;
			CurrentLifeAction = "Idle";
		}
	}

	private void DropConstructionMaterialAtCurrentCell(MercenaryController mercenary)
	{
		int dropAmount = mercenary.Inventory.GetAmount(_constructionMaterialType);

		if (dropAmount <= 0)
		{
			return;
		}

		BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

		if (buildManager == null)
		{
			return;
		}

		Vector2I currentCell = buildManager.WorldToCell(mercenary.GlobalPosition);
		buildManager.TrySpawnOrMergeResourcePile(_constructionMaterialType, currentCell, dropAmount);
		mercenary.Inventory.TryRemove(_constructionMaterialType, dropAmount, out _);
	}

	private void ResetCraftState(MercenaryController? mercenary, bool keepCarriedResource)
	{
		bool wasCraftAction = IsCraftAction();
		CraftJob? job = _targetCraftJob;

		if (mercenary != null && job != null)
		{
			job.ReleaseDelivery(mercenary);
			job.ReleaseCrafting(mercenary);
			job.ReleaseOutputPickup(mercenary);
		}

		ReleaseStorageInteractionReservation(mercenary);
		_targetCraftJob = null;
		_craftMaterialType = BaseResourceType.Wood;
		_craftMaterialAmount = 0;
		_craftStorageCell = null;
		_craftStorageAccessCell = null;
		_craftFacilityAccessCell = null;
		_craftInteractionTimer = 0.0f;
		_craftInteractionDuration = 0.0f;
		ClearUseProgress();

		if (!keepCarriedResource && mercenary != null && _isCarryingResource)
		{
			_carriedResourceAmount = mercenary.Inventory.GetAmount(_carriedResourceType);
			_isCarryingResource = _carriedResourceAmount > 0;
			_isHaulingToStorage = _isCarryingResource;
		}

		if (wasCraftAction)
		{
			_targetPoint = null;
			CurrentLifeAction = "Idle";
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

		_isDepositingToStorage = false;
		_isCarryingResource = false;
		_isHaulingToStorage = false;
		_preserveCarriedResourceOnDepositFailure = false;
		_carriedResourceAmount = 0;
		_storageInteractionTimer = 0.0f;
		_storageInteractionDuration = 0.0f;
		ClearUseProgress();
		ReleaseStorageInteractionReservation(mercenary);
		_targetHaulPile = null;
		_targetHaulPileLabel = "-";
		_targetStorageCell = null;
		_targetStorageAccessCell = null;

		if (CurrentLifeAction == "Haul")
		{
			_targetPoint = null;
		}

		if (mercenary != null)
		{
			RunLogisticsValidation(mercenary);
		}
	}

	private void AbortCarriedResourceDepositWithoutDrop(string reason)
	{
		_isDepositingToStorage = false;
		_isCarryingResource = false;
		_isHaulingToStorage = false;
		_preserveCarriedResourceOnDepositFailure = false;
		_carriedResourceAmount = 0;
		_storageInteractionTimer = 0.0f;
		_storageInteractionDuration = 0.0f;
		_reservedStorageInteractionCell = null;
		_targetStorageCell = null;
		_targetStorageAccessCell = null;
		ClearUseProgress();
		SetIdleWorkStatus(reason);
	}

	private void ReleaseStorageInteractionReservation(MercenaryController? mercenary)
	{
		mercenary ??= GetParent() as MercenaryController;

		if (!_reservedStorageInteractionCell.HasValue || mercenary == null)
		{
			_reservedStorageInteractionCell = null;
			return;
		}

		BaseBuildManager? buildManager = mercenary.GetBaseBuildManager()
			?? GetTree().CurrentScene?.GetNodeOrNull<BaseBuildManager>("BuildingLayer");
		buildManager?.ReleaseStorageInteraction(_reservedStorageInteractionCell.Value, mercenary);
		_reservedStorageInteractionCell = null;
	}

	private void DropCarriedResourceAtCurrentCell(MercenaryController mercenary)
	{
		int inventoryAmount = mercenary.Inventory.GetAmount(_carriedResourceType);

		if (!_isCarryingResource)
		{
			return;
		}

		if (_carriedResourceAmount <= 0 || inventoryAmount <= 0)
		{
			if (DebugHauling)
			{
				GD.Print($"{mercenary.MercenaryName} skip drop: carried flag was true but inventory had {inventoryAmount}");
			}

			_isCarryingResource = false;
			_isHaulingToStorage = false;
			_isDepositingToStorage = false;
			_carriedResourceAmount = 0;
			ReleaseStorageInteractionReservation(mercenary);
			ClearUseProgress();
			return;
		}

		BaseBuildManager? buildManager = mercenary.GetBaseBuildManager();

		if (buildManager == null)
		{
			return;
		}

		int dropAmount = inventoryAmount;
		Vector2I currentCell = buildManager.WorldToCell(mercenary.GlobalPosition);
		buildManager.TrySpawnOrMergeResourcePile(_carriedResourceType, currentCell, dropAmount);
		mercenary.Inventory.TryRemove(_carriedResourceType, dropAmount, out _);
		_carriedResourceAmount = mercenary.Inventory.GetAmount(_carriedResourceType);
		_isCarryingResource = _carriedResourceAmount > 0;
		_isHaulingToStorage = _isCarryingResource;

		if (DebugHauling)
		{
			GD.Print($"{mercenary.MercenaryName} dropped {_carriedResourceType} x{dropAmount} at {currentCell}");
		}

		RunLogisticsValidation(mercenary);
	}

	private string GetHaulingTargetLabel()
	{
		if (_isDepositingToStorage)
		{
			return _targetStorageCell.HasValue
				? $"Depositing to Storage {_targetStorageCell.Value}"
				: "Depositing to Storage";
		}

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
