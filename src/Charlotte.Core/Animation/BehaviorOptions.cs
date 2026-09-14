namespace Charlotte.Core.Animation;

public sealed record BehaviorOptions
{
    public static BehaviorOptions Default { get; }=new();

    public TimeSpan ClickWindow { get; init; }=TimeSpan.FromMilliseconds(1500);
    public TimeSpan RestAfter { get; init; }=TimeSpan.FromMinutes(3);
    public TimeSpan SleepAfter { get; init; }=TimeSpan.FromMinutes(10);
    public bool AutoWalkEnabled { get; init; }=true;
    public TimeSpan WalkDelayMinimum { get; init; }=TimeSpan.FromSeconds(20);
    public TimeSpan WalkDelayMaximum { get; init; }=TimeSpan.FromSeconds(40);
    public double WalkDistanceMinimumDip { get; init; }=24;
    public double WalkDistanceMaximumDip { get; init; }=72;
    public double WalkSpeedDipPerSecond { get; init; }=24;
    public double IdleStillRatio { get; init; }=.7;

    public void Validate()
    {
        if(ClickWindow<=TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(ClickWindow));
        if(RestAfter<=TimeSpan.Zero || SleepAfter<=RestAfter) throw new ArgumentException("Sleep must follow rest.");
        if(WalkDelayMinimum<=TimeSpan.Zero || WalkDelayMaximum<WalkDelayMinimum) throw new ArgumentException("Walk delay range is invalid.");
        if(!double.IsFinite(WalkDistanceMinimumDip) || !double.IsFinite(WalkDistanceMaximumDip)
            || WalkDistanceMinimumDip<=0 || WalkDistanceMaximumDip<WalkDistanceMinimumDip)
            throw new ArgumentException("Walk distance range is invalid.");
        if(!double.IsFinite(WalkSpeedDipPerSecond) || WalkSpeedDipPerSecond<=0) throw new ArgumentOutOfRangeException(nameof(WalkSpeedDipPerSecond));
        if(!double.IsFinite(IdleStillRatio) || IdleStillRatio<0 || IdleStillRatio>=1) throw new ArgumentOutOfRangeException(nameof(IdleStillRatio));
    }
}

public interface IRandomSource
{
    double NextUnit();
}

public sealed class SystemRandomSource : IRandomSource
{
    public double NextUnit()=>Random.Shared.NextDouble();
}

public sealed record WalkPlan(int Direction,double DistanceDip,double SpeedDipPerSecond)
{
    public TimeSpan Duration=>TimeSpan.FromSeconds(DistanceDip/SpeedDipPerSecond);

    public double OffsetAt(TimeSpan elapsed)
    {
        var seconds=Math.Clamp(elapsed.TotalSeconds,0,Duration.TotalSeconds);
        return Direction*seconds*SpeedDipPerSecond;
    }
}

public static class IdleRhythm
{
    public static TimeSpan AnimationElapsed(TimeSpan elapsed,TimeSpan movingDuration,double stillRatio)
    {
        if(movingDuration<=TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(movingDuration));
        if(!double.IsFinite(stillRatio) || stillRatio<0 || stillRatio>=1) throw new ArgumentOutOfRangeException(nameof(stillRatio));
        var cycleTicks=(long)Math.Round(movingDuration.Ticks/(1-stillRatio));
        var stillTicks=cycleTicks-movingDuration.Ticks;
        var elapsedTicks=Math.Max(0,elapsed.Ticks);
        var phase=elapsedTicks%cycleTicks;
        return phase<stillTicks?TimeSpan.Zero:TimeSpan.FromTicks(phase-stillTicks);
    }
}
