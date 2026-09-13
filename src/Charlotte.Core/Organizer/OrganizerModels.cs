namespace Charlotte.Core.Organizer;

public sealed record DailyTask(Guid Id, string Title, bool IsCompleted, DateTimeOffset CreatedAt, long Order);
public sealed record ScheduleItem(Guid Id, string Title, DateOnly Date, TimeOnly Time, bool IsCompleted, DateTimeOffset CreatedAt, long Order);

public sealed class OrganizerState
{
    public List<DailyTask> Tasks { get; } = [];
    public List<ScheduleItem> Schedules { get; } = [];
    public DateOnly LastResetDate { get; set; }
    public long NextOrder { get; set; }
    public static OrganizerState Empty(DateOnly date) => new() { LastResetDate = date };
}

public sealed class OrganizerValidationException(string message) : Exception(message);
