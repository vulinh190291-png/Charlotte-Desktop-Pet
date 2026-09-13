namespace Charlotte.Windows.Services;

public static class DesktopMessagePolicy
{
    private const int WmTimeChange=0x001E;
    private const int WmSettingChange=0x001A;
    private const int WmPowerBroadcast=0x0218;
    private const long PbtApmResumeSuspend=0x0007;
    private const long PbtApmResumeAutomatic=0x0012;

    public static bool RefreshesApplicationState(int message,long parameter)
        => message is WmTimeChange or WmSettingChange || message==WmPowerBroadcast
            && parameter is PbtApmResumeSuspend or PbtApmResumeAutomatic;

    public static bool ReflowsWindow(int message)
        => message is 0x02E0 or 0x007E or 0x001A;
}
