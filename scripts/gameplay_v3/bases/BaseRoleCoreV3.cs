using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameplayV3.Bases;

public enum BaseRoleV3 { Headquarters, Base, Outpost }

public enum BaseRoleAssignmentSourceV3
{
    FirstBaseAutoHeadquarters,
    NewBaseDefaultOutpost,
    Manual,
    HeadquartersPromotion,
    HeadquartersFallback,
    MergeRemap,
    SplitInherited,
    SplitCreatedOutpost
}

public sealed record BaseRoleStateV3(
    string BaseAreaId,
    string CompanyId,
    BaseRoleV3 Role,
    BaseRoleAssignmentSourceV3 AssignmentSource,
    long Revision,
    long CreatedOrder,
    string LastChangedReason);

public sealed record BaseRoleChangedV3(
    string CompanyId,
    string BaseAreaId,
    BaseRoleV3? OldRole,
    BaseRoleV3 NewRole,
    long Revision,
    string Reason);

public sealed record HeadquartersChangedV3(
    string CompanyId,
    string? OldHeadquartersId,
    string? NewHeadquartersId,
    long Revision,
    string Reason);

public sealed record BaseRoleRemovedV3(
    string CompanyId,
    string BaseAreaId,
    BaseRoleV3 OldRole,
    long Revision,
    string Reason);

public sealed record BaseRoleEventV3(
    string Kind,
    string CompanyId,
    string BaseAreaId,
    BaseRoleV3? OldRole,
    BaseRoleV3? NewRole,
    string? OldHeadquartersId,
    string? NewHeadquartersId,
    long Revision,
    string Reason);

public sealed class BaseRoleSessionV3 : IDisposable
{
    public const int MaxRecentEvents = 16;

    private readonly BaseAreaSessionV3 _baseAreas;
    private readonly Dictionary<string, BaseRoleStateV3> _roles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _headquartersByCompany = new(StringComparer.Ordinal);
    private readonly Queue<BaseRoleEventV3> _recentEvents = new();
    private long _revision;
    private bool _disposed;

    public BaseRoleSessionV3(long sessionRevision, BaseAreaSessionV3 baseAreas)
    {
        SessionRevision = sessionRevision;
        _baseAreas = baseAreas ?? throw new ArgumentNullException(nameof(baseAreas));
        _baseAreas.Changed += OnBaseAreaChanged;
        foreach (string companyId in _baseAreas.Areas.GetAll().Select(area => area.CompanyId).Distinct(StringComparer.Ordinal))
            ReconcileCurrentAreas(companyId, BaseRoleAssignmentSourceV3.NewBaseDefaultOutpost, "InitialRoleSync");
    }

    public long SessionRevision { get; }
    public long Revision => _revision;
    public int Count => _roles.Count;
    public int DirtyRoleCount => 0;
    public int RecentEventCount => _recentEvents.Count;
    public bool IsDisposed => _disposed;
    public int HeadquartersCount => _roles.Values.Count(state => state.Role == BaseRoleV3.Headquarters);
    public int BaseCount => _roles.Values.Count(state => state.Role == BaseRoleV3.Base);
    public int OutpostCount => _roles.Values.Count(state => state.Role == BaseRoleV3.Outpost);
    public int CompanyWithoutHeadquartersCount => _roles.Values.Select(state => state.CompanyId).Distinct(StringComparer.Ordinal).Count(company => !_headquartersByCompany.ContainsKey(company));

    public event Action<BaseRoleChangedV3>? BaseRoleChanged;
    public event Action<HeadquartersChangedV3>? HeadquartersChanged;
    public event Action<BaseRoleRemovedV3>? BaseRoleRemoved;

    public bool TryGetRole(string baseAreaId, out BaseRoleStateV3? state) => _roles.TryGetValue(baseAreaId, out state);

    public bool TryGetHeadquarters(string companyId, out BaseRoleStateV3? state)
    {
        state = null;
        return _headquartersByCompany.TryGetValue(companyId, out string? id) && _roles.TryGetValue(id, out state);
    }

    public bool IsHeadquarters(string baseAreaId) => _roles.TryGetValue(baseAreaId, out BaseRoleStateV3? state) && state.Role == BaseRoleV3.Headquarters;

