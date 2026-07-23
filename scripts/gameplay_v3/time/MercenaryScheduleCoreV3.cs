using System;
using System.Collections.Generic;
using GameplayV3.Mercenary;

namespace GameplayV3.Time;

public enum MercenaryScheduleStateV3 { Work, Anything, Recreation, Sleep }
public enum MercenarySchedulePresetV3 { Standard, DayShift, NightShift, Free, Custom }
public enum MercenaryScheduleChangeReasonV3 { Registered, PresetApplied, SlotEdited, HourChanged, ResetDefault, Removed }
public enum MercenaryScheduleEventTypeV3 { ScheduleChanged, CurrentScheduleStateChanged, SchedulePresetApplied, ScheduleRemoved }

public readonly record struct MercenarySchedulePolicyV3(string MercenaryId,MercenaryScheduleStateV3 CurrentState,bool AutomaticJobEligible,bool NeedsEligible,bool WantsScheduledRest,bool RecreationIntent,bool DirectOrderBypassAllowed,long Revision);
public readonly record struct MercenaryScheduleEventV3(string MercenaryId,MercenaryScheduleEventTypeV3 EventType,int? ChangedHour,MercenaryScheduleStateV3? PreviousState,MercenaryScheduleStateV3? CurrentState,MercenarySchedulePresetV3? PreviousPreset,MercenarySchedulePresetV3? CurrentPreset,long ScheduleRevision,long CurrentStateRevision,MercenaryScheduleChangeReasonV3 Reason);

public sealed class MercenaryScheduleSnapshotV3
{
    internal MercenaryScheduleSnapshotV3(string id,MercenarySchedulePresetV3 preset,MercenaryScheduleStateV3 current,int hour,int next,long revision,long stateRevision,MercenaryScheduleChangeReasonV3 reason){MercenaryId=id;Preset=preset;CurrentState=current;CurrentHour=hour;NextTransitionHour=next;Revision=revision;CurrentStateRevision=stateRevision;LastChangedReason=reason;}
    public string MercenaryId{get;}public MercenarySchedulePresetV3 Preset{get;}public MercenaryScheduleStateV3 CurrentState{get;}public int CurrentHour{get;}public int NextTransitionHour{get;}public long Revision{get;}public long CurrentStateRevision{get;}public MercenaryScheduleChangeReasonV3 LastChangedReason{get;}
}

public sealed class MercenaryScheduleDiagnosticsV3
{
    public long ScheduleEventCount{get;internal set;}public long TransitionBucketEvaluationCount{get;internal set;}public long FullMercenaryScanCount{get;internal set;}public long DelayedScheduleReleaseCount{get;internal set;}public long BlockedAutoAssignmentCount{get;internal set;}public long DuplicateClockSubscriptionCount{get;internal set;}public long DuplicateRegistrySubscriptionCount{get;internal set;}public long DirectOrderBlockedCount{get;internal set;}public long ReservationLeakCount{get;internal set;}
}

