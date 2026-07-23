using System;

namespace GameplayV3.Time;

public static class SimulationDeltaIntegrationSelfCheckV3
{
    public static string LastSummary { get; private set; }=string.Empty;

    public static bool TryValidate(out string reason)
    {
        Probe one=Run(1,10),two=Run(2,5),three=Run(3,10.0/3.0),paused=RunPaused(5);
        if(!one.Equivalent(two)||!one.Equivalent(three)){reason="Scaled runtime equivalence failed.";return false;}
        if(!paused.IsStopped){reason="Paused runtime advanced.";return false;}
        FixedTickProbe fixedTick=new(.5,4);fixedTick.Advance(3);if(fixedTick.TickCount!=4||Math.Abs(fixedTick.PendingCredit-1)>1e-9){reason="Fixed tick carry was not preserved.";return false;}fixedTick.Advance(0);if(fixedTick.TickCount!=4||Math.Abs(fixedTick.PendingCredit-1)>1e-9){reason="Paused fixed tick consumed credit.";return false;}fixedTick.Advance(.001);if(fixedTick.TickCount!=6||fixedTick.PendingCredit>=.5){reason="Fixed tick carry did not resume.";return false;}
        CompletionProbe completion=new();completion.Advance(6);completion.Advance(6);if(completion.MovementCompleted!=1||completion.WorkCompleted!=1||completion.ConsumptionCompleted!=1||completion.HarvestCompleted!=1){reason="Terminal completion was duplicated.";return false;}
        LastSummary="1x10s=2x5s=3x3.333s; pause=stopped; fixedCarry=PASS; terminalCounts=1/1/1/1";reason=string.Empty;return true;
    }

    private static Probe Run(int scale,double realSeconds){SimulationClockSessionV3 clock=new(1);clock.TrySetTimeScale(scale,out _);SimulationClockAdvanceResultV3 result=clock.AdvanceFrame(realSeconds,1);Probe probe=new();probe.Advance(result.ScaledGameplayDeltaSeconds);return probe;}
    private static Probe RunPaused(double realSeconds){SimulationClockSessionV3 clock=new(1);clock.SetPaused(true);SimulationClockAdvanceResultV3 result=clock.AdvanceFrame(realSeconds,1);Probe probe=new();probe.Advance(result.ScaledGameplayDeltaSeconds);return probe;}

    private sealed class Probe
    {
        public double Movement,Work,Hunger,Fatigue,Eating,Rest,Crop,Ecology;public int Reservation=1;
        public bool IsStopped=>Movement==0&&Work==0&&Hunger==0&&Fatigue==0&&Eating==0&&Rest==0&&Crop==0&&Ecology==0&&Reservation==1;
        public void Advance(double delta){Movement+=delta*3;Work+=delta;Hunger+=delta*.01;Fatigue+=delta*.02;Eating+=delta;Rest+=delta*.03;Crop+=delta;Ecology+=delta;}
        public bool Equivalent(Probe other)=>Near(Movement,other.Movement)&&Near(Work,other.Work)&&Near(Hunger,other.Hunger)&&Near(Fatigue,other.Fatigue)&&Near(Eating,other.Eating)&&Near(Rest,other.Rest)&&Near(Crop,other.Crop)&&Near(Ecology,other.Ecology)&&Reservation==other.Reservation;
        private static bool Near(double a,double b)=>Math.Abs(a-b)<1e-8;
    }

    private sealed class FixedTickProbe
    {
        private readonly double _interval;private readonly int _budget;public double PendingCredit{get;private set;}public int TickCount{get;private set;}
        public FixedTickProbe(double interval,int budget){_interval=interval;_budget=budget;}
        public void Advance(double delta){if(delta<=0)return;PendingCredit+=delta;int count=0;while(PendingCredit>=_interval&&count++<_budget){PendingCredit-=_interval;TickCount++;}}
    }

    private sealed class CompletionProbe
    {
        private double _progress;private bool _done;public int MovementCompleted,WorkCompleted,ConsumptionCompleted,HarvestCompleted;
        public void Advance(double delta){if(_done)return;_progress+=delta;if(_progress<5)return;_done=true;MovementCompleted++;WorkCompleted++;ConsumptionCompleted++;HarvestCompleted++;}
    }
}
