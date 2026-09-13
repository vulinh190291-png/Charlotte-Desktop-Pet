using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
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
    private readonly OrganizerState state;
    private readonly AppSettings settings;
    private readonly SemaphoreSlim saveGate=new(1,1);
    private readonly DispatcherTimer dateTimer=new() { Interval=TimeSpan.FromMinutes(1) };
    private ControlPanelWindow? panel;
    private bool exiting;
    public OrganizerService Organizer { get; }
    public UndoService Undo { get; }
    public OrganizerState State=>state;

    public AppCoordinator(PetWindow pet,AnimationPresenter presenter,IStateStore store,AppData data,AppSettings settings)
    {
        this.pet=pet; this.presenter=presenter; this.store=store; this.settings=settings;
        state=OrganizerState.Empty(data.LastResetDate); state.Tasks.AddRange(data.Tasks); state.Schedules.AddRange(data.Schedules); state.NextOrder=data.NextOrder;
        Undo=new(state,TimeProvider.System); Organizer=new(state,TimeProvider.System,()=>Undo.ReservedTaskSlots);
        Organizer.TaskCompleted+=_=>{ presenter.Request(AnimationRequest.Victory()); QueueSave(); };
        pet.Clicked+=()=>presenter.Request(AnimationRequest.Click());
        pet.DragStarted+=()=>presenter.Request(AnimationRequest.DragStart());
        pet.DragEnded+=()=>{ presenter.Request(AnimationRequest.DragEnd()); QueueSave(); };
        pet.OpenManagement=TogglePanel;
        dateTimer.Tick+=(_,_)=>CheckDate(); dateTimer.Start();
        CheckDate();
    }

    public void RequestAction(AnimationId id)=>presenter.Request(AnimationRequest.Panel(id));
    public void AddTask(string title) { Organizer.AddTask(title); QueueSave(); RefreshPanel(); }
    public void SetTask(Guid id,bool value) { Organizer.SetTaskCompleted(id,value); QueueSave(); RefreshPanel(); }
    public void DeleteTask(Guid id) { if(Undo.DeleteTask(id)) { QueueSave(); RefreshPanel(); } }
    public void AddSchedule(string title,DateOnly date,TimeOnly time) { Organizer.AddSchedule(title,date,time); QueueSave(); RefreshPanel(); }
    public void SetSchedule(Guid id,bool value) { Organizer.SetScheduleCompleted(id,value); QueueSave(); RefreshPanel(); }
    public void DeleteSchedule(Guid id) { if(Undo.DeleteSchedule(id)) { QueueSave(); RefreshPanel(); } }
    public void UndoDelete() { if(Undo.TryUndo()) { QueueSave(); RefreshPanel(); } }

    public void TogglePanel()
    {
        CheckDate();
        panel ??= new ControlPanelWindow(this) { Owner=pet,ShowInTaskbar=pet.ShowInTaskbar };
        if(panel.IsVisible) { panel.Hide(); return; }
        panel.RefreshAll(); panel.Show();
        var scale=Math.Max(96,WindowsInterop.GetDpiForWindow(new WindowInteropHelper(pet).Handle))/96.0;
        var position=PositionPolicy.PlacePanel(pet.PixelBounds,new(panel.Width*scale,panel.Height*scale),pet.CurrentMonitor.WorkArea,12*scale);
        WindowsInterop.Move(new WindowInteropHelper(panel).Handle,position);
        panel.Activate();
    }

    public async Task RequestExitAsync()
    {
        if(exiting) return; exiting=true; dateTimer.Stop();
        await SaveLatestAsync(); presenter.Dispose(); panel?.Close(); pet.Close();
    }

    private void CheckDate()
    {
        if(DateRollover.Apply(state,DateOnly.FromDateTime(DateTime.Now))) { QueueSave(); RefreshPanel(); }
    }
    private void QueueSave()=>_=SaveLatestAsync();
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
    public void Dispose() { dateTimer.Stop(); presenter.Dispose(); saveGate.Dispose(); }
}
