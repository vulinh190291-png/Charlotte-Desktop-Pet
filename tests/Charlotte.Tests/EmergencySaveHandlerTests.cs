using System.IO;
using Charlotte.Windows.Services;

namespace Charlotte.Tests;

public sealed class EmergencySaveHandlerTests
{
    [Fact]
    public void Unhandled_exception_attempts_save_and_records_failure_without_leaking_messages()
    {
        var root=Path.Combine(Path.GetTempPath(),"CharlotteTests",Guid.NewGuid().ToString("N"));
        try
        {
            var calls=0;
            var saveFailure=new IOException("私密保存内容");
            var handler=new EmergencySaveHandler(
                new DiagnosticLog(root),
                _=>
                {
                    calls++;
                    return new(SessionEndingSaveStatus.Failed,saveFailure);
                },
                TimeSpan.FromSeconds(2));

            handler.Handle("dispatcher-unhandled",new InvalidOperationException("私密任务正文"));

            var log=File.ReadAllText(Path.Combine(root,"logs","charlotte.log"));
            Assert.Equal(1,calls);
            Assert.Contains("dispatcher-unhandled",log);
            Assert.Contains("emergency-save-failed",log);
            Assert.DoesNotContain("私密任务正文",log);
            Assert.DoesNotContain("私密保存内容",log);
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }

    [Fact]
    public void Save_callback_exceptions_are_contained_and_diagnosed()
    {
        var root=Path.Combine(Path.GetTempPath(),"CharlotteTests",Guid.NewGuid().ToString("N"));
        try
        {
            var handler=new EmergencySaveHandler(
                new DiagnosticLog(root),
                _=>throw new IOException("disk unavailable"),
                TimeSpan.FromSeconds(2));

            handler.Handle("domain-unhandled",new Exception("boom"));

            Assert.Contains("emergency-save-failed",File.ReadAllText(Path.Combine(root,"logs","charlotte.log")));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
}
