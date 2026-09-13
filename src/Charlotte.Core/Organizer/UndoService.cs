namespace Charlotte.Core.Organizer;

public sealed class UndoService(OrganizerState state, TimeProvider clock)
{
    private const double WindowSeconds = 5;
    private DeletedItem? deleted;

    public int ReservedTaskSlots => Active() is DeletedTask ? 1 : 0;

    public TimeSpan ExpiresIn
    {
        get
        {
            var item = Active();
            if (item is null) return TimeSpan.Zero;
            return TimeSpan.FromSeconds(WindowSeconds) - clock.GetElapsedTime(item.Timestamp);
        }
    }

    public bool DeleteTask(Guid id)
    {
        var index = state.Tasks.FindIndex(x => x.Id == id);
        if (index < 0) return false;
        var item = state.Tasks[index];
        state.Tasks.RemoveAt(index);
        deleted = new DeletedTask(item, index, clock.GetTimestamp(), state.LastResetDate);
        return true;
    }

    public bool DeleteSchedule(Guid id)
    {
        var index = state.Schedules.FindIndex(x => x.Id == id);
        if (index < 0) return false;
        var item = state.Schedules[index];
        state.Schedules.RemoveAt(index);
        deleted = new DeletedSchedule(item, index, clock.GetTimestamp());
        return true;
    }

    public bool TryUndo()
    {
        var item = Active();
        deleted = null;
        switch (item)
        {
            case DeletedTask task:
                var restored = task.DateAtDeletion == state.LastResetDate ? task.Item : task.Item with { IsCompleted = false };
                state.Tasks.Insert(Math.Min(task.Index, state.Tasks.Count), restored);
                return true;
            case DeletedSchedule schedule:
                state.Schedules.Insert(Math.Min(schedule.Index, state.Schedules.Count), schedule.Item);
                return true;
            default:
                return false;
        }
    }

    public void Invalidate() => deleted = null;

    private DeletedItem? Active()
    {
        if (deleted is not null && clock.GetElapsedTime(deleted.Timestamp) >= TimeSpan.FromSeconds(WindowSeconds))
            deleted = null;
        return deleted;
    }

    private abstract record DeletedItem(int Index, long Timestamp);
    private sealed record DeletedTask(DailyTask Item, int ItemIndex, long DeletedAt, DateOnly DateAtDeletion)
        : DeletedItem(ItemIndex, DeletedAt);
    private sealed record DeletedSchedule(ScheduleItem Item, int ItemIndex, long DeletedAt)
        : DeletedItem(ItemIndex, DeletedAt);
}
