using System.Windows.Media.Imaging;

namespace Charlotte.Windows.Assets;

public sealed class FrameBudgetExceededException(long bytes,long budget)
    : Exception($"Decoded frame requires {bytes} bytes, exceeding the {budget} byte cache budget.");

public sealed class FrameCache
{
    private const long DefaultBudgetBytes=96L*1024*1024;
    private readonly long byteBudget;
    private readonly IFrameDecoder decoder;
    private readonly object sync=new();
    private readonly Dictionary<Key,Entry> frames=[];
    private readonly LinkedList<Key> recent=[];
    private long usedBytes;

    public FrameCache(long byteBudget=DefaultBudgetBytes,IFrameDecoder? decoder=null)
    {
        if(byteBudget<=0) throw new ArgumentOutOfRangeException(nameof(byteBudget));
        this.byteBudget=byteBudget;
        this.decoder=decoder??new FrameDecoder();
    }

    public async Task<BitmapSource> GetAsync(string path,int pixelWidth,CancellationToken cancellationToken)
        => (await GetFrameAsync(path,pixelWidth,16,cancellationToken)).Bitmap;

    public async Task<DecodedFrame> GetFrameAsync(string path,int pixelWidth,byte alphaThreshold,CancellationToken cancellationToken)
    {
        if(pixelWidth<=0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        var full=path=="builtin:idle"?path:System.IO.Path.GetFullPath(path);
        var key=new Key(full,pixelWidth,alphaThreshold);
        Entry entry;
        var created=false;
        lock(sync)
        {
            if(frames.TryGetValue(key,out entry!))
            {
                recent.Remove(entry.Node);
                recent.AddLast(entry.Node);
            }
            else
            {
                var node=recent.AddLast(key);
                entry=new(node,token=>decoder.DecodeAsync(key.Path,key.Width,key.AlphaThreshold,token));
                frames.Add(key,entry);
                created=true;
            }
        }
        if(created) _=TrackCompletionAsync(key,entry);
        DecodedFrame frame;
        try { frame=await entry.Frame.Value.WaitAsync(cancellationToken); }
        catch(OperationCanceledException)
        {
            if(entry.Frame.IsValueCreated && entry.Frame.Value.IsCanceled) Remove(key,entry);
            throw;
        }
        catch
        {
            Remove(key,entry);
            throw;
        }
        RegisterSize(key,entry,frame.EstimatedBytes);
        return frame;
    }

    private async Task TrackCompletionAsync(Key key,Entry entry)
    {
        try
        {
            var frame=await entry.Frame.Value.ConfigureAwait(false);
            RegisterSize(key,entry,frame.EstimatedBytes);
        }
        catch(FrameBudgetExceededException) { }
        catch { Remove(key,entry); }
    }

    public void EvictWidthsExcept(int pixelWidth)
    {
        if(pixelWidth<=0) throw new ArgumentOutOfRangeException(nameof(pixelWidth));
        lock(sync)
            foreach(var pair in frames.Where(x=>x.Key.Width!=pixelWidth).ToArray()) RemoveLocked(pair.Key,pair.Value,true);
    }

    public void CancelPending()
    {
        lock(sync)
            foreach(var pair in frames.Where(x=>x.Value.Bytes==0).ToArray()) RemoveLocked(pair.Key,pair.Value,true);
    }

    private void RegisterSize(Key key,Entry entry,long bytes)
    {
        lock(sync)
        {
            if(entry.Rejected) throw new FrameBudgetExceededException(entry.Bytes,byteBudget);
            if(!frames.TryGetValue(key,out var current) || !ReferenceEquals(current,entry) || entry.Bytes>0) return;
            entry.Bytes=bytes;
            usedBytes+=entry.Bytes;
            if(entry.Bytes>byteBudget)
            {
                entry.Rejected=true;
                RemoveLocked(key,entry,false);
                throw new FrameBudgetExceededException(entry.Bytes,byteBudget);
            }
            var node=recent.First;
            while(usedBytes>byteBudget && node is not null)
            {
                var next=node.Next;
                if(frames.TryGetValue(node.Value,out var candidate) && candidate.Bytes>0 && !ReferenceEquals(candidate,entry))
                    RemoveLocked(node.Value,candidate,false);
                node=next;
            }
        }
    }

    private void Remove(Key key,Entry entry)
    {
        lock(sync)
            if(frames.TryGetValue(key,out var current) && ReferenceEquals(current,entry)) RemoveLocked(key,entry,true);
    }

    private void RemoveLocked(Key key,Entry entry,bool cancelPending)
    {
        frames.Remove(key);
        recent.Remove(entry.Node);
        usedBytes-=entry.Bytes;
        entry.Dispose(cancelPending && entry.Bytes==0);
    }

    private readonly record struct Key(string Path,int Width,byte AlphaThreshold);

    private sealed class Entry
    {
        private readonly CancellationTokenSource cancellation=new();
        public Entry(LinkedListNode<Key> node,Func<CancellationToken,Task<DecodedFrame>> factory)
        {
            Node=node;
            Frame=new(()=>factory(cancellation.Token),LazyThreadSafetyMode.ExecutionAndPublication);
        }
        public Lazy<Task<DecodedFrame>> Frame { get; }
        public LinkedListNode<Key> Node { get; }
        public long Bytes { get; set; }
        public bool Rejected { get; set; }
        public void Dispose(bool cancel)
        {
            if(cancel) cancellation.Cancel();
            cancellation.Dispose();
        }
    }
}
