using System;
using System.Collections.Generic;
using GameplayV3.Company;
using GameplayV3.Deployment;
using Godot;
using WorldV2;

namespace GameplayV3.Mercenary.Runtime;

public static class MercenaryRuntimeSelfCheckV3
{
    public static bool TryValidate(
        string localPlayerId,
        string localCompanyId,
        CompanyDeploymentStateV3 deployment,
        MercenarySessionV3 mercenarySession,
        MercenaryViewRegistryV3 viewRegistry,
        WorldV2GridRenderer gridRenderer,
        out string reason)
    {
        IReadOnlyList<string> mercenaryIds = mercenarySession.Registry.GetMercenariesByCompany(localCompanyId);
        if (mercenaryIds.Count != 3 || viewRegistry.Count != 3)
        {
            reason = $"Expected 3 state/view pairs, found {mercenaryIds.Count}/{viewRegistry.Count}.";
            return false;
        }

        HashSet<Vector2> positions = new();
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string mercenaryId in mercenaryIds)
        {
            if (!ids.Add(mercenaryId)
                || !mercenarySession.Registry.TryGetMercenary(mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
                || profile == null
                || state == null
                || !viewRegistry.TryGetView(mercenaryId, out MercenaryEntityV3? view)
                || view == null)
            {
                reason = $"Runtime Profile/State/View lookup failed: {mercenaryId}.";
                return false;
            }

            Vector2 expectedPosition = gridRenderer.CellToWorldCenter(state.CurrentCell.Value);
            if (!profile.IsInitialSquadMember
                || !profile.InitialSquadSlotIndex.HasValue
                || state.CurrentCell.Value != deployment.FormationCells[profile.InitialSquadSlotIndex.Value].Value
                || !view.Position.IsEqualApprox(expectedPosition)
                || !positions.Add(view.Position)
                || !mercenarySession.CanPlayerControlMercenary(localPlayerId, mercenaryId)
                || !ValuesAreInRange(profile))
            {
                reason = $"Runtime mercenary validation failed: {mercenaryId}.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static bool ValuesAreInRange(MercenaryProfileV3 profile)
    {
        foreach (MercenaryAttributeTypeV3 type in Enum.GetValues<MercenaryAttributeTypeV3>())
        {
            if (profile.Attributes.GetValue(type) is < 0 or > 20)
            {
                return false;
            }
        }

        foreach (MercenaryWorkSkillTypeV3 type in Enum.GetValues<MercenaryWorkSkillTypeV3>())
        {
            if (profile.WorkSkills.GetValue(type) is < 0 or > 20)
            {
                return false;
            }
        }

        return true;
    }
}
