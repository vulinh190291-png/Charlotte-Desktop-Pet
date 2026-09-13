using Charlotte.Core.Organizer;

namespace Charlotte.Core.Persistence;

public sealed record AppData(int SchemaVersion, List<DailyTask> Tasks, List<ScheduleItem> Schedules, DateOnly LastResetDate, long NextOrder)
{
    public const int CurrentSchemaVersion = 1;
    public static AppData Empty(DateOnly date) => new(CurrentSchemaVersion, [], [], date, 0);
}

public sealed record AppSettings(int SchemaVersion, double? XRatio, bool AutoStart)
{
    public static AppSettings Default => new(AppData.CurrentSchemaVersion, null, false);
}

public sealed record LoadResult(AppData Data, AppSettings Settings, IReadOnlyList<string> Warnings);

public interface IStateStore
{
    Task<LoadResult> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppData data, AppSettings settings, CancellationToken cancellationToken);
}