    public IReadOnlyList<BaseRoleStateV3> GetBasesByRole(string companyId, BaseRoleV3 role) =>
        _roles.Values.Where(state => state.CompanyId == companyId && state.Role == role)
            .OrderBy(state => state.CreatedOrder).ThenBy(state => state.BaseAreaId, StringComparer.Ordinal).ToList().AsReadOnly();

    public IReadOnlyList<BaseRoleStateV3> GetAllRolesForCompany(string companyId) =>
        _roles.Values.Where(state => state.CompanyId == companyId)
            .OrderBy(state => state.CreatedOrder).ThenBy(state => state.BaseAreaId, StringComparer.Ordinal).ToList().AsReadOnly();

    public IReadOnlyList<BaseRoleEventV3> GetRecentEvents() => new ReadOnlyCollection<BaseRoleEventV3>(_recentEvents.ToList());

    public bool TrySetRole(string companyId, string baseAreaId, BaseRoleV3 role, out string reason)
    {
        if (!TryValidateOwnedLiveArea(companyId, baseAreaId, out BaseAreaV3? area, out reason)) return false;
        if (role == BaseRoleV3.Headquarters) return TrySetHeadquarters(companyId, baseAreaId, out reason);
        if (!_roles.TryGetValue(baseAreaId, out BaseRoleStateV3? current))
        {
            reason = "Base role state is missing.";
            return false;
        }
        if (current.Role == role)
        {
            reason = string.Empty;
            return true;
        }
        if (current.Role == BaseRoleV3.Headquarters)
        {
            reason = "Headquarters must be replaced through TrySetHeadquarters.";
            return false;
        }
        SetSingleRole(area!, role, BaseRoleAssignmentSourceV3.Manual, "ManualRoleChange");
        reason = string.Empty;
        return true;
    }

    public bool TrySetHeadquarters(string companyId, string baseAreaId, out string reason)
    {
        if (!TryValidateOwnedLiveArea(companyId, baseAreaId, out BaseAreaV3? area, out reason)) return false;
        if (_headquartersByCompany.TryGetValue(companyId, out string? existingId) && existingId == baseAreaId)
        {
            reason = string.Empty;
            return true;
        }

        string? oldHeadquartersId = _headquartersByCompany.GetValueOrDefault(companyId);
        long revision = ++_revision;
        BaseRoleChangedV3? demotedEvent = null;
        if (oldHeadquartersId != null && _roles.TryGetValue(oldHeadquartersId, out BaseRoleStateV3? oldHeadquarters))
        {
            _roles[oldHeadquartersId] = oldHeadquarters with
            {
                Role = BaseRoleV3.Base,
                AssignmentSource = BaseRoleAssignmentSourceV3.HeadquartersPromotion,
                Revision = revision,
                LastChangedReason = "HeadquartersReplaced"
            };
            demotedEvent = new(companyId, oldHeadquartersId, BaseRoleV3.Headquarters, BaseRoleV3.Base, revision, "HeadquartersReplaced");
        }

        BaseRoleStateV3 current = _roles.TryGetValue(baseAreaId, out BaseRoleStateV3? found)
            ? found
            : NewState(area!, BaseRoleV3.Outpost, BaseRoleAssignmentSourceV3.NewBaseDefaultOutpost, revision, "LateRoleRegistration");
        _roles[baseAreaId] = current with
        {
            Role = BaseRoleV3.Headquarters,
            AssignmentSource = BaseRoleAssignmentSourceV3.HeadquartersPromotion,
            Revision = revision,
            LastChangedReason = "HeadquartersPromoted"
        };
        _headquartersByCompany[companyId] = baseAreaId;

        if (demotedEvent != null) PublishRoleChanged(demotedEvent);
        PublishRoleChanged(new(companyId, baseAreaId, current.Role, BaseRoleV3.Headquarters, revision, "HeadquartersPromoted"));
        PublishHeadquartersChanged(new(companyId, oldHeadquartersId, baseAreaId, revision, "HeadquartersPromoted"));
        reason = string.Empty;
        return true;
    }

