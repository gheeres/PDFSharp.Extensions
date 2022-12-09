using SixLabors.ImageSharp;

// ReSharper disable once CheckNamespace
namespace PdfSharp.Pdf.Drawing
{
    public sealed class ColorPalette
    {
        public Color[] Entries { get; }

        public ColorPalette(int count = 1)
        {
            Entries = new Color[count];
        }
    }
}
