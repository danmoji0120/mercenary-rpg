using System;

namespace GameplayV3.Company;

public sealed class PlayerIdentityV3
{
    internal PlayerIdentityV3(
        string playerId,
        string displayName,
        DateTime createdUtc,
        bool isLocalPlayer)
    {
        PlayerId = playerId;
        DisplayName = NormalizeDisplayName(displayName);
        CreatedUtc = createdUtc.Kind == DateTimeKind.Utc
            ? createdUtc
            : createdUtc.ToUniversalTime();
        IsLocalPlayer = isLocalPlayer;
    }

    public string PlayerId { get; }
    public string DisplayName { get; private set; }
    public DateTime CreatedUtc { get; }
    public bool IsLocalPlayer { get; }

    public bool TryRename(string displayName, out string reason)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            reason = "Player display name cannot be empty.";
            return false;
        }

        DisplayName = displayName.Trim();
        reason = string.Empty;
        return true;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? "Local Player"
            : displayName.Trim();
    }
}
