using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using GameplayV3.Session;
using WorldV2;

namespace GameplayV3.Construction;

public enum DoorStateV3 { Closed, Opening, Open, Closing }
public enum DoorModeV3 { Auto, HoldOpen }

public sealed class DoorPassageSettingsV3
{
    public double OpenTransitionSeconds { get; init; } = .12;
    public double CloseTransitionSeconds { get; init; } = .12;
    public double CloseDelaySeconds { get; init; } = .75;
    public double RuntimeTickIntervalSeconds { get; init; } = .05;
    public int MaxDoorTransitionsPerTick { get; init; } = 64;
}

public sealed class DoorPassageStateV3
{
    private readonly HashSet<string> _activeUsers = new(StringComparer.Ordinal);
    internal DoorPassageStateV3(StructureStateV3 structure)
    {
        StructureId=structure.StructureId;CompanyId=structure.CompanyId;Cell=structure.AnchorCell;Rotation=structure.Orientation;
    }
    public string StructureId { get; }
    public string CompanyId { get; }
    public GlobalCellCoord Cell { get; }
    public StructureOrientationV3 Rotation { get; }
    public DoorStateV3 State { get; internal set; } = DoorStateV3.Closed;
    public DoorModeV3 Mode { get; internal set; } = DoorModeV3.Auto;
    public long Revision { get; internal set; }
    public double StateChangedAt { get; internal set; }
    public double CloseDueAt { get; internal set; } = -1;
    public long CloseScheduleToken { get; internal set; }
    public long TransitionToken { get; internal set; }
    public string LastOpenReason { get; internal set; } = string.Empty;
    public int ActivePassageUserCount => _activeUsers.Count;
    public IReadOnlyList<string> GetActivePassageUserIds()=>_activeUsers.OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly();
    internal bool AddUser(string id)=>_activeUsers.Add(id);
    internal bool RemoveUser(string id)=>_activeUsers.Remove(id);
    internal void ClearUsers()=>_activeUsers.Clear();
}

public sealed class DoorPassageDiagnosticsV3
{
    public long AcquireCount { get; internal set; }
    public long ReleaseCount { get; internal set; }
    public long DuplicateAcquireCount { get; internal set; }
    public long StaleScheduleCount { get; internal set; }
    public long TransitionCount { get; internal set; }
    public long FullDoorScanCount { get; internal set; }
    public long ProcessedScheduleCount { get; internal set; }
    public long PeakScheduledCount { get; internal set; }
    public double LastTickCpuMilliseconds { get; internal set; }
    public string LastFailureReason { get; internal set; } = string.Empty;
}

internal enum DoorScheduleKindV3 { FinishOpening, BeginClosing, FinishClosing }
internal readonly record struct DoorScheduleEntryV3(string StructureId,DoorScheduleKindV3 Kind,long Token,double DueAt);

public sealed class DoorPassageRegistryV3
{
    private readonly DoorPassageSettingsV3 _settings;
    private readonly Dictionary<string,DoorPassageStateV3> _byStructure = new(StringComparer.Ordinal);
    private readonly Dictionary<Vector2I,string> _byCell = new();
    private readonly Dictionary<string,string> _doorByMercenary = new(StringComparer.Ordinal);
    private readonly PriorityQueue<DoorScheduleEntryV3,(double Due,int Sequence)> _schedule = new();
    private int _sequence;
    private readonly int[] _stateCounts = new int[4];
    public DoorPassageRegistryV3(DoorPassageSettingsV3? settings=null){_settings=settings??new();}
    public event Action? Changed;
    public DoorPassageSettingsV3 Settings=>_settings;
    public DoorPassageDiagnosticsV3 Diagnostics { get; } = new();
    public int Count=>_byStructure.Count;
    public bool Contains(string structureId)=>_byStructure.ContainsKey(structureId);
    public int ActivePassageUserCount=>_doorByMercenary.Count;
    public int ScheduledTransitionCount=>_schedule.Count;
    public int ClosedCount=>_stateCounts[(int)DoorStateV3.Closed];
    public int OpeningCount=>_stateCounts[(int)DoorStateV3.Opening];
    public int OpenCount=>_stateCounts[(int)DoorStateV3.Open];
    public int ClosingCount=>_stateCounts[(int)DoorStateV3.Closing];
    public double ClockSeconds { get; private set; }
    public bool TryGet(string structureId,out DoorPassageStateV3? state)=>_byStructure.TryGetValue(structureId,out state);
    public bool TryGetAtCell(GlobalCellCoord cell,out DoorPassageStateV3? state){state=null;return _byCell.TryGetValue(cell.Value,out string? id)&&_byStructure.TryGetValue(id,out state);}
    public IReadOnlyList<DoorPassageStateV3> GetAll()=>_byStructure.Values.OrderBy(x=>x.StructureId,StringComparer.Ordinal).ToList().AsReadOnly();

