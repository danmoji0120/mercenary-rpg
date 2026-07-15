using System;

namespace GameplayV3.Company;

public static class CompanyIdFactoryV3
{
    private const string PlayerPrefix = "ply_";
    private const string CompanyPrefix = "cmp_";
    private const int GuidTextLength = 32;

    public static string CreatePlayerId()
    {
        return PlayerPrefix + Guid.NewGuid().ToString("N");
    }

    public static string CreateCompanyId()
    {
        return CompanyPrefix + Guid.NewGuid().ToString("N");
    }

    public static bool IsValidPlayerId(string? playerId)
    {
        return IsCanonicalId(playerId, PlayerPrefix);
    }

    public static bool IsValidCompanyId(string? companyId)
    {
        return IsCanonicalId(companyId, CompanyPrefix);
    }

    private static bool IsCanonicalId(string? value, string prefix)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != prefix.Length + GuidTextLength
            || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> guidText = value.AsSpan(prefix.Length);
        for (int index = 0; index < guidText.Length; index++)
        {
            char character = guidText[index];
            bool isDigit = character is >= '0' and <= '9';
            bool isLowerHex = character is >= 'a' and <= 'f';
            if (!isDigit && !isLowerHex)
            {
                return false;
            }
        }

        return true;
    }
}
