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

    [Fact]
    public void Rotation_keeps_only_three_two_megabyte_log_files()
    {
        var root=Path.Combine(Path.GetTempPath(),"CharlotteTests",Guid.NewGuid().ToString("N"));
        var directory=Path.Combine(root,"logs");
        var path=Path.Combine(directory,"charlotte.log");
        try
        {
            Directory.CreateDirectory(directory);
            var log=new DiagnosticLog(root);
            for(var rotation=0;rotation<3;rotation++)
            {
                File.WriteAllBytes(path,new byte[2*1024*1024]);
                log.Write("rotation-check");
            }

            Assert.Equal(3,Directory.GetFiles(directory,"charlotte.log*").Length);
            Assert.True(File.Exists(path));
            Assert.True(File.Exists(path+".1"));
            Assert.True(File.Exists(path+".2"));
            Assert.False(File.Exists(path+".3"));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
}
