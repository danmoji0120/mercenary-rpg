using GameplayV3.Company;

namespace GameplayV3.Mercenary;

public static class MercenaryOwnershipRulesV3
{
    public static bool CanPlayerControlMercenary(
        string playerId,
        string mercenaryId,
        MercenaryRegistryV3 mercenaryRegistry,
        CompanyRegistryV3 companyRegistry)
    {
        return mercenaryRegistry.TryGetState(mercenaryId, out MercenaryStateV3? state)
            && state != null
            && CompanyOwnershipRulesV3.CanPlayerControlCompany(playerId, state.CompanyId, companyRegistry);
    }
}
