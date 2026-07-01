using System.Collections.Generic;

public sealed class MercenaryWorkSettings
{
    public static readonly MercenaryWorkType[] OrderedTypes =
    {
        MercenaryWorkType.Haul,
        MercenaryWorkType.Farm,
        MercenaryWorkType.Build,
        MercenaryWorkType.Gather,
        MercenaryWorkType.Guard,
        MercenaryWorkType.Medical,
        MercenaryWorkType.Craft,
        MercenaryWorkType.Rest
    };

    private readonly Dictionary<MercenaryWorkType, MercenaryWorkPriority> _priorities = new();

    public MercenaryWorkPriority GetPriority(MercenaryWorkType workType)
    {
        return _priorities.TryGetValue(workType, out MercenaryWorkPriority priority)
            ? priority
            : MercenaryWorkPriority.Normal;
    }

    public void SetPriority(MercenaryWorkType workType, MercenaryWorkPriority priority)
    {
        _priorities[workType] = priority;
    }

    public MercenaryWorkPriority CyclePriority(MercenaryWorkType workType)
    {
        MercenaryWorkPriority next = GetPriority(workType) switch
        {
            MercenaryWorkPriority.Disabled => MercenaryWorkPriority.Low,
            MercenaryWorkPriority.Low => MercenaryWorkPriority.Normal,
            MercenaryWorkPriority.Normal => MercenaryWorkPriority.High,
            _ => MercenaryWorkPriority.Disabled
        };

        SetPriority(workType, next);
        return next;
    }

    public bool IsEnabled(MercenaryWorkType workType)
    {
        return GetPriority(workType) != MercenaryWorkPriority.Disabled;
    }

    public float GetChanceMultiplier(MercenaryWorkType workType)
    {
        return GetPriority(workType) switch
        {
            MercenaryWorkPriority.Disabled => 0.0f,
            MercenaryWorkPriority.Low => 0.65f,
            MercenaryWorkPriority.High => 1.35f,
            _ => 1.0f
        };
    }

    public MercenaryWorkSettings Clone()
    {
        MercenaryWorkSettings clone = new();

        foreach (MercenaryWorkType workType in OrderedTypes)
        {
            clone.SetPriority(workType, GetPriority(workType));
        }

        return clone;
    }

    public string GetCompactSummary()
    {
        return $"{GetWorkTypeDisplayName(MercenaryWorkType.Haul)} {GetPriorityDisplayName(GetPriority(MercenaryWorkType.Haul))} / "
            + $"{GetWorkTypeDisplayName(MercenaryWorkType.Farm)} {GetPriorityDisplayName(GetPriority(MercenaryWorkType.Farm))} / "
            + $"{GetWorkTypeDisplayName(MercenaryWorkType.Build)} {GetPriorityDisplayName(GetPriority(MercenaryWorkType.Build))} / "
            + $"{GetWorkTypeDisplayName(MercenaryWorkType.Gather)} {GetPriorityDisplayName(GetPriority(MercenaryWorkType.Gather))}";
    }

    public string GetTopSummary(int maxCount)
    {
        List<string> parts = new();

        foreach (MercenaryWorkPriority priority in new[] { MercenaryWorkPriority.High, MercenaryWorkPriority.Normal, MercenaryWorkPriority.Low })
        {
            foreach (MercenaryWorkType workType in OrderedTypes)
            {
                if (GetPriority(workType) != priority)
                {
                    continue;
                }

                parts.Add($"{GetWorkTypeDisplayName(workType)} {GetPriorityDisplayName(priority)}");

                if (parts.Count >= maxCount)
                {
                    return string.Join(" / ", parts);
                }
            }
        }

        return parts.Count == 0 ? "-" : string.Join(" / ", parts);
    }

    public static MercenaryWorkSettings CreateDefault()
    {
        MercenaryWorkSettings settings = new();

        foreach (MercenaryWorkType workType in OrderedTypes)
        {
            settings.SetPriority(workType, MercenaryWorkPriority.Normal);
        }

        settings.SetPriority(MercenaryWorkType.Medical, MercenaryWorkPriority.Low);
        settings.SetPriority(MercenaryWorkType.Craft, MercenaryWorkPriority.Low);
        return settings;
    }

