using System;
using System.Collections.Generic;

namespace GameplayV3.Time;

public enum DayPhaseV3 { Dawn, Day, Evening, Night }
public enum SimulationClockEventTypeV3 { TimeAdvanced, HourChanged, DayPhaseChanged, DayStarted, DayEnded, TimeScaleChanged, PauseChanged }

public sealed record SimulationClockSettingsV3(
    double RealSecondsPerGameHour=60.0,
    int HoursPerDay=24,
    int StartDayIndex=1,
    int StartHour=8,
    int StartMinute=0,
    double MaxAdvanceStepRealSeconds=.25)
{
    public static SimulationClockSettingsV3 Default { get; }=new();
    public static readonly int[] SupportedTimeScales={1,2,3};
    public double GameSecondsPerRealSecond=>3600.0/RealSecondsPerGameHour;
}

public readonly record struct SimulationClockSnapshotV3(
    long SimulationTick,double ElapsedSimulationSeconds,int DayIndex,int Hour,int Minute,
    double FractionalMinute,DayPhaseV3 DayPhase,int TimeScale,bool IsPaused,long Revision);

public readonly record struct SimulationClockAdvanceResultV3(
    bool Advanced,double AcceptedRealDeltaSeconds,double ScaledGameplayDeltaSeconds,
    double WorldSecondsAdvanced,int TimeScale,bool WasPaused,long FrameToken,long ClockRevision);

public readonly record struct SimulationClockEventV3(
    SimulationClockEventTypeV3 EventType,int PreviousDayIndex,int CurrentDayIndex,
    int PreviousHour,int CurrentHour,DayPhaseV3 PreviousDayPhase,DayPhaseV3 CurrentDayPhase,
    int PreviousTimeScale,int CurrentTimeScale,bool PreviousPaused,bool CurrentPaused,long Revision);

public sealed class SimulationClockDiagnosticsV3
{
    public long ClockAdvanceCallCount { get; internal set; }
    public long ClockDuplicateAdvanceFrameCount { get; internal set; }
    public long InvalidDeltaCount { get; internal set; }
    public long HourBoundaryCount { get; internal set; }
    public long DayBoundaryCount { get; internal set; }
    public long PhaseBoundaryCount { get; internal set; }
}

public sealed class SimulationDeltaRoutingDiagnosticsV3
{
    public long SimulationStepFrameCount { get; internal set; }
    public long DuplicateSimulationStepCount { get; internal set; }
    public long MovementAdvanceCount { get; internal set; }
    public long WorkAdvanceCount { get; internal set; }
    public long NeedsAdvanceCount { get; internal set; }
    public long FarmingAdvanceCount { get; internal set; }
    public long EcologyAdvanceCount { get; internal set; }
    public long PausedSimulationAdvanceViolationCount { get; internal set; }
    public long RawDeltaBypassCount { get; internal set; }
    public double LastRealDelta { get; internal set; }
    public double LastScaledGameplayDelta { get; internal set; }
    public double LastWorldSecondsAdvanced { get; internal set; }
    public double NeedsPendingTickCredit { get; internal set; }
    public double FarmingPendingTickCredit { get; internal set; }
    public double EcologyPendingTickCredit { get; internal set; }
}

public sealed class SimulationClockSessionV3 : IDisposable
{
    private const int RecentEventLimit=16;
    private const long MaxHourBoundariesPerAdvance=24L*366L;
    private readonly Queue<SimulationClockEventV3> _recentEvents=new();
    private double _elapsedSimulationSeconds;
    private long _simulationTick;
    private long _revision;
    private long _lastFrameToken=-1;
    private int _timeScale=1;
    private bool _paused;

    public SimulationClockSessionV3(long sessionRevision,SimulationClockSettingsV3? settings=null)
    {
        SessionRevision=sessionRevision;
        Settings=settings??SimulationClockSettingsV3.Default;
    }

    public long SessionRevision { get; private set; }
    public SimulationClockSettingsV3 Settings { get; }
    public SimulationClockDiagnosticsV3 Diagnostics { get; }=new();
    public SimulationDeltaRoutingDiagnosticsV3 RoutingDiagnostics { get; }=new();
    public SimulationClockAdvanceResultV3 LastAdvanceResult { get; private set; }
    public bool IsDisposed { get; private set; }
    public int DayIndex=>GetSnapshot().DayIndex;
    public int Hour=>GetSnapshot().Hour;
    public int Minute=>GetSnapshot().Minute;
    public DayPhaseV3 DayPhase=>GetSnapshot().DayPhase;
    public int TimeScale=>_timeScale;
    public bool IsPaused=>_paused;
    public double ElapsedSimulationSeconds=>_elapsedSimulationSeconds;
    public long Revision=>_revision;

