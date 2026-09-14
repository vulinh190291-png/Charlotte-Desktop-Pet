using Charlotte.Core.Animation;
using Charlotte.Core.Organizer;
using Charlotte.Windows.Services;
using Charlotte.Windows.ViewModels;

namespace Charlotte.Tests.Fixtures;

public sealed class PanelFixture : IDisposable
{
    private readonly TaskCompletionRouter completionRouter;

    private PanelFixture()
    {
        Clock=new(new DateTimeOffset(2026,9,14,8,0,0,TimeSpan.Zero));
        State=OrganizerState.Empty(new(2026,9,14));
        Undo=new(State,Clock);
        Organizer=new(State,Clock,()=>Undo.ReservedTaskSlots);
        ViewModel=new(State,Organizer,Undo,Clock,_=>ActionRequests++,()=>SaveRequests++);
        Host=new();
        Panel=new(Host,ViewModel,()=>OpenPreparations++,visible=>LastReportedVisibility=visible);
        completionRouter=new(Organizer,()=>Interactions++,()=>VictoryRequests++);
    }

    private ManualTimeProvider Clock { get; }
    public OrganizerState State { get; }
    public OrganizerService Organizer { get; }
    public UndoService Undo { get; }
    public ControlPanelViewModel ViewModel { get; }
    public FakePanelHost Host { get; }
    public PanelController Panel { get; }
    public int ActionRequests { get; private set; }
    public int Interactions { get; private set; }
    public int VictoryRequests { get; private set; }
    public int SaveRequests { get; private set; }
    public int OpenPreparations { get; private set; }
    public bool? LastReportedVisibility { get; private set; }

    public static PanelFixture Create()=>new();
    public void Open()=>Panel.Open();
    public void Close()=>Panel.Close();
    public void Dispose()=>completionRouter.Dispose();

    public sealed class FakePanelHost : IPanelHost
    {
        public bool IsVisible { get; private set; }
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public void Show() { IsVisible=true; ShowCount++; }
        public void Hide() { IsVisible=false; HideCount++; }
    }
}
