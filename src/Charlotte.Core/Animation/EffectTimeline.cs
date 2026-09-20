namespace Charlotte.Core.Animation;

public enum EffectKind { SleepBubble,Rose,Petal,Sparkle,DragBubble }

public sealed record EffectCue(
    EffectKind Kind,
    string? Text,
    double X,
    double Y,
    double Opacity=1,
    double Scale=1,
    double Rotation=0);

public static class EffectTimeline
{
    private const double SleepLifecycleMs=1900;
    private const double BattleLifecycleMs=1690;
    private const double VictoryLifecycleMs=1400;

    public static IReadOnlyList<EffectCue> Sample(AnimationId animation,TimeSpan elapsed)
    {
        var milliseconds=Math.Max(0,elapsed.TotalMilliseconds);
        return animation switch
        {
            AnimationId.Sleep=>Sleep(milliseconds),
            AnimationId.Battle when milliseconds<BattleLifecycleMs=>Battle(milliseconds/BattleLifecycleMs),
            AnimationId.Victory when milliseconds<VictoryLifecycleMs=>Victory(milliseconds/VictoryLifecycleMs),
            AnimationId.DragStart when milliseconds<120=>[new(EffectKind.DragBubble,"!",166,88,Fade(milliseconds/120),.85+.2*milliseconds/120)],
            _=>[]
        };
    }

    private static IReadOnlyList<EffectCue> Sleep(double milliseconds)
    {
        var cycle=(long)Math.Floor(milliseconds/SleepLifecycleMs);
        var progress=milliseconds%SleepLifecycleMs/SleepLifecycleMs;
        var opacity=Math.Clamp(Math.Sin(Math.PI*progress)*1.3,0,1);
        return [new(EffectKind.SleepBubble,cycle%2==0?"ZZZ":"ZZ",170,82-28*progress,opacity,.9+.12*progress)];
    }

    private static IReadOnlyList<EffectCue> Battle(double progress)
        =>
        [
            new(EffectKind.Rose,null,182-20*progress,152-16*progress,Fade(progress),.75+.2*progress,-12),
            new(EffectKind.Petal,null,152-50*progress,137-42*progress,Fade(progress),.65,25+100*progress),
            new(EffectKind.Petal,null,175+42*progress,129-28*progress,Fade(progress),.55,-18-80*progress),
            new(EffectKind.Petal,null,194+54*progress,158+18*progress,Fade(progress),.5,42+120*progress),
            new(EffectKind.Sparkle,null,205,116,Fade(progress),.65+.35*Math.Sin(Math.PI*progress),0)
        ];

    private static IReadOnlyList<EffectCue> Victory(double progress)
        => [new(EffectKind.Sparkle,null,176,96-12*progress,Fade(progress),.7+.4*Math.Sin(Math.PI*progress),0)];

    private static double Fade(double progress)=>Math.Clamp(Math.Sin(Math.PI*Math.Clamp(progress,0,1))*1.4,0,1);
}
