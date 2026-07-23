using System;
using System.Collections.Generic;
using Godot;

namespace GameplayV3.Time;

public readonly record struct DayNightVisualKeyframeV3(double MinuteOfDay, Color Tint);

public static class DayNightVisualCoreV3
{
    public const double MinutesPerDay=1440.0;
    public const double DefaultFallbackMinute=720.0;

    private static readonly DayNightVisualKeyframeV3[] KeyframeValues=
    {
        new(0.0,new Color(.50f,.58f,.76f,1f)),
        new(300.0,new Color(.64f,.69f,.82f,1f)),
        new(420.0,new Color(.92f,.94f,.98f,1f)),
        new(720.0,new Color(1f,1f,1f,1f)),
        new(1050.0,new Color(1f,.94f,.86f,1f)),
        new(1140.0,new Color(.91f,.76f,.66f,1f)),
        new(1260.0,new Color(.62f,.67f,.82f,1f)),
        new(1440.0,new Color(.50f,.58f,.76f,1f))
    };

    public static IReadOnlyList<DayNightVisualKeyframeV3> Keyframes=>KeyframeValues;

    public static Color EvaluateTint(int hour,int minute,double fractionalMinute)=>
        EvaluateTint(hour*60.0+minute+fractionalMinute);

    public static Color EvaluateTint(double minuteOfDay)
    {
        double normalized=NormalizeMinuteOfDay(minuteOfDay);
        int segment=FindKeyframeSegment(normalized);
        DayNightVisualKeyframeV3 from=KeyframeValues[segment];
        DayNightVisualKeyframeV3 to=KeyframeValues[segment+1];
        double length=to.MinuteOfDay-from.MinuteOfDay;
        double t=length<=0?0:SmoothStep((normalized-from.MinuteOfDay)/length);
        return Lerp(from.Tint,to.Tint,t);
    }

    public static double NormalizeMinuteOfDay(double minuteOfDay)
    {
        if(!double.IsFinite(minuteOfDay))return DefaultFallbackMinute;
        double normalized=minuteOfDay%MinutesPerDay;
        return normalized<0?normalized+MinutesPerDay:normalized;
    }

    public static int FindKeyframeSegment(double minuteOfDay)
    {
        double normalized=NormalizeMinuteOfDay(minuteOfDay);
        for(int index=0;index<KeyframeValues.Length-2;index++)
            if(normalized<KeyframeValues[index+1].MinuteOfDay)return index;
        return KeyframeValues.Length-2;
    }

    public static double SmoothStep(double value)
    {
        double t=Math.Clamp(value,0.0,1.0);
        return t*t*(3.0-2.0*t);
    }

    private static Color Lerp(Color from,Color to,double amount)
    {
        float t=(float)Math.Clamp(amount,0.0,1.0);
        return new Color(
            Mathf.Clamp(Mathf.Lerp(from.R,to.R,t),0f,1f),
            Mathf.Clamp(Mathf.Lerp(from.G,to.G,t),0f,1f),
            Mathf.Clamp(Mathf.Lerp(from.B,to.B,t),0f,1f),
            1f);
    }
}
