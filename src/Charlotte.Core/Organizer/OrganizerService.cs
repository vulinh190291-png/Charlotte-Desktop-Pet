namespace Charlotte.Core.Organizer;

public sealed class OrganizerService(OrganizerState state, TimeProvider clock, Func<int>? reservedTaskSlots = null)
{
    public event Action<Guid>? TaskCompleted;
    public OrganizerState State => state;
    public Guid AddTask(string title)
    {
        if (state.Tasks.Count + (reservedTaskSlots?.Invoke() ?? 0) >= 7)
            throw new OrganizerValidationException("每日任务最多 7 条。");
        var id = Guid.NewGuid();
        state.Tasks.Add(new(id, TitleRules.Validate(title), false, clock.GetUtcNow(), state.NextOrder++));
        return id;
    }

    public void SetTaskCompleted(Guid id, bool completed)
    {
        var index = state.Tasks.FindIndex(x => x.Id == id);
        if (index < 0) throw new KeyNotFoundException("找不到每日任务。");
        var previous = state.Tasks[index];
        if (previous.IsCompleted == completed) return;
        state.Tasks[index] = previous with { IsCompleted = completed };
        if (completed) TaskCompleted?.Invoke(id);
    }

    public Guid AddSchedule(string title, DateOnly date, TimeOnly time)
    {
        var id = Guid.NewGuid();
        state.Schedules.Add(new(id, TitleRules.Validate(title), date, time, false, clock.GetUtcNow(), state.NextOrder++));
        return id;
    }

    public void SetScheduleCompleted(Guid id, bool completed)
    {
        var index = state.Schedules.FindIndex(x => x.Id == id);
        if (index < 0) throw new KeyNotFoundException("找不到日程。");
        state.Schedules[index] = state.Schedules[index] with { IsCompleted = completed };
    }

    public IReadOnlyList<ScheduleItem> ForDate(DateOnly date) => state.Schedules
        .Where(x => x.Date == date).OrderBy(x => x.Time).ThenBy(x => x.Order).ToArray();

    public bool IsOverdue(ScheduleItem item, DateTime localNow)
        => !item.IsCompleted && item.Date.ToDateTime(item.Time) < localNow;
}

public static class DateRollover
{
    public static bool Apply(OrganizerState state, DateOnly today)
    {
        if (state.LastResetDate == today) return false;
        for (var i = 0; i < state.Tasks.Count; i++)
            state.Tasks[i] = state.Tasks[i] with { IsCompleted = false };
        state.LastResetDate = today;
        return true;
    }
}
