using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameplayV3.Construction;
using GameplayV3.Mercenary;
using GameplayV3.Work;
using WorldV2;

namespace GameplayV3.Needs;

public enum FatigueActivityV3{Idle,Moving,Gathering,Hauling,Construction,Demolition,Resting}
public sealed class MercenaryFatigueSettingsV3
{
    public float TickIntervalSeconds{get;init;}=.5f;public float IdlePerSecond{get;init;}=.00025f;public float MovingPerSecond{get;init;}=.001f;public float GatheringPerSecond{get;init;}=.0015f;public float HaulingPerSecond{get;init;}=.0015f;public float ConstructionPerSecond{get;init;}=.002f;public float DemolitionPerSecond{get;init;}=.002f;public float RecoveryPerSecond{get;init;}=.015f;public float TiredThreshold{get;init;}=.35f;public float WearyThreshold{get;init;}=.65f;public float AutoRestThreshold{get;init;}=.75f;public float ExhaustedThreshold{get;init;}=.90f;public float WorkBlockThreshold{get;init;}=.95f;public float RestCompletionThreshold{get;init;}=.15f;public float ManualRestMinimum{get;init;}=.20f;
}
public sealed class MercenaryFatigueStateV3
{
    public MercenaryFatigueStateV3(string mercenaryId,float fatigue=.15f){if(!MercenaryIdFactoryV3.IsValidMercenaryId(mercenaryId))throw new ArgumentException("Invalid mercenary id.");MercenaryId=mercenaryId;Fatigue=Math.Clamp(fatigue,0,1);}
    public string MercenaryId{get;}public float Fatigue{get;private set;}public long Revision{get;private set;}public void Set(float value){float next=Math.Clamp(value,0,1);if(Math.Abs(next-Fatigue)<.000001f)return;Fatigue=next;Revision++;}
}
public static class MercenaryFatiguePolicyV3
{
    public static float MovementMultiplier(float fatigue)=>fatigue>=.9f?.8f:fatigue>=.65f?.9f:1f;
    public static float WorkMultiplier(float fatigue)=>fatigue>=.9f?.6f:fatigue>=.65f?.8f:fatigue>=.35f?.95f:1f;
    public static bool BlocksNewWork(float fatigue)=>fatigue>=.95f;
    public static float DeltaPerSecond(FatigueActivityV3 activity,MercenaryFatigueSettingsV3 s)=>activity switch{FatigueActivityV3.Moving=>s.MovingPerSecond,FatigueActivityV3.Gathering=>s.GatheringPerSecond,FatigueActivityV3.Hauling=>s.HaulingPerSecond,FatigueActivityV3.Construction=>s.ConstructionPerSecond,FatigueActivityV3.Demolition=>s.DemolitionPerSecond,FatigueActivityV3.Resting=>-s.RecoveryPerSecond,_=>s.IdlePerSecond};
}
public sealed class MercenaryFatigueRegistryV3
{
    private readonly Dictionary<string,MercenaryFatigueStateV3> _states=new(StringComparer.Ordinal);public int Count=>_states.Count;public long Revision{get;private set;}
    public MercenaryFatigueStateV3 GetOrCreate(string id){if(!_states.TryGetValue(id,out var state)){state=new(id);_states.Add(id,state);Revision++;}return state;}public bool TryGet(string id,out MercenaryFatigueStateV3? state)=>_states.TryGetValue(id,out state);public float GetValue(string id)=>_states.TryGetValue(id,out var state)?state.Fatigue:.15f;
    public void Apply(string id,FatigueActivityV3 activity,float seconds,MercenaryFatigueSettingsV3 settings,float recoveryMultiplier=1f){var state=GetOrCreate(id);float rate=MercenaryFatiguePolicyV3.DeltaPerSecond(activity,settings);if(rate<0)rate*=Math.Max(.01f,recoveryMultiplier);float before=state.Fatigue;state.Set(before+rate*Math.Max(0,seconds));if(state.Fatigue!=before)Revision++;}
    public void Clear(){_states.Clear();Revision++;}
}

