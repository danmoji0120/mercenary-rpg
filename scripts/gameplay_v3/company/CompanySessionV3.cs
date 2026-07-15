using System;
using GameplayV3.Deployment;

namespace GameplayV3.Company;

public sealed class CompanySessionV3
{
    public const string DefaultLocalPlayerName = "Local Player";
    public const string DefaultLocalCompanyName = "Unnamed Mercenary Company";

    public CompanySessionV3()
    {
        CompanyRegistry = new CompanyRegistryV3();
        LocalContext = new LocalPlayerCompanyContextV3(CompanyRegistry);
        DeploymentRegistry = new CompanyDeploymentRegistryV3(CompanyRegistry);
    }

    public CompanyRegistryV3 CompanyRegistry { get; }
    public LocalPlayerCompanyContextV3 LocalContext { get; }
    public CompanyDeploymentRegistryV3 DeploymentRegistry { get; }
    public PlayerIdentityV3? LocalPlayer { get; private set; }
    public StartingDeploymentPlacementResultV3? StartingDeploymentResult { get; private set; }
    public bool IsInitialized => LocalPlayer != null && LocalContext.IsInitialized;

    public bool TryInitializeLocalSinglePlayer(out bool createdNow, out string reason)
    {
        createdNow = false;
        if (IsInitialized)
        {
            reason = string.Empty;
            return true;
        }

        if (LocalPlayer != null || CompanyRegistry.Count != 0)
        {
            reason = "Company session is partially initialized and must be reset first.";
            return false;
        }

        PlayerIdentityV3 player = new(
            CompanyIdFactoryV3.CreatePlayerId(),
            DefaultLocalPlayerName,
            DateTime.UtcNow,
            isLocalPlayer: true);

        if (!LocalContext.InitializeLocalPlayer(player, out reason))
        {
            Reset();
            return false;
        }

        if (!CompanyRegistry.TryCreateCompany(
                player.PlayerId,
                DefaultLocalCompanyName,
                out CompanyStateV3? company,
                out reason)
            || company == null)
        {
            Reset();
            return false;
        }

        if (!LocalContext.AssignLocalCompany(company.CompanyId, out reason))
        {
            Reset();
            return false;
        }

        LocalPlayer = player;
        createdNow = true;
        reason = string.Empty;
        return true;
    }

    public bool CanPlayerControlCompany(string playerId, string companyId)
    {
        return CompanyOwnershipRulesV3.CanPlayerControlCompany(playerId, companyId, CompanyRegistry);
    }

    public void Reset()
    {
        LocalContext.Reset();
        DeploymentRegistry.Clear();
        CompanyRegistry.Clear();
        StartingDeploymentResult = null;
        LocalPlayer = null;
    }

    internal void SetStartingDeploymentResult(StartingDeploymentPlacementResultV3 result)
    {
        StartingDeploymentResult = result;
    }
}
