using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public void A_second_thread_cannot_acquire_the_same_instance_identity()
    {
        var identity=$"CharlotteTest-{Guid.NewGuid():N}";
        using var primary=new SingleInstanceService(identity);
        Assert.True(primary.TryAcquire());

        bool? acquired=null;
        var thread=new Thread(() =>
        {
            using var secondary=new SingleInstanceService(identity);
            acquired=secondary.TryAcquire();
        });
        thread.Start();
        thread.Join();

        Assert.False(acquired);
    }

    [Fact]
    public void Wake_notification_retries_until_the_primary_listener_is_ready()
    {
        Fixtures.StaTest.Run(() =>
        {
            var identity=$"CharlotteTest-{Guid.NewGuid():N}";
            using var primary=new SingleInstanceService(identity);
            using var secondary=new SingleInstanceService(identity);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var awakened=new ManualResetEventSlim();
            using var notificationCompleted=new ManualResetEventSlim();
            Assert.True(primary.TryAcquire());
            primary.WakeRequested+=awakened.Set;

            var notification=secondary.NotifyExistingAsync(timeout.Token);
            bool? notificationSucceeded=null;
            Exception? notificationError=null;
            _=ObserveNotificationAsync();
            Thread.Sleep(1700);
            primary.StartListening();

            Assert.True(awakened.Wait(TimeSpan.FromSeconds(1)));
            Assert.True(notificationCompleted.Wait(TimeSpan.FromSeconds(1)));
            Assert.Null(notificationError);
            Assert.True(notificationSucceeded);

            async Task ObserveNotificationAsync()
            {
                try { notificationSucceeded=await notification; }
                catch(Exception error) { notificationError=error; }
                finally { notificationCompleted.Set(); }
            }
        });
    }
}