public readonly record struct RestFacilitySlotV3(string RestSlotId,string StructureId,int SlotIndex,GlobalCellCoord UseCell,float RecoveryMultiplier,string QualityLabel);
public static class RestFacilitySlotResolverV3
{
    public static IReadOnlyList<RestFacilitySlotV3> Resolve(StructureStateV3 structure,StructureDefinitionV3 definition){if(definition.RestFacility==null)return Array.Empty<RestFacilitySlotV3>();List<RestFacilitySlotV3> result=new();foreach(var slot in definition.RestFacility.Slots){GlobalCellCoord cell=StructureFootprintResolverV3.ResolveLocalCell(definition,structure.AnchorCell,structure.Orientation,slot.LocalCell);result.Add(new($"restslot_{structure.StructureId}_{slot.SlotIndex}",structure.StructureId,slot.SlotIndex,cell,definition.RestFacility.RecoveryMultiplier,definition.RestFacility.QualityLabel));}return result.AsReadOnly();}
}
public sealed record RestAssignmentV3(string MercenaryId,string RestSlotId,string StructureId,int SlotIndex,DateTime AssignedUtc);
public sealed class RestAssignmentRegistryV3
{
    private readonly Dictionary<string,RestAssignmentV3> _byMercenary=new(StringComparer.Ordinal);private readonly Dictionary<string,string> _mercenaryBySlot=new(StringComparer.Ordinal);public int Count=>_byMercenary.Count;public long Revision{get;private set;}public event Action? Changed;
    public bool TryAssign(string mercenaryId,RestFacilitySlotV3 slot,out string reason){if(_mercenaryBySlot.TryGetValue(slot.RestSlotId,out string? owner)&&owner!=mercenaryId){reason="RestSlotAssigned";return false;}if(_byMercenary.TryGetValue(mercenaryId,out var old)){if(old.RestSlotId==slot.RestSlotId){reason=string.Empty;return true;}_mercenaryBySlot.Remove(old.RestSlotId);}_byMercenary[mercenaryId]=new(mercenaryId,slot.RestSlotId,slot.StructureId,slot.SlotIndex,DateTime.UtcNow);_mercenaryBySlot[slot.RestSlotId]=mercenaryId;Revision++;Changed?.Invoke();reason=string.Empty;return true;}
    public bool TryGetByMercenary(string id,out RestAssignmentV3? value)=>_byMercenary.TryGetValue(id,out value);public bool TryGetMercenaryBySlot(string slotId,out string mercenaryId)=>_mercenaryBySlot.TryGetValue(slotId,out mercenaryId!);public bool IsSlotAssigned(string id)=>_mercenaryBySlot.ContainsKey(id);public bool TryUnassign(string id){if(!_byMercenary.Remove(id,out var old))return false;_mercenaryBySlot.Remove(old.RestSlotId);Revision++;Changed?.Invoke();return true;}public int RemoveByStructure(string structureId){var ids=_byMercenary.Values.Where(x=>x.StructureId==structureId).Select(x=>x.MercenaryId).ToList();foreach(var id in ids)TryUnassign(id);return ids.Count;}public void Clear(){if(_byMercenary.Count==0)return;_byMercenary.Clear();_mercenaryBySlot.Clear();Revision++;Changed?.Invoke();}
}
public sealed record RestReservationV3(string RestSlotId,string MercenaryId,string RestWorkId,DateTime CreatedUtc);
public sealed class RestReservationRegistryV3
{
    private readonly Dictionary<string,RestReservationV3> _bySlot=new(StringComparer.Ordinal);public int Count=>_bySlot.Count;public event Action? Changed;public bool IsReserved(string slotId)=>_bySlot.ContainsKey(slotId);public bool TryReserve(RestAssignmentV3 assignment,string workId,out string reason){if(_bySlot.TryGetValue(assignment.RestSlotId,out var old)){if(old.MercenaryId==assignment.MercenaryId&&old.RestWorkId==workId){reason=string.Empty;return true;}reason="RestSlotReserved";return false;}_bySlot.Add(assignment.RestSlotId,new(assignment.RestSlotId,assignment.MercenaryId,workId,DateTime.UtcNow));Changed?.Invoke();reason=string.Empty;return true;}public bool TryGet(string slot,out RestReservationV3? value)=>_bySlot.TryGetValue(slot,out value);public int ReleaseByMercenary(string id){var slots=_bySlot.Where(x=>x.Value.MercenaryId==id).Select(x=>x.Key).ToList();foreach(var slot in slots)_bySlot.Remove(slot);if(slots.Count>0)Changed?.Invoke();return slots.Count;}public int ReleaseByStructure(IEnumerable<string> slots){int count=0;foreach(string slot in slots)if(_bySlot.Remove(slot))count++;if(count>0)Changed?.Invoke();return count;}public void Clear(){if(_bySlot.Count==0)return;_bySlot.Clear();Changed?.Invoke();}
}
public enum RestWorkPhaseV3{MovingToRestSlot,Resting,Completed,Failed,Cancelled}
public sealed class RestWorkStateV3
{
    public RestWorkStateV3(string id,string mercenaryId,RestFacilitySlotV3 slot,bool automatic){RestWorkId=id;MercenaryId=mercenaryId;Slot=slot;IsAutomatic=automatic;CreatedUtc=DateTime.UtcNow;}public string RestWorkId{get;}public string MercenaryId{get;}public RestFacilitySlotV3 Slot{get;}public bool IsAutomatic{get;}public DateTime CreatedUtc{get;}public RestWorkPhaseV3 Phase{get;internal set;}=RestWorkPhaseV3.MovingToRestSlot;public string FailureReason{get;internal set;}=string.Empty;
}
public sealed class MercenaryNeedsDiagnosticsV3{public int ManualRestCount{get;internal set;}public int AutoRestCount{get;internal set;}public int CompletedRestCount{get;internal set;}public int CancelledRestCount{get;internal set;}public int BlockedWorkCount{get;internal set;}public string LastFailureReason{get;internal set;}=string.Empty;}
public sealed class MercenaryNeedsSessionV3
{
    private readonly Dictionary<string,RestWorkStateV3> _activeRestByMercenary=new(StringComparer.Ordinal);private readonly HashSet<string> _structuresUnderDemolition=new(StringComparer.Ordinal);private long _nextWork;
    public MercenaryNeedsSessionV3(long revision){SessionRevision=revision;Hunger=new(HungerConfig);}public long SessionRevision{get;}public MercenaryFatigueSettingsV3 Settings{get;}=new();public HungerConfigV3 HungerConfig{get;}=new();public MercenaryFatigueRegistryV3 Fatigue{get;}=new();public MercenaryHungerRegistryV3 Hunger{get;}public RestAssignmentRegistryV3 Assignments{get;}=new();public RestReservationRegistryV3 Reservations{get;}=new();public MercenaryNeedsDiagnosticsV3 Diagnostics{get;}=new();public int ActiveRestCount=>_activeRestByMercenary.Count;public long HungerTickCount{get;private set;}public float LastHungerDelta{get;private set;}
    public void EnsureMercenaries(MercenaryRegistryV3 registry){foreach(string id in registry.GetAllMercenaryIds()){Fatigue.GetOrCreate(id);Hunger.EnsureForMercenary(id);}}public float MovementMultiplier(string id)=>MercenaryFatiguePolicyV3.MovementMultiplier(Fatigue.GetValue(id));public float FatigueWorkMultiplier(string id)=>MercenaryFatiguePolicyV3.WorkMultiplier(Fatigue.GetValue(id));public float HungerWorkMultiplier(string id)=>HungerPolicyV3.WorkMultiplier(Hunger.GetHunger(id));public float WorkMultiplier(string id)=>FatigueWorkMultiplier(id)*HungerWorkMultiplier(id);public bool CanStartWork(string id,WorkTypeV3 type,out string reason){if(type is WorkTypeV3.Rest or WorkTypeV3.Eating){reason=string.Empty;return true;}if(Fatigue.GetValue(id)>=Settings.WorkBlockThreshold){Diagnostics.BlockedWorkCount++;reason="TooExhausted";return false;}reason=string.Empty;return true;}public void TickHunger(string id,float seconds){LastHungerDelta=HungerConfig.HungerIncreasePerSecond*Math.Max(0,seconds);Hunger.EnsureForMercenary(id).TryAddHunger(LastHungerDelta,out _);HungerTickCount++;}
    public bool CanBeginRest(string mercenaryId,RestFacilitySlotV3 slot,bool automatic,out string reason){if(_structuresUnderDemolition.Contains(slot.StructureId)){reason="StructureUnderDemolition";return false;}float value=Fatigue.GetValue(mercenaryId);if(!automatic&&value<=Settings.ManualRestMinimum){reason="NotTiredEnough";return false;}if(automatic&&value<Settings.AutoRestThreshold){reason="AutoRestThresholdNotReached";return false;}if(!Assignments.TryGetByMercenary(mercenaryId,out var assignment)||assignment==null||assignment.RestSlotId!=slot.RestSlotId){reason="NoValidBedAssignment";return false;}reason=string.Empty;return true;}
    public bool TryBeginRest(string mercenaryId,RestFacilitySlotV3 slot,bool automatic,out RestWorkStateV3? work,out string reason){work=null;if(!CanBeginRest(mercenaryId,slot,automatic,out reason))return false;Assignments.TryGetByMercenary(mercenaryId,out var assignment);string id=$"rest_{SessionRevision}_{++_nextWork}";CancelRest(mercenaryId,"Superseded");if(!Reservations.TryReserve(assignment!,id,out reason))return false;work=new(id,mercenaryId,slot,automatic);_activeRestByMercenary[mercenaryId]=work;if(automatic)Diagnostics.AutoRestCount++;else Diagnostics.ManualRestCount++;reason=string.Empty;return true;}
    public bool TryGetActiveRest(string id,out RestWorkStateV3? work)=>_activeRestByMercenary.TryGetValue(id,out work);public bool MarkAtSlot(string id,GlobalCellCoord cell){if(!_activeRestByMercenary.TryGetValue(id,out var work)||work.Slot.UseCell.Value!=cell.Value)return false;work.Phase=RestWorkPhaseV3.Resting;return true;}
    public void Tick(string id,FatigueActivityV3 activity,float seconds,float recoveryMultiplier=1f){Fatigue.Apply(id,activity,seconds,Settings,recoveryMultiplier);if(activity==FatigueActivityV3.Resting&&Fatigue.GetValue(id)<=Settings.RestCompletionThreshold)CompleteRest(id);}
    public bool CompleteRest(string id){if(!_activeRestByMercenary.Remove(id,out var work))return false;work.Phase=RestWorkPhaseV3.Completed;Reservations.ReleaseByMercenary(id);Diagnostics.CompletedRestCount++;return true;}public bool CancelRest(string id,string reason){if(!_activeRestByMercenary.Remove(id,out var work))return false;work.Phase=RestWorkPhaseV3.Cancelled;work.FailureReason=reason;Reservations.ReleaseByMercenary(id);Diagnostics.CancelledRestCount++;Diagnostics.LastFailureReason=reason;return true;}
    public int RemoveStructure(string structureId,StructureDefinitionV3 definition,StructureStateV3 structure){_structuresUnderDemolition.Remove(structureId);var slots=RestFacilitySlotResolverV3.Resolve(structure,definition);foreach(var work in _activeRestByMercenary.Values.Where(x=>x.Slot.StructureId==structureId).ToList())CancelRest(work.MercenaryId,"RestFacilityRemoved");Reservations.ReleaseByStructure(slots.Select(x=>x.RestSlotId));return Assignments.RemoveByStructure(structureId);}
    public IReadOnlyList<string> BeginStructureDemolition(string structureId){_structuresUnderDemolition.Add(structureId);List<string> affected=_activeRestByMercenary.Values.Where(x=>x.Slot.StructureId==structureId).Select(x=>x.MercenaryId).ToList();foreach(string id in affected)CancelRest(id,"StructureUnderDemolition");return affected.AsReadOnly();}public void EndStructureDemolition(string structureId)=>_structuresUnderDemolition.Remove(structureId);public bool IsStructureUnderDemolition(string structureId)=>_structuresUnderDemolition.Contains(structureId);
}

