using System;
using GameplayV3.Company;
using GameplayV3.Mercenary;
using Godot;
using WorldV2;

namespace GameplayV3.Time;

public static class MercenaryScheduleSelfCheckV3
{
    public static string LastSummary{get;private set;}=string.Empty;
    public static bool TryValidate(out string reason)
    {
        if(!Create(4,out SimulationClockSessionV3 clock,out MercenarySessionV3 mercenaries,out MercenaryScheduleSessionV3 schedules,out string[] ids,out reason))return false;
        try
        {
            if(!schedules.TryGetSchedule(ids[0],out var standard)||standard==null||standard.Preset!=MercenarySchedulePresetV3.Standard||standard.CurrentState!=MercenaryScheduleStateV3.Work||standard.NextTransitionHour!=18)return Fail("Standard 08:00 state failed.",out reason);
            long stableStateRevision=standard.CurrentStateRevision;clock.Advance(60);schedules.TryGetSchedule(ids[0],out standard);if(standard!.CurrentStateRevision!=stableStateRevision)return Fail("Same-state hour changed revision.",out reason);
            clock.Advance(9*60);if(!schedules.TryGetCurrentState(ids[0],out var state)||state!=MercenaryScheduleStateV3.Anything)return Fail("18:00 transition failed.",out reason);
            clock.Advance(4*60);if(!schedules.TryGetCurrentState(ids[0],out state)||state!=MercenaryScheduleStateV3.Sleep)return Fail("22:00 transition failed.",out reason);
            if(!schedules.TryApplyPreset(ids[1],MercenarySchedulePresetV3.NightShift,out reason)||!schedules.TryGetSlot(ids[1],8,out state)||state!=MercenaryScheduleStateV3.Sleep||!schedules.TryGetSlot(ids[1],20,out state)||state!=MercenaryScheduleStateV3.Work)return false;
            if(!schedules.TryApplyPreset(ids[2],MercenarySchedulePresetV3.DayShift,out reason)||!schedules.TryGetSlot(ids[2],7,out state)||state!=MercenaryScheduleStateV3.Work)return false;
            if(!schedules.TryApplyPreset(ids[3],MercenarySchedulePresetV3.Free,out reason)||!schedules.IsAutomaticJobEligible(ids[3]))return Fail("Free preset failed.",out reason);
            long before=standard.Revision;if(!schedules.TrySetHourSlot(ids[0],clock.Hour,MercenaryScheduleStateV3.Recreation,out reason)||schedules.IsAutomaticJobEligible(ids[0])||!schedules.GetCurrentPolicy(ids[0]).RecreationIntent)return Fail("Current slot edit did not apply immediately.",out reason);schedules.TryGetSchedule(ids[0],out standard);long edited=standard!.Revision;if(!schedules.TrySetHourSlot(ids[0],clock.Hour,MercenaryScheduleStateV3.Recreation,out reason)||!schedules.TryGetSchedule(ids[0],out standard)||standard!.Revision!=edited)return Fail("Idempotent slot edit changed revision.",out reason);
            if(schedules.TrySetHourRange(ids[0],5,5,MercenaryScheduleStateV3.Sleep,out reason)||reason!="EmptyHourRange")return Fail("Empty range policy failed.",out reason);
            if(!schedules.TrySetHourRange(ids[0],22,6,MercenaryScheduleStateV3.Sleep,out reason))return false;for(int h=0;h<24;h++){schedules.TryGetSlot(ids[0],h,out state);bool expected=h>=22||h<6;if((state==MercenaryScheduleStateV3.Sleep)!=expected&&h!=clock.Hour)return Fail("Midnight range failed.",out reason);}
            MercenarySchedulePolicyV3 sleep=schedules.GetCurrentPolicy(ids[0]);if(sleep.CurrentState!=MercenaryScheduleStateV3.Sleep||sleep.AutomaticJobEligible||!sleep.NeedsEligible||!sleep.WantsScheduledRest||!sleep.DirectOrderBypassAllowed)return Fail("Sleep policy failed.",out reason);
            if(!mercenaries.Registry.TryRemoveMercenary(ids[3],out reason)||schedules.TryGetSchedule(ids[3],out _)||schedules.TransitionIndexEntryCount<0)return Fail("Removal cleanup failed.",out reason);
            if(schedules.GetRecentEvents().Count>16||schedules.Diagnostics.FullMercenaryScanCount!=0||schedules.ClockSubscriptionCount!=1||schedules.RegistrySubscriptionCount!=1)return Fail("Bounded diagnostics failed.",out reason);
        }
        finally{schedules.Dispose();clock.Dispose();}
        if(!RunIndexFixture(out reason))return false;
        LastSummary="standard/presets/edit/midnight/policy/removal=PASS; 300 mercenaries/24h/200 edits=PASS; fullScan=0";reason=string.Empty;return true;
    }

