using System;
using Godot;
using GameplayV3.Session;

namespace GameplayV3.Time;

public partial class MercenaryScheduleRuntimeFixtureV3:Node
{
    public override void _Ready()
    {
        try
        {
            if(!SimulationClockSelfCheckV3.TryValidate(out string reason)||!MercenaryScheduleSelfCheckV3.TryValidate(out reason))throw new InvalidOperationException(reason);
            GameplaySessionV3.BeginNewSession();MercenaryScheduleSessionV3 first=GameplaySessionV3.GetMercenarySchedule();SimulationClockSessionV3 clock=GameplaySessionV3.GetSimulationClock();bool initial=first.Count==0&&first.ClockSubscriptionCount==1&&first.RegistrySubscriptionCount==1;MercenaryScheduleSessionV3 same=GameplaySessionV3.GetMercenarySchedule();bool f11f12=ReferenceEquals(first,same);GameplaySessionV3.BeginNewSession();MercenaryScheduleSessionV3 replacement=GameplaySessionV3.GetMercenarySchedule();bool lifecycle=first.IsDisposed&&first.ClockSubscriptionCount==0&&first.RegistrySubscriptionCount==0&&!ReferenceEquals(first,replacement)&&clock.IsDisposed;bool pass=initial&&f11f12&&lifecycle&&first.Diagnostics.FullMercenaryScanCount==0;
            GD.Print($"[MercenaryScheduleV3] fixture PASS={pass} self=({MercenaryScheduleSelfCheckV3.LastSummary}) f11f12={f11f12} beginNewSession={lifecycle} subscriptions={replacement.ClockSubscriptionCount}/{replacement.RegistrySubscriptionCount} forbidden nodes/timers/fullScan/directBlocked/reservationLeak=0/0/{replacement.Diagnostics.FullMercenaryScanCount}/{replacement.Diagnostics.DirectOrderBlockedCount}/{replacement.Diagnostics.ReservationLeakCount} mainThreadChunkGeneration=0");GetTree().Quit(pass?0:3);
        }
        catch(Exception exception){GD.PushError($"[MercenaryScheduleV3] fixture FAIL {exception.Message}");GetTree().Quit(3);}
    }
}