    public bool TryRegister(StructureStateV3 structure,out string reason)
    {
        if(structure.MovementKind!=StructureMovementKindV3.PassableDoor){reason="StructureIsNotPassableDoor";return false;}
        if(structure.OccupiedCells.Count!=1){reason="PassableDoorMustOccupyOneCell";return false;}
        if(_byStructure.ContainsKey(structure.StructureId)){reason="DuplicateDoorStructureId";return false;}
        if(_byCell.ContainsKey(structure.AnchorCell.Value)){reason="DuplicateDoorCell";return false;}
        DoorPassageStateV3 state=new(structure){StateChangedAt=ClockSeconds};
        _byStructure.Add(state.StructureId,state);_byCell.Add(state.Cell.Value,state.StructureId);_stateCounts[(int)DoorStateV3.Closed]++;reason=string.Empty;Changed?.Invoke();return true;
    }

    public bool TryUnregister(string structureId,out DoorPassageStateV3? removed)
    {
        if(!_byStructure.Remove(structureId,out removed)||removed==null)return false;
        _byCell.Remove(removed.Cell.Value);_stateCounts[(int)removed.State]--;
        foreach(string user in removed.GetActivePassageUserIds())_doorByMercenary.Remove(user);
        removed.ClearUsers();removed.TransitionToken++;removed.CloseScheduleToken++;Changed?.Invoke();return true;
    }

    public bool AcquirePassage(GlobalCellCoord cell,string mercenaryId,string reason,out string failureReason)
    {
        if(string.IsNullOrWhiteSpace(mercenaryId)){failureReason="MercenaryIdRequired";return false;}
        if(!TryGetAtCell(cell,out DoorPassageStateV3? door)||door==null){failureReason="DoorNotFound";return false;}
        if(_doorByMercenary.TryGetValue(mercenaryId,out string? previous)&&previous!=door.StructureId)ReleasePassage(mercenaryId,out _);
        if(!door.AddUser(mercenaryId)){Diagnostics.DuplicateAcquireCount++;failureReason=string.Empty;return true;}
        _doorByMercenary[mercenaryId]=door.StructureId;Diagnostics.AcquireCount++;door.CloseScheduleToken++;door.CloseDueAt=-1;door.LastOpenReason=reason;
        if(door.State is DoorStateV3.Closed or DoorStateV3.Closing){SetState(door,DoorStateV3.Opening);Schedule(door,DoorScheduleKindV3.FinishOpening,ClockSeconds+_settings.OpenTransitionSeconds,++door.TransitionToken);}
        else Touch(door);
        failureReason=string.Empty;return true;
    }

    public bool ReleasePassage(string mercenaryId,out string reason)
    {
        if(!_doorByMercenary.Remove(mercenaryId,out string? structureId)||!_byStructure.TryGetValue(structureId,out DoorPassageStateV3? door)){reason="PassageUserNotFound";return false;}
        door.RemoveUser(mercenaryId);Diagnostics.ReleaseCount++;
        if(door.ActivePassageUserCount==0&&door.Mode==DoorModeV3.Auto)
        {door.CloseDueAt=ClockSeconds+_settings.CloseDelaySeconds;Schedule(door,DoorScheduleKindV3.BeginClosing,door.CloseDueAt,++door.CloseScheduleToken);}
        Touch(door);reason=string.Empty;return true;
    }

    public bool TrySetMode(string structureId,DoorModeV3 mode,out string reason)
    {
        if(!_byStructure.TryGetValue(structureId,out DoorPassageStateV3? door)){reason="DoorNotFound";return false;}
        if(door.Mode==mode){reason=string.Empty;return true;}door.Mode=mode;door.CloseScheduleToken++;door.CloseDueAt=-1;
        if(mode==DoorModeV3.HoldOpen&&door.State is DoorStateV3.Closed or DoorStateV3.Closing){SetState(door,DoorStateV3.Opening);Schedule(door,DoorScheduleKindV3.FinishOpening,ClockSeconds+_settings.OpenTransitionSeconds,++door.TransitionToken);}
        else if(mode==DoorModeV3.Auto&&door.ActivePassageUserCount==0){door.CloseDueAt=ClockSeconds+_settings.CloseDelaySeconds;Schedule(door,DoorScheduleKindV3.BeginClosing,door.CloseDueAt,++door.CloseScheduleToken);Touch(door);}
        reason=string.Empty;return true;
    }