    public bool TryGetDefaultCompanyBase(string companyId, out BaseAreaV3? area)
    {
        area = null;
        if (TryGetHeadquarters(companyId, out BaseRoleStateV3? headquarters) && headquarters != null && _baseAreas.Areas.TryGet(headquarters.BaseAreaId, out area) && area != null) return true;
        foreach (BaseRoleV3 role in new[] { BaseRoleV3.Base, BaseRoleV3.Outpost })
        {
            area = GetCandidates(companyId, role).FirstOrDefault();
            if (area != null) return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _baseAreas.Changed -= OnBaseAreaChanged;
        _roles.Clear();
        _headquartersByCompany.Clear();
        _recentEvents.Clear();
    }

    private void OnBaseAreaChanged(BaseAreaChangeEventV3 change)
    {
        if (_disposed) return;
        Dictionary<string, BaseRoleStateV3> before = _roles.Values.Where(state => state.CompanyId == change.CompanyId).ToDictionary(state => state.BaseAreaId, StringComparer.Ordinal);
        string? headquartersBefore = _headquartersByCompany.GetValueOrDefault(change.CompanyId);

        if (change.Kind == BaseAreaChangeKindV3.Merged)
            ApplyMerge(change, before);

        ReconcileCurrentAreas(change.CompanyId, BaseRoleAssignmentSourceV3.NewBaseDefaultOutpost, change.Kind.ToString());

        if (change.Kind == BaseAreaChangeKindV3.Split)
            ApplySplitMetadata(change);

        List<BaseRoleRemovedV3> removed = RemoveRetiredStates(change.CompanyId, change.Kind == BaseAreaChangeKindV3.Merged ? "MergeRemap" : "BaseAreaRemoved");
        string? removedHeadquarters = removed.Any(value => value.BaseAreaId == headquartersBefore && value.OldRole == BaseRoleV3.Headquarters) ? headquartersBefore : null;
        EnsureHeadquartersAfterRemoval(change.CompanyId, change.Kind == BaseAreaChangeKindV3.Removed ? "HeadquartersRemoved" : change.Kind.ToString(), removedHeadquarters);
        foreach (BaseRoleRemovedV3 value in removed) PublishRoleRemoved(value);
    }

    private void ApplyMerge(BaseAreaChangeEventV3 change, IReadOnlyDictionary<string, BaseRoleStateV3> before)
    {
        if (!_baseAreas.Areas.TryGet(change.BaseAreaId, out BaseAreaV3? survivor) || survivor == null) return;
        List<BaseRoleStateV3> candidates = change.OldBaseAreaIds.Append(change.BaseAreaId).Distinct(StringComparer.Ordinal)
            .Where(before.ContainsKey).Select(id => before[id]).ToList();
        if (candidates.Count == 0) return;
        BaseRoleV3 mergedRole = candidates.Any(state => state.Role == BaseRoleV3.Headquarters)
            ? BaseRoleV3.Headquarters
            : candidates.Any(state => state.Role == BaseRoleV3.Base) ? BaseRoleV3.Base : BaseRoleV3.Outpost;
        SetMergedRole(survivor, mergedRole);
    }

    private void SetMergedRole(BaseAreaV3 survivor, BaseRoleV3 role)
    {
        if (role == BaseRoleV3.Headquarters)
        {
            PromoteWithoutIntermediateDuplicate(survivor, BaseRoleAssignmentSourceV3.MergeRemap, "MergeRemap");
            return;
        }
        if (!_roles.TryGetValue(survivor.BaseAreaId, out BaseRoleStateV3? current) || RoleRank(current.Role) < RoleRank(role))
            SetSingleRole(survivor, role, BaseRoleAssignmentSourceV3.MergeRemap, "MergeRemap");
    }

    private void ApplySplitMetadata(BaseAreaChangeEventV3 change)
    {
        HashSet<string> oldIds = new(change.OldBaseAreaIds, StringComparer.Ordinal);
        foreach (string id in change.NewBaseAreaIds.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!_roles.TryGetValue(id, out BaseRoleStateV3? current)) continue;
            BaseRoleAssignmentSourceV3 source = oldIds.Contains(id) ? BaseRoleAssignmentSourceV3.SplitInherited : BaseRoleAssignmentSourceV3.SplitCreatedOutpost;
            BaseRoleV3 role = oldIds.Contains(id) ? current.Role : BaseRoleV3.Outpost;
            UpdateMetadata(current, role, source, oldIds.Contains(id) ? "SplitInherited" : "SplitCreatedOutpost");
        }
    }