    public static MercenaryWorkSettings CreateForRoles(MercenaryRole primaryRole, MercenaryRole secondaryRole)
    {
        MercenaryWorkSettings settings = CreateDefault();
        ApplyRoleDefaults(settings, primaryRole, true);

        if (secondaryRole != primaryRole)
        {
            ApplyRoleDefaults(settings, secondaryRole, false);
        }

        return settings;
    }

    public static string GetWorkTypeDisplayName(MercenaryWorkType workType)
    {
        return workType switch
        {
            MercenaryWorkType.Haul => "\uC6B4\uBC18",
            MercenaryWorkType.Farm => "\uB18D\uC0AC",
            MercenaryWorkType.Build => "\uAC74\uC124",
            MercenaryWorkType.Gather => "\uCC44\uC9D1",
            MercenaryWorkType.Guard => "\uACBD\uBE44",
            MercenaryWorkType.Medical => "\uCE58\uB8CC",
            MercenaryWorkType.Craft => "\uC81C\uC791",
            MercenaryWorkType.Rest => "\uD734\uC2DD",
            _ => workType.ToString()
        };
    }

    public static string GetPriorityDisplayName(MercenaryWorkPriority priority)
    {
        return priority switch
        {
            MercenaryWorkPriority.Disabled => "\uB054",
            MercenaryWorkPriority.Low => "\uB0AE\uC74C",
            MercenaryWorkPriority.High => "\uB192\uC74C",
            _ => "\uBCF4\uD1B5"
        };
    }

    private static void ApplyRoleDefaults(MercenaryWorkSettings settings, MercenaryRole role, bool isPrimary)
    {
        switch (role)
        {
            case MercenaryRole.Hauler:
                settings.SetPriority(MercenaryWorkType.Haul, MercenaryWorkPriority.High);
                settings.SetPriority(MercenaryWorkType.Gather, MercenaryWorkPriority.Normal);
                break;
            case MercenaryRole.Farmer:
                settings.SetPriority(MercenaryWorkType.Farm, isPrimary ? MercenaryWorkPriority.High : MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Haul, MercenaryWorkPriority.Normal);
                break;
            case MercenaryRole.Cook:
                settings.SetPriority(MercenaryWorkType.Farm, MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Craft, MercenaryWorkPriority.Normal);
                break;
            case MercenaryRole.Medic:
                settings.SetPriority(MercenaryWorkType.Medical, isPrimary ? MercenaryWorkPriority.High : MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Haul, MercenaryWorkPriority.Normal);
                break;
            case MercenaryRole.Crafter:
                settings.SetPriority(MercenaryWorkType.Craft, isPrimary ? MercenaryWorkPriority.High : MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Build, MercenaryWorkPriority.Normal);
                break;
            case MercenaryRole.Guard:
                settings.SetPriority(MercenaryWorkType.Guard, isPrimary ? MercenaryWorkPriority.High : MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Haul, MercenaryWorkPriority.Low);
                settings.SetPriority(MercenaryWorkType.Build, MercenaryWorkPriority.Normal);
                break;
            case MercenaryRole.Tank:
                settings.SetPriority(MercenaryWorkType.Guard, MercenaryWorkPriority.High);
                settings.SetPriority(MercenaryWorkType.Haul, MercenaryWorkPriority.Low);
                settings.SetPriority(MercenaryWorkType.Build, MercenaryWorkPriority.Normal);
                break;
            case MercenaryRole.Fighter:
                settings.SetPriority(MercenaryWorkType.Guard, MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Gather, MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Haul, MercenaryWorkPriority.Low);
                break;
            case MercenaryRole.Scout:
                settings.SetPriority(MercenaryWorkType.Gather, MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Farm, MercenaryWorkPriority.Low);
                settings.SetPriority(MercenaryWorkType.Build, MercenaryWorkPriority.Low);
                settings.SetPriority(MercenaryWorkType.Guard, MercenaryWorkPriority.Low);
                break;
            case MercenaryRole.Worker:
                settings.SetPriority(MercenaryWorkType.Haul, MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Build, MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Gather, MercenaryWorkPriority.Normal);
                settings.SetPriority(MercenaryWorkType.Farm, MercenaryWorkPriority.Low);
                break;
        }
    }
}
