using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Diagnostics;
using Charlotte.Core.Animation;
using Charlotte.Core.Geometry;
using Charlotte.Core.Organizer;
using Charlotte.Core.Persistence;
using Charlotte.Windows.Interop;
using Charlotte.Windows.Services;
using Charlotte.Windows.Windows;

namespace Charlotte.Windows;

public sealed class AppCoordinator : IDisposable
{
    private readonly PetWindow pet;
    private readonly AnimationPresenter presenter;
    private readonly IStateStore store;
    private readonly DiagnosticLog log;
    private readonly OrganizerState state;
    private AppSettings settings;
    private readonly AutoStartService autoStart=new(new CurrentUserAutoStartRegistry());
    private readonly SemaphoreSlim saveGate=new(1,1);
    private readonly object pendingGate=new();
    private readonly HashSet<Task> pendingSaves=[];
    private readonly DispatcherTimer dateTimer=new() { Interval=TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer visibilityTimer=new() { Interval=TimeSpan.FromMilliseconds(100) };
    private readonly VisibilityPolicy visibilityPolicy=new(TimeSpan.FromMilliseconds(200));
    private readonly long visibilityEpoch=Stopwatch.GetTimestamp();
    private ControlPanelWindow? panel;
    private bool exiting;
    private bool fullscreenHidden;
    public OrganizerService Organizer { get; }
    public UndoService Undo { get; }
    public OrganizerState State=>state;
    public bool AutoStartEnabled=>settings.AutoStart;

    public AppCoordinator(PetWindow pet,AnimationPresenter presenter,IStateStore store,AppData data,AppSettings settings,DiagnosticLog log)
    {
        this.pet=pet; this.presenter=presenter; this.store=store; this.settings=settings; this.log=log;
        state=OrganizerState.Empty(data.LastResetDate); state.Tasks.AddRange(data.Tasks); state.Schedules.AddRange(data.Schedules); state.NextOrder=data.NextOrder;
        Undo=new(state,TimeProvider.System); Organizer=new(state,TimeProvider.System,()=>Undo.ReservedTaskSlots);
        Organizer.TaskCompleted+=_=>{ presenter.Request(AnimationRequest.Victory()); QueueSave(); };
        pet.Clicked+=()=>presenter.Request(AnimationRequest.Click());
        pet.DragStarted+=()=>presenter.Request(AnimationRequest.DragStart());
        pet.DragEnded+=()=>{ presenter.Request(AnimationRequest.DragEnd()); QueueSave(); };
        pet.OpenManagement=TogglePanel;
        dateTimer.Tick+=(_,_)=>CheckDate(); dateTimer.Start();
        visibilityTimer.Tick+=(_,_)=>EvaluateVisibility(); visibilityTimer.Start();
        CheckDate();
        ReconcileAutoStart();
    }

    public void RequestAction(AnimationId id)=>presenter.Request(AnimationRequest.Panel(id));
    public void AddTask(string title) { Organizer.AddTask(title); QueueSave(); RefreshPanel(); }
    public void SetTask(Guid id,bool value) { Organizer.SetTaskCompleted(id,value); QueueSave(); RefreshPanel(); }
    public void DeleteTask(Guid id) { if(Undo.DeleteTask(id)) { QueueSave(); RefreshPanel(); } }
    public void AddSchedule(string title,DateOnly date,TimeOnly time) { Organizer.AddSchedule(title,date,time); QueueSave(); RefreshPanel(); }
    public void SetSchedule(Guid id,bool value) { Organizer.SetScheduleCompleted(id,value); QueueSave(); RefreshPanel(); }
    public void DeleteSchedule(Guid id) { if(Undo.DeleteSchedule(id)) { QueueSave(); RefreshPanel(); } }
    public void UndoDelete() { if(Undo.TryUndo()) { QueueSave(); RefreshPanel(); } }
    public AutoStartResult SetAutoStart(bool enabled)
    {
        var path=Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序路径。");
        var result=autoStart.SetEnabled(enabled,path);
        settings=settings with { AutoStart=result.Enabled };
        QueueSave();
        return result;
    }

    public void TogglePanel()
    {
        CheckDate();
        panel ??= new ControlPanelWindow(this) { Owner=pet,ShowInTaskbar=pet.ShowInTaskbar };
        if(panel.IsVisible) { panel.Hide(); return; }
        presenter.NotifyInteraction();
        panel.RefreshAll(); panel.Show();
        var scale=Math.Max(96,WindowsInterop.GetDpiForWindow(new WindowInteropHelper(pet).Handle))/96.0;
        var position=PositionPolicy.PlacePanel(pet.PixelBounds,new(panel.Width*scale,panel.Height*scale),pet.CurrentMonitor.WorkArea,12*scale);
        WindowsInterop.Move(new WindowInteropHelper(panel).Handle,position);
        panel.Activate();
    }

    public async Task RequestExitAsync()
    {
        if(exiting) return; exiting=true; dateTimer.Stop(); visibilityTimer.Stop();
        Task[] pending;
        lock(pendingGate) pending=[..pendingSaves];
        await Task.WhenAll(pending);
        await SaveWithRetryAsync();
        presenter.Dispose(); panel?.Close(); pet.Close();
    }

    public void Wake()
    {
        if(ForegroundInterop.IsFullscreenOn(pet.CurrentMonitor.Bounds,PetHandle,PanelHandle)) return;
        SetFullscreenHidden(false);
        pet.Topmost=false;
        pet.Topmost=true;
    }

    private void CheckDate()
    {
        if(DateRollover.Apply(state,DateOnly.FromDateTime(DateTime.Now))) { QueueSave(); RefreshPanel(); }
    }
    private void QueueSave()
    {
        if(exiting) return;
        var task=SaveWithRetryAsync();
        lock(pendingGate) pendingSaves.Add(task);
        _=ObserveSaveAsync(task);
    }
    private async Task ObserveSaveAsync(Task task)
    {
        await task;
        lock(pendingGate) pendingSaves.Remove(task);
    }
    private async Task<bool> SaveWithRetryAsync()
    {
        for(var attempt=1;attempt<=3;attempt++)
        {
            try { await SaveLatestAsync(); return true; }
            catch(Exception error)
            {
                log.Write("save-failed",error);
                if(attempt<3) await Task.Delay(TimeSpan.FromMilliseconds(attempt*150));
            }
        }
        return false;
    }
    private async Task SaveLatestAsync()
    {
        await saveGate.WaitAsync();
        try
        {
            var data=new AppData(1,[..state.Tasks],[..state.Schedules],state.LastResetDate,state.NextOrder);
            await store.SaveAsync(data,settings with { XRatio=pet.SaveXRatio() },default);
        }
        finally { saveGate.Release(); }
    }
    private void RefreshPanel()=>panel?.RefreshAll();
    private void ReconcileAutoStart()
    {
        try
        {
            var path=Environment.ProcessPath;
            if(string.IsNullOrWhiteSpace(path)) return;
            var actual=autoStart.IsEnabledFor(path);
            if(actual==settings.AutoStart) return;
            settings=settings with { AutoStart=actual };
            QueueSave();
        }
        catch(Exception error) { log.Write("autostart-read-failed",error); }
    }
    private nint PetHandle=>new WindowInteropHelper(pet).Handle;
    private nint PanelHandle=>panel is null?0:new WindowInteropHelper(panel).Handle;
    private void EvaluateVisibility()
    {
        if(exiting) return;
        var fullscreen=ForegroundInterop.IsFullscreenOn(pet.CurrentMonitor.Bounds,PetHandle,PanelHandle);
        var hidden=visibilityPolicy.Observe(fullscreen,Stopwatch.GetElapsedTime(visibilityEpoch));
        SetFullscreenHidden(hidden);
    }
    private void SetFullscreenHidden(bool hidden)
    {
        if(fullscreenHidden==hidden) return;
        fullscreenHidden=hidden;
        if(hidden)
        {
            panel?.Hide();
            presenter.SetHidden(true);
            pet.Hide();
        }
        else
        {
            pet.Show();
            presenter.SetHidden(false);
        }
    }
    public void Dispose() { dateTimer.Stop(); visibilityTimer.Stop(); presenter.Dispose(); saveGate.Dispose(); }
}
