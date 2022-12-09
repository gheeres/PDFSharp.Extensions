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
    }
}
