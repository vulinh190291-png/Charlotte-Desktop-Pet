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
    protected override async void OnStartup(StartupEventArgs e)
    {
        var startupMonitor = WindowsInterop.MonitorAt(WindowsInterop.Cursor);
        base.OnStartup(e);
        var assets=ManifestLoader.Load(System.IO.Path.Combine(AppContext.BaseDirectory,"assets"),System.IO.Path.Combine(AppContext.BaseDirectory,"config","animations.json"));
        var dataRoot=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CharlotteDesktopPet");
        var store=new JsonStateStore(dataRoot); var loaded=await store.LoadAsync(default);
        var window=new PetWindow(startupMonitor,e.Args.Contains("--diagnostic-shell",StringComparer.OrdinalIgnoreCase),loaded.Settings.XRatio) { Width=assets.DisplaySizeDip.Width,Height=assets.DisplaySizeDip.Height };
        MainWindow=window;
        presenter=new(window,assets,new FrameCache());
        coordinator=new(window,presenter,store,loaded.Data,loaded.Settings);
        window.Closed+=(_,_)=>coordinator.Dispose();
        window.Show(); presenter.Start();
        if(loaded.Warnings.Count>0) MessageBox.Show(string.Join(Environment.NewLine,loaded.Warnings),"Charlotte 数据恢复");
    }
}
