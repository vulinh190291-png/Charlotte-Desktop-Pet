using System.IO;
using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class DiagnosticLogTests
{
    [Fact]
    public void Log_records_whitelisted_diagnostics_without_exception_message()
    {
        var root=Path.Combine(Path.GetTempPath(),"CharlotteTests",Guid.NewGuid().ToString("N"));
        try
        {
            var log=new DiagnosticLog(root);
            log.Write("save-failed",new IOException("私人任务正文不应进入日志",unchecked((int)0x80070020)));

            var text=File.ReadAllText(Path.Combine(root,"logs","charlotte.log"));
            Assert.Contains("save-failed",text);
            Assert.Contains("IOException",text);
            Assert.Contains("0x80070020",text);
            Assert.DoesNotContain("私人任务正文",text);
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
}
