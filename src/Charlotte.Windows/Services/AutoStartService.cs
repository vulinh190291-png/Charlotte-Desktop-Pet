using System.IO;
using Microsoft.Win32;

namespace Charlotte.Windows.Services;

public interface IAutoStartRegistry
{
    string? Read();
    void Write(string command);
    void Delete();
}

public sealed record AutoStartResult(bool Success,bool Enabled,string? ErrorCode=null);

public sealed class AutoStartService(IAutoStartRegistry registry)
{
    public AutoStartResult SetEnabled(bool enabled,string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        try
        {
            if(enabled) registry.Write($"\"{executablePath}\"");
            else registry.Delete();
            return new(true,IsEnabledFor(executablePath));
        }
        catch(Exception error) when(error is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            return new(false,SafeIsEnabledFor(executablePath),"registry-write-failed");
        }
    }

    public bool IsEnabledFor(string executablePath)
        => string.Equals(registry.Read(),$"\"{executablePath}\"",StringComparison.OrdinalIgnoreCase);

    private bool SafeIsEnabledFor(string executablePath)
    {
        try { return IsEnabledFor(executablePath); }
        catch { return false; }
    }
}

public sealed class CurrentUserAutoStartRegistry : IAutoStartRegistry
{
    private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName="CharlotteDesktopPet";
    public string? Read()
    {
        using var key=Registry.CurrentUser.OpenSubKey(RunKey,false);
        return key?.GetValue(ValueName) as string;
    }
    public void Write(string command)
    {
        using var key=Registry.CurrentUser.CreateSubKey(RunKey,true);
        key.SetValue(ValueName,command,RegistryValueKind.String);
    }
    public void Delete()
    {
        using var key=Registry.CurrentUser.OpenSubKey(RunKey,true);
        key?.DeleteValue(ValueName,false);
    }
}
