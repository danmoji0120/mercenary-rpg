using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldV2;

namespace GameplayV3.Bases.UI;

public static class BaseManagementUiSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        try
        {
            ValidateSorting();
            ValidateButtonStates();
            ValidateRoleCommands();
            ValidateSelectionFallbacks();
            ValidateZeroBaseState();
            ValidateDeferredRefreshSafety();
            reason = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            reason = exception.Message;
            return false;
        }
    }

    private static void ValidateSorting()
    {
        List<(string Id, BaseRoleV3 Role, long CreationOrder)> rows = new()
        {
            ("outpost_late", BaseRoleV3.Outpost, 1),
            ("base_early", BaseRoleV3.Base, 50),
            ("hq_late", BaseRoleV3.Headquarters, 99),
            ("hq_early", BaseRoleV3.Headquarters, 2),
            ("base_early_b", BaseRoleV3.Base, 50)
        };
        rows.Sort((left, right) =>
        {
            int role = BaseManagementPanelV3.RoleRank(left.Role).CompareTo(BaseManagementPanelV3.RoleRank(right.Role));
            if (role != 0) return role;
            int order = left.CreationOrder.CompareTo(right.CreationOrder);
            return order != 0 ? order : string.CompareOrdinal(left.Id, right.Id);
        });
        Require(string.Join(',', rows.Select(row => row.Id)) == "hq_early,hq_late,base_early,base_early_b,outpost_late", "Sorting order mismatch.");
    }

    private static void ValidateButtonStates()
    {
        Require(BaseManagementRoleButtonStateV3.For(BaseRoleV3.Headquarters) == new BaseManagementRoleButtonStateV3(false, false, false), "HQ buttons were not locked.");
        Require(BaseManagementRoleButtonStateV3.For(BaseRoleV3.Base) == new BaseManagementRoleButtonStateV3(true, false, true), "Base button state mismatch.");
        Require(BaseManagementRoleButtonStateV3.For(BaseRoleV3.Outpost) == new BaseManagementRoleButtonStateV3(true, true, false), "Outpost button state mismatch.");
    }

    private static void ValidateRoleCommands()
    {
        const string company = "company_local";
        BaseAreaSessionV3 areas = new(9001);
        areas.SetWorldBounds(new Rect2I(-100, -100, 400, 400));
        AddBed(areas, "bed_a", company, new(0, 0));
        AddBed(areas, "bed_b", company, new(20, 0));
        AddBed(areas, "bed_c", company, new(40, 0));
        areas.RebuildCompanyNow(company);
        using BaseRoleSessionV3 roles = new(9001, areas);
        IReadOnlyList<BaseRoleStateV3> initial = roles.GetAllRolesForCompany(company);
        Require(initial.Count == 3 && initial.Count(state => state.Role == BaseRoleV3.Headquarters) == 1, "Role fixture did not initialize.");

        BaseRoleStateV3 headquarters = initial.Single(state => state.Role == BaseRoleV3.Headquarters);
        BaseRoleStateV3 baseArea = initial.First(state => state.Role == BaseRoleV3.Outpost);
        BaseRoleStateV3 outpost = initial.Last(state => state.Role == BaseRoleV3.Outpost);
        Require(roles.TrySetRole(company, outpost.BaseAreaId, BaseRoleV3.Base, out string reason) && string.IsNullOrEmpty(reason), "Outpost to Base failed.");
        long sameRoleRevision = roles.Revision;
        Require(roles.TrySetRole(company, outpost.BaseAreaId, BaseRoleV3.Base, out reason) && roles.Revision == sameRoleRevision, "Same-role command changed revision.");
        Require(roles.TrySetHeadquarters(company, baseArea.BaseAreaId, out reason), "Base to Headquarters failed.");
        Require(roles.IsHeadquarters(baseArea.BaseAreaId) && roles.TryGetRole(headquarters.BaseAreaId, out BaseRoleStateV3? demoted) && demoted?.Role == BaseRoleV3.Base, "Atomic HQ replacement failed.");
        Require(!roles.TrySetRole("company_other", baseArea.BaseAreaId, BaseRoleV3.Outpost, out _), "Other-company command was accepted.");

        areas.RemoveSource("bed_c");
        areas.RebuildCompanyNow(company);
        Require(!roles.TrySetRole(company, outpost.BaseAreaId, BaseRoleV3.Outpost, out _), "Retired base command was accepted.");
    }

    private static void ValidateSelectionFallbacks()
    {
        string[] ids = { "hq", "base", "outpost" };
        Require(BaseManagementPanelV3.SelectFallback(ids, "hq", "base") == "base", "Live selection was not retained.");
        Require(BaseManagementPanelV3.SelectFallback(ids, "hq", "retired") == "hq", "HQ fallback was not selected.");
        Require(BaseManagementPanelV3.SelectFallback(new[] { "survivor" }, "retired", "retired") == "survivor", "Merge fallback was not selected.");
        Require(BaseManagementPanelV3.SelectFallback(new[] { "old", "new_outpost" }, "old", "old") == "old", "Split selection was not retained.");
        Require(BaseManagementPanelV3.SelectFallback(Array.Empty<string>(), null, null) == string.Empty, "Empty selection was not cleared.");
    }

    private static void ValidateZeroBaseState()
    {
        Require(BaseManagementPanelV3.SelectFallback(Array.Empty<string>(), null, string.Empty) == string.Empty, "Zero-base selection was not cleared.");
        Require(BaseManagementPanelV3.EmptyStateSummaryText == "\uAE30\uC9C0 \uC5C6\uC74C", "Empty summary text mismatch.");
        Require(BaseManagementPanelV3.EmptyStateDetailText.Contains("\uC544\uC9C1 \uC124\uB9BD\uB41C \uAE30\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.", StringComparison.Ordinal), "Empty detail text mismatch.");
        Require(BaseManagementPanelV3.LoadingStateText.Contains("\uC815\uBCF4\uB97C", StringComparison.Ordinal), "Loading state text mismatch.");
    }

    private static void ValidateDeferredRefreshSafety()
    {
        Require(BaseManagementPanelV3.IsDeferredRefreshCurrent(4, 4, true, false), "Current deferred refresh was rejected.");
        Require(!BaseManagementPanelV3.IsDeferredRefreshCurrent(4, 3, true, false), "Stale generation was accepted.");
        Require(!BaseManagementPanelV3.IsDeferredRefreshCurrent(4, 4, false, false), "Out-of-tree refresh was accepted.");
        Require(!BaseManagementPanelV3.IsDeferredRefreshCurrent(4, 4, true, true), "Exit-tree refresh was accepted.");
    }

    private static void AddBed(BaseAreaSessionV3 areas, string id, string company, Vector2I cell) =>
        areas.UpsertSource(new(
            id,
            company,
            BaseSpatialSourceKindV3.Bed,
            BaseSpatialSourceRoleV3.Anchor,
            new[] { new GlobalCellCoord(cell) },
            4,
            areas.NextSourceCreationOrder(),
            1));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
