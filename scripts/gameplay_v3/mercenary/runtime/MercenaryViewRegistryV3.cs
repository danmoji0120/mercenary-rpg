using System;
using System.Collections.Generic;
using Godot;

namespace GameplayV3.Mercenary.Runtime;

public sealed class MercenaryViewRegistryV3
{
    private readonly Dictionary<string, MercenaryEntityV3> _viewsByMercenaryId = new(StringComparer.Ordinal);

    public int Count => _viewsByMercenaryId.Count;
    public int DuplicateViewRejectedCount { get; private set; }

    public bool TryRegisterView(string mercenaryId, MercenaryEntityV3? view, out string reason)
    {
        if (!MercenaryIdFactoryV3.IsValidMercenaryId(mercenaryId)
            || view == null
            || !GodotObject.IsInstanceValid(view)
            || !view.IsInitialized
            || view.MercenaryId != mercenaryId)
        {
            reason = "A valid initialized MercenaryEntityV3 is required.";
            return false;
        }

        if (_viewsByMercenaryId.TryGetValue(mercenaryId, out MercenaryEntityV3? existing))
        {
            if (ReferenceEquals(existing, view) && GodotObject.IsInstanceValid(existing))
            {
                reason = string.Empty;
                return true;
            }

            DuplicateViewRejectedCount++;
            reason = $"Mercenary view is already registered: {mercenaryId}";
            return false;
        }

        _viewsByMercenaryId.Add(mercenaryId, view);
        reason = string.Empty;
        return true;
    }

    public bool TryGetView(string mercenaryId, out MercenaryEntityV3? view)
    {
        if (!_viewsByMercenaryId.TryGetValue(mercenaryId, out view))
        {
            return false;
        }

        if (GodotObject.IsInstanceValid(view))
        {
            return true;
        }

        _viewsByMercenaryId.Remove(mercenaryId);
        view = null;
        return false;
    }

    public bool ContainsView(string mercenaryId)
    {
        return TryGetView(mercenaryId, out _);
    }

    public bool TryRemoveView(string mercenaryId, out MercenaryEntityV3? view)
    {
        if (!_viewsByMercenaryId.Remove(mercenaryId, out view))
        {
            view = null;
            return false;
        }

        return true;
    }

    public int ClearInvalidViews()
    {
        List<string> invalidIds = new();
        foreach ((string mercenaryId, MercenaryEntityV3 view) in _viewsByMercenaryId)
        {
            if (!GodotObject.IsInstanceValid(view))
            {
                invalidIds.Add(mercenaryId);
            }
        }

        foreach (string invalidId in invalidIds)
        {
            _viewsByMercenaryId.Remove(invalidId);
        }

        return invalidIds.Count;
    }

    public IReadOnlyList<string> GetAllViewIds()
    {
        ClearInvalidViews();
        List<string> ids = new(_viewsByMercenaryId.Keys);
        ids.Sort(StringComparer.Ordinal);
        return ids.AsReadOnly();
    }

    public void Clear()
    {
        _viewsByMercenaryId.Clear();
        DuplicateViewRejectedCount = 0;
    }
}
