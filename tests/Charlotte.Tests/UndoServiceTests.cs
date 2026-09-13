using Charlotte.Core.Organizer;

namespace Charlotte.Tests;

public class UndoServiceTests
{
    private static (OrganizerState State, OrganizerService Organizer, UndoService Undo, ManualTimeProvider Clock) Create()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026,9,13,0,0,0,TimeSpan.Zero));
        var state = OrganizerState.Empty(new(2026,9,13));
        var undo = new UndoService(state, clock);
        var organizer = new OrganizerService(state, clock, () => undo.ReservedTaskSlots);
        return (state, organizer, undo, clock);
    }

    [Fact]
    public void Undo_before_five_seconds_restores_original_identity_and_order()
    {
        var f = Create(); var first = f.Organizer.AddTask("一"); f.Organizer.AddTask("二");
        f.Undo.DeleteTask(first); f.Clock.Advance(TimeSpan.FromMilliseconds(4999));
        Assert.True(f.Undo.TryUndo());
        Assert.Equal(first, f.State.Tasks.OrderBy(x => x.Order).First().Id);
    }

    [Fact]
    public void Undo_at_five_seconds_is_expired()
    {
        var f = Create(); var id = f.Organizer.AddTask("一");
        f.Undo.DeleteTask(id); f.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.False(f.Undo.TryUndo());
        Assert.Empty(f.State.Tasks);
    }

    [Fact]
    public void Second_deletion_replaces_the_previous_undo_slot()
    {
        var f = Create(); var first=f.Organizer.AddTask("一"); var second=f.Organizer.AddTask("二");
        f.Undo.DeleteTask(first); f.Undo.DeleteTask(second); f.Undo.TryUndo();
        Assert.DoesNotContain(f.State.Tasks,x=>x.Id==first);
        Assert.Contains(f.State.Tasks,x=>x.Id==second);
    }

    [Fact]
    public void Deleted_task_reserves_capacity_until_slot_expires()
    {
        var f = Create(); for(var i=0;i<7;i++) f.Organizer.AddTask($"{i}");
        f.Undo.DeleteTask(f.State.Tasks[0].Id);
        Assert.Throws<OrganizerValidationException>(()=>f.Organizer.AddTask("新"));
        f.Clock.Advance(TimeSpan.FromSeconds(5));
        f.Organizer.AddTask("新");
        Assert.Equal(7,f.State.Tasks.Count);
    }

    [Fact]
    public void Rollover_clears_completion_before_deleted_task_is_restored()
    {
        var f=Create(); var id=f.Organizer.AddTask("一"); f.Organizer.SetTaskCompleted(id,true);
        f.Undo.DeleteTask(id); DateRollover.Apply(f.State,new(2026,9,14));
        f.Undo.TryUndo();
        Assert.False(f.State.Tasks.Single().IsCompleted);
    }
}
