using System;
using System.Collections.Generic;
using GameplayV3.Company;

namespace GameplayV3.Mercenary;

public sealed class MercenaryRegistryV3
{
    private readonly CompanyRegistryV3 _companyRegistry;
    private readonly Dictionary<string, MercenaryProfileV3> _profilesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MercenaryStateV3> _statesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _mercenaryIdsByCompany = new(StringComparer.Ordinal);

    public MercenaryRegistryV3(CompanyRegistryV3 companyRegistry)
    {
        _companyRegistry = companyRegistry;
    }

    public int Count => _profilesById.Count;
    public int DuplicateMercenaryRejectedCount { get; private set; }

    public bool CanRegisterMercenary(MercenaryProfileV3? profile, MercenaryStateV3? state, out string reason)
    {
        if (profile == null || state == null)
        {
            reason = "Profile and state must be registered together.";
            return false;
        }

        if (profile.MercenaryId != state.MercenaryId
            || !MercenaryIdFactoryV3.IsValidMercenaryId(profile.MercenaryId))
        {
            reason = "Profile and state must share the same canonical MercenaryId.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayName))
        {
            reason = "Mercenary DisplayName cannot be empty.";
            return false;
        }

        if (!_companyRegistry.ContainsCompany(state.CompanyId))
        {
            reason = $"Mercenary company is not registered: {state.CompanyId}";
            return false;
        }

        if (_profilesById.ContainsKey(profile.MercenaryId) || _statesById.ContainsKey(profile.MercenaryId))
        {
            reason = $"MercenaryId is already registered: {profile.MercenaryId}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool TryRegisterMercenary(MercenaryProfileV3? profile, MercenaryStateV3? state, out string reason)
    {
        if (!CanRegisterMercenary(profile, state, out reason))
        {
            if (profile != null && (_profilesById.ContainsKey(profile.MercenaryId) || _statesById.ContainsKey(profile.MercenaryId)))
            {
                DuplicateMercenaryRejectedCount++;
            }
            return false;
        }

        MercenaryProfileV3 validProfile = profile!;
        MercenaryStateV3 validState = state!;
        _profilesById.Add(validProfile.MercenaryId, validProfile);
        _statesById.Add(validState.MercenaryId, validState);
        GetOrCreateCompanyIds(validState.CompanyId).Add(validState.MercenaryId);
        reason = string.Empty;
        return true;
    }

    public bool TryGetProfile(string mercenaryId, out MercenaryProfileV3? profile)
    {
        return _profilesById.TryGetValue(mercenaryId, out profile);
    }

    public bool TryGetState(string mercenaryId, out MercenaryStateV3? state)
    {
        return _statesById.TryGetValue(mercenaryId, out state);
    }

    public bool TryGetMercenary(string mercenaryId, out MercenaryProfileV3? profile, out MercenaryStateV3? state)
    {
        bool hasProfile = _profilesById.TryGetValue(mercenaryId, out profile);
        bool hasState = _statesById.TryGetValue(mercenaryId, out state);
        if (hasProfile && hasState)
        {
            return true;
        }

        profile = null;
        state = null;
        return false;
    }

    public bool ContainsMercenary(string mercenaryId)
    {
        return _profilesById.ContainsKey(mercenaryId) && _statesById.ContainsKey(mercenaryId);
    }

    public bool TryRemoveMercenary(string mercenaryId, out string reason)
    {
        if (!TryGetMercenary(mercenaryId, out _, out MercenaryStateV3? state) || state == null)
        {
            reason = $"Mercenary is not registered: {mercenaryId}";
            return false;
        }

        _profilesById.Remove(mercenaryId);
        _statesById.Remove(mercenaryId);
        if (_mercenaryIdsByCompany.TryGetValue(state.CompanyId, out HashSet<string>? companyIds))
        {
            companyIds.Remove(mercenaryId);
            if (companyIds.Count == 0)
            {
                _mercenaryIdsByCompany.Remove(state.CompanyId);
            }
        }

        reason = string.Empty;
        return true;
    }

    public IReadOnlyList<string> GetMercenariesByCompany(string companyId)
    {
        if (!_mercenaryIdsByCompany.TryGetValue(companyId, out HashSet<string>? ids))
        {
            return Array.Empty<string>();
        }

        List<string> result = new(ids);
        result.Sort(StringComparer.Ordinal);
        return result.AsReadOnly();
    }

    public int CountByCompany(string companyId)
    {
        return _mercenaryIdsByCompany.TryGetValue(companyId, out HashSet<string>? ids) ? ids.Count : 0;
    }

    public IReadOnlyList<string> GetAllMercenaryIds()
    {
        List<string> result = new(_profilesById.Keys);
        result.Sort(StringComparer.Ordinal);
        return result.AsReadOnly();
    }

    public void Clear()
    {
        _profilesById.Clear();
        _statesById.Clear();
        _mercenaryIdsByCompany.Clear();
        DuplicateMercenaryRejectedCount = 0;
    }

    private HashSet<string> GetOrCreateCompanyIds(string companyId)
    {
        if (!_mercenaryIdsByCompany.TryGetValue(companyId, out HashSet<string>? ids))
        {
            ids = new HashSet<string>(StringComparer.Ordinal);
            _mercenaryIdsByCompany.Add(companyId, ids);
        }

        return ids;
    }

}
