using GameplayV3.Construction.Runtime;
using GameplayV3.Control;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Mercenary.UI;

public enum MercenaryInspectHudModeV3
{
    None,
    Single,
    Multiple
}

public sealed class MercenaryInspectHudSnapshotV3
{
    public required string MercenaryId { get; init; }
    public required string DisplayName { get; init; }
    public required MercenaryProfileV3 Profile { get; init; }
    public required MercenaryStateV3 State { get; init; }
    public required MercenaryConditionSnapshotV3 Condition { get; init; }
    public required MercenaryDerivedStatsV3 Derived { get; init; }
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = "대기";
    public string CommandSource { get; set; } = "자유 행동";
    public string WorkType { get; set; } = "대기";
    public string Phase { get; set; } = "없음";
    public string Target { get; set; } = "없음";
    public string WorkRequestId { get; set; } = string.Empty;
    public string Carry { get; set; } = "없음";
    public GlobalCellCoord? Destination { get; set; }
    public float Progress { get; set; }
    public float Required { get; set; }
    public bool HasProgress { get; set; }
    public int MaxCarryUnits { get; set; }
    public float ProgressNormalized => HasProgress && Required > 0f && float.IsFinite(Progress) && float.IsFinite(Required)
        ? Mathf.Clamp(Progress / Required, 0f, 1f)
        : 0f;
}

public static class MercenaryHudTextFormatterV3
{
    public static string Activity(MercenaryActivityStateV3 value) => value switch
    {
        MercenaryActivityStateV3.Idle => "대기",
        MercenaryActivityStateV3.Moving => "이동 중",
        MercenaryActivityStateV3.Working => "작업 중",
        _ => "확인 필요"
    };

    public static string Work(WorkTypeV3 value) => value switch
    {
        WorkTypeV3.Gathering => "채집",
        WorkTypeV3.Hauling => "운반",
        WorkTypeV3.Construction => "건설",
        WorkTypeV3.Demolition => "철거",
        _ => "확인 필요"
    };

    public static string Resource(object value) => value.ToString() switch
    {
        "Wood" => "목재",
        "Stone" => "석재",
        _ => "자원"
    };

    public static string Phase(string raw) => raw switch
    {
        "SelectingApproach" => "접근 위치 선택",
        "WaitingForPath" or "WaitingForSourcePath" or "WaitingForDestinationPath" => "경로 대기",
        "MovingToApproach" or "MovingToApproachCell" => "접근 위치로 이동",
        "Working" => "작업 중",
        "PlanningTrip" => "운반 계획",
        "MovingToSource" or "MoveToSource" => "자원으로 이동",
        "PickingUp" or "PickUp" => "싣는 중",
        "MovingToDestination" => "목적지로 이동",
        "DroppingOff" => "내리는 중",
        "MoveToBlueprint" => "청사진으로 이동",
        "Deliver" => "자재 전달",
        "MoveToBuild" => "건설 위치로 이동",
        "Build" => "건설 중",
        "WaitingClear" => "작업 공간 비움 대기",
        "Completed" => "완료",
        "Failed" => "실패",
        "Cancelled" => "취소",
        _ => raw
    };

    public static string Skill(MercenaryWorkSkillTypeV3 type) => type switch
    {
        MercenaryWorkSkillTypeV3.Hauling => "운반",
        MercenaryWorkSkillTypeV3.Construction => "건설",
        MercenaryWorkSkillTypeV3.Gathering => "채집",
        MercenaryWorkSkillTypeV3.Farming => "농사",
        MercenaryWorkSkillTypeV3.Production => "생산",
        MercenaryWorkSkillTypeV3.Medicine => "치료",
        MercenaryWorkSkillTypeV3.Guarding => "경계",
        _ => "-"
    };
}

public sealed class MercenaryInspectHudSnapshotBuilderV3
{
    private readonly MercenarySessionV3 _mercenaries;
    private readonly MercenaryControlSessionV3 _control;
    private readonly MercenaryWorkSessionV3 _work;
    private readonly ConstructionWorkCoordinatorV3? _construction;
    private readonly DemolitionWorkCoordinatorV3? _demolition;
    private readonly IMercenaryConditionSnapshotProviderV3 _conditions;
    private readonly string _companyName;

    public MercenaryInspectHudSnapshotBuilderV3(
        MercenarySessionV3 mercenaries,
        MercenaryControlSessionV3 control,
        MercenaryWorkSessionV3 work,
        string companyName,
        IMercenaryConditionSnapshotProviderV3 conditions,
        ConstructionWorkCoordinatorV3? construction,
        DemolitionWorkCoordinatorV3? demolition)
    {
        _mercenaries = mercenaries;
        _control = control;
        _work = work;
        _companyName = companyName;
        _conditions = conditions;
        _construction = construction;
        _demolition = demolition;
    }

