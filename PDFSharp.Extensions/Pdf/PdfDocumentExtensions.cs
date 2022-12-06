using System;
using System.Collections.Generic;
using System.Drawing;

// ReSharper disable once CheckNamespace
namespace PdfSharp.Pdf
{
  /// <summary>
  /// Extension methods for the PdfSharp library PdfItem object.
  /// </summary>
  public static class PdfDocumentExtensions
  {
    /// <summary>
    /// Get's all of the images from the specified document.
    /// </summary>
    /// <param name="document">The document to extract or retrieve images from.</param>
    /// <returns>An enumeration of images contained on the page.</returns>
    public static IEnumerable<Image> GetImages(this PdfDocument document)
    {
      if (document == null) throw new ArgumentNullException("document", "The provided PDF document was null.");

      foreach(PdfPage page in document.Pages) {
        foreach(Image image in page.GetImages()) {
          yield return (image);
        }
      }
    }
  }
}