using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Charlotte.AssetPrep;

internal static class Program
{
    private const int CanvasWidth=480;
    private const int CanvasHeight=600;
    private const double CenterX=240;
    private const double GroundY=560;
    private const double ContentWidth=448;
    private const double ContentHeight=540;

    private static readonly IReadOnlyList<PlanEntry> Plan=BuildPlan();

    [STAThread]
    private static int Main(string[] args)
    {
        if(args.Length==1 && args[0]=="--list")
        {
            foreach(var item in Plan)
                Console.WriteLine(item.ProjectOverrideRelative is null
                    ? $"{item.SourceRelative} -> {item.OutputRelative}"
                    : $"{item.ProjectOverrideRelative.Replace('\\','/')} => {item.OutputRelative}");
            Console.WriteLine($"Mapped frames: {Plan.Count}");
            return Plan.Count==77?0:1;
        }
        if(args.Length!=2)
        {
            Console.Error.WriteLine("Usage: Charlotte.AssetPrep <reference-root> <output-root>");
            Console.Error.WriteLine("       Charlotte.AssetPrep --list");
            return 2;
        }

        try
        {
            Prepare(Path.GetFullPath(args[0]),Path.GetFullPath(args[1]));
            Console.WriteLine($"Prepared {Plan.Count} formal frames at {Path.GetFullPath(args[1])}");
            return 0;
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static void Prepare(string sourceRoot,string outputRoot)
    {
        if(!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException($"Reference root does not exist: {sourceRoot}");
        var parent=Directory.GetParent(outputRoot)?.FullName??throw new InvalidDataException("Output root requires a parent directory.");
        Directory.CreateDirectory(parent);
        var staging=Path.Combine(parent,$".{Path.GetFileName(outputRoot)}.staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            var records=new List<SourceRecord>(Plan.Count);
            foreach(var item in Plan)
            {
                var sourcePath=item.ProjectOverrideRelative is null
                    ? Path.GetFullPath(Path.Combine(sourceRoot,item.SourceRelative))
                    : Path.GetFullPath(Path.Combine(parent,item.ProjectOverrideRelative));
                var expectedRoot=item.ProjectOverrideRelative is null?sourceRoot:parent;
                if(!sourcePath.StartsWith(expectedRoot.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Mapped source escapes its root: {item.SourceRelative}");
                if(!File.Exists(sourcePath)) throw new FileNotFoundException($"Mapped source is missing: {item.ProjectOverrideRelative??item.SourceRelative}");

                var source=LoadAlphaPng(sourcePath);
                var outputPath=Path.Combine(staging,item.OutputRelative);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                Save(Render(source,item),outputPath);
                records.Add(new(
                    item.ProjectOverrideRelative is null?item.SourceRelative.Replace('\\','/'):$"project:{item.ProjectOverrideRelative.Replace('\\','/')}",
                    item.OutputRelative.Replace('\\','/'),
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant(),
                    source.PixelWidth,
                    source.PixelHeight,
                    CanvasWidth,
                    CanvasHeight,
                    CenterX,
                    GroundY,
                    "family-fixed"));
            }

            var map=new SourceMap(1,"formal-v1",Plan.Count,records);
            File.WriteAllText(Path.Combine(staging,"source-map.json"),JsonSerializer.Serialize(map,new JsonSerializerOptions { WriteIndented=true }));

            string? previous=null;
            if(Directory.Exists(outputRoot))
            {
                previous=Path.Combine(parent,$".{Path.GetFileName(outputRoot)}.previous-{Guid.NewGuid():N}");
                Directory.Move(outputRoot,previous);
            }
            try { Directory.Move(staging,outputRoot); }
            catch
            {
                if(previous is not null && Directory.Exists(previous) && !Directory.Exists(outputRoot)) Directory.Move(previous,outputRoot);
                throw;
            }
            if(previous is not null && Directory.Exists(previous)) Directory.Delete(previous,true);
        }
        finally
        {
            if(Directory.Exists(staging)) Directory.Delete(staging,true);
        }
    }

    private static BitmapSource LoadAlphaPng(string path)
    {
        var image=new BitmapImage();
        image.BeginInit();
        image.CacheOption=BitmapCacheOption.OnLoad;
        image.CreateOptions=BitmapCreateOptions.PreservePixelFormat;
        image.UriSource=new(path);
        image.EndInit();
        image.Freeze();
        if(!Path.GetExtension(path).Equals(".png",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"Source is not PNG: {path}");
        BitmapSource converted=image.Format==PixelFormats.Bgra32?image:new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
        var stride=converted.PixelWidth*4;
        var pixels=new byte[stride*converted.PixelHeight];
        converted.CopyPixels(pixels,stride,0);
        var transparent=false;
        var visible=false;
        for(var i=3;i<pixels.Length;i+=4)
        {
            transparent|=pixels[i]==0;
            visible|=pixels[i]>0;
            if(transparent&&visible) break;
        }
        if(!transparent||!visible) throw new InvalidDataException($"Source must contain transparent and visible pixels: {path}");
        return image;
    }

    private static RenderTargetBitmap Render(BitmapSource source,PlanEntry item)
    {
        var correction=Math.Min(1,Math.Min(item.CanonicalWidth/source.PixelWidth,item.CanonicalHeight/source.PixelHeight));
        var canonicalScale=Math.Min(ContentWidth/item.CanonicalWidth,ContentHeight/item.CanonicalHeight);
        var width=source.PixelWidth*correction*canonicalScale;
        var height=source.PixelHeight*correction*canonicalScale;
        var left=CenterX-width/2;
        var visual=new DrawingVisual();
        using(var drawing=visual.RenderOpen())
            drawing.DrawImage(source,new Rect(left,GroundY-height,width,height));
        var result=new RenderTargetBitmap(CanvasWidth,CanvasHeight,96,96,PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    private static void Save(BitmapSource image,string path)
    {
        using var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);
        var encoder=new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(stream);
    }

    private static IReadOnlyList<PlanEntry> BuildPlan()
    {
        var items=new List<PlanEntry>();
        Add(items,"Idle/Idle/idle_{0:00}.png","idle",1,8);
        AddWalk(items);
        Add(items,"Rest/Rest/rest_{0:00}.png","rest",1,8);
        AddSleep(items,"Sleep_Enter","sleep-enter",5);
        AddSleep(items,"Sleep_Loop","sleep",8);
        AddSleep(items,"Sleep_Exit","sleep-exit",5);
        AddDrag(items);
        AddBattle(items);
        Add(items,"Victory/Victory_10_Frames/victory_{0:00}_base.png","victory",1,1,1086,1448);
        var victoryNames=new[] { "compose","sword_lower","look_to_user","bow_start","elegant_bow","rise","subtle_smile","settle","return_to_idle" };
        for(var i=0;i<victoryNames.Length;i++) items.Add(new($"Victory/Victory_10_Frames/victory_{i+2:00}_{victoryNames[i]}.png",$"victory/{i+2:00}.png",1086,1448));
        return items;
    }

    private static void Add(List<PlanEntry> items,string sourcePattern,string output,int first,int last,double canonicalWidth=1254,double canonicalHeight=1254)
    {
        for(var index=first;index<=last;index++) items.Add(new(string.Format(sourcePattern,index),$"{output}/{index-first+1:00}.png",canonicalWidth,canonicalHeight));
    }

    private static void AddWalk(List<PlanEntry> items)
    {
        for(var index=1;index<=8;index++)
        {
            var source=index<=2?$"Walk/Walk({index}).png":$"Walk/Walk ({index}).png";
            items.Add(new(source,$"walk/{index:00}.png",1254,1254));
        }
    }

    private static void AddSleep(List<PlanEntry> items,string sourceGroup,string output,int count)
    {
        for(var index=1;index<=count;index++)
        {
            var separated=sourceGroup switch
            {
                "Sleep_Enter"=>index==5,
                "Sleep_Loop"=>index is 3 or 6 or 7 or 8,
                "Sleep_Exit"=>index is 4 or 5,
                _=>false
            };
            var file=$"{sourceGroup}{(separated?" ":string.Empty)}({index}).png";
            var projectOverride=sourceGroup=="Sleep_Loop"&&index==7?"source/formal-overrides/sleep-loop-07.png":null;
            items.Add(new($"Sleep/{sourceGroup}/{file}",$"{output}/{index:00}.png",1254,1254,projectOverride));
        }
    }

    private static void AddDrag(List<PlanEntry> items)
    {
        for(var index=1;index<=12;index++)
        {
            var separated=index is >=2 and <=8 or 10 or 12;
            var source=$"Drag/Drag{(separated?" ":string.Empty)}({index}).png";
            var (group,frame)=index switch
            {
                <=4=>("drag-start",index),
                <=8=>("drag-hold",index-4),
                _=>("drag-release",index-8)
            };
            items.Add(new(source,$"{group}/{frame:00}.png",1254,1254));
        }
    }

    private static void AddBattle(List<PlanEntry> items)
    {
        for(var index=1;index<=13;index++)
        {
            var spaces=index is >=4 and <=11 or 13?"  ":" ";
            items.Add(new($"Battle/Battle{spaces}({index}).png",$"battle/{index:00}.png",1254,1254));
        }
    }

    private sealed record PlanEntry(string SourceRelative,string OutputRelative,double CanonicalWidth,double CanonicalHeight,string? ProjectOverrideRelative=null);
    private sealed record SourceRecord(string Source,string Output,string Sha256,int SourceWidth,int SourceHeight,int OutputWidth,int OutputHeight,double CenterX,double GroundY,string Transform);
    private sealed record SourceMap(int SchemaVersion,string AssetVersion,int FrameCount,IReadOnlyList<SourceRecord> Frames);
}
