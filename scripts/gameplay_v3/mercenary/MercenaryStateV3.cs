using System;
using GameplayV3.Company;
using WorldV2;

namespace GameplayV3.Mercenary;

public sealed class MercenaryStateV3
{
    private MercenaryStateV3(
        string mercenaryId,
        string companyId,
        GlobalCellCoord currentCell,
        MercenaryActivityStateV3 activityState,
        DateTime createdUtc)
    {
        MercenaryId = mercenaryId;
        CompanyId = companyId;
        CurrentCell = currentCell;
        ActivityState = activityState;
        CreatedUtc = createdUtc.Kind == DateTimeKind.Utc ? createdUtc : createdUtc.ToUniversalTime();
    }

    public string MercenaryId { get; }
    public string CompanyId { get; }
    public GlobalCellCoord CurrentCell { get; private set; }
    public MercenaryActivityStateV3 ActivityState { get; private set; }
    public DateTime CreatedUtc { get; }

    public static bool TryCreate(
        string mercenaryId,
        string companyId,
        GlobalCellCoord currentCell,
        MercenaryActivityStateV3 activityState,
        DateTime createdUtc,
        out MercenaryStateV3? state,
        out string reason)
    {
        if (!MercenaryIdFactoryV3.IsValidMercenaryId(mercenaryId)
            || !CompanyIdFactoryV3.IsValidCompanyId(companyId))
        {
            state = null;
            reason = "MercenaryId and CompanyId must be canonical.";
            return false;
        }

        if (!Enum.IsDefined(activityState))
        {
            state = null;
            reason = "ActivityState must be valid.";
            return false;
        }

        state = new MercenaryStateV3(mercenaryId, companyId, currentCell, activityState, createdUtc);
        reason = string.Empty;
        return true;
    }

    public bool TrySetCurrentCell(GlobalCellCoord currentCell, out string reason)
    {
        CurrentCell = currentCell;
        reason = string.Empty;
        return true;
    }

    public bool TrySetActivityState(MercenaryActivityStateV3 activityState, out string reason)
    {
        if (!Enum.IsDefined(activityState))
        {
            reason = "ActivityState is invalid.";
            return false;
        }

        ActivityState = activityState;
        reason = string.Empty;
        return true;
    }

    public bool IsOwnedByCompany(string companyId)
    {
        return CompanyId == companyId;
    }
}
