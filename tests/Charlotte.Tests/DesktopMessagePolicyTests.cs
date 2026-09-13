using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class DesktopMessagePolicyTests
{
    [Theory]
    [InlineData(0x001E,0)]
    [InlineData(0x001A,0)]
    [InlineData(0x0218,0x0007)]
    [InlineData(0x0218,0x0012)]
    public void Time_change_and_resume_messages_refresh_application_state(int message,int parameter)
        => Assert.True(DesktopMessagePolicy.RefreshesApplicationState(message,parameter));

    [Theory]
    [InlineData(0x0218,0x0004)]
    [InlineData(0x0218,0x000A)]
    [InlineData(0x000F,0)]
    public void Suspend_query_and_unrelated_messages_do_not_refresh_state(int message,int parameter)
        => Assert.False(DesktopMessagePolicy.RefreshesApplicationState(message,parameter));

    [Theory]
    [InlineData(0x02E0)]
    [InlineData(0x007E)]
    [InlineData(0x001A)]
    public void Dpi_display_and_setting_changes_reflow_the_window(int message)
        => Assert.True(DesktopMessagePolicy.ReflowsWindow(message));

    [Fact]
    public void Paint_message_does_not_reflow_the_window()
        => Assert.False(DesktopMessagePolicy.ReflowsWindow(0x000F));
}