    private void ReconcileCurrentAreas(string companyId, BaseRoleAssignmentSourceV3 source, string reason)
    {
        IReadOnlyList<BaseAreaV3> areas = _baseAreas.Areas.GetForCompany(companyId);
        bool hasHeadquarters = _headquartersByCompany.TryGetValue(companyId, out string? headquartersId) && areas.Any(area => area.BaseAreaId == headquartersId);
        foreach (BaseAreaV3 area in areas.OrderBy(area => area.CreationOrder).ThenBy(area => area.BaseAreaId, StringComparer.Ordinal))
        {
            if (_roles.ContainsKey(area.BaseAreaId)) continue;
            BaseRoleV3 role = hasHeadquarters ? BaseRoleV3.Outpost : BaseRoleV3.Headquarters;
            BaseRoleAssignmentSourceV3 assignment = role == BaseRoleV3.Headquarters ? BaseRoleAssignmentSourceV3.FirstBaseAutoHeadquarters : source;
            SetSingleRole(area, role, assignment, role == BaseRoleV3.Headquarters ? "FirstBaseCreated" : reason);
            hasHeadquarters |= role == BaseRoleV3.Headquarters;
        }
    }

    private List<BaseRoleRemovedV3> RemoveRetiredStates(string companyId, string reason)
    {
        List<BaseRoleRemovedV3> removed = new();
        HashSet<string> live = new(_baseAreas.Areas.GetForCompany(companyId).Select(area => area.BaseAreaId), StringComparer.Ordinal);
        foreach (BaseRoleStateV3 state in _roles.Values.Where(state => state.CompanyId == companyId && !live.Contains(state.BaseAreaId)).ToList())
        {
            long revision = ++_revision;
            _roles.Remove(state.BaseAreaId);
            if (_headquartersByCompany.GetValueOrDefault(companyId) == state.BaseAreaId) _headquartersByCompany.Remove(companyId);
            removed.Add(new(companyId, state.BaseAreaId, state.Role, revision, reason));
        }
        return removed;
    }

    private void EnsureHeadquartersAfterRemoval(string companyId, string reason, string? removedHeadquartersId)
    {
        if (_headquartersByCompany.ContainsKey(companyId)) return;
        if (_baseAreas.Areas.GetForCompany(companyId).Count == 0)
        {
            if (removedHeadquartersId != null)
            {
                long emptyRevision = ++_revision;
                PublishHeadquartersChanged(new(companyId, removedHeadquartersId, null, emptyRevision, reason));
            }
            return;
        }
        BaseAreaV3? successor = GetCandidates(companyId, BaseRoleV3.Base).FirstOrDefault() ?? GetCandidates(companyId, BaseRoleV3.Outpost).FirstOrDefault();
        if (successor != null) PromoteWithoutIntermediateDuplicate(successor, BaseRoleAssignmentSourceV3.HeadquartersFallback, reason, removedHeadquartersId);
    }

    private IReadOnlyList<BaseAreaV3> GetCandidates(string companyId, BaseRoleV3 role) =>
        _roles.Values.Where(state => state.CompanyId == companyId && state.Role == role)
            .Select(state => _baseAreas.Areas.TryGet(state.BaseAreaId, out BaseAreaV3? area) ? area : null).Where(area => area != null).Cast<BaseAreaV3>()
            .OrderByDescending(area => area.AnchorCount > 0 ? area.AnchorCount : area.MemberSourceCount)
            .ThenBy(area => area.CreationOrder).ThenBy(area => area.BaseAreaId, StringComparer.Ordinal).ToList().AsReadOnly();

    private bool TryValidateOwnedLiveArea(string companyId, string baseAreaId, out BaseAreaV3? area, out string reason)
    {
        area = null;
        if (string.IsNullOrWhiteSpace(companyId) || string.IsNullOrWhiteSpace(baseAreaId)) { reason = "Company and BaseArea ids are required."; return false; }
        if (!_baseAreas.Areas.TryGet(baseAreaId, out area) || area == null) { reason = "BaseArea does not exist or is retired."; return false; }
        if (area.CompanyId != companyId) { area = null; reason = "BaseArea belongs to another company."; return false; }
        reason = string.Empty;
        return true;
    }

