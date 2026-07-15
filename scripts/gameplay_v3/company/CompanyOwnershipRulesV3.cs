namespace GameplayV3.Company;

public static class CompanyOwnershipRulesV3
{
    public static bool CanPlayerControlCompany(
        string playerId,
        string companyId,
        CompanyRegistryV3 registry)
    {
        return CompanyIdFactoryV3.IsValidPlayerId(playerId)
            && CompanyIdFactoryV3.IsValidCompanyId(companyId)
            && registry.TryGetCompany(companyId, out CompanyStateV3? company)
            && company != null
            && company.OwnerPlayerId == playerId;
    }
}
