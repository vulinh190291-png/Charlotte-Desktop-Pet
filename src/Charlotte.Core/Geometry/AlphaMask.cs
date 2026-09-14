namespace Charlotte.Core.Geometry;
public sealed class AlphaMask
{
    private readonly bool[] bits;
    public int Width { get; }
    public int Height { get; }
    public long EstimatedBytes=>bits.LongLength;
    private AlphaMask(int width, int height) { Width = width; Height = height; bits = new bool[checked(width * height)]; }
    public static AlphaMask Create(byte[] pixels, int width, int height, int stride, byte threshold)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || stride < checked(width * 4) || pixels.Length < checked(stride * height))
            throw new ArgumentException("Invalid BGRA dimensions");
        var mask = new AlphaMask(width, height);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                mask.bits[y * width + x] = pixels[y * stride + x * 4 + 3] >= Math.Max(1, (int)threshold);
        return mask;
    }
    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && bits[y * Width + x];
}
