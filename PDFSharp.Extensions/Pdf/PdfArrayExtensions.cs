using PdfSharpCore.Pdf;
using System;

// ReSharper disable once CheckNamespace
namespace PdfSharp.Pdf
{
  /// <summary>
  /// Extension methods for the PdfSharp library PdfArray object
  /// </summary>
  public static class PdfArrayExtensions
  {
    /// <summary>
    /// Helper method for inspecting the contents of the dictionary / dumping the contents to the specified output.
    /// </summary>
    /// <param name="array">The array to dump.</param>
    /// <param name="output">The optional output method. If not provided, then the output will be directed to standard output.</param>
    public static void Dump(this PdfArray array, Action<PdfItem> output = null)
    {
      if (array == null) return;
      // If not output method was specified, write to the console.
      if (output == null) output = (i) => Console.WriteLine("{0}", i);

      foreach (PdfItem element in array.Elements) {
        output(element);
      }
    }

    /// <summary>
    /// Checks to see if the specified PdfArray is empty.
    /// </summary>
    /// <param name="array">The array to inspect.</param>
    /// <returns>True if empty (or null), false if contains 1 or more elements.</returns>
    public static bool IsEmpty(this PdfArray array)
    {
      return ((array == null) || (array.Elements.Count == 0));
    }
  }
}