    public event Action<SimulationClockEventV3>? TimeAdvanced;
    public event Action<SimulationClockEventV3>? HourChanged;
    public event Action<SimulationClockEventV3>? DayPhaseChanged;
    public event Action<SimulationClockEventV3>? DayStarted;
    public event Action<SimulationClockEventV3>? DayEnded;
    public event Action<SimulationClockEventV3>? TimeScaleChanged;
    public event Action<SimulationClockEventV3>? PauseChanged;

    public void RebindSessionRevision(long sessionRevision)
    {
        if(IsDisposed)throw new ObjectDisposedException(nameof(SimulationClockSessionV3));
        if(sessionRevision<1)throw new ArgumentOutOfRangeException(nameof(sessionRevision));
        SessionRevision=sessionRevision;
    }

    internal void RestorePersistentState(long simulationTick,double elapsedSimulationSeconds,int timeScale,long revision)
    {
        if(IsDisposed||simulationTick<0||!double.IsFinite(elapsedSimulationSeconds)||elapsedSimulationSeconds<0||revision<0)
            throw new ArgumentOutOfRangeException(nameof(revision));
        if(!TrySetTimeScale(timeScale,out string reason))throw new InvalidOperationException(reason);
        _simulationTick=simulationTick;_elapsedSimulationSeconds=elapsedSimulationSeconds;_revision=revision;_lastFrameToken=-1;
        _recentEvents.Clear();
    }

    public bool Advance(double realDeltaSeconds)=>AdvanceInternal(realDeltaSeconds,-1).AcceptedRealDeltaSeconds>0||realDeltaSeconds==0;
    public bool AdvanceForFrame(double realDeltaSeconds,long frameToken)=>AdvanceInternal(realDeltaSeconds,frameToken).AcceptedRealDeltaSeconds>0||realDeltaSeconds==0;
    public SimulationClockAdvanceResultV3 AdvanceFrame(double realDeltaSeconds,long frameToken)=>AdvanceInternal(realDeltaSeconds,frameToken);

    private SimulationClockAdvanceResultV3 AdvanceInternal(double realDeltaSeconds,long frameToken)
    {
        if(IsDisposed)return LastAdvanceResult=new(false,0,0,0,_timeScale,_paused,frameToken,_revision);
        if(frameToken>=0&&frameToken==_lastFrameToken){Diagnostics.ClockDuplicateAdvanceFrameCount++;return LastAdvanceResult=new(false,0,0,0,_timeScale,_paused,frameToken,_revision);}
        if(frameToken>=0)_lastFrameToken=frameToken;
        Diagnostics.ClockAdvanceCallCount++;
        if(!double.IsFinite(realDeltaSeconds)||realDeltaSeconds<0){Diagnostics.InvalidDeltaCount++;return LastAdvanceResult=new(false,0,0,0,_timeScale,_paused,frameToken,_revision);}
        double scaledGameplayDelta=_paused?0:realDeltaSeconds*_timeScale;
        double simulationDelta=scaledGameplayDelta*Settings.GameSecondsPerRealSecond;
        if(!double.IsFinite(simulationDelta)){Diagnostics.InvalidDeltaCount++;return LastAdvanceResult=new(false,0,0,0,_timeScale,_paused,frameToken,_revision);}
        if(realDeltaSeconds==0||_paused)return LastAdvanceResult=new(false,realDeltaSeconds,0,0,_timeScale,_paused,frameToken,_revision);
        long previousAbsoluteHour=AbsoluteHour(_elapsedSimulationSeconds);
        long currentAbsoluteHour=AbsoluteHour(_elapsedSimulationSeconds+simulationDelta);
        if(currentAbsoluteHour-previousAbsoluteHour>MaxHourBoundariesPerAdvance){Diagnostics.InvalidDeltaCount++;return LastAdvanceResult=new(false,0,0,0,_timeScale,_paused,frameToken,_revision);}
        SimulationClockSnapshotV3 previous=GetSnapshot();
        _elapsedSimulationSeconds+=simulationDelta;
        _simulationTick++;
        _revision++;
        for(long boundary=previousAbsoluteHour+1;boundary<=currentAbsoluteHour;boundary++)EmitHourBoundary(boundary);
        SimulationClockSnapshotV3 current=GetSnapshot();
        Emit(SimulationClockEventTypeV3.TimeAdvanced,previous,current);
        return LastAdvanceResult=new(true,realDeltaSeconds,scaledGameplayDelta,simulationDelta,_timeScale,false,frameToken,_revision);
    }

    public bool SetPaused(bool paused)
    {
        if(IsDisposed)return false;
        if(_paused==paused)return true;
        SimulationClockSnapshotV3 previous=GetSnapshot();_paused=paused;_revision++;
        Emit(SimulationClockEventTypeV3.PauseChanged,previous,GetSnapshot());return true;
    }

    public bool TogglePaused()=>SetPaused(!_paused);

