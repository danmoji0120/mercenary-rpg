using System;
using GameplayV3.Resources;
using GameplayV3.Time;

namespace GameplayV3.Objectives;

public static class FrontierSurvivalSelfCheckV3
{
    public static string LastSummary{get;private set;}=string.Empty;
    public static bool TryValidate(out string reason)
    {
        SimulationClockSessionV3 clock=new(1);FrontierSurvivalSessionV3 objective=new(1,"company_local",clock);int completedEvents=0;objective.FrontierSurvivalCompleted+=_=>completedEvents++;
        try
        {
            FrontierSurvivalSnapshotV3 initial=objective.GetSnapshot();if(initial.TotalMilestoneCount!=8||initial.CompletedMilestoneCount!=0||initial.SurvivedHours!=0||initial.IsCompleted)return Fail("Initial objective state failed.",out reason);
            objective.RecordGathered("company_other",ResourceTypeV3.Wood,100,"Gathering");objective.RecordGathered("company_local",ResourceTypeV3.Wood,20,"Salvage");objective.RecordGathered("company_local",ResourceTypeV3.Wood,30,"Gathering");objective.RecordGathered("company_local",ResourceTypeV3.Stone,14,"Gathering");if(!Met(objective,FrontierSurvivalMilestoneIdV3.GatherWood,true,30)||!Met(objective,FrontierSurvivalMilestoneIdV3.GatherStone,false,14))return Fail("Gathering source policy failed.",out reason);objective.RecordGathered("company_local",ResourceTypeV3.Stone,1,"Gathering");
            objective.UpdateStockpile(0);objective.UpdateStockpile(1);objective.UpdateStockpile(0);if(!LatchedButCurrentlyMissing(objective,FrontierSurvivalMilestoneIdV3.CreateStockpile))return Fail("Stockpile latch failed.",out reason);
            objective.UpdateBeds(2);if(Met(objective,FrontierSurvivalMilestoneIdV3.BuildBeds,true,2))return Fail("Bed completed early.",out reason);objective.UpdateBeds(3);objective.UpdateBeds(2);if(!LatchedButCurrentlyMissing(objective,FrontierSurvivalMilestoneIdV3.BuildBeds))return Fail("Bed latch failed.",out reason);objective.UpdateBeds(3);
            objective.UpdateFarmCells(8);objective.UpdateFarmCells(9);objective.UpdateRooms(1);objective.UpdateHeadquarters(true);
            clock.SetPaused(true);clock.Advance(72*60);objective.UpdateSurvival();if(objective.GetSurvivedHours()!=0)return Fail("Paused survival advanced.",out reason);clock.SetPaused(false);clock.Advance(71*60);objective.UpdateSurvival();if(Met(objective,FrontierSurvivalMilestoneIdV3.SurviveThreeDays,true,71))return Fail("Survival completed before 72 hours.",out reason);clock.Advance(60);objective.UpdateSurvival();if(!objective.IsCompleted||completedEvents!=1||objective.GetCompletedCount()!=8)return Fail("Full completion failed.",out reason);objective.UpdateRooms(0);objective.UpdateHeadquarters(false);if(!objective.IsCompleted||completedEvents!=1||!LatchedButCurrentlyMissing(objective,FrontierSurvivalMilestoneIdV3.CreateRoom))return Fail("Completion was revoked or duplicated.",out reason);
            if(objective.GetRecentEvents().Count>16||objective.Diagnostics.ObjectiveFullWorldScanCount!=0||objective.ClockSubscriptionCount!=1)return Fail("Bounded objective diagnostics failed.",out reason);
            reason=string.Empty;LastSummary="8 milestones; gathering source filter; current/completed latch; paused/72h; completion once=PASS";return true;
        }
        finally{objective.Dispose();clock.Dispose();}
    }
    private static bool Met(FrontierSurvivalSessionV3 objective,FrontierSurvivalMilestoneIdV3 id,bool completed,int current)=>objective.TryGetMilestone(id,out var value)&&value!=null&&value.CompletedOnce==completed&&value.CurrentValue==current;
    private static bool LatchedButCurrentlyMissing(FrontierSurvivalSessionV3 objective,FrontierSurvivalMilestoneIdV3 id)=>objective.TryGetMilestone(id,out var value)&&value!=null&&value.CompletedOnce&&!value.CurrentConditionMet;
    private static bool Fail(string value,out string reason){reason=value;return false;}
}
