using System;

namespace GameplayV3.Company;

public sealed class CompanyStateV3
{
    internal CompanyStateV3(
        string companyId,
        string ownerPlayerId,
        string displayName,
        DateTime createdUtc)
    {
        CompanyId = companyId;
        OwnerPlayerId = ownerPlayerId;
        DisplayName = NormalizeDisplayName(displayName);
        CreatedUtc = createdUtc.Kind == DateTimeKind.Utc
            ? createdUtc
            : createdUtc.ToUniversalTime();
    }

    public string CompanyId { get; }
    public string OwnerPlayerId { get; }
    public string DisplayName { get; private set; }
    public DateTime CreatedUtc { get; }

    public bool TryRename(string displayName, out string reason)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            reason = "Company display name cannot be empty.";
            return false;
        }

        DisplayName = displayName.Trim();
        reason = string.Empty;
        return true;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? "Unnamed Mercenary Company"
            : displayName.Trim();
    }
}
