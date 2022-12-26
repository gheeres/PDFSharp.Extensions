using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.Advanced;

// ReSharper disable once CheckNamespace
namespace PdfSharp.Pdf
{
  /// <summary>
  /// Extension methods for the PdfSharp library PdfItem object.
  /// </summary>
  public static class PdfPageExtensions
  {
    /// <summary>
    /// Get's all of the images from the specified page.
    /// </summary>
    /// <param name="page">The page to extract or retrieve images from.</param>
    /// <param name="filter">An optional filter to perform additional modifications or actions on the image.</param>
    /// <returns>An enumeration of images contained on the page.</returns>
    public static IEnumerable<Image> GetImages(this PdfPage page, Func<PdfPage, int, Image, Image> filter = null)
    {
      if (page == null) throw new ArgumentNullException("page", "The provided PDF page was null.");
      if (filter == null) filter = (pg, idx, img) => img;

      int index = 0;
      var resources = page.Elements.GetDictionary("/Resources");
      if (resources != null) {
        var xObjects = resources.Elements.GetDictionary("/XObject");
        if (xObjects != null) { 
          var items = xObjects.Elements.Values;
          foreach (PdfItem item in items) {
            var reference = item as PdfReference;
            if (reference != null) {
              var xObject = reference.Value as PdfDictionary;
              if (xObject.IsImage()) {
                yield return filter.Invoke(page, index++, xObject.ToImage());
              }
            }
          }
        }
      }
    }
  }
}