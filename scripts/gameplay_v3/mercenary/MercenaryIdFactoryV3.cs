using System;

namespace GameplayV3.Mercenary;

public static class MercenaryIdFactoryV3
{
    private const string Prefix = "merc_";
    private const int GuidTextLength = 32;

    public static string CreateMercenaryId()
    {
        return Prefix + Guid.NewGuid().ToString("N");
    }

    public static bool IsValidMercenaryId(string? mercenaryId)
    {
        if (string.IsNullOrWhiteSpace(mercenaryId)
            || mercenaryId.Length != Prefix.Length + GuidTextLength
            || !mercenaryId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> value = mercenaryId.AsSpan(Prefix.Length);
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }
}
