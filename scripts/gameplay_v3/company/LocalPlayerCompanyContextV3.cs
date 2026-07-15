namespace GameplayV3.Company;

public sealed class LocalPlayerCompanyContextV3
{
    private readonly CompanyRegistryV3 _registry;
    private bool _hasLocalPlayer;

    public LocalPlayerCompanyContextV3(CompanyRegistryV3 registry)
    {
        _registry = registry;
    }

    public string LocalPlayerId { get; private set; } = string.Empty;
    public string LocalCompanyId { get; private set; } = string.Empty;
    public bool IsInitialized => _hasLocalPlayer && !string.IsNullOrEmpty(LocalCompanyId);

    public bool InitializeLocalPlayer(PlayerIdentityV3? player, out string reason)
    {
        if (player == null || !CompanyIdFactoryV3.IsValidPlayerId(player.PlayerId))
        {
            reason = "A valid local PlayerIdentityV3 is required.";
            return false;
        }

        if (!player.IsLocalPlayer)
        {
            reason = "The player identity is not marked as local.";
            return false;
        }

        if (_hasLocalPlayer)
        {
            if (LocalPlayerId == player.PlayerId)
            {
                reason = string.Empty;
                return true;
            }

            reason = "Local player is already initialized for this session.";
            return false;
        }

        LocalPlayerId = player.PlayerId;
        _hasLocalPlayer = true;
        reason = string.Empty;
        return true;
    }

    public bool AssignLocalCompany(string companyId, out string reason)
    {
        if (!_hasLocalPlayer)
        {
            reason = "Local player must be initialized before assigning a company.";
            return false;
        }

        if (!CompanyIdFactoryV3.IsValidCompanyId(companyId))
        {
            reason = "A valid CompanyId is required.";
            return false;
        }

        if (!string.IsNullOrEmpty(LocalCompanyId))
        {
            if (LocalCompanyId == companyId)
            {
                reason = string.Empty;
                return true;
            }

            reason = "Local company is already assigned for this session.";
            return false;
        }

        if (!_registry.TryGetCompany(companyId, out CompanyStateV3? company) || company == null)
        {
            reason = "The local company is not registered.";
            return false;
        }

        if (company.OwnerPlayerId != LocalPlayerId)
        {
            reason = "The local player does not own the requested company.";
            return false;
        }

        LocalCompanyId = companyId;
        reason = string.Empty;
        return true;
    }

    public bool TryGetLocalCompany(out CompanyStateV3? company, out string reason)
    {
        company = null;
        if (!IsInitialized)
        {
            reason = "Local player/company context is not initialized.";
            return false;
        }

        if (!_registry.TryGetCompany(LocalCompanyId, out company) || company == null)
        {
            reason = "The assigned local company is missing from the registry.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryGetLocalPlayerId(out string playerId, out string reason)
    {
        playerId = string.Empty;
        if (!_hasLocalPlayer)
        {
            reason = "Local player context is not initialized.";
            return false;
        }

        playerId = LocalPlayerId;
        reason = string.Empty;
        return true;
    }

    public bool TryGetLocalCompanyId(out string companyId, out string reason)
    {
        companyId = string.Empty;
        if (!IsInitialized)
        {
            reason = "Local player/company context is not initialized.";
            return false;
        }

        companyId = LocalCompanyId;
        reason = string.Empty;
        return true;
    }

    public bool IsLocalCompany(string companyId)
    {
        return IsInitialized && LocalCompanyId == companyId;
    }

    public bool CanLocalPlayerControl(string companyId)
    {
        return IsInitialized
            && CompanyOwnershipRulesV3.CanPlayerControlCompany(LocalPlayerId, companyId, _registry);
    }

    public void Reset()
    {
        LocalPlayerId = string.Empty;
        LocalCompanyId = string.Empty;
        _hasLocalPlayer = false;
    }
}
