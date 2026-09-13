namespace Charlotte.Windows.Services;

public sealed class VisibilityPolicy(TimeSpan debounce)
{
    private bool hidden;
    private bool? pending;
    private TimeSpan pendingSince;

    public bool Observe(bool fullscreen,TimeSpan now)
    {
        if(fullscreen==hidden)
        {
            pending=null;
            return hidden;
        }

        if(pending!=fullscreen)
        {
            pending=fullscreen;
            pendingSince=now;
            return hidden;
        }

        if(now-pendingSince>=debounce)
        {
            hidden=fullscreen;
            pending=null;
        }

        return hidden;
    }
}
