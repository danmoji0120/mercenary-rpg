using System;
using System.Collections.Generic;
using System.Linq;
using GameplayV3.Time.UI;

namespace GameplayV3.Time;

public static class SimulationClockSelfCheckV3
{
    public static string LastSummary { get; private set; }=string.Empty;

    public static bool TryValidate(out string reason)
    {
        if(!SimulationClockHudV3.TryValidateLayout(new Godot.Vector2(1280,720),out reason)
            || !SimulationClockHudV3.TryValidateLayout(new Godot.Vector2(1920,1080),out reason))return false;
        SimulationClockSessionV3 initial=new(1);SimulationClockSnapshotV3 s=initial.GetSnapshot();
        if(s.DayIndex!=1||s.Hour!=8||s.Minute!=0||s.DayPhase!=DayPhaseV3.Day||s.TimeScale!=1||s.IsPaused)return Fail("Initial state",out reason);
        int hours=0;initial.HourChanged+=_=>hours++;initial.Advance(60);
        if(initial.Hour!=9||initial.Minute!=0||hours!=1)return Fail("One-hour advance",out reason);

        SimulationClockSessionV3 scale2=new(1);scale2.TrySetTimeScale(2,out _);scale2.Advance(30);
        SimulationClockSessionV3 scale3=new(1);scale3.TrySetTimeScale(3,out _);scale3.Advance(20);
        if(scale2.Hour!=9||scale3.Hour!=9||scale3.TrySetTimeScale(0,out _)||scale3.TrySetTimeScale(4,out _)||scale3.TrySetTimeScale(-1,out _))return Fail("Time scales",out reason);

        SimulationClockSessionV3 paused=new(1);long revision=paused.Revision;paused.SetPaused(true);long pausedRevision=paused.Revision;paused.SetPaused(true);paused.Advance(60);
        if(paused.Hour!=8||paused.Revision!=pausedRevision||pausedRevision==revision||!paused.SetPaused(false)||paused.Hour!=8)return Fail("Pause/idempotency",out reason);
        paused.Advance(60);if(paused.Hour!=9)return Fail("Resume",out reason);

        if(!PhaseBoundary(4,59,DayPhaseV3.Night,DayPhaseV3.Dawn)||!PhaseBoundary(6,59,DayPhaseV3.Dawn,DayPhaseV3.Day)||!PhaseBoundary(17,59,DayPhaseV3.Day,DayPhaseV3.Evening)||!PhaseBoundary(20,59,DayPhaseV3.Evening,DayPhaseV3.Night))return Fail("Day phases",out reason);

        SimulationClockSessionV3 midnight=new(1,new(StartHour:23,StartMinute:59));List<SimulationClockEventTypeV3> order=new();
        midnight.DayEnded+=e=>order.Add(e.EventType);midnight.HourChanged+=e=>order.Add(e.EventType);midnight.DayStarted+=e=>order.Add(e.EventType);midnight.Advance(1);
        if(midnight.DayIndex!=2||midnight.Hour!=0||midnight.Minute!=0||!order.SequenceEqual(new[]{SimulationClockEventTypeV3.DayEnded,SimulationClockEventTypeV3.HourChanged,SimulationClockEventTypeV3.DayStarted}))return Fail("Day boundary order",out reason);

        SimulationClockSessionV3 large=new(1);int largeHours=0,largeDays=0,largePhases=0;large.HourChanged+=_=>largeHours++;large.DayStarted+=_=>largeDays++;large.DayPhaseChanged+=_=>largePhases++;large.Advance(3000);
        if(largeHours!=50||largeDays!=2||largePhases!=8||large.DayIndex!=3||large.Hour!=10)return Fail("Large bounded advance",out reason);

        SimulationClockSessionV3 same=new(1);same.TrySetTimeScale(1,out _);same.SetPaused(false);if(same.Revision!=0||same.GetRecentEvents().Count!=0)return Fail("Same state idempotency",out reason);
        SimulationClockSnapshotV3 before=same.GetSnapshot();same.Advance(-1);same.Advance(double.NaN);same.Advance(double.PositiveInfinity);
        if(same.ElapsedSimulationSeconds!=before.ElapsedSimulationSeconds||same.Diagnostics.InvalidDeltaCount!=3)return Fail("Invalid delta",out reason);

        SimulationClockSessionV3 frameGuard=new(1);if(!frameGuard.AdvanceForFrame(1,7)||frameGuard.AdvanceForFrame(1,7)||frameGuard.ElapsedSimulationSeconds!=60||frameGuard.Diagnostics.ClockDuplicateAdvanceFrameCount!=1)return Fail("Duplicate frame guard",out reason);

        SimulationClockSessionV3 deltaClock=new(1);SimulationClockAdvanceResultV3 one=deltaClock.AdvanceFrame(1,1);deltaClock.TrySetTimeScale(2,out _);SimulationClockAdvanceResultV3 two=deltaClock.AdvanceFrame(1,2);deltaClock.TrySetTimeScale(3,out _);SimulationClockAdvanceResultV3 three=deltaClock.AdvanceFrame(1,3);deltaClock.SetPaused(true);SimulationClockAdvanceResultV3 stopped=deltaClock.AdvanceFrame(1,4);
        if(one.ScaledGameplayDeltaSeconds!=1||one.WorldSecondsAdvanced!=60||two.ScaledGameplayDeltaSeconds!=2||two.WorldSecondsAdvanced!=120||three.ScaledGameplayDeltaSeconds!=3||three.WorldSecondsAdvanced!=180||stopped.ScaledGameplayDeltaSeconds!=0||stopped.WorldSecondsAdvanced!=0||!stopped.WasPaused)return Fail("Advance result",out reason);

        LastSummary=$"initial=1d08:00 scales=1/2/3 pause=PASS hour/day/phase={largeHours}/{largeDays}/{largePhases} recent<={large.GetRecentEvents().Count}";
        reason=string.Empty;return true;
    }

    private static bool PhaseBoundary(int hour,int minute,DayPhaseV3 before,DayPhaseV3 after)
    {
        SimulationClockSessionV3 clock=new(1,new(StartHour:hour,StartMinute:minute));int phases=0;clock.DayPhaseChanged+=_=>phases++;clock.Advance(1);
        return SimulationClockSessionV3.GetDayPhase(hour,minute)==before&&clock.DayPhase==after&&phases==1;
    }

    private static bool Fail(string value,out string reason){reason=value;LastSummary=value;return false;}
}
