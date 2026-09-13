using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    private const int CanvasWidth=480, CanvasHeight=600;
    private static readonly (string Name,int Count)[] Clips =
    [
        ("idle",8),("walk",8),("rest",8),("sleep",8),("click-soft",8),("click-annoyed",10),
        ("click-warning",12),("drag-start",1),("drag-hold",6),("drag-release",1),("battle",14),("victory",10)
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length!=2) { Console.Error.WriteLine("Usage: builder <base.png> <output-directory>"); return 2; }
        var source=Load(Path.GetFullPath(args[0]));
        var output=Path.GetFullPath(args[1]);
        foreach(var clip in Clips)
        {
            var directory=Path.Combine(output,clip.Name); Directory.CreateDirectory(directory);
            for(var frame=0;frame<clip.Count;frame++) Save(Render(source,clip.Name,frame,clip.Count),Path.Combine(directory,$"{frame+1:00}.png"));
        }
        Console.WriteLine($"Generated {Clips.Sum(x=>x.Count)} placeholder frames at {output}");
        return 0;
    }

    private static BitmapSource Load(string path)
    {
        var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.UriSource=new(path); image.EndInit(); image.Freeze(); return image;
    }

    private static RenderTargetBitmap Render(BitmapSource source,string clip,int frame,int count)
    {
        var phase=count==1?0:frame/(double)(count-1); var wave=Math.Sin(phase*Math.PI*2);
        var (dx,dy,angle,scaleX,scaleY)=Motion(clip,phase,wave);
        const double height=540;
        var width=height*source.PixelWidth/source.PixelHeight;
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen())
        {
            dc.PushTransform(new RotateTransform(angle,240,560));
            dc.DrawImage(source,new Rect((CanvasWidth-width*scaleX)/2+dx,560-height*scaleY+dy,width*scaleX,height*scaleY));
            dc.Pop();
        }
        var bitmap=new RenderTargetBitmap(CanvasWidth,CanvasHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }

    private static (double dx,double dy,double angle,double sx,double sy) Motion(string clip,double phase,double wave) => clip switch
    {
        "idle" => (wave*2,-Math.Abs(wave)*2,wave*.7,1,1+wave*.003),
        "walk" => (wave*6,-Math.Abs(wave)*4,wave*1.2,1,1),
        "rest" => (wave,-2,wave*.4,1,.98),
        "sleep" => (wave,-1,3+wave*.5,1,.965),
        "click-soft" => (wave*4,-Math.Sin(phase*Math.PI)*4,wave*2,1,1),
        "click-annoyed" => (-Math.Abs(wave)*5,0,-2+wave*1.5,1,1),
        "click-warning" => (wave*7,-Math.Abs(wave)*2,wave*3,1,1),
        "drag-start" => (0,-10,0,1,.98),
        "drag-hold" => (wave*3,-14+wave*2,wave*2,1,.98),
        "drag-release" => (0,0,0,1,1),
        "battle" => (Math.Sin(phase*Math.PI)*12,-Math.Sin(phase*Math.PI)*8,-8+phase*16,1,1),
        "victory" => (0,Math.Sin(phase*Math.PI)*8,wave*.8,1,1-Math.Sin(phase*Math.PI)*.035),
        _ => (0,0,0,1,1)
    };

    private static void Save(BitmapSource bitmap,string path)
    {
        using var stream=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
    }
}