    public bool TrySetTimeScale(int scale,out string reason)
    {
        if(IsDisposed){reason="ClockDisposed";return false;}
        if(!IsSupportedTimeScale(scale)){reason="UnsupportedTimeScale";return false;}
        if(_timeScale==scale){reason=string.Empty;return true;}
        SimulationClockSnapshotV3 previous=GetSnapshot();_timeScale=scale;_revision++;
        Emit(SimulationClockEventTypeV3.TimeScaleChanged,previous,GetSnapshot());reason=string.Empty;return true;
    }

    public static bool IsSupportedTimeScale(int scale)=>scale is 1 or 2 or 3;

    public SimulationClockSnapshotV3 GetSnapshot()
    {
        double absolute=StartAbsoluteSeconds+_elapsedSimulationSeconds;
        long wholeMinutes=(long)Math.Floor((absolute+1e-9)/60.0);
        int minute=(int)(wholeMinutes%60);long wholeHours=wholeMinutes/60;
        int hour=(int)(wholeHours%Settings.HoursPerDay);
        int day=Settings.StartDayIndex+(int)(wholeHours/Settings.HoursPerDay);
        double fractional=(absolute/60.0)-wholeMinutes;
        return new(_simulationTick,_elapsedSimulationSeconds,day,hour,minute,fractional,GetDayPhase(hour,minute),_timeScale,_paused,_revision);
    }

    public IReadOnlyList<SimulationClockEventV3> GetRecentEvents()=>Array.AsReadOnly(_recentEvents.ToArray());
    public static DayPhaseV3 GetDayPhase(int hour,int minute=0)=>hour switch { >=5 and <7=>DayPhaseV3.Dawn,>=7 and <18=>DayPhaseV3.Day,>=18 and <21=>DayPhaseV3.Evening,_=>DayPhaseV3.Night };
    public static string FormatClockTime(int hour,int minute)=>$"{hour:00}:{minute:00}";
    public static bool IsDaytime(int hour,int minute=0)=>GetDayPhase(hour,minute) is DayPhaseV3.Dawn or DayPhaseV3.Day;
    public static bool IsNighttime(int hour,int minute=0)=>GetDayPhase(hour,minute)==DayPhaseV3.Night;

    private double StartAbsoluteSeconds=>Settings.StartHour*3600.0+Settings.StartMinute*60.0;
    private long AbsoluteHour(double elapsed)=>(long)Math.Floor((StartAbsoluteSeconds+elapsed+1e-9)/3600.0);

    private void EmitHourBoundary(long absoluteHour)
    {
        double saved=_elapsedSimulationSeconds;
        double boundaryElapsed=absoluteHour*3600.0-StartAbsoluteSeconds;
        _elapsedSimulationSeconds=Math.Max(0,boundaryElapsed-1e-6);SimulationClockSnapshotV3 previous=GetSnapshot();
        _elapsedSimulationSeconds=boundaryElapsed;SimulationClockSnapshotV3 current=GetSnapshot();
        if(current.DayIndex!=previous.DayIndex){Emit(SimulationClockEventTypeV3.DayEnded,previous,current);Diagnostics.DayBoundaryCount++;}
        Emit(SimulationClockEventTypeV3.HourChanged,previous,current);Diagnostics.HourBoundaryCount++;
        if(current.DayIndex!=previous.DayIndex)Emit(SimulationClockEventTypeV3.DayStarted,previous,current);
        if(current.DayPhase!=previous.DayPhase){Emit(SimulationClockEventTypeV3.DayPhaseChanged,previous,current);Diagnostics.PhaseBoundaryCount++;}
        _elapsedSimulationSeconds=saved;
    }

    private void Emit(SimulationClockEventTypeV3 type,SimulationClockSnapshotV3 previous,SimulationClockSnapshotV3 current)
    {
        SimulationClockEventV3 value=new(type,previous.DayIndex,current.DayIndex,previous.Hour,current.Hour,previous.DayPhase,current.DayPhase,previous.TimeScale,current.TimeScale,previous.IsPaused,current.IsPaused,_revision);
        _recentEvents.Enqueue(value);while(_recentEvents.Count>RecentEventLimit)_recentEvents.Dequeue();
        switch(type){case SimulationClockEventTypeV3.TimeAdvanced:TimeAdvanced?.Invoke(value);break;case SimulationClockEventTypeV3.HourChanged:HourChanged?.Invoke(value);break;case SimulationClockEventTypeV3.DayPhaseChanged:DayPhaseChanged?.Invoke(value);break;case SimulationClockEventTypeV3.DayStarted:DayStarted?.Invoke(value);break;case SimulationClockEventTypeV3.DayEnded:DayEnded?.Invoke(value);break;case SimulationClockEventTypeV3.TimeScaleChanged:TimeScaleChanged?.Invoke(value);break;case SimulationClockEventTypeV3.PauseChanged:PauseChanged?.Invoke(value);break;}
    }

    public void Dispose()
    {
        if(IsDisposed)return;IsDisposed=true;
        TimeAdvanced=null;HourChanged=null;DayPhaseChanged=null;DayStarted=null;DayEnded=null;TimeScaleChanged=null;PauseChanged=null;
    }
}