    private static bool RunIndexFixture(out string reason)
    {
        if(!Create(300,out SimulationClockSessionV3 clock,out _,out MercenaryScheduleSessionV3 schedules,out string[] ids,out reason))return false;
        try
        {
            MercenarySchedulePresetV3[] presets={MercenarySchedulePresetV3.Standard,MercenarySchedulePresetV3.DayShift,MercenarySchedulePresetV3.NightShift,MercenarySchedulePresetV3.Free};for(int i=0;i<ids.Length;i++)if(!schedules.TryApplyPreset(ids[i],presets[i%4],out reason))return false;
            for(int i=0;i<100;i++)if(!schedules.TrySetHourSlot(ids[i],i%24,i%2==0?MercenaryScheduleStateV3.Recreation:MercenaryScheduleStateV3.Anything,out reason))return false;
            for(int i=0;i<100;i++)if(!schedules.TryApplyPreset(ids[200+i],presets[(i+1)%4],out reason))return false;
            for(int hour=0;hour<24;hour++)clock.Advance(60);if(schedules.Count!=300||schedules.Diagnostics.FullMercenaryScanCount!=0||schedules.GetRecentEvents().Count>16)return Fail("300 mercenary transition fixture failed.",out reason);
            reason=string.Empty;return true;
        }
        finally{schedules.Dispose();clock.Dispose();}
    }

    private static bool Create(int count,out SimulationClockSessionV3 clock,out MercenarySessionV3 mercenaries,out MercenaryScheduleSessionV3 schedules,out string[] ids,out string reason)
    {
        CompanySessionV3 companies=new();if(!companies.TryInitializeLocalSinglePlayer(out _,out reason)){clock=null!;mercenaries=null!;schedules=null!;ids=Array.Empty<string>();return false;}mercenaries=new(companies.CompanyRegistry);clock=new(1);schedules=new(1,clock,mercenaries.Registry);ids=new string[count];MercenaryAttributeSetV3.TryCreate(10,10,10,10,10,out var attributes,out _);MercenaryWorkSkillSetV3.TryCreate(8,8,8,8,8,8,8,out var skills,out _);string company=companies.LocalContext.LocalCompanyId!;
        for(int i=0;i<count;i++){string id=MercenaryIdFactoryV3.CreateMercenaryId();DateTime created=DateTime.UtcNow;MercenaryProfileV3.TryCreate(id,$"Schedule {i}","placeholder",attributes,skills,created,out var profile,out _);MercenaryStateV3.TryCreate(id,company,new GlobalCellCoord(new Vector2I(i,0)),MercenaryActivityStateV3.Idle,created,out var state,out _);if(!mercenaries.Registry.TryRegisterMercenary(profile,state,out reason)){schedules.Dispose();clock.Dispose();return false;}ids[i]=id;}reason=string.Empty;return true;
    }
    private static bool Fail(string value,out string reason){reason=value;return false;}
}
