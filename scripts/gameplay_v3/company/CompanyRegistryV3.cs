using System;
using System.Collections.Generic;

namespace GameplayV3.Company;

public sealed class CompanyRegistryV3
{
    private readonly Dictionary<string, CompanyStateV3> _companiesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _companyIdsByOwner = new(StringComparer.Ordinal);

    public int Count => _companiesById.Count;

    public bool TryRegisterCompany(CompanyStateV3? company, out string reason)
    {
        if (company == null)
        {
            reason = "Company cannot be null.";
            return false;
        }

        if (!CompanyIdFactoryV3.IsValidCompanyId(company.CompanyId))
        {
            reason = "CompanyId must be a non-empty canonical cmp_ ID.";
            return false;
        }

        if (!CompanyIdFactoryV3.IsValidPlayerId(company.OwnerPlayerId))
        {
            reason = "OwnerPlayerId must be a non-empty canonical ply_ ID.";
            return false;
        }

        if (_companiesById.ContainsKey(company.CompanyId))
        {
            reason = $"CompanyId is already registered: {company.CompanyId}";
            return false;
        }

        if (_companyIdsByOwner.ContainsKey(company.OwnerPlayerId))
        {
            reason = $"OwnerPlayerId already owns a company: {company.OwnerPlayerId}";
            return false;
        }

        _companiesById.Add(company.CompanyId, company);
        _companyIdsByOwner.Add(company.OwnerPlayerId, company.CompanyId);
        reason = string.Empty;
        return true;
    }

    public bool TryCreateCompany(
        string ownerPlayerId,
        string displayName,
        out CompanyStateV3? company,
        out string reason)
    {
        company = null;
        if (!CompanyIdFactoryV3.IsValidPlayerId(ownerPlayerId))
        {
            reason = "OwnerPlayerId must be a non-empty canonical ply_ ID.";
            return false;
        }

        CompanyStateV3 candidate = new(
            CompanyIdFactoryV3.CreateCompanyId(),
            ownerPlayerId,
            displayName,
            DateTime.UtcNow);

        if (!TryRegisterCompany(candidate, out reason))
        {
            return false;
        }

        company = candidate;
        return true;
    }

    public bool TryGetCompany(string companyId, out CompanyStateV3? company)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            company = null;
            return false;
        }

        return _companiesById.TryGetValue(companyId, out company);
    }

    public bool TryGetCompanyByOwner(string playerId, out CompanyStateV3? company)
    {
        company = null;
        return !string.IsNullOrWhiteSpace(playerId)
            && _companyIdsByOwner.TryGetValue(playerId, out string? companyId)
            && _companiesById.TryGetValue(companyId, out company);
    }

    public bool ContainsCompany(string companyId)
    {
        return !string.IsNullOrWhiteSpace(companyId) && _companiesById.ContainsKey(companyId);
    }

    public bool ContainsOwner(string playerId)
    {
        return !string.IsNullOrWhiteSpace(playerId) && _companyIdsByOwner.ContainsKey(playerId);
    }

    public bool TryRemoveCompany(string companyId, out string reason)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            reason = "CompanyId cannot be empty.";
            return false;
        }

        if (!_companiesById.Remove(companyId, out CompanyStateV3? company) || company == null)
        {
            reason = $"Company is not registered: {companyId}";
            return false;
        }

        _companyIdsByOwner.Remove(company.OwnerPlayerId);
        reason = string.Empty;
        return true;
    }

    public IReadOnlyList<CompanyStateV3> GetAllCompanies()
    {
        return new List<CompanyStateV3>(_companiesById.Values).AsReadOnly();
    }

    public void Clear()
    {
        _companiesById.Clear();
        _companyIdsByOwner.Clear();
    }
}
