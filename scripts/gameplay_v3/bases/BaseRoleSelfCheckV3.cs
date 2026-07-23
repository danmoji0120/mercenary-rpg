using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using WorldV2;

namespace GameplayV3.Bases;

public static class BaseRoleSelfCheckV3
{
    public static string LastValidationSummary { get; private set; } = string.Empty;

    public static bool TryValidate(out string reason)
    {
        try
        {
            BaseAreaSessionV3 areas = NewAreaSession(901);
            using BaseRoleSessionV3 roles = new(901, areas);

            AddBed(areas, "a_left", "company_a", 0, 0);
            areas.RebuildCompanyNow("company_a");
            BaseAreaV3 first = AreaForSource(areas, "a_left");
            Require(roles.TryGetHeadquarters("company_a", out BaseRoleStateV3? firstHeadquarters) && firstHeadquarters?.BaseAreaId == first.BaseAreaId, "First base was not auto-assigned Headquarters.");
            Require(firstHeadquarters?.AssignmentSource == BaseRoleAssignmentSourceV3.FirstBaseAutoHeadquarters, "First base assignment source mismatch.");

            AddBed(areas, "a_right", "company_a", 20, 0);
            areas.RebuildCompanyNow("company_a");
            BaseAreaV3 second = AreaForSource(areas, "a_right");
            Require(roles.TryGetRole(second.BaseAreaId, out BaseRoleStateV3? secondRole) && secondRole?.Role == BaseRoleV3.Outpost, "Second base was not an Outpost.");

            Require(roles.TrySetRole("company_a", second.BaseAreaId, BaseRoleV3.Base, out reason), reason);
            long idempotentRevision = roles.Revision;
            int idempotentEvents = roles.RecentEventCount;
            Require(roles.TrySetRole("company_a", second.BaseAreaId, BaseRoleV3.Base, out reason), reason);
            Require(roles.Revision == idempotentRevision && roles.RecentEventCount == idempotentEvents, "Idempotent role assignment changed revision or events.");

            Require(roles.TrySetHeadquarters("company_a", second.BaseAreaId, out reason), reason);
            Require(roles.TryGetHeadquarters("company_a", out BaseRoleStateV3? replacement) && replacement?.BaseAreaId == second.BaseAreaId, "Headquarters replacement failed.");
            Require(roles.TryGetRole(first.BaseAreaId, out BaseRoleStateV3? demoted) && demoted?.Role == BaseRoleV3.Base && roles.HeadquartersCount == 1, "Old Headquarters was not atomically demoted.");

            AddBed(areas, "a_bridge_1", "company_a", 8, 0);
            AddBed(areas, "a_bridge_2", "company_a", 16, 0);
            areas.RebuildCompanyNow("company_a");
            BaseAreaV3 merged = areas.Areas.GetForCompany("company_a").Single();
            Require(roles.IsHeadquarters(merged.BaseAreaId) && roles.Count == 1, "Merge did not preserve Headquarters or retire role state.");

            areas.RemoveSource("a_bridge_1");
            areas.RemoveSource("a_bridge_2");
            areas.RebuildCompanyNow("company_a");
            BaseAreaV3 inherited = AreaForSource(areas, "a_left");
            BaseAreaV3 splitNew = AreaForSource(areas, "a_right");
            Require(roles.IsHeadquarters(inherited.BaseAreaId), "Split did not preserve the survivor Headquarters.");
            Require(roles.TryGetRole(splitNew.BaseAreaId, out BaseRoleStateV3? splitRole) && splitRole?.Role == BaseRoleV3.Outpost && splitRole.AssignmentSource == BaseRoleAssignmentSourceV3.SplitCreatedOutpost, "Split-created base was not an Outpost.");

            areas.RemoveSource("a_right");
            areas.RebuildCompanyNow("company_a");
            Require(!roles.TryGetRole(splitNew.BaseAreaId, out _) && roles.IsHeadquarters(inherited.BaseAreaId), "Removing a normal base affected Headquarters or leaked role state.");

            AddBed(areas, "a_successor", "company_a", 24, 0);
            areas.RebuildCompanyNow("company_a");
            BaseAreaV3 successor = AreaForSource(areas, "a_successor");
            Require(roles.TrySetRole("company_a", successor.BaseAreaId, BaseRoleV3.Base, out reason), reason);
            areas.RemoveSource("a_left");
            areas.RebuildCompanyNow("company_a");
            Require(roles.TryGetHeadquarters("company_a", out BaseRoleStateV3? fallback) && fallback?.BaseAreaId == successor.BaseAreaId && fallback.AssignmentSource == BaseRoleAssignmentSourceV3.HeadquartersFallback, "Base successor was not promoted after Headquarters removal.");

            AddBed(areas, "b_first", "company_b", 0, 40);
            AddBed(areas, "b_outpost", "company_b", 20, 40);
            areas.RebuildCompanyNow("company_b");
            BaseAreaV3 companyBOutpost = AreaForSource(areas, "b_outpost");
            Require(!roles.TrySetRole("company_a", companyBOutpost.BaseAreaId, BaseRoleV3.Base, out _), "Cross-company role change was accepted.");
            Require(roles.TryGetHeadquarters("company_a", out _) && roles.TryGetHeadquarters("company_b", out _) && roles.HeadquartersCount == 2, "Company Headquarters isolation failed.");

            BaseAreaSessionV3 outpostFallbackAreas = NewAreaSession(902);
            using BaseRoleSessionV3 outpostFallbackRoles = new(902, outpostFallbackAreas);
            AddBed(outpostFallbackAreas, "hq", "company", 0, 0);
            AddBed(outpostFallbackAreas, "outpost", "company", 20, 0);
            outpostFallbackAreas.RebuildCompanyNow("company");
            string oldHeadquarters = AreaForSource(outpostFallbackAreas, "hq").BaseAreaId;
            string expectedOutpost = AreaForSource(outpostFallbackAreas, "outpost").BaseAreaId;
            outpostFallbackAreas.RemoveSource("hq");
            outpostFallbackAreas.RebuildCompanyNow("company");
            Require(!outpostFallbackRoles.TryGetRole(oldHeadquarters, out _) && outpostFallbackRoles.TryGetHeadquarters("company", out BaseRoleStateV3? outpostFallback) && outpostFallback?.BaseAreaId == expectedOutpost, "Outpost fallback failed.");
            outpostFallbackAreas.RemoveSource("outpost");
            outpostFallbackAreas.RebuildCompanyNow("company");
            Require(!outpostFallbackRoles.TryGetHeadquarters("company", out _) && outpostFallbackRoles.Count == 0, "Last Headquarters removal leaked role state.");

            Require(roles.RecentEventCount <= BaseRoleSessionV3.MaxRecentEvents && roles.DirtyRoleCount == 0, "Role event history or dirty state is unbounded.");
            LastValidationSummary = $"roles={roles.Count} headquarters={roles.HeadquartersCount} base={roles.BaseCount} outpost={roles.OutpostCount} events={roles.RecentEventCount}";
            reason = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            reason = exception.Message;
            return false;
        }
    }

