using System.Windows;
using Charlotte.Windows.Interop;
using Charlotte.Windows.Assets;
using Charlotte.Windows.Services;
using Charlotte.Windows.Windows;
using Charlotte.Core.Animation;

namespace Charlotte.Windows;
public partial class App : Application
{
    private AnimationPresenter? presenter;
    private AppCoordinator? coordinator;
    private SingleInstanceService? singleInstance;
    private DiagnosticLog? log;
    private bool sessionEnding;
    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupMonitor = WindowsInterop.MonitorAt(WindowsInterop.Cursor);
        base.OnStartup(e);
        var defaultDataRoot=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CharlotteDesktopPet");
        var options=StartupOptions.Parse(e.Args,defaultDataRoot,StartupOptions.FindProjectRoot(AppContext.BaseDirectory));
        var dataRoot=options.DataRoot;
        log=new DiagnosticLog(dataRoot);
        var emergencySave=new EmergencySaveHandler(
            log,
            timeout=>coordinator?.SaveForSessionEnding(timeout),
            TimeSpan.FromSeconds(2));
        singleInstance=new SingleInstanceService();
        var acquired=singleInstance.TryAcquire();
        if(!acquired)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
            if(!await singleInstance.NotifyExistingAsync(timeout.Token)) log.Write("single-instance-notify-failed");
            singleInstance.Dispose();
            Shutdown();
            return;
        }
        singleInstance.StartListening();
        var assetLoad=ManifestLoader.LoadResilient(System.IO.Path.Combine(AppContext.BaseDirectory,"assets"),System.IO.Path.Combine(AppContext.BaseDirectory,"config","animations.json"));
        var assets=assetLoad.Assets;
        foreach(var issue in assetLoad.Issues) log.Write($"asset-{issue.Code}");
        DispatcherUnhandledException+=(_,args)=>emergencySave.Handle("dispatcher-unhandled",args.Exception);
        AppDomain.CurrentDomain.UnhandledException+=(_,args)=>emergencySave.Handle("domain-unhandled",args.ExceptionObject as Exception);
        var store=new JsonStateStore(dataRoot); var loaded=await store.LoadAsync(default);
        var footOffsetDip=assets.FootAnchor.Y*assets.DisplaySizeDip.Height/assets.LogicalCanvas.Height;
        var window=new PetWindow(startupMonitor,footOffsetDip,options.DiagnosticShell,loaded.Settings.XRatio) { Width=assets.DisplaySizeDip.Width,Height=assets.DisplaySizeDip.Height };
        MainWindow=window;
        window.Show();
        presenter=new(window,assets,new FrameCache());
        coordinator=new(window,presenter,store,loaded.Data,loaded.Settings,log);
        window.Closing+=async (_,args)=>
        {
            if(sessionEnding || coordinator.IsExiting) return;
            args.Cancel=true;
            try { await coordinator.RequestExitAsync(); }
            catch(Exception error) { log.Write("shutdown-failed",error); Shutdown(-1); }
        };
        singleInstance.WakeRequested+=()=>Dispatcher.BeginInvoke(coordinator.Wake);
        window.Closed+=(_,_)=>{ coordinator.Dispose(); singleInstance.Dispose(); };
        presenter.Start();
        if(options.StartAction is AnimationId startAction) presenter.Request(AnimationRequest.Panel(startAction));
        if(options.StartHidden) { presenter.SetHidden(true); window.Hide(); }
        if(loaded.Warnings.Count>0) MessageBox.Show(string.Join(Environment.NewLine,loaded.Warnings),"Charlotte 数据恢复");
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        sessionEnding=true;
        if(coordinator is not null)
        {
            var result=coordinator.SaveForSessionEnding(TimeSpan.FromSeconds(2));
            if(result.Status==SessionEndingSaveStatus.TimedOut) log?.Write("session-ending-save-timeout");
            else if(result.Status==SessionEndingSaveStatus.Failed) log?.Write("session-ending-save-failed",result.Error);
        }
        base.OnSessionEnding(e);
    }
}
