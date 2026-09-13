using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Charlotte.Windows.Assets;

public sealed class FrameBudgetExceededException(long bytes,long budget)
    : Exception($"Decoded frame requires {bytes} bytes, exceeding the {budget} byte cache budget.");

public sealed class FrameCache
{
    private const long DefaultBudgetBytes=96L*1024*1024;
    private readonly long byteBudget;
    private readonly object sync=new();
    private readonly Dictionary<(string Path,int Width),Entry> frames=[];
    private readonly LinkedList<(string Path,int Width)> recent=[];
    private long usedBytes;

    public FrameCache(long byteBudget=DefaultBudgetBytes)
    {
        if(byteBudget<=0) throw new ArgumentOutOfRangeException(nameof(byteBudget));
        this.byteBudget=byteBudget;
    }

    public async Task<BitmapSource> GetAsync(string path, int pixelWidth, CancellationToken cancellationToken)
    {
        if(pixelWidth<=0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        var full=path=="builtin:idle"?path:System.IO.Path.GetFullPath(path);
        var key=(full,pixelWidth);
        Entry entry;
        lock(sync)
        {
            if(frames.TryGetValue(key,out entry!))
            {
                recent.Remove(entry.Node); recent.AddLast(entry.Node);
            }
            else
            {
                var node=recent.AddLast(key);
                entry=new(new(()=>Task.Run(()=>Decode(key.full,key.pixelWidth)),LazyThreadSafetyMode.ExecutionAndPublication),node);
                frames.Add(key,entry);
            }
        }
        BitmapSource bitmap;
        try { bitmap=await entry.Bitmap.Value.WaitAsync(cancellationToken); }
        catch(OperationCanceledException) { throw; }
        catch
        {
            Remove(key,entry);
            throw;
        }
        RegisterSize(key,entry,bitmap);
        return bitmap;
    }

    private void RegisterSize((string Path,int Width) key,Entry entry,BitmapSource bitmap)
    {
        lock(sync)
        {
            if(entry.Rejected) throw new FrameBudgetExceededException(entry.Bytes,byteBudget);
            if(!frames.TryGetValue(key,out var current) || !ReferenceEquals(current,entry) || entry.Bytes>0) return;
            entry.Bytes=(long)bitmap.PixelWidth*bitmap.PixelHeight*Math.Max(1,bitmap.Format.BitsPerPixel/8);
            usedBytes+=entry.Bytes;
            if(entry.Bytes>byteBudget)
            {
                entry.Rejected=true;
                RemoveLocked(key,entry);
                throw new FrameBudgetExceededException(entry.Bytes,byteBudget);
            }
            var node=recent.First;
            while(usedBytes>byteBudget && node is not null)
            {
                var next=node.Next;
                if(frames.TryGetValue(node.Value,out var candidate) && candidate.Bytes>0 && !ReferenceEquals(candidate,entry))
                    RemoveLocked(node.Value,candidate);
                node=next;
            }
        }
    }

    private void Remove((string Path,int Width) key,Entry entry)
    {
        lock(sync)
            if(frames.TryGetValue(key,out var current) && ReferenceEquals(current,entry)) RemoveLocked(key,entry);
    }

    private void RemoveLocked((string Path,int Width) key,Entry entry)
    {
        frames.Remove(key); recent.Remove(entry.Node); usedBytes-=entry.Bytes;
    }

    private static BitmapSource Decode(string path,int width)
    {
        if(path=="builtin:idle") return CreateBuiltinIdle(width);
        var source=new BitmapImage(); source.BeginInit(); source.CacheOption=BitmapCacheOption.OnLoad; source.DecodePixelWidth=width; source.UriSource=new(path); source.EndInit(); source.Freeze();
        if(source.Format==PixelFormats.Pbgra32) return source;
        var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0); converted.Freeze(); return converted;
    }

    private static BitmapSource CreateBuiltinIdle(int width)
    {
        var height=Math.Max(1,(int)Math.Round(width*1.25));
        var stride=width*4;
        var pixels=new byte[stride*height];
        var scale=width/240d;
        PaintEllipse(pixels,width,height,stride,120*scale,92*scale,55*scale,62*scale,0x82,0xBD,0xD8);
        PaintEllipse(pixels,width,height,stride,120*scale,205*scale,50*scale,92*scale,0x3D,0x23,0x17);
        PaintEllipse(pixels,width,height,stride,120*scale,193*scale,38*scale,72*scale,0xA6,0x80,0x4C);
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,pixels,stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static void PaintEllipse(byte[] pixels,int width,int height,int stride,double centerX,double centerY,double radiusX,double radiusY,byte red,byte green,byte blue)
    {
        for(var y=Math.Max(0,(int)(centerY-radiusY));y<Math.Min(height,(int)Math.Ceiling(centerY+radiusY));y++)
        for(var x=Math.Max(0,(int)(centerX-radiusX));x<Math.Min(width,(int)Math.Ceiling(centerX+radiusX));x++)
        {
            var dx=(x+.5-centerX)/radiusX;
            var dy=(y+.5-centerY)/radiusY;
            if(dx*dx+dy*dy>1) continue;
            var offset=y*stride+x*4;
            pixels[offset]=blue; pixels[offset+1]=green; pixels[offset+2]=red; pixels[offset+3]=255;
        }
    }

    private sealed class Entry(Lazy<Task<BitmapSource>> bitmap,LinkedListNode<(string Path,int Width)> node)
    {
        public Lazy<Task<BitmapSource>> Bitmap { get; }=bitmap;
        public LinkedListNode<(string Path,int Width)> Node { get; }=node;
        public long Bytes { get; set; }
        public bool Rejected { get; set; }
    }
}
