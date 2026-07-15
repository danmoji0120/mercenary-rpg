using GameplayV3.Company;

namespace GameplayV3.Deployment;

public sealed class StartingDeploymentCoordinatorV3
{
    private readonly StartingDeploymentPlacementSettingsV3 _settings;

    public StartingDeploymentCoordinatorV3(StartingDeploymentPlacementSettingsV3? settings = null)
    {
        _settings = settings ?? StartingDeploymentPlacementSettingsV3.Default;
    }

    public bool TryEnsureDeployments(
        CompanySessionV3 companySession,
        StartingDeploymentWorldQueryV3 worldQuery,
        out StartingDeploymentPlacementResultV3 result)
    {
        result = StartingDeploymentPlacementServiceV3.AssignMissingDeployments(companySession, worldQuery, _settings);
        companySession.SetStartingDeploymentResult(result);
        return result.IsSuccessful;
    }
}
