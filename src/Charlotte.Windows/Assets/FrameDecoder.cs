using Charlotte.Core.Geometry;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Charlotte.Windows.Assets;

public sealed record DecodedFrame(BitmapSource Bitmap,AlphaMask Mask,long EstimatedBytes);

public interface IFrameDecoder
{
    Task<DecodedFrame> DecodeAsync(string path,int pixelWidth,byte alphaThreshold,CancellationToken cancellationToken);
}

public sealed class FrameDecoder : IFrameDecoder
{
    public Task<DecodedFrame> DecodeAsync(string path,int pixelWidth,byte alphaThreshold,CancellationToken cancellationToken)
        => Task.Run(()=>Decode(path,pixelWidth,alphaThreshold,cancellationToken),cancellationToken);

    private static DecodedFrame Decode(string path,int width,byte alphaThreshold,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bitmap=path=="builtin:idle"?CreateBuiltinIdle(width):DecodeFile(path,width);
        cancellationToken.ThrowIfCancellationRequested();
        var stride=bitmap.PixelWidth*4;
        var pixels=new byte[stride*bitmap.PixelHeight];
        bitmap.CopyPixels(pixels,stride,0);
        cancellationToken.ThrowIfCancellationRequested();
        var mask=AlphaMask.Create(pixels,bitmap.PixelWidth,bitmap.PixelHeight,stride,alphaThreshold);
        return new(bitmap,mask,(long)stride*bitmap.PixelHeight+mask.EstimatedBytes);
    }

    private static BitmapSource DecodeFile(string path,int width)
    {
        var source=new BitmapImage();
        source.BeginInit();
        source.CacheOption=BitmapCacheOption.OnLoad;
        source.DecodePixelWidth=width;
        source.UriSource=new(path);
        source.EndInit();
        source.Freeze();
        if(source.Format==PixelFormats.Pbgra32) return source;
        var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);
        converted.Freeze();
        return converted;
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
}