    public void OnSegmentStarting(string mercenaryId,GlobalCellCoord from,GlobalCellCoord to)
    {
        if(_doorByMercenary.TryGetValue(mercenaryId,out string? held)&&(!_byStructure.TryGetValue(held,out DoorPassageStateV3? current)||current.Cell.Value!=to.Value))ReleasePassage(mercenaryId,out _);
        if(TryGetAtCell(to,out _))AcquirePassage(to,mercenaryId,"MovementSegment",out _);
    }
    public void OnMovementEnded(string mercenaryId)=>ReleasePassage(mercenaryId,out _);

    public int AdvanceClock(double deltaSeconds)
    {
        ClockSeconds+=Math.Max(0,deltaSeconds);Stopwatch sw=Stopwatch.StartNew();int processed=0;
        while(processed<_settings.MaxDoorTransitionsPerTick&&_schedule.TryPeek(out _,out var priority)&&priority.Due<=ClockSeconds)
        {DoorScheduleEntryV3 entry=_schedule.Dequeue();processed++;Diagnostics.ProcessedScheduleCount++;Apply(entry);}
        sw.Stop();Diagnostics.LastTickCpuMilliseconds=sw.Elapsed.TotalMilliseconds;return processed;
    }
    private void Apply(DoorScheduleEntryV3 entry)
    {
        if(!_byStructure.TryGetValue(entry.StructureId,out DoorPassageStateV3? door)){Diagnostics.StaleScheduleCount++;return;}
        switch(entry.Kind)
        {
            case DoorScheduleKindV3.FinishOpening:
                if(entry.Token!=door.TransitionToken||door.State!=DoorStateV3.Opening){Diagnostics.StaleScheduleCount++;return;}SetState(door,DoorStateV3.Open);break;
            case DoorScheduleKindV3.BeginClosing:
                if(entry.Token!=door.CloseScheduleToken||door.ActivePassageUserCount!=0||door.Mode!=DoorModeV3.Auto){Diagnostics.StaleScheduleCount++;return;}
                door.CloseDueAt=-1;if(door.State==DoorStateV3.Closed)return;SetState(door,DoorStateV3.Closing);Schedule(door,DoorScheduleKindV3.FinishClosing,ClockSeconds+_settings.CloseTransitionSeconds,++door.TransitionToken);break;
            case DoorScheduleKindV3.FinishClosing:
                if(entry.Token!=door.TransitionToken||door.State!=DoorStateV3.Closing||door.ActivePassageUserCount!=0||door.Mode!=DoorModeV3.Auto){Diagnostics.StaleScheduleCount++;return;}SetState(door,DoorStateV3.Closed);break;
        }
    }
    private void Schedule(DoorPassageStateV3 door,DoorScheduleKindV3 kind,double due,long token){_schedule.Enqueue(new(door.StructureId,kind,token,due),(due,++_sequence));Diagnostics.PeakScheduledCount=Math.Max(Diagnostics.PeakScheduledCount,_schedule.Count);}
    private void SetState(DoorPassageStateV3 door,DoorStateV3 next){if(door.State==next){Touch(door);return;}_stateCounts[(int)door.State]--;door.State=next;_stateCounts[(int)door.State]++;door.StateChangedAt=ClockSeconds;Diagnostics.TransitionCount++;Touch(door);}
    private void Touch(DoorPassageStateV3 door){door.Revision++;Changed?.Invoke();}
    public void Clear(){foreach(var door in _byStructure.Values){door.ClearUsers();door.TransitionToken++;door.CloseScheduleToken++;}_byStructure.Clear();_byCell.Clear();_doorByMercenary.Clear();_schedule.Clear();Array.Clear(_stateCounts);ClockSeconds=0;Changed?.Invoke();}
}

public partial class DoorPassageRuntimeV3 : Node
{
    private DoorPassageRegistryV3? _registry;private ConstructionSessionV3? _session;private double _accumulator;
    public void Initialize(ConstructionSessionV3 session){_session=session;_registry=session.DoorPassages;}
    public override void _PhysicsProcess(double delta)
    {
        if(_registry==null||_session==null||!GameplaySessionV3.IsCurrentConstructionSession(_session))return;_accumulator+=Math.Max(0,delta);
        double interval=Math.Max(.001,_registry.Settings.RuntimeTickIntervalSeconds);while(_accumulator+0.0000001>=interval){_accumulator-=interval;_registry.AdvanceClock(interval);}
    }
}
