using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Charlotte.Core.Geometry;
using Charlotte.Windows.Interop;
using Charlotte.Windows.Services;
namespace Charlotte.Windows.Windows;
public partial class PetWindow : Window
{
    private readonly MonitorSnapshot startupMonitor;
    private readonly double? startupXRatio;
    private nint hwnd;
    private AlphaMask? mask;
    private PxPoint? pressed;
    private PxPoint grab;
    private bool dragging;
    private PlacementMode mode;
    private Window? panel;
    private readonly bool diagnosticShell;
    private readonly DispatcherTimer hitTestTimer=new() { Interval=TimeSpan.FromMilliseconds(16) };
    private bool clickThrough;
    public event Action? Clicked;
    public event Action? DragStarted;
    public event Action? DragEnded;
    public event Action? SystemStateChanged;
    public Action? OpenManagement { get; set; }
    public PxRect PixelBounds => WindowsInterop.Bounds(hwnd);
    public MonitorSnapshot CurrentMonitor => WindowsInterop.MonitorAt(new(PixelBounds.Left+PixelBounds.Width/2,PixelBounds.Top+280*Scale));
    public (double Left,double Right) HorizontalWalkSpaceDip
    {
        get
        {
            if(hwnd==0) return (0,0);
            var bounds=PixelBounds;
            var area=CurrentMonitor.WorkArea;
            return (Math.Max(0,(bounds.Left-area.Left)/Scale),Math.Max(0,(area.Right-bounds.Right)/Scale));
        }
    }
    private double Scale => Math.Max(96,WindowsInterop.GetDpiForWindow(hwnd))/96.0;
    public PetWindow(MonitorSnapshot monitor, bool diagnosticShell = false, double? startupXRatio = null)
    {
        startupMonitor = monitor;
        this.startupXRatio = startupXRatio;
        this.diagnosticShell = diagnosticShell;
        InitializeComponent();
        ShowInTaskbar = diagnosticShell;
        SourceInitialized += (_,_) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WindowMessage);
        };
        Loaded += (_,_) =>
        {
            WindowsInterop.Move(hwnd,new(PositionPolicy.RestoreX(startupXRatio,monitor.WorkArea.Left,monitor.WorkArea.Width,ActualWidth*Scale),monitor.WorkArea.Bottom-280*Scale));
            RefreshMask(); SetClickThrough(false); hitTestTimer.Start(); UpdateClickThrough();
        };
        hitTestTimer.Tick+=(_,_)=>UpdateClickThrough();
        MouseLeftButtonDown += OnDown;
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        LostMouseCapture += (_,_) => { if (dragging) EndDrag(); Dispatcher.BeginInvoke(UpdateClickThrough); };
        MouseRightButtonUp += (_,e) => { if (OpenManagement != null) OpenManagement(); else TogglePrototypePanel(); e.Handled=true; };
        Closed += (_,_) => { hitTestTimer.Stop(); panel?.Close(); };
    }
    public double SaveXRatio()
    {
        var bounds=PixelBounds; var monitor=CurrentMonitor;
        return PositionPolicy.SaveRatio(bounds.Left,monitor.WorkArea.Left,monitor.WorkArea.Width,bounds.Width);
    }
    public void MoveAmbientBy(double deltaDip)
    {
        if(hwnd==0 || !double.IsFinite(deltaDip) || Math.Abs(deltaDip)<.001) return;
        var bounds=PixelBounds;
        var area=CurrentMonitor.WorkArea;
        var left=Math.Clamp(bounds.Left+deltaDip*Scale,area.Left,Math.Max(area.Left,area.Right-bounds.Width));
        WindowsInterop.Move(hwnd,new(left,bounds.Top));
    }
    public void ClosePanel() { if(panel?.IsVisible==true) panel.Hide(); }
    public void ShowFrame(BitmapSource bitmap)
    {
        if (bitmap.Format != PixelFormats.Pbgra32) bitmap = new FormatConvertedBitmap(bitmap,PixelFormats.Pbgra32,null,0);
        int stride = bitmap.PixelWidth*4;
        byte[] bytes=new byte[stride*bitmap.PixelHeight]; bitmap.CopyPixels(bytes,stride,0);
        for(int i=0;i<bytes.Length;i+=4) if(bytes[i+3]<16) Array.Clear(bytes,i,4);
        bitmap=BitmapSource.Create(bitmap.PixelWidth,bitmap.PixelHeight,bitmap.DpiX,bitmap.DpiY,PixelFormats.Pbgra32,null,bytes,stride);
        bitmap.Freeze();
        Body.Children.Clear();
        var image = new Image { Source=bitmap,Stretch=Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
        Body.Children.Add(image); Body.UpdateLayout(); RefreshMask();
    }
    private void RefreshMask()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
        bitmap.Render(Body);
        int stride=bitmap.PixelWidth*4;
        byte[] pixels=new byte[stride*bitmap.PixelHeight]; bitmap.CopyPixels(pixels,stride,0);
        mask=AlphaMask.Create(pixels,bitmap.PixelWidth,bitmap.PixelHeight,stride,16);
    }
    private void OnDown(object sender,MouseButtonEventArgs e)
    {
        SetClickThrough(false);
        var eventPoint=PointToScreen(e.GetPosition(this));
        pressed=new(eventPoint.X,eventPoint.Y); var bounds=PixelBounds;
        grab=new((pressed.Value.X-bounds.Left)/Scale,(pressed.Value.Y-bounds.Top)/Scale);
        CaptureMouse(); e.Handled=true;
    }
    private void OnMove(object sender,MouseEventArgs e)
    {
        UpdateClickThrough();
        if (pressed is not PxPoint start || e.LeftButton != MouseButtonState.Pressed) return;
        var eventPoint=PointToScreen(e.GetPosition(this));
        var cursor=new PxPoint(eventPoint.X,eventPoint.Y);
        if (!dragging && DragGesture.HasCrossedThreshold(start,cursor,SystemParameters.MinimumHorizontalDragDistance*Scale,SystemParameters.MinimumVerticalDragDistance*Scale))
        { dragging=true; DragStarted?.Invoke(); }
        if (!dragging) return;
        WindowsInterop.Move(hwnd,DragGesture.WindowOrigin(cursor,grab,Scale));
    }
    private void OnUp(object sender,MouseButtonEventArgs e)
    {
        bool wasDragging=dragging;
        if (dragging) EndDrag();
        pressed=null; ReleaseMouseCapture();
        if (!wasDragging) Clicked?.Invoke();
        UpdateClickThrough();
        e.Handled=true;
    }
    private void EndDrag()
    {
        dragging=false; pressed=null; mode=PlacementMode.FreePlaced; Reflow(); DragEnded?.Invoke();
    }
    private void Reflow()
    {
        var b=PixelBounds; var monitor=CurrentMonitor;
        var next=new PetPlacement(new(b.Left,b.Top),b.Top+280*Scale,mode,monitor.Id)
            .Reflow(monitor.WorkArea,new(b.Width,b.Height),280*Scale);
        WindowsInterop.Move(hwnd,next.Origin); RefreshMask();
    }
    private void UpdateClickThrough()
    {
        if(hwnd==0 || !IsVisible) return;
        SetClickThrough(PointerTransparency.ShouldClickThrough(mask,PixelBounds,WindowsInterop.Cursor,Scale,IsMouseCaptured));
    }
    private void SetClickThrough(bool enabled)
    {
        if(clickThrough==enabled) return;
        WindowsInterop.ClickThrough(hwnd,enabled);
        clickThrough=enabled;
    }
    private nint WindowMessage(nint h,int message,nint w,nint l,ref bool handled)
    {
        if (DesktopMessagePolicy.ReflowsWindow(message))
            Dispatcher.BeginInvoke(() => { if (!dragging) Reflow(); else RefreshMask(); });
        if (DesktopMessagePolicy.RefreshesApplicationState(message,w.ToInt64()))
            Dispatcher.BeginInvoke(() => SystemStateChanged?.Invoke());
        return 0;
    }
    private void TogglePrototypePanel()
    {
        if (panel?.IsVisible == true) { panel.Hide(); return; }
        var stack=new StackPanel { Margin=new Thickness(20) };
        stack.Children.Add(new TextBlock { Text="Charlotte · 桌面能力验证",Margin=new Thickness(0,0,0,12) });
        var close=new Button { Content="退出程序",Padding=new Thickness(16,8,16,8) };
        close.Click += (_,_) => Close(); stack.Children.Add(close);
        panel=new Window { Owner=this,Title="Charlotte 管理",Content=stack,Width=300,Height=160,Topmost=true,ShowInTaskbar=diagnosticShell,ResizeMode=ResizeMode.NoResize };
        panel.KeyDown += (_,e) => { if (e.Key==Key.Escape) panel.Close(); };
        panel.Deactivated += (_,_) => panel?.Close();
        panel.Show();
        var p=PositionPolicy.PlacePanel(PixelBounds,new(300*Scale,160*Scale),CurrentMonitor.WorkArea,12*Scale);
        WindowsInterop.Move(new WindowInteropHelper(panel).Handle,p);
    }
}
