using System.ComponentModel;
using System.Runtime.CompilerServices;
using Charlotte.Core.Animation;
using Charlotte.Core.Organizer;

namespace Charlotte.Windows.ViewModels;

public enum PanelTab { Actions,DailyTasks,Schedule }

public sealed record TaskCompletionChange(Guid Id,bool Completed);
public sealed record ScheduleCompletionChange(Guid Id,bool Completed);

public sealed class ControlPanelViewModel : INotifyPropertyChanged
{
    private readonly OrganizerState state;
    private readonly OrganizerService organizer;
    private readonly UndoService undo;
    private readonly TimeProvider clock;
    private readonly Action stateChanged;
    private PanelTab selectedTab=PanelTab.DailyTasks;
    private DateOnly selectedDate;
    private string taskInput=string.Empty;
    private string scheduleInput=string.Empty;
    private string scheduleTime="09:00";
    private string? errorText;

    public ControlPanelViewModel(
        OrganizerState state,
        OrganizerService organizer,
        UndoService undo,
        TimeProvider clock,
        Action<AnimationId> requestAction,
        Action stateChanged)
    {
        this.state=state;
        this.organizer=organizer;
        this.undo=undo;
        this.clock=clock;
        this.stateChanged=stateChanged;
        selectedDate=Today;
        AddTaskCommand=Command(_=>AddTask());
        DeleteTaskCommand=Command(value=>Mutate(()=>undo.DeleteTask(Require<Guid>(value))));
        SetTaskCommand=Command(value=>
        {
            var change=Require<TaskCompletionChange>(value);
            Mutate(()=>{ organizer.SetTaskCompleted(change.Id,change.Completed); return true; });
        });
        AddScheduleCommand=Command(_=>AddSchedule());
        DeleteScheduleCommand=Command(value=>Mutate(()=>undo.DeleteSchedule(Require<Guid>(value))));
        SetScheduleCommand=Command(value=>
        {
            var change=Require<ScheduleCompletionChange>(value);
            Mutate(()=>{ organizer.SetScheduleCompleted(change.Id,change.Completed); return true; });
        });
        UndoCommand=Command(_=>Mutate(undo.TryUndo));
        RequestActionCommand=Command(value=>requestAction(Require<AnimationId>(value)));
        PreviousDayCommand=Command(_=>SelectedDate=SelectedDate.AddDays(-1));
        NextDayCommand=Command(_=>SelectedDate=SelectedDate.AddDays(1));
        TodayCommand=Command(_=>SelectedDate=Today);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;

    public PanelTab SelectedTab { get=>selectedTab; set=>Set(ref selectedTab,value); }
    public DateOnly SelectedDate { get=>selectedDate; set { if(Set(ref selectedDate,value)) Refresh(); } }
    public string TaskInput { get=>taskInput; set=>Set(ref taskInput,value??string.Empty); }
    public string ScheduleInput { get=>scheduleInput; set=>Set(ref scheduleInput,value??string.Empty); }
    public string ScheduleTime { get=>scheduleTime; set=>Set(ref scheduleTime,value??string.Empty); }
    public string? ErrorText { get=>errorText; private set=>Set(ref errorText,value); }
    public IReadOnlyList<DailyTask> Tasks=>state.Tasks.OrderBy(x=>x.Order).ToArray();
    public IReadOnlyList<ScheduleItem> Schedules=>organizer.ForDate(SelectedDate);
    public string ProgressText=>$"已完成 {state.Tasks.Count(x=>x.IsCompleted)} / {state.Tasks.Count} · 未完成 {state.Tasks.Count(x=>!x.IsCompleted)}（最多 7 条）";
    public string ScheduleDateText=>SelectedDate==Today?$"{SelectedDate:yyyy-MM-dd} · 今天":SelectedDate.ToString("yyyy-MM-dd");
    public bool CanUndo=>undo.ExpiresIn>TimeSpan.Zero;
    public string UndoText=>$"撤销最近删除（{Math.Max(1,(int)Math.Ceiling(undo.ExpiresIn.TotalSeconds))} 秒）";

    public RelayCommand AddTaskCommand { get; }
    public RelayCommand DeleteTaskCommand { get; }
    public RelayCommand SetTaskCommand { get; }
    public RelayCommand AddScheduleCommand { get; }
    public RelayCommand DeleteScheduleCommand { get; }
    public RelayCommand SetScheduleCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RequestActionCommand { get; }
    public RelayCommand PreviousDayCommand { get; }
    public RelayCommand NextDayCommand { get; }
    public RelayCommand TodayCommand { get; }

    public bool IsOverdue(ScheduleItem item)=>organizer.IsOverdue(item,clock.GetLocalNow().LocalDateTime);

    public void ReportError(string message)=>ErrorText=message;

    public void Refresh()
    {
        OnPropertyChanged(nameof(Tasks));
        OnPropertyChanged(nameof(Schedules));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ScheduleDateText));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(UndoText));
        Changed?.Invoke();
    }

    private void AddTask()
    {
        organizer.AddTask(TaskInput);
        TaskInput=string.Empty;
        MutationCompleted();
    }

    private void AddSchedule()
    {
        if(!TimeOnly.TryParseExact(ScheduleTime,"HH:mm",out var time))
            throw new OrganizerValidationException("时间格式必须为 HH:mm。");
        organizer.AddSchedule(ScheduleInput,SelectedDate,time);
        ScheduleInput=string.Empty;
        MutationCompleted();
    }

    private void Mutate(Func<bool> mutation)
    {
        if(mutation()) MutationCompleted();
    }

    private void MutationCompleted()
    {
        ErrorText=null;
        stateChanged();
        Refresh();
    }

    private RelayCommand Command(Action<object?> execute)=>new(execute,HandleExpectedError);

    private bool HandleExpectedError(Exception error)
    {
        if(error is not OrganizerValidationException and not KeyNotFoundException and not FormatException) return false;
        ErrorText=error.Message;
        return true;
    }

    private DateOnly Today=>DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    private static T Require<T>(object? value)
        => value is T typed?typed:throw new ArgumentException($"Command parameter must be {typeof(T).Name}.",nameof(value));

    private bool Set<T>(ref T field,T value,[CallerMemberName] string? propertyName=null)
    {
        if(EqualityComparer<T>.Default.Equals(field,value)) return false;
        field=value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName=null)
        => PropertyChanged?.Invoke(this,new(propertyName));
}
