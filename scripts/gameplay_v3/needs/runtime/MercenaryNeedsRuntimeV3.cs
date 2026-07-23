using System;
using GameplayV3.Mercenary;
using GameplayV3.Session;
using GameplayV3.Time;
using Godot;

namespace GameplayV3.Needs.Runtime;

public partial class MercenaryNeedsRuntimeV3:Node
{
    private MercenaryNeedsSessionV3? _needs;private MercenarySessionV3? _mercenaries;private MercenaryScheduleSessionV3? _schedules;private RestWorkCoordinatorV3? _rest;private EatingWorkCoordinatorV3? _eating;private double _accumulator;public int TickCount{get;private set;}public double PendingTickCredit=>_accumulator;
    public void Initialize(MercenaryNeedsSessionV3 needs,MercenarySessionV3 mercenaries){_needs=needs;_mercenaries=mercenaries;GameplaySessionV3.TryGetMercenarySchedule(out _schedules);SetPhysicsProcess(false);needs.EnsureMercenaries(mercenaries.Registry);}
    public void AttachRestCoordinator(RestWorkCoordinatorV3 coordinator)=>_rest=coordinator;
    public void AttachEatingCoordinator(EatingWorkCoordinatorV3 coordinator)=>_eating=coordinator;
    public void AdvanceSimulation(double delta){if(_needs==null||_mercenaries==null||!GameplaySessionV3.TryGetNeedsSession(out var current)||!ReferenceEquals(current,_needs)||delta<=0)return;_accumulator+=delta;int catchup=0;while(_accumulator>=_needs.Settings.TickIntervalSeconds&&catchup++<4){_accumulator-=_needs.Settings.TickIntervalSeconds;TickNeeds(_needs.Settings.TickIntervalSeconds);TickCount++;}}
    private void TickNeeds(float seconds){foreach(string id in _mercenaries!.Registry.GetAllMercenaryIds()){if(!_mercenaries.Registry.TryGetState(id,out var state)||state==null)continue;_needs!.TickHunger(id,seconds);if(_needs.TryGetActiveRest(id,out var rest)&&rest!=null){if(rest.Phase==RestWorkPhaseV3.MovingToRestSlot&&state.CurrentCell.Value==rest.Slot.UseCell.Value){_needs.MarkAtSlot(id,state.CurrentCell);state.TrySetActivityState(MercenaryActivityStateV3.Resting,out _);}if(rest.Phase==RestWorkPhaseV3.Resting){_needs.Tick(id,FatigueActivityV3.Resting,seconds,rest.Slot.RecoveryMultiplier);if(!_needs.TryGetActiveRest(id,out _))state.TrySetActivityState(MercenaryActivityStateV3.Idle,out _);continue;}}FatigueActivityV3 activity=state.ActivityState switch{MercenaryActivityStateV3.Moving=>FatigueActivityV3.Moving,MercenaryActivityStateV3.Working=>FatigueActivityV3.Gathering,_=>FatigueActivityV3.Idle};_needs.Tick(id,activity,seconds);if(state.ActivityState==MercenaryActivityStateV3.Idle)TryStartNeedAction(id);}}
    private void TryStartNeedAction(string id){foreach(NeedActionKindV3 action in NeedActionPriorityV3.GetCandidates(_needs!.Hunger.GetHunger(id),_needs.Fatigue.GetValue(id),_needs.HungerConfig,_needs.Settings)){if(action==NeedActionKindV3.Eat&&_eating?.TryIssueAuto(id,out _)==true)return;if(action==NeedActionKindV3.Rest&&_rest?.TryAutoRest(id)==true)return;}if(_schedules?.WantsScheduledRest(id)==true)_rest?.TryScheduledRest(id);}
}