    public static string RunSmallFixture()
    {
        BaseAreaSessionV3 areas = NewAreaSession(903);
        using BaseRoleSessionV3 roles = new(903, areas);
        const int companyCount = 4;
        const int basesPerCompany = 50;
        for (int companyIndex = 0; companyIndex < companyCount; companyIndex++)
        {
            string company = $"company_{companyIndex}";
            for (int i = 0; i < basesPerCompany; i++) AddBed(areas, $"bed_{companyIndex}_{i}", company, i * 20, companyIndex * 40);
            areas.RebuildCompanyNow(company);
        }
        Require(roles.Count == 200 && roles.HeadquartersCount == companyCount, "Small fixture initial role counts mismatch.");

        IReadOnlyList<BaseRoleStateV3> roleList = roles.GetAllRolesForCompany("company_0");
        for (int i = 0; i < 500; i++)
        {
            BaseRoleStateV3 target = roleList[1 + i % (roleList.Count - 1)];
            BaseRoleV3 role = i % 2 == 0 ? BaseRoleV3.Base : BaseRoleV3.Outpost;
            Require(roles.TrySetRole("company_0", target.BaseAreaId, role, out string reason), reason);
        }

        BaseAreaSessionV3 churnAreas = NewAreaSession(904);
        using BaseRoleSessionV3 churnRoles = new(904, churnAreas);
        AddBed(churnAreas, "left", "churn", 0, 0);
        AddBed(churnAreas, "right", "churn", 20, 0);
        churnAreas.RebuildCompanyNow("churn");
        for (int i = 0; i < 20; i++)
        {
            AddBed(churnAreas, "bridge_1", "churn", 8, 0);
            AddBed(churnAreas, "bridge_2", "churn", 16, 0);
            churnAreas.RebuildCompanyNow("churn");
            Require(churnRoles.Count == 1 && churnRoles.HeadquartersCount == 1, "Merge churn violated role invariants.");
            churnAreas.RemoveSource("bridge_1");
            churnAreas.RemoveSource("bridge_2");
            churnAreas.RebuildCompanyNow("churn");
            Require(churnRoles.Count == 2 && churnRoles.HeadquartersCount == 1, "Split churn violated role invariants.");
        }
        Require(roles.RecentEventCount <= 16 && churnRoles.RecentEventCount <= 16, "Small fixture event history exceeded its bound.");
        return $"companies={companyCount} bases={roles.Count} roleChanges=500 mergeSplitCycles=20 headquarters={roles.HeadquartersCount} churnRoles={churnRoles.Count}";
    }

    public static bool ValidateDisposedSubscription()
    {
        BaseAreaSessionV3 areas = NewAreaSession(905);
        BaseRoleSessionV3 roles = new(905, areas);
        roles.Dispose();
        AddBed(areas, "after_dispose", "company", 0, 0);
        areas.RebuildCompanyNow("company");
        return roles.IsDisposed && roles.Count == 0 && roles.RecentEventCount == 0;
    }

    private static BaseAreaSessionV3 NewAreaSession(long revision)
    {
        BaseAreaSessionV3 session = new(revision);
        session.SetWorldBounds(new Rect2I(-1024, -1024, 8192, 8192));
        return session;
    }

    private static void AddBed(BaseAreaSessionV3 session, string id, string company, int x, int y) =>
        session.UpsertSource(new(id, company, BaseSpatialSourceKindV3.Bed, BaseSpatialSourceRoleV3.Anchor,
            new[] { new GlobalCellCoord(new Vector2I(x, y)) }, 4, session.NextSourceCreationOrder(), 1));

    private static BaseAreaV3 AreaForSource(BaseAreaSessionV3 session, string sourceId)
    {
        Require(session.Areas.TryGetForSource(sourceId, out BaseAreaV3? area) && area != null, $"No BaseArea for source {sourceId}.");
        return area!;
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
