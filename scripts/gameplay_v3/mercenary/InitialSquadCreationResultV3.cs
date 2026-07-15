using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GameplayV3.Mercenary;

public sealed class InitialSquadCreationResultV3
{
    public InitialSquadCreationResultV3(
        bool succeeded,
        bool reusedExisting,
        string companyId,
        IReadOnlyList<string> mercenaryIds,
        int rollbackCount,
        string failureReason)
    {
        Succeeded = succeeded;
        ReusedExisting = reusedExisting;
        CompanyId = companyId;
        MercenaryIds = new ReadOnlyCollection<string>(new List<string>(mercenaryIds));
        RollbackCount = rollbackCount;
        FailureReason = failureReason;
    }

    public bool Succeeded { get; }
    public bool ReusedExisting { get; }
    public string CompanyId { get; }
    public IReadOnlyList<string> MercenaryIds { get; }
    public int RollbackCount { get; }
    public string FailureReason { get; }
}
