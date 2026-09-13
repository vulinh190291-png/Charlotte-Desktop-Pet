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
    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupMonitor = WindowsInterop.MonitorAt(WindowsInterop.Cursor);
        base.OnStartup(e);
        var defaultDataRoot=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CharlotteDesktopPet");
        var options=StartupOptions.Parse(e.Args,defaultDataRoot,StartupOptions.FindProjectRoot(AppContext.BaseDirectory));
        singleInstance=new SingleInstanceService();
        var acquired=singleInstance.TryAcquire();
        if(!acquired)
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await singleInstance.NotifyExistingAsync(timeout.Token);
            singleInstance.Dispose();
            Shutdown();
            return;
        }
        singleInstance.StartListening();
        var assets=ManifestLoader.Load(System.IO.Path.Combine(AppContext.BaseDirectory,"assets"),System.IO.Path.Combine(AppContext.BaseDirectory,"config","animations.json"));
        var dataRoot=options.DataRoot;
        log=new DiagnosticLog(dataRoot);
        DispatcherUnhandledException+=(_,args)=>log.Write("dispatcher-unhandled",args.Exception);
        AppDomain.CurrentDomain.UnhandledException+=(_,args)=>log.Write("domain-unhandled",args.ExceptionObject as Exception);
        var store=new JsonStateStore(dataRoot); var loaded=await store.LoadAsync(default);
        var window=new PetWindow(startupMonitor,options.DiagnosticShell,loaded.Settings.XRatio) { Width=assets.DisplaySizeDip.Width,Height=assets.DisplaySizeDip.Height };
        MainWindow=window;
        presenter=new(window,assets,new FrameCache());
        window.Show();
        coordinator=new(window,presenter,store,loaded.Data,loaded.Settings,log);
        window.Closing+=async (_,args)=>
        {
            if(coordinator.IsExiting) return;
            args.Cancel=true;
            try { await coordinator.RequestExitAsync(); }
            catch(Exception error) { log.Write("shutdown-failed",error); Shutdown(-1); }
        };
        singleInstance.WakeRequested+=()=>Dispatcher.BeginInvoke(coordinator.Wake);
        window.Closed+=(_,_)=>{ coordinator.Dispose(); singleInstance.Dispose(); };
        presenter.Start();
        if(options.StartHidden) { presenter.SetHidden(true); window.Hide(); }
        if(loaded.Warnings.Count>0) MessageBox.Show(string.Join(Environment.NewLine,loaded.Warnings),"Charlotte 数据恢复");
    }
}