public sealed class MercenaryScheduleSessionV3:IDisposable
{
    private const int RecentLimit=16;
    private sealed class Entry
    {
        public readonly MercenaryScheduleStateV3[] Slots=new MercenaryScheduleStateV3[24];public MercenarySchedulePresetV3 Preset;public MercenaryScheduleStateV3 Current;public long Revision;public long StateRevision;public MercenaryScheduleChangeReasonV3 Reason;
    }
    private readonly SimulationClockSessionV3 _clock;private readonly MercenaryRegistryV3 _registry;private readonly Dictionary<string,Entry> _entries=new(StringComparer.Ordinal);private readonly HashSet<string>[] _transitions=new HashSet<string>[24];private readonly HashSet<string> _dirty=new(StringComparer.Ordinal);private readonly Queue<MercenaryScheduleEventV3> _recent=new();private bool _disposed;
    public MercenaryScheduleSessionV3(long sessionRevision,SimulationClockSessionV3 clock,MercenaryRegistryV3 registry)
    {
        SessionRevision=sessionRevision;_clock=clock;_registry=registry;for(int i=0;i<24;i++)_transitions[i]=new(StringComparer.Ordinal);_clock.HourChanged+=OnHourChanged;_registry.MercenaryRegistered+=OnRegistered;_registry.MercenaryRemoved+=OnRemoved;ClockSubscriptionCount=1;RegistrySubscriptionCount=1;foreach(string id in registry.GetAllMercenaryIds())Add(id);
    }
    public long SessionRevision{get;private set;}public int Count=>_entries.Count;public int DirtyCount=>_dirty.Count;public int TransitionIndexEntryCount{get{int count=0;for(int i=0;i<24;i++)count+=_transitions[i].Count;return count;}}public int ClockSubscriptionCount{get;private set;}public int RegistrySubscriptionCount{get;private set;}public bool IsDisposed=>_disposed;public MercenaryScheduleDiagnosticsV3 Diagnostics{get;}=new();
    public void RebindSessionRevision(long sessionRevision){if(_disposed)throw new ObjectDisposedException(nameof(MercenaryScheduleSessionV3));if(sessionRevision<1)throw new ArgumentOutOfRangeException(nameof(sessionRevision));SessionRevision=sessionRevision;}
    public event Action<MercenaryScheduleEventV3>? ScheduleChanged;public event Action<MercenaryScheduleEventV3>? CurrentScheduleStateChanged;public event Action<MercenaryScheduleEventV3>? SchedulePresetApplied;public event Action<MercenaryScheduleEventV3>? ScheduleRemoved;
    public bool TryGetSchedule(string id,out MercenaryScheduleSnapshotV3? snapshot){if(!_entries.TryGetValue(id,out Entry? e)){snapshot=null;return false;}snapshot=Snapshot(id,e);return true;}
    public bool TryGetCurrentState(string id,out MercenaryScheduleStateV3 state){if(_entries.TryGetValue(id,out Entry? e)){state=e.Current;return true;}state=default;return false;}
    public bool TryGetSlot(string id,int hour,out MercenaryScheduleStateV3 state){if(hour is >=0 and <24&&_entries.TryGetValue(id,out Entry? e)){state=e.Slots[hour];return true;}state=default;return false;}
    public bool TrySetHourSlot(string id,int hour,MercenaryScheduleStateV3 state,out string reason)
    {
        if(hour is <0 or >23){reason="InvalidHour";return false;}if(!Enum.IsDefined(state)){reason="InvalidScheduleState";return false;}if(!_entries.TryGetValue(id,out Entry? e)||!_registry.ContainsMercenary(id)){reason="MercenaryNotRegistered";return false;}if(e.Slots[hour]==state){reason=string.Empty;return true;}MercenarySchedulePresetV3 oldPreset=e.Preset;e.Slots[hour]=state;e.Preset=MercenarySchedulePresetV3.Custom;e.Revision++;e.Reason=MercenaryScheduleChangeReasonV3.SlotEdited;RefreshBoundary(id,e,hour);RefreshBoundary(id,e,(hour+1)%24);Emit(id,e,MercenaryScheduleEventTypeV3.ScheduleChanged,hour,null,null,oldPreset,e.Preset,e.Reason);RefreshCurrent(id,e,e.Reason);reason=string.Empty;return true;
    }
    public bool TrySetHourRange(string id,int startHourInclusive,int endHourExclusive,MercenaryScheduleStateV3 state,out string reason)
    {
        if(startHourInclusive is <0 or >23||endHourExclusive is <0 or >23){reason="InvalidHour";return false;}if(startHourInclusive==endHourExclusive){reason="EmptyHourRange";return false;}if(!_entries.ContainsKey(id)){reason="MercenaryNotRegistered";return false;}int hour=startHourInclusive;do{if(!TrySetHourSlot(id,hour,state,out reason))return false;hour=(hour+1)%24;}while(hour!=endHourExclusive);reason=string.Empty;return true;
    }
    public bool TryApplyPreset(string id,MercenarySchedulePresetV3 preset,out string reason)
    {
        if(preset==MercenarySchedulePresetV3.Custom||!Enum.IsDefined(preset)){reason="InvalidPreset";return false;}if(!_entries.TryGetValue(id,out Entry? e)||!_registry.ContainsMercenary(id)){reason="MercenaryNotRegistered";return false;}MercenaryScheduleStateV3[] desired=BuildPreset(preset);bool same=e.Preset==preset;for(int i=0;i<24&&same;i++)same=e.Slots[i]==desired[i];if(same){reason=string.Empty;return true;}MercenarySchedulePresetV3 old=e.Preset;Array.Copy(desired,e.Slots,24);e.Preset=preset;e.Revision++;e.Reason=MercenaryScheduleChangeReasonV3.PresetApplied;Reindex(id,e);Emit(id,e,MercenaryScheduleEventTypeV3.SchedulePresetApplied,null,null,null,old,preset,e.Reason);RefreshCurrent(id,e,e.Reason);reason=string.Empty;return true;
    }
    public bool TryResetDefault(string id,out string reason){bool ok=TryApplyPreset(id,MercenarySchedulePresetV3.Standard,out reason);if(ok&&_entries.TryGetValue(id,out Entry? e))e.Reason=MercenaryScheduleChangeReasonV3.ResetDefault;return ok;}
    public MercenarySchedulePolicyV3 GetCurrentPolicy(string id)=>_entries.TryGetValue(id,out Entry? e)?Policy(id,e):new(id,MercenaryScheduleStateV3.Anything,true,true,false,false,true,0);
    public int GetNextTransitionHour(string id)=>_entries.TryGetValue(id,out Entry? e)?NextTransition(e):-1;
    public bool IsAutomaticJobEligible(string id)=>!_entries.TryGetValue(id,out Entry? e)||e.Current is MercenaryScheduleStateV3.Work or MercenaryScheduleStateV3.Anything;
    public bool WantsScheduledRest(string id)=>_entries.TryGetValue(id,out Entry? e)&&e.Current==MercenaryScheduleStateV3.Sleep;
    public IReadOnlyList<MercenaryScheduleEventV3> GetRecentEvents()=>Array.AsReadOnly(_recent.ToArray());
    public IReadOnlyList<string> DrainDirty(){if(_dirty.Count==0)return Array.Empty<string>();List<string> ids=new(_dirty);ids.Sort(StringComparer.Ordinal);_dirty.Clear();return ids.AsReadOnly();}
    private void Add(string id){if(_entries.ContainsKey(id))return;Entry e=new(){Preset=MercenarySchedulePresetV3.Standard,Reason=MercenaryScheduleChangeReasonV3.Registered};Array.Copy(BuildPreset(e.Preset),e.Slots,24);e.Current=e.Slots[_clock.Hour];e.Revision=1;e.StateRevision=1;_entries.Add(id,e);Reindex(id,e);_dirty.Add(id);Emit(id,e,MercenaryScheduleEventTypeV3.ScheduleChanged,null,null,e.Current,null,e.Preset,e.Reason);}
    private void Remove(string id){if(!_entries.Remove(id,out Entry? e))return;for(int i=0;i<24;i++)_transitions[i].Remove(id);_dirty.Remove(id);e.Revision++;Emit(id,e,MercenaryScheduleEventTypeV3.ScheduleRemoved,null,e.Current,null,e.Preset,null,MercenaryScheduleChangeReasonV3.Removed);}
    private void OnRegistered(MercenaryStateV3 state)=>Add(state.MercenaryId);private void OnRemoved(string id,string company)=>Remove(id);
    private void OnHourChanged(SimulationClockEventV3 value){int hour=value.CurrentHour;string[] ids=new string[_transitions[hour].Count];_transitions[hour].CopyTo(ids);Array.Sort(ids,StringComparer.Ordinal);Diagnostics.TransitionBucketEvaluationCount+=ids.Length;foreach(string id in ids)if(_entries.TryGetValue(id,out Entry? e))RefreshCurrent(id,e,MercenaryScheduleChangeReasonV3.HourChanged);}
    private void RefreshCurrent(string id,Entry e,MercenaryScheduleChangeReasonV3 reason){MercenaryScheduleStateV3 next=e.Slots[_clock.Hour];if(next==e.Current)return;MercenaryScheduleStateV3 previous=e.Current;e.Current=next;e.StateRevision++;e.Revision++;e.Reason=reason;_dirty.Add(id);Emit(id,e,MercenaryScheduleEventTypeV3.CurrentScheduleStateChanged,_clock.Hour,previous,next,e.Preset,e.Preset,reason);}
    private void Reindex(string id,Entry e){for(int hour=0;hour<24;hour++)RefreshBoundary(id,e,hour);}
    private void RefreshBoundary(string id,Entry e,int hour){if(e.Slots[(hour+23)%24]!=e.Slots[hour])_transitions[hour].Add(id);else _transitions[hour].Remove(id);}
    private MercenaryScheduleSnapshotV3 Snapshot(string id,Entry e)=>new(id,e.Preset,e.Current,_clock.Hour,NextTransition(e),e.Revision,e.StateRevision,e.Reason);
    private static MercenarySchedulePolicyV3 Policy(string id,Entry e)=>new(id,e.Current,e.Current is MercenaryScheduleStateV3.Work or MercenaryScheduleStateV3.Anything,true,e.Current==MercenaryScheduleStateV3.Sleep,e.Current==MercenaryScheduleStateV3.Recreation,true,e.Revision);
    private int NextTransition(Entry e){for(int offset=1;offset<=24;offset++){int hour=(_clock.Hour+offset)%24;if(e.Slots[hour]!=e.Current)return hour;}return -1;}
    private void Emit(string id,Entry e,MercenaryScheduleEventTypeV3 type,int? hour,MercenaryScheduleStateV3? previous,MercenaryScheduleStateV3? current,MercenarySchedulePresetV3? previousPreset,MercenarySchedulePresetV3? currentPreset,MercenaryScheduleChangeReasonV3 reason){MercenaryScheduleEventV3 value=new(id,type,hour,previous,current,previousPreset,currentPreset,e.Revision,e.StateRevision,reason);_recent.Enqueue(value);while(_recent.Count>RecentLimit)_recent.Dequeue();Diagnostics.ScheduleEventCount++;switch(type){case MercenaryScheduleEventTypeV3.ScheduleChanged:ScheduleChanged?.Invoke(value);break;case MercenaryScheduleEventTypeV3.CurrentScheduleStateChanged:CurrentScheduleStateChanged?.Invoke(value);break;case MercenaryScheduleEventTypeV3.SchedulePresetApplied:SchedulePresetApplied?.Invoke(value);break;case MercenaryScheduleEventTypeV3.ScheduleRemoved:ScheduleRemoved?.Invoke(value);break;}}
    private static MercenaryScheduleStateV3[] BuildPreset(MercenarySchedulePresetV3 preset){MercenaryScheduleStateV3[] slots=new MercenaryScheduleStateV3[24];for(int h=0;h<24;h++)slots[h]=preset switch{MercenarySchedulePresetV3.Standard=>h<6||h>=22?MercenaryScheduleStateV3.Sleep:h<8||h>=18?MercenaryScheduleStateV3.Anything:MercenaryScheduleStateV3.Work,MercenarySchedulePresetV3.DayShift=>h<6||h>=22?MercenaryScheduleStateV3.Sleep:h<7||h>=19?MercenaryScheduleStateV3.Anything:MercenaryScheduleStateV3.Work,MercenarySchedulePresetV3.NightShift=>h<6||h>=20?MercenaryScheduleStateV3.Work:h<8||h>=16?MercenaryScheduleStateV3.Anything:MercenaryScheduleStateV3.Sleep,_=>MercenaryScheduleStateV3.Anything};return slots;}
    public void Dispose(){if(_disposed)return;_disposed=true;_clock.HourChanged-=OnHourChanged;_registry.MercenaryRegistered-=OnRegistered;_registry.MercenaryRemoved-=OnRemoved;ClockSubscriptionCount=0;RegistrySubscriptionCount=0;_entries.Clear();_dirty.Clear();_recent.Clear();for(int i=0;i<24;i++)_transitions[i].Clear();ScheduleChanged=null;CurrentScheduleStateChanged=null;SchedulePresetApplied=null;ScheduleRemoved=null;}
}
