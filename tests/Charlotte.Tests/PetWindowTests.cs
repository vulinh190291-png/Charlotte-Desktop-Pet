using System.Windows;
using System.Windows.Input;
using Charlotte.Core.Geometry;
using Charlotte.Tests.Fixtures;
using Charlotte.Windows.Windows;

namespace Charlotte.Tests;

public sealed class PetWindowTests
{
    [Fact]
    public void Every_right_click_requests_management_toggle()
    {
        StaTest.Run(() =>
        {
            var monitor=new MonitorSnapshot("test",new(0,0,1920,1080),new(0,0,1920,1040),96);
            var window=new PetWindow(monitor,270);
            var requests=0;
            window.OpenManagement=()=>requests++;
            try
            {
                RaiseRightClick(window);
                RaiseRightClick(window);

                Assert.Equal(2,requests);
            }
            finally { window.Close(); }
        });
    }

    private static void RaiseRightClick(PetWindow window)
    {
        var input=new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Right)
        {
            RoutedEvent=UIElement.MouseRightButtonUpEvent,
            Source=window
        };
        window.RaiseEvent(input);
    }
}