public static class MercenaryNeedsSelfCheckV3
{
    public static bool TryValidate(out string reason)
    {
        MercenaryNeedsSessionV3 session=new(1);string id=MercenaryIdFactoryV3.CreateMercenaryId();MercenaryFatigueStateV3 fatigue=session.Fatigue.GetOrCreate(id);
        if(Math.Abs(fatigue.Fatigue-.15f)>.0001f||MercenaryFatiguePolicyV3.MovementMultiplier(.1f)!=1f||MercenaryFatiguePolicyV3.MovementMultiplier(.7f)!=.9f||MercenaryFatiguePolicyV3.MovementMultiplier(.95f)!=.8f||MercenaryFatiguePolicyV3.WorkMultiplier(.1f)!=1f||MercenaryFatiguePolicyV3.WorkMultiplier(.4f)!=.95f||MercenaryFatiguePolicyV3.WorkMultiplier(.7f)!=.8f||MercenaryFatiguePolicyV3.WorkMultiplier(.95f)!=.6f){reason="Fatigue defaults or modifier bands are invalid.";return false;}
        fatigue.Set(.949f);foreach(WorkTypeV3 type in new[]{WorkTypeV3.Gathering,WorkTypeV3.Hauling,WorkTypeV3.Construction,WorkTypeV3.Demolition})if(!session.CanStartWork(id,type,out reason))return false;
        fatigue.Set(.95f);foreach(WorkTypeV3 type in new[]{WorkTypeV3.Gathering,WorkTypeV3.Hauling,WorkTypeV3.Construction,WorkTypeV3.Demolition})if(session.CanStartWork(id,type,out reason)||reason!="TooExhausted"){reason=$"{type} exhaustion guard failed.";return false;}if(!session.CanStartWork(id,WorkTypeV3.Rest,out reason)){reason="Rest was blocked by exhaustion.";return false;}
        RestFacilitySlotV3 slot=new("restslot_test_0","test",0,new GlobalCellCoord(new Godot.Vector2I(2,3)),1f,"낮음");if(!session.Assignments.TryAssign(id,slot,out reason)){return false;}fatigue.Set(.7f);if(!session.TryBeginRest(id,slot,false,out _,out reason)||session.Reservations.Count!=1)return false;IReadOnlyList<string> affected=session.BeginStructureDemolition(slot.StructureId);if(affected.Count!=1||session.ActiveRestCount!=0||session.Reservations.Count!=0||session.Assignments.Count!=1){reason="Demolition did not cancel rest while preserving assignment.";return false;}if(session.TryBeginRest(id,slot,false,out _,out reason)||reason!="StructureUnderDemolition"){reason="Rest was allowed during demolition.";return false;}session.EndStructureDemolition(slot.StructureId);if(!session.TryBeginRest(id,slot,false,out _,out reason)){reason="Rest did not recover after demolition cancellation.";return false;}session.MarkAtSlot(id,slot.UseCell);session.Tick(id,FatigueActivityV3.Resting,40f);if(session.Fatigue.GetValue(id)>.151f||session.Reservations.Count!=0||session.Assignments.Count!=1){reason="Rest completion invariants failed.";return false;}MercenaryNeedsSessionV3 replacement=new(2);if(replacement.Assignments.Count!=0||replacement.Reservations.Count!=0||replacement.ActiveRestCount!=0){reason="New-session needs state was not clean.";return false;}reason=string.Empty;return true;
    }
}
