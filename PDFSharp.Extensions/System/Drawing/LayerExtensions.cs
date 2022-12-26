﻿using System.Linq;
using System.Text;
using BitMiracle.LibTiff.Classic;
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

        public static Rgba32 FromPacked(this int bits)
        {
            return new Rgba32(Tiff.GetR(bits), Tiff.GetG(bits), Tiff.GetB(bits), Tiff.GetA(bits));
        }

        public static byte[] FixB4ToI8(this byte[] input)
        {
            const int step = 2;
            var copy = new byte[input.Length * step];
            for (var i = 0; i < input.Length; i++)
            {
                var raw = input[i];
                var p = i * step;
                copy[p + 0] = (byte)(raw >> 4);
                copy[p + 1] = (byte)(raw & 0x0F);
            }
            return copy;
        }

        public static bool HasError(byte[] bytes, out string error)
        {
            if (bytes.Length == 37)
            {
                var text = Encoding.UTF8.GetString(bytes);
                error = text.Trim((char)65533);
                return true;
            }

            error = default;
            return false;
        }
    }
}
