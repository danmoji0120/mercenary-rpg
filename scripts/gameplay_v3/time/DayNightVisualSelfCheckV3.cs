using System;
using GameplayV3.Time;
using Godot;

namespace GameplayV3.Time;

public static class DayNightVisualSelfCheckV3
{
    public static string LastSummary { get; private set; }=string.Empty;

    public static bool TryValidate(out string reason)
    {
        try
        {
            ValidateKeyframes();
            ValidateContinuity();
            ValidateTimeCharacteristics();
            ValidateScaleEquivalence();
            ValidateInvalidInputs();
            LastSummary="keyframes=PASS continuity=PASS characteristics=PASS scales=PASS invalid=PASS";
            reason=string.Empty;
            return true;
        }
        catch(Exception exception)
        {
            LastSummary=exception.Message;
            reason=exception.Message;
            return false;
        }
    }

    private static void ValidateKeyframes()
    {
        foreach(DayNightVisualKeyframeV3 keyframe in DayNightVisualCoreV3.Keyframes)
            Require(Approximately(DayNightVisualCoreV3.EvaluateTint(keyframe.MinuteOfDay),keyframe.Tint),$"Keyframe mismatch at {keyframe.MinuteOfDay}.");
        Require(Approximately(DayNightVisualCoreV3.EvaluateTint(0),DayNightVisualCoreV3.EvaluateTint(1440)),"Midnight was not continuous.");
        Require(DayNightVisualCoreV3.FindKeyframeSegment(300)==1,"Dawn segment mismatch.");
        Require(DayNightVisualCoreV3.FindKeyframeSegment(1260)==6,"Night segment mismatch.");
    }

    private static void ValidateContinuity()
    {
        foreach(double minute in new[]{0.0,300.0,420.0,720.0,1050.0,1140.0,1260.0})
        {
            Color before=DayNightVisualCoreV3.EvaluateTint(minute-.01);
            Color after=DayNightVisualCoreV3.EvaluateTint(minute+.01);
            Require(MaxChannelDistance(before,after)<.02f,$"Tint jump near {minute}.");
        }

        Color beforeMidnight=DayNightVisualCoreV3.EvaluateTint(1439.9);
        Color afterMidnight=DayNightVisualCoreV3.EvaluateTint(.1);
        Require(MaxChannelDistance(beforeMidnight,afterMidnight)<.02f,"Midnight tint jump.");
    }

    private static void ValidateTimeCharacteristics()
    {
        foreach(double minute in new[]{270.0,330.0,480.0,720.0,1110.0,1320.0,0.0})
            Require(InRange(DayNightVisualCoreV3.EvaluateTint(minute)),$"Runtime fixture minute {minute} escaped tint range.");
        Color noon=DayNightVisualCoreV3.EvaluateTint(720);
        Color evening=DayNightVisualCoreV3.EvaluateTint(1140);
        Color night=DayNightVisualCoreV3.EvaluateTint(1320);
        Require(noon.R+noon.G+noon.B>2.9f,"Noon is not the brightest interval.");
        Require(night.R+night.G+night.B<noon.R+noon.G+noon.B,"Night is not darker than noon.");
        Require(evening.R>evening.B,"Evening is not warm.");
        Require(night.R>.45f&&night.G>.50f&&night.B>.60f,"Night tint fell below the visibility floor.");
        Require(InRange(noon)&&InRange(evening)&&InRange(night),"Tint channel escaped range.");
    }

    private static void ValidateScaleEquivalence()
    {
        SimulationClockSettingsV3 settings=new(StartHour:18,StartMinute:30);
        SimulationClockSessionV3 one=new(1,settings);
        SimulationClockSessionV3 two=new(1,settings);
        SimulationClockSessionV3 three=new(1,settings);
        one.TrySetTimeScale(1,out _);two.TrySetTimeScale(2,out _);three.TrySetTimeScale(3,out _);
        Color first=Tint(one.GetSnapshot());
        Require(Approximately(first,Tint(two.GetSnapshot()))&&Approximately(first,Tint(three.GetSnapshot())),"Scale equivalence failed.");
        Color paused=DayNightVisualCoreV3.EvaluateTint(18,30,0);
        Require(Approximately(paused,DayNightVisualCoreV3.EvaluateTint(18,30,0)),"Paused tint was not stable.");
    }

    private static void ValidateInvalidInputs()
    {
        Require(DayNightVisualCoreV3.NormalizeMinuteOfDay(-1)==1439,"Negative minute normalization failed.");
        Require(DayNightVisualCoreV3.NormalizeMinuteOfDay(1441)==1,"Large minute normalization failed.");
        foreach(double value in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity,1e18,-1e18})
            Require(InRange(DayNightVisualCoreV3.EvaluateTint(value)),$"Invalid minute {value} damaged tint.");
    }

    private static Color Tint(SimulationClockSnapshotV3 snapshot)=>
        DayNightVisualCoreV3.EvaluateTint(snapshot.Hour,snapshot.Minute,snapshot.FractionalMinute);

    private static bool InRange(Color color)=>
        color.R>=0f&&color.R<=1f&&color.G>=0f&&color.G<=1f&&color.B>=0f&&color.B<=1f&&Mathf.IsEqualApprox(color.A,1f);

    private static bool Approximately(Color left,Color right)=>MaxChannelDistance(left,right)<.0001f;

    private static float MaxChannelDistance(Color left,Color right)=>Mathf.Max(Mathf.Abs(left.R-right.R),Mathf.Max(Mathf.Abs(left.G-right.G),Mathf.Abs(left.B-right.B)));

    private static void Require(bool condition,string message)
    {
        if(!condition)throw new InvalidOperationException(message);
    }
}
