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
using Charlotte.Windows.ViewModels;
using Charlotte.Windows.Windows;

namespace Charlotte.Windows;

public sealed class AppCoordinator : IDisposable
{
    private readonly PetWindow pet;
    private readonly AnimationPresenter presenter;
    private readonly DiagnosticLog log;
    private readonly OrganizerState state;
    private AppSettings settings;
    private readonly StateSaveGate stateSaveGate;
    private readonly AutoStartService autoStart=new(new CurrentUserAutoStartRegistry());
    private readonly SaveQueue saveQueue;
    private readonly ShutdownSequence shutdown;
    private readonly TaskCompletionRouter taskCompletionRouter;
    private readonly DispatcherTimer dateTimer=new() { Interval=TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer visibilityTimer=new() { Interval=TimeSpan.FromMilliseconds(100) };
    private readonly VisibilityPolicy visibilityPolicy=new(TimeSpan.FromMilliseconds(200));
    private readonly long visibilityEpoch=Stopwatch.GetTimestamp();
    private ControlPanelWindow? panel;
    private ControlPanelViewModel? panelViewModel;
    private PanelController? panelController;
    private bool fullscreenHidden;
    public OrganizerService Organizer { get; }
    public UndoService Undo { get; }
    public OrganizerState State=>state;
    public bool AutoStartEnabled=>settings.AutoStart;
    public bool IsExiting=>shutdown.IsExiting;

    public AppCoordinator(PetWindow pet,AnimationPresenter presenter,IStateStore store,AppData data,AppSettings settings,DiagnosticLog log)
    {
        this.pet=pet; this.presenter=presenter; this.settings=settings; this.log=log;
        stateSaveGate=new(store);
        state=OrganizerState.Empty(data.LastResetDate); state.Tasks.AddRange(data.Tasks); state.Schedules.AddRange(data.Schedules); state.NextOrder=data.NextOrder;
        saveQueue=new(_=>SaveLatestAsync(),attempt=>TimeSpan.FromMilliseconds(attempt*150),(error,_)=>log.Write("save-failed",error));
        shutdown=new(async()=>
        {
            dateTimer.Stop(); visibilityTimer.Stop();
            await saveQueue.FlushAsync();
        },()=>{ panel?.Close(); pet.Close(); });
        Undo=new(state,TimeProvider.System); Organizer=new(state,TimeProvider.System,()=>Undo.ReservedTaskSlots);
        taskCompletionRouter=new(Organizer,presenter.NotifyInteraction,()=>presenter.Request(AnimationRequest.Victory()));
        pet.Clicked+=()=>presenter.Request(AnimationRequest.Click());
        pet.DragStarted+=()=>presenter.Request(AnimationRequest.DragStart());
        pet.DragEnded+=()=>{ presenter.Request(AnimationRequest.DragEnd()); QueueSave(); };
        pet.SystemStateChanged+=OnSystemStateChanged;
        pet.DisplayTopologyChanged+=ClampPanelIfNeeded;
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

    public void OpenPanel()
    {
        EnsurePanel();
        if(!panelController!.Open()) return;
        var scale=Math.Max(96,WindowsInterop.GetDpiForWindow(new WindowInteropHelper(pet).Handle))/96.0;
        var position=PositionPolicy.PlacePanel(pet.PixelBounds,new(panel!.Width*scale,panel.Height*scale),pet.CurrentMonitor.WorkArea,12*scale);
        WindowsInterop.Move(new WindowInteropHelper(panel).Handle,position);
        panel.Activate();
    }

    public void ClosePanel()=>panelController?.Close();

    public void TogglePanel()
    {
        if(panel?.IsVisible==true) ClosePanel();
        else OpenPanel();
    }

    public Task RequestExitAsync()=>shutdown.RequestAsync();

    public SessionEndingSaveResult SaveForSessionEnding(TimeSpan timeout)
    {
        dateTimer.Stop(); visibilityTimer.Stop();
        var snapshot=CreateSnapshot();
        return new SessionEndingSaver(
            token=>stateSaveGate.SaveAsync(snapshot.Data,snapshot.Settings,token),timeout).Save();
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
    private void OnSystemStateChanged()
    {
        if(IsExiting) return;
        CheckDate();
        EvaluateVisibility();
    }
    private void QueueSave()
    {
        if(IsExiting) return;
        saveQueue.Request();
    }
    private async Task SaveLatestAsync()
    {
        var snapshot=CreateSnapshot();
        await stateSaveGate.SaveAsync(snapshot.Data,snapshot.Settings,default);
    }
    private (AppData Data,AppSettings Settings) CreateSnapshot()
        => (new(1,[..state.Tasks],[..state.Schedules],state.LastResetDate,state.NextOrder),
            settings with { XRatio=pet.SaveXRatio() });
    private void RefreshPanel()=>panelViewModel?.Refresh();
    private void EnsurePanel()
    {
        if(panelController is not null) return;
        panelViewModel=new(state,Organizer,Undo,TimeProvider.System,RequestAction,QueueSave);
        panel=new(panelViewModel,()=>AutoStartEnabled,SetAutoStart,RequestExitAsync,ClosePanel)
        {
            Owner=pet,
            ShowInTaskbar=pet.ShowInTaskbar
        };
        panelController=new(panel,panelViewModel,
            ()=>{ CheckDate(); panelViewModel.Refresh(); },
            visible=>{ if(!IsExiting) presenter.SetPanelOpen(visible); });
    }
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
    private void ClampPanelIfNeeded()
    {
        if(panel?.IsVisible!=true) return;
        var handle=new WindowInteropHelper(panel).Handle;
        var bounds=WindowsInterop.Bounds(handle);
        var clamped=PositionPolicy.ClampPanelIfOutside(bounds,pet.CurrentMonitor.WorkArea);
        if(clamped is PxPoint position) WindowsInterop.Move(handle,position);
    }
    private nint PetHandle=>new WindowInteropHelper(pet).Handle;
    private nint PanelHandle=>panel is null?0:new WindowInteropHelper(panel).Handle;
    private void EvaluateVisibility()
    {
        if(IsExiting) return;
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
            ClosePanel();
            presenter.SetHidden(true);
            pet.Hide();
        }
        else
        {
            pet.Show();
            presenter.SetHidden(false);
        }
    }
    public void Dispose()
    {
        pet.SystemStateChanged-=OnSystemStateChanged;
        pet.DisplayTopologyChanged-=ClampPanelIfNeeded;
        dateTimer.Stop(); visibilityTimer.Stop(); taskCompletionRouter.Dispose(); presenter.Dispose();
    }
}
