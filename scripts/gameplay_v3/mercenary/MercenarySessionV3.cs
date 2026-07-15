using GameplayV3.Company;
using GameplayV3.Deployment;
using Godot;

namespace GameplayV3.Mercenary;

public sealed class MercenarySessionV3
{
    private readonly CompanyRegistryV3 _companyRegistry;

    public MercenarySessionV3(CompanyRegistryV3 companyRegistry)
    {
        _companyRegistry = companyRegistry;
        Registry = new MercenaryRegistryV3(companyRegistry);
    }

    public MercenaryRegistryV3 Registry { get; }
    public InitialSquadCreationResultV3? LastInitialSquadCreationResult { get; private set; }
    public bool IsInitialized => LastInitialSquadCreationResult?.Succeeded == true;

    public InitialSquadCreationResultV3 CreateOrReuseInitialSquad(
        string companyId,
        CompanyDeploymentRegistryV3 deploymentRegistry,
        Rect2I worldBounds)
    {
        LastInitialSquadCreationResult = InitialSquadCreationServiceV3.CreateOrReuseInitialSquad(
            companyId,
            _companyRegistry,
            deploymentRegistry,
            Registry,
            worldBounds);
        return LastInitialSquadCreationResult;
    }

    public bool CanPlayerControlMercenary(string playerId, string mercenaryId)
    {
        return MercenaryOwnershipRulesV3.CanPlayerControlMercenary(playerId, mercenaryId, Registry, _companyRegistry);
    }

    public void Reset()
    {
        Registry.Clear();
        LastInitialSquadCreationResult = null;
    }
}
