using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Charlotte.Core.Animation;
using Charlotte.Windows.Assets;

if(args.Length!=2)
{
    Console.Error.WriteLine("Usage: Charlotte.AssetCheck <assets-root> <animation-manifest>");
    return 2;
}

try
{
    var assets=ManifestLoader.Load(args[0],args[1]);
    var frameCount=0;
    foreach(var id in Enum.GetValues<AnimationId>())
    {
        var clip=assets.Catalog.Get(id);
        foreach(var frame in clip.Frames)
        {
            using var stream=File.OpenRead(frame.Path);
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            var bitmap=decoder.Frames[0];
            if(bitmap.PixelWidth!=(int)assets.LogicalCanvas.Width || bitmap.PixelHeight!=(int)assets.LogicalCanvas.Height)
                throw new InvalidDataException($"{Path.GetFileName(frame.Path)} 画布不是 {assets.LogicalCanvas.Width}×{assets.LogicalCanvas.Height}。 ");
            if(bitmap.Format!=PixelFormats.Bgra32 && bitmap.Format!=PixelFormats.Pbgra32)
                throw new InvalidDataException($"{Path.GetFileName(frame.Path)} 必须是 32 位带 Alpha PNG。");
            var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];
            bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);
            var hasTransparent=false; var hasBody=false;
            for(var offset=3;offset<pixels.Length;offset+=4)
            {
                hasTransparent|=pixels[offset]<assets.AlphaThreshold;
                hasBody|=pixels[offset]>=assets.AlphaThreshold;
                if(hasTransparent && hasBody) break;
            }
            if(!hasTransparent || !hasBody)
                throw new InvalidDataException($"{Path.GetFileName(frame.Path)} 必须同时包含透明背景和可见角色像素。");
            frameCount++;
        }
    }
    Console.WriteLine($"资产校验通过：{Enum.GetValues<AnimationId>().Length} 个动作，{frameCount} 帧，阶段=placeholder。");
    return 0;
}
catch(Exception error)
{
    Console.Error.WriteLine($"资产校验失败：{error.Message}");
    return 1;
}
