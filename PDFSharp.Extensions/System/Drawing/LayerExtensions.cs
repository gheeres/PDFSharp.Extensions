using System.Linq;
using MigraDocCore.DocumentObjectModel.MigraDoc.DocumentObjectModel.Shapes;
using PdfSharpCore.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable once CheckNamespace
namespace PdfSharp.Pdf.Drawing
{
    public static class LayerExtensions
    {
        public static ImageSource.IImageSource ToSource(this Image image,
            int quality = 100, IImageFormat format = null)
        {
            var fmt = format ?? PngFormat.Instance;
            var copy = image.CloneAs<Rgba32>();
            var source = ImageSharpImageSource<Rgba32>.FromImageSharpImage(copy, fmt, quality);
            return source;
        }

        public static Rgba32[] Unpack(this Color[] colors)
        {
            return colors.Select(c => c.ToPixel<Rgba32>()).ToArray();
        }

        public static bool IsIndexed(this PixelFormat format)
        {
            return format
                is PixelFormat.Format8bppIndexed
                or PixelFormat.Format4bppIndexed
                or PixelFormat.Format1bppIndexed;
        }

        public static Image<Rgba32> ApplyColorPalette(this Image<L8> image, ColorPalette palette)
        {
            Image<Rgba32> target = new(image.Width, image.Height);
            image.ProcessPixelRows(target, (srcAcc, dstAcc) =>
            {
                for (var y = 0; y < srcAcc.Height; y++)
                {
                    var srcRow = srcAcc.GetRowSpan(y);
                    var dstRow = dstAcc.GetRowSpan(y);
                    for (var x = 0; x < srcRow.Length; x++)
                    {
                        ref var srcPixel = ref srcRow[x];
                        ref var dstPixel = ref dstRow[x];
                        var color = palette.Entries[srcPixel.PackedValue];
                        dstPixel = color;
                    }
                }
            });
            return target;
        }
    }
}
