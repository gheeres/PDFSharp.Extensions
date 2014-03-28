using System.Collections.Generic;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

// ReSharper disable once CheckNamespace
namespace System.Drawing
{
  /// <summary>
  /// Extension methods for the <see cref="Image"/> class.
  /// </summary>
  public static class ImageExtensions
  {
    /// <summary>
    /// Generates a PDF page from the image.
    /// </summary>
    /// <param name="image">The image to convert to a <see cref="PdfPage"/>.</param>
    /// <returns>The <see cref="PdfPage"/> containing the image.</returns>
    public static PdfDocument ToPdf(this Image image)
    {
      if (image == null) return (new PdfDocument());
      return (ToPdf(new[] { image }));
    }

    /// <summary>
    /// Generates a PDF document from the collection of enumerations with each image representing
    /// a new page.
    /// </summary>
    /// <param name="images">The collection of images to convert to pages.</param>
    /// <returns>The <see cref="PdfDocument"/> containing the images.</returns>
    public static PdfDocument ToPdf(this IEnumerable<Image> images)
    {
      PdfDocument document = new PdfDocument();
      foreach (var image in images) {
        PdfPage page = new PdfPage() {
          Width = image.Width,
          Height = image.Height
        };
        XGraphics xGraphics = XGraphics.FromPdfPage(page);
        XImage xImage = XImage.FromGdiPlusImage(image);
        xGraphics.DrawImage(xImage, 0, 0, image.Width, image.Height);

        document.AddPage(page);
      }
      return (document);
    }
  }
}
