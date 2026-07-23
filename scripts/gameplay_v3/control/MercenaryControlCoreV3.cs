using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameplayV3.Company;
using GameplayV3.Mercenary;
using GameplayV3.Movement;
using GameplayV3.Navigation;
using GameplayV3.Work;
using Godot;
using WorldV2;

namespace GameplayV3.Control;

public static class CommandIdFactoryV3
{
    private const string Prefix = "cmd_";
    public static string CreateCommandId() => Prefix + Guid.NewGuid().ToString("N");
    public static bool IsValidCommandId(string? value)
    {
        if (value == null || value.Length != Prefix.Length + 32 || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        for (int i = Prefix.Length; i < value.Length; i++)
        {
            char c = value[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        }
        return true;
    }
}

public sealed class MercenarySelectionStateV3
{
    private readonly CompanySessionV3 _companySession;
    private readonly MercenarySessionV3 _mercenarySession;
    private readonly List<string> _selected = new();
    private readonly HashSet<string> _selectedSet = new(StringComparer.Ordinal);

    public MercenarySelectionStateV3(CompanySessionV3 companySession, MercenarySessionV3 mercenarySession)
    {
        _companySession = companySession;
        _mercenarySession = mercenarySession;
    }

    public int Count => _selected.Count;
    public event Action? SelectionChanged;
    public long Revision { get; private set; }
    public string LastSelectionAction { get; private set; } = "none";
    public bool Contains(string id) => _selectedSet.Contains(id);
    public IReadOnlyList<string> GetSelectedIds() => new List<string>(_selected).AsReadOnly();

    public bool TrySelectSingle(string id, out string reason) => TryReplaceSelection(new[] { id }, "single", out reason);
    public bool TryAdd(string id, out string reason)
    {
        if (!TryValidate(id, out reason)) return false;
        if (_selectedSet.Contains(id)) { reason = string.Empty; return true; }
        _selected.Add(id); _selectedSet.Add(id); Changed("add"); reason = string.Empty; return true;
    }
    public bool TryRemove(string id, out string reason)
    {
        if (string.IsNullOrWhiteSpace(id)) { reason = "MercenaryId is required."; return false; }
        if (!_selectedSet.Remove(id)) { reason = string.Empty; return true; }
        _selected.Remove(id); Changed("remove"); reason = string.Empty; return true;
    }
    public bool TryToggle(string id, out string reason) => Contains(id) ? TryRemove(id, out reason) : TryAdd(id, out reason);
    public bool TryReplaceSelection(IEnumerable<string> ids, out string reason) => TryReplaceSelection(ids, "replace", out reason);
    public void Clear()
    {
        if (_selected.Count == 0) return;
        _selected.Clear(); _selectedSet.Clear(); Changed("clear");
    }

    private bool TryReplaceSelection(IEnumerable<string> ids, string action, out string reason)
    {
        List<string> next = new(); HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (!TryValidate(id, out reason)) return false;
            if (!unique.Add(id)) { reason = $"Duplicate selection id: {id}"; return false; }
            next.Add(id);
        }
        if (Same(next)) { reason = string.Empty; return true; }
        _selected.Clear(); _selected.AddRange(next); _selectedSet.Clear(); foreach (string id in next) _selectedSet.Add(id);
        Changed(action); reason = string.Empty; return true;
    }
    private bool TryValidate(string id, out string reason)
    {
        if (!MercenaryIdFactoryV3.IsValidMercenaryId(id) || !_mercenarySession.Registry.ContainsMercenary(id))
        { reason = "Mercenary does not exist."; return false; }
        string playerId = _companySession.LocalContext.LocalPlayerId;
        if (!_mercenarySession.CanPlayerControlMercenary(playerId, id)) { reason = "Local player cannot control mercenary."; return false; }
        reason = string.Empty; return true;
    }
    private bool Same(List<string> other)
    {
        if (other.Count != _selected.Count) return false;
        for (int i = 0; i < other.Count; i++) if (other[i] != _selected[i]) return false;
        return true;
    }
    private void Changed(string action) { Revision++; LastSelectionAction = action; SelectionChanged?.Invoke(); }
}

public enum DirectMoveCommandStatusV3 { Created, PathPending, Active, Completed, CompletedWithFailures, Failed, Cancelled, Superseded }
public enum MercenaryMoveOrderStatusV3 { PendingPath, PathReady, Moving, Completed, Failed, Superseded }

public sealed class DirectMoveCommandV3
{
    private readonly List<string> _requestedIds;
    private readonly IReadOnlyDictionary<string, GlobalCellCoord> _destinations;
    internal DirectMoveCommandV3(string id, long sessionRevision, long revision, string issuer, string companyId,
        IReadOnlyList<string> ids, GlobalCellCoord target, IReadOnlyDictionary<string, GlobalCellCoord> destinations, DateTime createdUtc)
    {
        CommandId = id; SessionRevision = sessionRevision; CommandRevision = revision; IssuerPlayerId = issuer; CompanyId = companyId;
        _requestedIds = new List<string>(ids); RequestedTargetCell = target; _destinations = new ReadOnlyDictionary<string, GlobalCellCoord>(new Dictionary<string, GlobalCellCoord>(destinations, StringComparer.Ordinal));
        CreatedUtc = createdUtc; Status = DirectMoveCommandStatusV3.Created;
    }
    public string CommandId { get; }
    public long SessionRevision { get; }
    public long CommandRevision { get; }
    public string IssuerPlayerId { get; }
    public string CompanyId { get; }
    public IReadOnlyList<string> RequestedMercenaryIds => _requestedIds.AsReadOnly();
    public GlobalCellCoord RequestedTargetCell { get; }
    public IReadOnlyDictionary<string, GlobalCellCoord> ResolvedDestinationCells => _destinations;
    public DateTime CreatedUtc { get; }
    public DirectMoveCommandStatusV3 Status { get; internal set; }
    public string FailureReason { get; internal set; } = string.Empty;
    internal int CompletedOrderCount { get; set; }
    internal int FailedOrderCount { get; set; }
    internal int SupersededOrderCount { get; set; }
}

public sealed class MercenaryMoveOrderV3
{
    internal MercenaryMoveOrderV3(string commandId, string mercenaryId, GlobalCellCoord start, GlobalCellCoord destination,
        long sessionRevision, long orderRevision, DateTime createdUtc)
    {
        MoveOrderId = $"{commandId}:{mercenaryId}"; CommandId = commandId; MercenaryId = mercenaryId; StartCell = start;
        DestinationCell = destination; SessionRevision = sessionRevision; OrderRevision = orderRevision; CreatedUtc = createdUtc;
        PathRequest = new MercenaryPathRequestV3(MoveOrderId, mercenaryId, start, destination, sessionRevision, orderRevision);
    }
    public string MoveOrderId { get; }
    public string CommandId { get; }
    public string MercenaryId { get; }
    public GlobalCellCoord StartCell { get; }
    public GlobalCellCoord DestinationCell { get; }
    public long SessionRevision { get; }
    public long OrderRevision { get; }
    public DateTime CreatedUtc { get; }
    public MercenaryPathRequestV3 PathRequest { get; }
    public MercenaryPathResultV3? PathResult { get; internal set; }
    public MercenaryMoveOrderStatusV3 Status { get; internal set; } = MercenaryMoveOrderStatusV3.PendingPath;
    public string FailureReason { get; internal set; } = string.Empty;
}

public sealed class MercenaryControlDiagnosticsV3
{
    public string LastCommandId { get; internal set; } = string.Empty;
    public GlobalCellCoord? LastRequestedTargetCell { get; internal set; }
    public int SupersededOrderCount { get; internal set; }
    public int CompletedPathCount { get; internal set; }
    public int FailedPathCount { get; internal set; }
    public int StalePathResultDiscardCount { get; internal set; }
    public int SearchLimitExceededCount { get; internal set; }
    public int CompletedMovementCount { get; internal set; }
    public int FailedMovementCount { get; internal set; }
    public int DestinationCollisionCount { get; internal set; }
    public int LastPathLength { get; internal set; }
    public float LastPathCost { get; internal set; }
    public int LastExpandedNodeCount { get; internal set; }
    public double LastSearchDurationMs { get; internal set; }
    public int PeakDiscoveredCellCount { get; internal set; }
}

public sealed class DirectMoveCommandRegistryV3
{
    private readonly Dictionary<string, DirectMoveCommandV3> _commands = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MercenaryMoveOrderV3> _activeOrders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _orderRevisions = new(StringComparer.Ordinal);
    private readonly Queue<string> _recentCompleted = new();
    private readonly Queue<MercenaryMoveOrderV3> _recentOrderSummaries = new();
    public int ActiveCommandCount { get { HashSet<string> ids = new(StringComparer.Ordinal); foreach (MercenaryMoveOrderV3 o in _activeOrders.Values) ids.Add(o.CommandId); return ids.Count; } }
    public int ActiveMoveOrderCount => _activeOrders.Count;
    public long NextOrderRevision(string id) { _orderRevisions.TryGetValue(id, out long value); value++; _orderRevisions[id] = value; return value; }
    public bool TryGetActiveOrder(string id, out MercenaryMoveOrderV3? order) => _activeOrders.TryGetValue(id, out order);
    public bool TryGetCommand(string id, out DirectMoveCommandV3? command) => _commands.TryGetValue(id, out command);
    public IReadOnlyList<MercenaryMoveOrderV3> GetPendingOrders()
    { List<MercenaryMoveOrderV3> result = new(); foreach (MercenaryMoveOrderV3 o in _activeOrders.Values) if (o.Status == MercenaryMoveOrderStatusV3.PendingPath) result.Add(o); result.Sort((a,b)=>string.CompareOrdinal(a.MercenaryId,b.MercenaryId)); return result.AsReadOnly(); }
    internal void Register(DirectMoveCommandV3 command, IReadOnlyList<MercenaryMoveOrderV3> orders, MercenaryMovementRegistryV3 movement, MercenaryControlDiagnosticsV3 diag)
    {
        _commands.Add(command.CommandId, command);
        HashSet<string> supersededCommands = new(StringComparer.Ordinal);
        foreach (MercenaryMoveOrderV3 order in orders)
        {
            if (_activeOrders.TryGetValue(order.MercenaryId, out MercenaryMoveOrderV3? old))
            {
                old.Status = MercenaryMoveOrderStatusV3.Superseded;
                if (_commands.TryGetValue(old.CommandId, out DirectMoveCommandV3? oldCommand)) oldCommand.SupersededOrderCount++;
                AddRecentOrder(old);
                diag.SupersededOrderCount++;
                movement.RequestStopAfterCurrentSegment(order.MercenaryId);
                supersededCommands.Add(old.CommandId);
            }
            _activeOrders[order.MercenaryId] = order;
        }
        foreach (string commandId in supersededCommands) UpdateCommand(commandId);
        command.Status = DirectMoveCommandStatusV3.PathPending;
    }
    internal bool IsCurrent(MercenaryMoveOrderV3 order) => _activeOrders.TryGetValue(order.MercenaryId, out MercenaryMoveOrderV3? current) && ReferenceEquals(current, order);
    public bool SupersedeForWork(string mercenaryId,MercenaryMovementRegistryV3 movement,MercenaryControlDiagnosticsV3 diagnostics)
    {if(!_activeOrders.Remove(mercenaryId,out MercenaryMoveOrderV3? old))return false;old.Status=MercenaryMoveOrderStatusV3.Superseded;if(_commands.TryGetValue(old.CommandId,out DirectMoveCommandV3? command))command.SupersededOrderCount++;AddRecentOrder(old);diagnostics.SupersededOrderCount++;movement.RequestStopAfterCurrentSegment(mercenaryId);UpdateCommand(old.CommandId);return true;}
    public bool CancelForMercenary(string mercenaryId,MercenaryMovementRegistryV3 movement)
    {if(!_activeOrders.Remove(mercenaryId,out MercenaryMoveOrderV3? old))return false;old.Status=MercenaryMoveOrderStatusV3.Superseded;old.FailureReason="CancelledByPlayer";if(_commands.TryGetValue(old.CommandId,out DirectMoveCommandV3? command))command.SupersededOrderCount++;AddRecentOrder(old);movement.RequestStopAfterCurrentSegment(mercenaryId);UpdateCommand(old.CommandId);return true;}
    public IReadOnlyList<MercenaryMoveOrderV3> GetActiveOrders(){List<MercenaryMoveOrderV3> result=new(_activeOrders.Values);result.Sort((a,b)=>string.CompareOrdinal(a.MercenaryId,b.MercenaryId));return result.AsReadOnly();}
    internal void FinishOrder(MercenaryMoveOrderV3 order, bool success, string reason)
    {
        if (!IsCurrent(order)) return;
        order.Status = success ? MercenaryMoveOrderStatusV3.Completed : MercenaryMoveOrderStatusV3.Failed; order.FailureReason = reason;
        if (_commands.TryGetValue(order.CommandId, out DirectMoveCommandV3? command))
        {
            if (success) command.CompletedOrderCount++; else command.FailedOrderCount++;
        }
        AddRecentOrder(order);
        _activeOrders.Remove(order.MercenaryId); UpdateCommand(order.CommandId);
    }
    private void UpdateCommand(string commandId)
    {
        if (!_commands.TryGetValue(commandId, out DirectMoveCommandV3? command)) return;
        bool active = false; foreach (MercenaryMoveOrderV3 o in _activeOrders.Values) if (o.CommandId == commandId) { active = true; break; }
        if (active) { command.Status = DirectMoveCommandStatusV3.Active; return; }
        int finalized = command.CompletedOrderCount + command.FailedOrderCount + command.SupersededOrderCount;
        if (finalized < command.RequestedMercenaryIds.Count) return;
        command.Status = command.FailedOrderCount > 0
            ? DirectMoveCommandStatusV3.CompletedWithFailures
            : command.SupersededOrderCount == command.RequestedMercenaryIds.Count
                ? DirectMoveCommandStatusV3.Superseded
                : DirectMoveCommandStatusV3.Completed;
        _recentCompleted.Enqueue(commandId); while (_recentCompleted.Count > 64) { string remove = _recentCompleted.Dequeue(); if (remove != commandId) _commands.Remove(remove); }
    }
    private void AddRecentOrder(MercenaryMoveOrderV3 order)
    {
        _recentOrderSummaries.Enqueue(order);
        while (_recentOrderSummaries.Count > 128) _recentOrderSummaries.Dequeue();
    }
}

public static class MercenaryDestinationAssignmentServiceV3
{
    public static bool TryAssign(IReadOnlyList<string> ids, GlobalCellCoord target, IMercenaryNavigationWorldQueryV3 query,
        out IReadOnlyDictionary<string, GlobalCellCoord> destinations, out string reason)
    {
        int neededRadius = Mathf.Clamp(Mathf.Max(4, Mathf.CeilToInt((Mathf.Sqrt(ids.Count) - 1.0f) * 0.5f)), 4, 8);
        List<(Vector2I Cell, int Ring, float Distance)> candidates = new(); Vector2I center = target.Value;
        for (int y = -neededRadius; y <= neededRadius; y++) for (int x = -neededRadius; x <= neededRadius; x++)
        { Vector2I cell = center + new Vector2I(x,y); int ring = Mathf.Max(Mathf.Abs(x),Mathf.Abs(y)); if (query.IsWalkable(cell)) candidates.Add((cell,ring,MercenaryMovementCostPolicyV3.Octile(center,cell))); }
        candidates.Sort((a,b)=> { int c=a.Ring.CompareTo(b.Ring); if(c!=0)return c; c=a.Distance.CompareTo(b.Distance); if(c!=0)return c; c=a.Cell.Y.CompareTo(b.Cell.Y); return c!=0?c:a.Cell.X.CompareTo(b.Cell.X); });
        if (candidates.Count < ids.Count) { destinations = new Dictionary<string,GlobalCellCoord>(); reason = "Not enough unique walkable destination cells."; return false; }
        Dictionary<string,GlobalCellCoord> result = new(StringComparer.Ordinal); for(int i=0;i<ids.Count;i++) result.Add(ids[i],new GlobalCellCoord(candidates[i].Cell));
        destinations = result; reason = string.Empty; return true;
    }
}

public sealed class MercenaryControlSessionV3
{
    private readonly CompanySessionV3 _company;
    private readonly MercenarySessionV3 _mercenary;
    private long _commandRevision;
    private MercenaryWorkSessionV3? _workSession;
    public MercenaryControlSessionV3(long sessionRevision, CompanySessionV3 company, MercenarySessionV3 mercenary)
    { SessionRevision=sessionRevision; _company=company; _mercenary=mercenary; Selection=new(company,mercenary); Commands=new(); Movements=new(); ExternalMovements=new(); Diagnostics=new(); }
    public long SessionRevision { get; }
    public MercenarySelectionStateV3 Selection { get; }
    public DirectMoveCommandRegistryV3 Commands { get; }
    public MercenaryMovementRegistryV3 Movements { get; }
    public MercenaryMovementRequestRegistryV3 ExternalMovements { get; }
    public MercenaryControlDiagnosticsV3 Diagnostics { get; }
    public bool TryIssueDirectMove(string issuer, string companyId, IReadOnlyList<string> ids, GlobalCellCoord target,
        IMercenaryNavigationWorldQueryV3 query, long currentSessionRevision, out DirectMoveCommandV3? command, out string reason)
    {
        command=null;
        if (currentSessionRevision != SessionRevision) { reason="Control session is stale."; return false; }
        if (!_company.CanPlayerControlCompany(issuer,companyId)) { reason="Issuer cannot control company."; return false; }
        if (ids.Count==0) { reason="At least one mercenary is required."; return false; }
        HashSet<string> unique=new(StringComparer.Ordinal); foreach(string id in ids)
        { if(!unique.Add(id)){reason="Duplicate requested mercenary.";return false;} if(!_mercenary.Registry.TryGetState(id,out MercenaryStateV3? state)||state==null||state.CompanyId!=companyId||!_mercenary.CanPlayerControlMercenary(issuer,id)){reason="Requested mercenary ownership is invalid.";return false;} }
        if(!query.IsInsideWorld(target.Value)){reason="Target is outside world bounds.";return false;}
        if(!MercenaryDestinationAssignmentServiceV3.TryAssign(ids,target,query,out IReadOnlyDictionary<string,GlobalCellCoord> destinations,out reason))return false;
        foreach(string id in ids){_workSession?.CancelForDirectMove(id);_constructionCancellation?.Invoke(id);ExternalMovements.Cancel(id,"CancelledByDirectMove",Movements);Movements.RequestStopAfterCurrentSegment(id);}
        DateTime now=DateTime.UtcNow; string commandId=CommandIdFactoryV3.CreateCommandId(); long revision=++_commandRevision;
        DirectMoveCommandV3 created=new(commandId,SessionRevision,revision,issuer,companyId,ids,target,destinations,now); List<MercenaryMoveOrderV3> orders=new();
        foreach(string id in ids)
        { _mercenary.Registry.TryGetState(id,out MercenaryStateV3? state); GlobalCellCoord start=Movements.TryGet(id,out MercenaryMovementStateV3? moving)&&moving!=null?moving.ToCell:state!.CurrentCell; orders.Add(new(commandId,id,start,destinations[id],SessionRevision,Commands.NextOrderRevision(id),now)); }
        Commands.Register(created,orders,Movements,Diagnostics); Diagnostics.LastCommandId=commandId; Diagnostics.LastRequestedTargetCell=target; command=created; reason=string.Empty; return true;
    }
    public bool IsCurrentOrder(MercenaryMoveOrderV3 order)=>order.SessionRevision==SessionRevision&&Commands.IsCurrent(order);
    public MercenarySessionV3 MercenarySession => _mercenary;
    public void AttachWorkSession(MercenaryWorkSessionV3 workSession){_workSession=workSession;}
    private Func<string,bool>? _constructionCancellation;
    public void AttachConstructionCancellation(Func<string,bool> cancellation)=>_constructionCancellation=cancellation;
    public bool SupersedeDirectMovementForWork(string mercenaryId)=>Commands.SupersedeForWork(mercenaryId,Movements,Diagnostics);
    public bool CancelCurrentActivity(string mercenaryId)
    {
        bool changed=_workSession?.CancelForDirectMove(mercenaryId)==true;
        changed=_constructionCancellation?.Invoke(mercenaryId)==true||changed;
        changed=ExternalMovements.Cancel(mercenaryId,"CancelledByPlayer",Movements)||changed;
        changed=Commands.CancelForMercenary(mercenaryId,Movements)||changed;
        if(!Movements.TryGet(mercenaryId,out _)&&_mercenary.Registry.TryGetState(mercenaryId,out MercenaryStateV3? state)&&state!=null)
            state.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);
        return changed;
    }
}