    public bool TryBuild(string mercenaryId, out MercenaryInspectHudSnapshotV3? snapshot)
    {
        snapshot = null;
        if (!_mercenaries.Registry.TryGetMercenary(mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
            || profile == null
            || state == null
            || !_conditions.TryGetSnapshot(profile, state, out MercenaryConditionSnapshotV3 condition))
        {
            return false;
        }

        MercenaryInspectHudSnapshotV3 value = new()
        {
            MercenaryId = mercenaryId,
            DisplayName = profile.DisplayName,
            Profile = profile,
            State = state,
            Condition = condition,
            Derived = MercenaryDerivedStatsCalculatorV3.Calculate(profile),
            CompanyName = _companyName,
            Status = MercenaryHudTextFormatterV3.Activity(state.ActivityState),
            MaxCarryUnits = HaulingWorkCalculatorV3.GetMaxCarryUnits(profile)
        };

        if (_control.Movements.TryGet(mercenaryId, out var movement) && movement != null)
        {
            value.Destination = movement.DestinationCell;
        }

        if (_work.Carries.TryGetCarry(mercenaryId, out var carry) && carry != null)
        {
            value.Carry = $"{MercenaryHudTextFormatterV3.Resource(carry.ResourceType)} {carry.Amount} / {value.MaxCarryUnits}";
        }

        if (_construction?.TryGetHudSnapshot(mercenaryId, out ConstructionWorkHudSnapshotV3 construction) == true)
        {
            value.WorkRequestId = construction.WorkRequestId;
            value.WorkType = construction.IsSupply ? "건설 자재 운반" : "건설";
            value.Status = construction.IsSupply ? "자재 운반 중" : "건설 중";
            value.CommandSource = "직접 명령";
            value.Phase = MercenaryHudTextFormatterV3.Phase(construction.Phase);
            value.Target = "청사진";
            value.Destination = construction.TargetCell;
            value.Progress = construction.ProgressSeconds;
            value.Required = construction.RequiredSeconds;
            value.HasProgress = construction.RequiredSeconds > 0f;
            snapshot = value;
            return true;
        }

        if (_demolition?.TryGetHudSnapshot(mercenaryId, out DemolitionWorkHudSnapshotV3 demolition) == true)
        {
            value.WorkRequestId = demolition.WorkRequestId;
            value.WorkType = "철거";
            value.Status = "철거 중";
            value.CommandSource = "직접 명령";
            value.Phase = MercenaryHudTextFormatterV3.Phase(demolition.Phase);
            value.Target = "구조물";
            value.Destination = demolition.TargetCell;
            value.Progress = demolition.ProgressSeconds;
            value.Required = demolition.RequiredSeconds;
            value.HasProgress = demolition.RequiredSeconds > 0f;
            snapshot = value;
            return true;
        }

        if (_work.TryGetAssignment(mercenaryId, out MercenaryWorkAssignmentV3? assignment)
            && assignment != null
            && _work.TryGetRequest(assignment.WorkRequestId, out WorkRequestV3? request)
            && request != null)
        {
            value.WorkRequestId = request.WorkRequestId;
            value.WorkType = MercenaryHudTextFormatterV3.Work(request.WorkType);
            value.Status = $"{value.WorkType} 중";
            value.CommandSource = "직접 명령";
            value.Target = request.WorkType == WorkTypeV3.Gathering ? "자원 노드" : request.WorkType == WorkTypeV3.Hauling ? "저장 영역" : "목표";
            value.Destination = request.TargetCell;
            if (request.WorkType == WorkTypeV3.Hauling
                && _work.TryGetHaulingExecution(request.WorkRequestId, out HaulingWorkExecutionStateV3? hauling)
                && hauling != null)
            {
                value.Phase = MercenaryHudTextFormatterV3.Phase(hauling.Phase.ToString());
                value.Progress = hauling.HandlingProgressSeconds;
                value.Required = hauling.RequiredHandlingSeconds;
                value.HasProgress = hauling.Phase is HaulingExecutionPhaseV3.PickingUp or HaulingExecutionPhaseV3.DroppingOff;
            }
            else if (_work.TryGetExecution(request.WorkRequestId, out MercenaryWorkExecutionStateV3? execution) && execution != null)
            {
                value.Phase = MercenaryHudTextFormatterV3.Phase(execution.Phase.ToString());
                value.Progress = execution.WorkProgressSeconds;
                value.Required = execution.RequiredWorkSeconds;
                value.HasProgress = execution.Phase == WorkExecutionPhaseV3.Working;
            }

            snapshot = value;
            return true;
        }

        if (_control.Commands.TryGetActiveOrder(mercenaryId, out MercenaryMoveOrderV3? order) && order != null)
        {
            value.WorkType = "이동";
            value.Status = "이동 중";
            value.CommandSource = "직접 이동";
            value.Phase = "목적지로 이동";
            value.Target = $"Cell {order.DestinationCell}";
            value.Destination = order.DestinationCell;
            value.WorkRequestId = order.CommandId;
        }

        snapshot = value;
        return true;
    }
}
