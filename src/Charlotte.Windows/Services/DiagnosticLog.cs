using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Charlotte.Windows.Services;

public sealed class DiagnosticLog
{
    private const long MaxBytes=2*1024*1024;
    private readonly string directory;
    private readonly string path;
    private readonly object gate=new();

    public DiagnosticLog(string dataRoot)
    {
        directory=Path.Combine(dataRoot,"logs");
        path=Path.Combine(directory,"charlotte.log");
    }

    public void Write(string eventCode,Exception? error=null)
    {
        try
        {
            lock(gate)
            {
                Directory.CreateDirectory(directory);
                if(File.Exists(path) && new FileInfo(path).Length>=MaxBytes) Rotate();
                var entry=new
                {
                    utc=DateTimeOffset.UtcNow,
                    eventCode,
                    version=Assembly.GetEntryAssembly()?.GetName().Version?.ToString(),
                    exceptionType=error?.GetType().Name,
                    hresult=error is null?null:$"0x{error.HResult:X8}"
                };
                File.AppendAllText(path,JsonSerializer.Serialize(entry)+Environment.NewLine);
            }
        }
        catch { }
    }

    private void Rotate()
    {
        var third=path+".3"; var second=path+".2"; var first=path+".1";
        if(File.Exists(second)) File.Move(second,third,true);
        if(File.Exists(first)) File.Move(first,second,true);
        File.Move(path,first,true);
    }
}
