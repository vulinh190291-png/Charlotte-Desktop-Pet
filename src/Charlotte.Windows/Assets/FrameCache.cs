using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Charlotte.Windows.Assets;

public sealed class FrameCache
{
    private readonly ConcurrentDictionary<(string Path,int Width),Lazy<Task<BitmapSource>>> frames = new();

    public Task<BitmapSource> GetAsync(string path, int pixelWidth, CancellationToken cancellationToken)
    {
        if(pixelWidth<=0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        var full=System.IO.Path.GetFullPath(path);
        var task=frames.GetOrAdd((full,pixelWidth),key=>new(()=>Task.Run(()=>Decode(key.Path,key.Width)),LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        return task.WaitAsync(cancellationToken);
    }

    private static BitmapSource Decode(string path,int width)
    {
        var source=new BitmapImage(); source.BeginInit(); source.CacheOption=BitmapCacheOption.OnLoad; source.DecodePixelWidth=width; source.UriSource=new(path); source.EndInit(); source.Freeze();
        if(source.Format==PixelFormats.Pbgra32) return source;
        var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0); converted.Freeze(); return converted;
    }
}
