using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Charlotte.Core.Animation;
using Charlotte.Core.Geometry;
using Charlotte.Windows.Interop;

namespace Charlotte.Windows.Windows;

public partial class EffectWindow : Window
{
    private const double PaddingDip=60;
    private nint hwnd;

    public EffectWindow(PetWindow owner)
    {
        Owner=owner;
        InitializeComponent();
        SourceInitialized+=(_,_)=>
        {
            hwnd=new WindowInteropHelper(this).Handle;
            WindowsInterop.MakeEffectWindow(hwnd);
        };
    }

    public void Render(IReadOnlyList<EffectCue> cues,PxRect petBounds,double scale)
    {
        if(cues.Count==0)
        {
            if(IsVisible) Hide();
            return;
        }
        if(!IsVisible) Show();
        var widthDip=petBounds.Width/scale+PaddingDip*2;
        var heightDip=petBounds.Height/scale+PaddingDip*2;
        Width=widthDip;
        Height=heightDip;
        WindowsInterop.MoveAndResize(hwnd,new(
            petBounds.Left-PaddingDip*scale,
            petBounds.Top-PaddingDip*scale,
            widthDip*scale,
            heightDip*scale));

        Layer.Children.Clear();
        foreach(var cue in cues)
        {
            var element=Create(cue);
            element.Opacity=cue.Opacity;
            element.RenderTransformOrigin=new(.5,.5);
            element.RenderTransform=new TransformGroup
            {
                Children=
                {
                    new ScaleTransform(cue.Scale,cue.Scale),
                    new RotateTransform(cue.Rotation)
                }
            };
            Canvas.SetLeft(element,PaddingDip+cue.X);
            Canvas.SetTop(element,PaddingDip+cue.Y);
            Layer.Children.Add(element);
        }
    }

    private static FrameworkElement Create(EffectCue cue)
        => cue.Kind switch
        {
            EffectKind.SleepBubble=>Bubble(cue.Text??"Z",34),
            EffectKind.DragBubble=>Bubble(cue.Text??"!",26),
            EffectKind.Rose=>new TextBlock { Text="✿",FontSize=30,Foreground=new SolidColorBrush(Color.FromRgb(166,54,76)),FontWeight=FontWeights.Bold },
            EffectKind.Petal=>new Ellipse { Width=12,Height=6,Fill=new SolidColorBrush(Color.FromRgb(203,92,118)) },
            _=>new TextBlock { Text="✦",FontSize=24,Foreground=new SolidColorBrush(Color.FromRgb(255,214,102)),FontWeight=FontWeights.Bold }
        };

    private static FrameworkElement Bubble(string text,double fontSize)
        => new Border
        {
            Background=new SolidColorBrush(Color.FromArgb(210,255,255,255)),
            BorderBrush=new SolidColorBrush(Color.FromRgb(90,111,159)),
            BorderThickness=new(1.5),
            CornerRadius=new(14),
            Padding=new(9,4,9,4),
            Child=new TextBlock { Text=text,FontSize=fontSize,Foreground=new SolidColorBrush(Color.FromRgb(38,53,88)),FontWeight=FontWeights.SemiBold }
        };
}
