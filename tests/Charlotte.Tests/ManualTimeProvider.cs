namespace Charlotte.Tests;

internal sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private long timestamp;
    public override DateTimeOffset GetUtcNow() => utcNow + TimeSpan.FromTicks(timestamp);
    public override long GetTimestamp() => timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public void Advance(TimeSpan delta) => timestamp += delta.Ticks;
}