    private void PromoteWithoutIntermediateDuplicate(BaseAreaV3 area, BaseRoleAssignmentSourceV3 source, string reason, string? eventOldHeadquartersId = null)
    {
        string? oldId = _headquartersByCompany.GetValueOrDefault(area.CompanyId);
        if (oldId == area.BaseAreaId && _roles.TryGetValue(area.BaseAreaId, out BaseRoleStateV3? same) && same.Role == BaseRoleV3.Headquarters)
        {
            UpdateMetadata(same, same.Role, source, reason);
            return;
        }
        long revision = ++_revision;
        if (oldId != null && _roles.TryGetValue(oldId, out BaseRoleStateV3? old))
            _roles[oldId] = old with { Role = BaseRoleV3.Base, AssignmentSource = source, Revision = revision, LastChangedReason = reason };
        BaseRoleStateV3 current = _roles.TryGetValue(area.BaseAreaId, out BaseRoleStateV3? found) ? found : NewState(area, BaseRoleV3.Outpost, source, revision, reason);
        _roles[area.BaseAreaId] = current with { Role = BaseRoleV3.Headquarters, AssignmentSource = source, Revision = revision, LastChangedReason = reason };
        _headquartersByCompany[area.CompanyId] = area.BaseAreaId;
        if (oldId != null && oldId != area.BaseAreaId) PublishRoleChanged(new(area.CompanyId, oldId, BaseRoleV3.Headquarters, BaseRoleV3.Base, revision, reason));
        if (current.Role != BaseRoleV3.Headquarters) PublishRoleChanged(new(area.CompanyId, area.BaseAreaId, current.Role, BaseRoleV3.Headquarters, revision, reason));
        PublishHeadquartersChanged(new(area.CompanyId, eventOldHeadquartersId ?? oldId, area.BaseAreaId, revision, reason));
    }

    private void SetSingleRole(BaseAreaV3 area, BaseRoleV3 role, BaseRoleAssignmentSourceV3 source, string reason)
    {
        BaseRoleStateV3? old = _roles.GetValueOrDefault(area.BaseAreaId);
        if (old?.Role == role) return;
        long revision = ++_revision;
        _roles[area.BaseAreaId] = NewState(area, role, source, revision, reason);
        if (role == BaseRoleV3.Headquarters)
        {
            string? oldHeadquarters = _headquartersByCompany.GetValueOrDefault(area.CompanyId);
            _headquartersByCompany[area.CompanyId] = area.BaseAreaId;
            PublishHeadquartersChanged(new(area.CompanyId, oldHeadquarters, area.BaseAreaId, revision, reason));
        }
        PublishRoleChanged(new(area.CompanyId, area.BaseAreaId, old?.Role, role, revision, reason));
    }

    private void UpdateMetadata(BaseRoleStateV3 current, BaseRoleV3 role, BaseRoleAssignmentSourceV3 source, string reason)
    {
        if (current.Role == role && current.AssignmentSource == source && current.LastChangedReason == reason) return;
        long revision = ++_revision;
        _roles[current.BaseAreaId] = current with { Role = role, AssignmentSource = source, Revision = revision, LastChangedReason = reason };
        PublishRoleChanged(new(current.CompanyId, current.BaseAreaId, current.Role, role, revision, reason));
    }

    private static BaseRoleStateV3 NewState(BaseAreaV3 area, BaseRoleV3 role, BaseRoleAssignmentSourceV3 source, long revision, string reason) =>
        new(area.BaseAreaId, area.CompanyId, role, source, revision, area.CreationOrder, reason);

    private static int RoleRank(BaseRoleV3 role) => role switch { BaseRoleV3.Headquarters => 3, BaseRoleV3.Base => 2, _ => 1 };

    private void PublishRoleChanged(BaseRoleChangedV3 value)
    {
        Record(new("RoleChanged", value.CompanyId, value.BaseAreaId, value.OldRole, value.NewRole, null, null, value.Revision, value.Reason));
        BaseRoleChanged?.Invoke(value);
    }

    private void PublishHeadquartersChanged(HeadquartersChangedV3 value)
    {
        Record(new("HeadquartersChanged", value.CompanyId, value.NewHeadquartersId ?? value.OldHeadquartersId ?? string.Empty, null, null, value.OldHeadquartersId, value.NewHeadquartersId, value.Revision, value.Reason));
        HeadquartersChanged?.Invoke(value);
    }

    private void PublishRoleRemoved(BaseRoleRemovedV3 value)
    {
        Record(new("RoleRemoved", value.CompanyId, value.BaseAreaId, value.OldRole, null, null, null, value.Revision, value.Reason));
        BaseRoleRemoved?.Invoke(value);
    }

    private void Record(BaseRoleEventV3 value)
    {
        _recentEvents.Enqueue(value);
        while (_recentEvents.Count > MaxRecentEvents) _recentEvents.Dequeue();
    }
}
