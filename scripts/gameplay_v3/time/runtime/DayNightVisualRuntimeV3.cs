using System;
using GameplayV3.Time;
using Godot;

namespace GameplayV3.Time.Runtime;

public sealed class DayNightVisualRuntimeV3 : IDisposable
{
    private const double RefreshIntervalSeconds=.125;
    private SimulationClockSessionV3? _clock;
    private CanvasModulate? _canvasModulate;
    private double _accumulator;
    private bool _hasTint;

    public bool IsActive=>_clock!=null&&!_clock.IsDisposed&&CanvasModulateNodeCount==1;
    public double MinuteOfDay { get; private set; }
    public Color CurrentTint { get; private set; }=Colors.White;
    public int VisualUpdateCount { get; private set; }
    public int SkippedUnchangedTintCount { get; private set; }
    public int ImmediateRefreshCount { get; private set; }
    public int DuplicateVisualRuntimeCount { get; private set; }
    public int DuplicateClockSubscriptionCount { get; private set; }
    public int CanvasModulateNodeCount=>_canvasModulate!=null&&GodotObject.IsInstanceValid(_canvasModulate)?1:0;
    public int DayNightVisualTimerCount=>0;
    public int PerChunkDayNightNodeCount=>0;
    public int PerEntityDayNightNodeCount=>0;

    public void Bind(SimulationClockSessionV3 clock,CanvasModulate canvasModulate)
    {
        if(ReferenceEquals(_clock,clock)&&ReferenceEquals(_canvasModulate,canvasModulate))
        {
            RefreshImmediate();
            return;
        }

        UnbindClock();
        _clock=clock;
        _canvasModulate=canvasModulate;
        _accumulator=0;
        _hasTint=false;
        _clock.DayPhaseChanged+=OnDayPhaseChanged;
        RefreshImmediate();
    }

    public void AdvanceDisplay(double realDeltaSeconds)
    {
        if(!IsActive||!double.IsFinite(realDeltaSeconds)||realDeltaSeconds<=0)return;
        _accumulator+=realDeltaSeconds;
        if(_accumulator<RefreshIntervalSeconds)return;
        _accumulator%=RefreshIntervalSeconds;
        Refresh();
    }

    public void RefreshImmediate()
    {
        ImmediateRefreshCount++;
        Refresh();
    }

    public void Dispose()
    {
        UnbindClock();
        _canvasModulate=null;
        _hasTint=false;
    }

    private void OnDayPhaseChanged(SimulationClockEventV3 _)=>RefreshImmediate();

    private void Refresh()
    {
        if(!IsActive||_clock==null||_canvasModulate==null)return;
        SimulationClockSnapshotV3 snapshot=_clock.GetSnapshot();
        double minute=snapshot.Hour*60.0+snapshot.Minute+snapshot.FractionalMinute;
        MinuteOfDay=DayNightVisualCoreV3.NormalizeMinuteOfDay(minute);
        Color tint=DayNightVisualCoreV3.EvaluateTint(minute);
        if(_hasTint&&SameTint(CurrentTint,tint))
        {
            SkippedUnchangedTintCount++;
            return;
        }

        _canvasModulate.Color=tint;
        CurrentTint=tint;
        _hasTint=true;
        VisualUpdateCount++;
    }

    private void UnbindClock()
    {
        if(_clock!=null)_clock.DayPhaseChanged-=OnDayPhaseChanged;
        _clock=null;
    }

    private static bool SameTint(Color left,Color right)=>
        Mathf.IsEqualApprox(left.R,right.R)&&Mathf.IsEqualApprox(left.G,right.G)&&Mathf.IsEqualApprox(left.B,right.B)&&Mathf.IsEqualApprox(left.A,right.A);
}
