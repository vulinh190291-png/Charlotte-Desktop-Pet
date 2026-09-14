using Charlotte.Tests.Fixtures;

namespace Charlotte.Tests;

public sealed class ShutdownIntegrationTests
{
    [Fact]
    public async Task Exit_waits_for_pending_persistence_before_disposing_resources()
    {
        var app=ShutdownFixture.CreateWithBlockedWriter();
        await app.WriterStarted;

        var exiting=app.Coordinator.RequestAsync();

        Assert.False(exiting.IsCompleted);
        Assert.False(app.ResourcesDisposed);
        app.ReleaseWriter();
        await exiting;
        Assert.True(app.ResourcesDisposed);
    }
}
