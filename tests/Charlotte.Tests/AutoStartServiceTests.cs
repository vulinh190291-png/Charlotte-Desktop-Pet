using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class AutoStartServiceTests
{
    [Fact]
    public void Enable_writes_fully_quoted_current_executable()
    {
        var registry=new MemoryRegistry();
        var service=new AutoStartService(registry);

        var result=service.SetEnabled(true,@"C:\Program Files\Charlotte Pet\Charlotte.Windows.exe");

        Assert.True(result.Success);
        Assert.True(result.Enabled);
        Assert.Equal("\"C:\\Program Files\\Charlotte Pet\\Charlotte.Windows.exe\"",registry.Value);
    }

    [Fact]
    public void Disable_removes_value()
    {
        var registry=new MemoryRegistry { Value="old" };

        var result=new AutoStartService(registry).SetEnabled(false,@"C:\Charlotte\Charlotte.Windows.exe");

        Assert.True(result.Success);
        Assert.False(result.Enabled);
        Assert.Null(registry.Value);
    }

    [Fact]
    public void Failed_write_reports_actual_registry_state()
    {
        var registry=new MemoryRegistry { Value="old",ThrowOnWrite=true };

        var result=new AutoStartService(registry).SetEnabled(true,@"C:\Charlotte\Charlotte.Windows.exe");

        Assert.False(result.Success);
        Assert.False(result.Enabled);
        Assert.Equal("registry-write-failed",result.ErrorCode);
    }

    private sealed class MemoryRegistry : IAutoStartRegistry
    {
        public string? Value { get; set; }
        public bool ThrowOnWrite { get; set; }
        public string? Read()=>Value;
        public void Write(string command) { if(ThrowOnWrite) throw new UnauthorizedAccessException(); Value=command; }
        public void Delete() { if(ThrowOnWrite) throw new UnauthorizedAccessException(); Value=null; }
    }
}
