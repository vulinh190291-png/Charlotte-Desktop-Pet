using Charlotte.Core.Organizer;

namespace Charlotte.Windows.Services;

public sealed class TaskCompletionRouter : IDisposable
{
    private readonly OrganizerService organizer;
    private readonly Action notifyInteraction;
    private readonly Action requestVictory;
    private bool disposed;

    public TaskCompletionRouter(OrganizerService organizer,Action notifyInteraction,Action requestVictory)
    {
        this.organizer=organizer;
        this.notifyInteraction=notifyInteraction;
        this.requestVictory=requestVictory;
        organizer.TaskCompleted+=OnTaskCompleted;
    }

    private void OnTaskCompleted(Guid id)
    {
        notifyInteraction();
        requestVictory();
    }

    public void Dispose()
    {
        if(disposed) return;
        disposed=true;
        organizer.TaskCompleted-=OnTaskCompleted;
    }
}
