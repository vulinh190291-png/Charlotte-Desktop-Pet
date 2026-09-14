namespace Charlotte.Windows.Services;

public sealed class EmergencySaveHandler(
    DiagnosticLog log,
    Func<TimeSpan,SessionEndingSaveResult?> save,
    TimeSpan timeout)
{
    public void Handle(string eventCode,Exception? error)
    {
        log.Write(eventCode,error);
        try
        {
            var result=save(timeout);
            if(result?.Status==SessionEndingSaveStatus.TimedOut)
                log.Write("emergency-save-timeout");
            else if(result?.Status==SessionEndingSaveStatus.Failed)
                log.Write("emergency-save-failed",result.Error);
        }
        catch(Exception saveError)
        {
            log.Write("emergency-save-failed",saveError);
        }
    }
}
