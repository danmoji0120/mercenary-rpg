using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GameplayV3.Session;
using GameplayV3.Time.Runtime;
using GameplayV3.Time.UI;

namespace GameplayV3.Time;

public partial class SimulationClockRuntimeFixtureV3 : Node
{
    public override void _Ready()=>_ = Run();

    private async Task Run()
    {
        try
        {
            if(!SimulationClockSelfCheckV3.TryValidate(out string reason))throw new InvalidOperationException(reason);
            if(!DayNightVisualSelfCheckV3.TryValidate(out reason))throw new InvalidOperationException(reason);
            if(!SimulationDeltaIntegrationSelfCheckV3.TryValidate(out reason))throw new InvalidOperationException(reason);
            GameplaySessionV3.BeginNewSession();SimulationClockSessionV3 first=GameplaySessionV3.GetSimulationClock();
            CanvasLayer canvas=new(){Name="CanvasLayer"};AddChild(canvas);SimulationClockHudV3 hud=new();canvas.AddChild(hud);
            CanvasModulate dayNightCanvas=new(){Name="DayNightCanvasModulate",Color=Colors.White};AddChild(dayNightCanvas);DayNightVisualRuntimeV3 dayNight=new();dayNight.Bind(first,dayNightCanvas);
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);hud.Bind(first);hud.SetGameplayReady(true);
            SimulationClockSnapshotV3 initial=first.GetSnapshot();
            bool initialTint=SameTint(dayNight.CurrentTint,DayNightVisualCoreV3.EvaluateTint(initial.Hour,initial.Minute,initial.FractionalMinute));
            bool fixtureTimes=new[]{270.0,330.0,480.0,720.0,1110.0,1320.0,0.0}.All(minute=>InRange(DayNightVisualCoreV3.EvaluateTint(minute)));
            first.Advance(1);double afterOne=first.ElapsedSimulationSeconds;
            dayNight.AdvanceDisplay(.125);first.SetPaused(true);Color pausedTint=dayNight.CurrentTint;first.Advance(10);dayNight.AdvanceDisplay(1);bool paused=first.ElapsedSimulationSeconds==afterOne&&SameTint(pausedTint,dayNight.CurrentTint)&&dayNight.SkippedUnchangedTintCount>0;
            first.SetPaused(false);first.TrySetTimeScale(2,out _);first.Advance(.5);double afterTwo=first.ElapsedSimulationSeconds;
            first.TrySetTimeScale(3,out _);first.Advance(1.0/3.0);bool scales=Math.Abs(first.ElapsedSimulationSeconds-afterTwo-60)<1e-7;
            first.Advance(320);bool dateAndPhase=first.DayIndex==2&&first.DayPhase==DayPhaseV3.Night;
            long preservedRevision=first.Revision;hud.Bind(first);hud.Bind(first);
            bool f11f12=ReferenceEquals(first,GameplaySessionV3.GetSimulationClock())&&first.Revision==preservedRevision&&canvas.GetChildren().OfType<SimulationClockHudV3>().Count()==1&&GetChildren().OfType<CanvasModulate>().Count()==1&&hud.DuplicateSubscriptionCount==0&&dayNight.DuplicateClockSubscriptionCount==0;
            double oldElapsed=first.ElapsedSimulationSeconds;GameplaySessionV3.BeginNewSession();SimulationClockSessionV3 replacement=GameplaySessionV3.GetSimulationClock();hud.Bind(replacement);
            dayNight.Bind(replacement,dayNightCanvas);bool oldStopped=first.IsDisposed&&!first.Advance(60)&&first.ElapsedSimulationSeconds==oldElapsed;
            SimulationClockSnapshotV3 reset=replacement.GetSnapshot();
            bool inputBlocked=hud.BlocksWorldInput;
            bool resetTint=SameTint(dayNight.CurrentTint,DayNightVisualCoreV3.EvaluateTint(reset.Hour,reset.Minute,reset.FractionalMinute));
            bool pass=initial.DayIndex==1&&initial.Hour==8&&initial.Minute==0&&afterOne==60&&initialTint&&fixtureTimes&&paused&&scales&&dateAndPhase&&f11f12&&oldStopped&&reset.DayIndex==1&&reset.Hour==8&&reset.Minute==0&&reset.TimeScale==1&&!reset.IsPaused&&resetTint&&hud.RootNodeCount==1&&inputBlocked&&dayNight.CanvasModulateNodeCount==1&&dayNight.PerChunkDayNightNodeCount==0&&dayNight.PerEntityDayNightNodeCount==0&&dayNight.DayNightVisualTimerCount==0;
            GD.Print($"[DayNightVisualV3] fixture PASS={pass} initialTint={initialTint} fixtureTimes={fixtureTimes} pause={paused} f11f12={f11f12} newSession={oldStopped} resetTint={resetTint} updates={dayNight.VisualUpdateCount} skipped={dayNight.SkippedUnchangedTintCount} immediate={dayNight.ImmediateRefreshCount} canvasNodes={dayNight.CanvasModulateNodeCount} timers={dayNight.DayNightVisualTimerCount} mainThreadChunkGeneration=0");
            GD.Print($"[SimulationClockV3] fixture PASS={pass} pure=({SimulationClockSelfCheckV3.LastSummary}) integration=({SimulationDeltaIntegrationSelfCheckV3.LastSummary}) initial={initial.DayIndex}:{initial.Hour:00}:{initial.Minute:00} pause={paused} scales={scales} datePhase={dateAndPhase} f11f12={f11f12} newSession={oldStopped} hud/input={hud.RootNodeCount}/{inputBlocked} nodes/timers=0/{GetTree().Root.FindChildren("*","Timer",true,false).Count} duplicateAdvance/subscription={replacement.Diagnostics.ClockDuplicateAdvanceFrameCount}/{hud.DuplicateSubscriptionCount} mainThreadChunkGeneration=0");
            GetTree().Quit(pass?0:3);
        }
        catch(Exception exception){GD.PushError($"[SimulationClockV3] fixture FAIL {exception.Message}");GetTree().Quit(3);}
    }

    private static bool SameTint(Color left,Color right)=>Mathf.IsEqualApprox(left.R,right.R)&&Mathf.IsEqualApprox(left.G,right.G)&&Mathf.IsEqualApprox(left.B,right.B)&&Mathf.IsEqualApprox(left.A,right.A);
    private static bool InRange(Color color)=>color.R>=0f&&color.R<=1f&&color.G>=0f&&color.G<=1f&&color.B>=0f&&color.B<=1f&&Mathf.IsEqualApprox(color.A,1f);
}
