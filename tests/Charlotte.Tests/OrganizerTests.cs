using Charlotte.Core.Organizer;

namespace Charlotte.Tests;

public class OrganizerTests
{
    private static OrganizerService Create(out OrganizerState state)
    {
        state = OrganizerState.Empty(new(2026, 9, 13));
        return new(state, TimeProvider.System);
    }

    [Fact]
    public void Completion_event_only_occurs_on_false_to_true()
    {
        var service = Create(out _); int count = 0;
        service.TaskCompleted += _ => count++;
        var id = service.AddTask("喝水");
        service.SetTaskCompleted(id, true);
        service.SetTaskCompleted(id, true);
        service.SetTaskCompleted(id, false);
        service.SetTaskCompleted(id, true);
        Assert.Equal(2, count);
    }

    [Fact]
    public void Eighth_task_is_rejected_without_mutating_state()
    {
        var service = Create(out var state);
        for (var i = 0; i < 7; i++) service.AddTask($"任务{i}");
        Assert.Throws<OrganizerValidationException>(() => service.AddTask("第八条"));
        Assert.Equal(7, state.Tasks.Count);
    }

    [Fact]
    public void Title_limit_counts_unicode_graphemes()
    {
        var service = Create(out _);
        service.AddTask(string.Concat(Enumerable.Repeat("👨‍👩‍👧‍👦", 30)));
        Assert.Throws<OrganizerValidationException>(() => service.AddSchedule(string.Concat(Enumerable.Repeat("👨‍👩‍👧‍👦", 31)), new(2026,9,13), new(9,0)));
        Assert.Throws<OrganizerValidationException>(() => service.AddTask("   "));
    }

    [Fact]
    public void Schedules_sort_by_time_then_creation_order()
    {
        var service = Create(out _); var day = new DateOnly(2026,9,13);
        var late = service.AddSchedule("晚", day, new(10,0));
        var first = service.AddSchedule("早一", day, new(9,0));
        var second = service.AddSchedule("早二", day, new(9,0));
        Assert.Equal([first, second, late], service.ForDate(day).Select(x => x.Id));
    }

    [Fact]
    public void Overdue_is_derived_and_completion_suppresses_it()
    {
        var service = Create(out var state); var day = new DateOnly(2026,9,13);
        var id = service.AddSchedule("会议", day, new(9,0));
        var item = state.Schedules.Single();
        Assert.False(service.IsOverdue(item, new(2026,9,13,9,0,0)));
        Assert.True(service.IsOverdue(item, new(2026,9,13,9,0,1)));
        service.SetScheduleCompleted(id, true);
        Assert.False(service.IsOverdue(state.Schedules.Single(), new(2026,9,14,9,0,0)));
    }

    [Theory]
    [InlineData(12, true)]
    [InlineData(13, false)]
    [InlineData(14, true)]
    public void Different_date_resets_in_either_direction(int day, bool changed)
    {
        var service = Create(out var state); var id = service.AddTask("阅读");
        service.SetTaskCompleted(id, true);
        Assert.Equal(changed, DateRollover.Apply(state, new(2026,9,day)));
        Assert.Equal(!changed, state.Tasks.Single().IsCompleted);
    }
}
