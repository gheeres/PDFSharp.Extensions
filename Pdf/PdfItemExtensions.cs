using PdfSharp.Pdf.Advanced;

// ReSharper disable once CheckNamespace
namespace PdfSharp.Pdf
{
  /// <summary>
  /// Extension methods for the PdfSharp library PdfItem object.
  /// </summary>
  public static class PdfItemExtensions
  {
    /// <summary>
    /// A helper for a PdfItem that will automatically lookup / fetch the corresponding
    /// PdfReference item if the item is a PdfReference. If the item is not a PdfItem, 
    /// then the original item is returned.
    /// </summary>
    /// <param name="item">The PdfItem to expand or fetch from.</param>
    /// <returns>The expanded PdfReference item if a PdfReference, otherwise the original item is returned.</returns>
    public static PdfItem Get(this PdfItem item)
    {
      if (item != null) {
        if (item.IsReference()) {
          return(Get(((PdfReference) item).Value));
        }
      }
      return (item);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfBoolean
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to a Boolean.</returns>
    public static bool IsBoolean(this PdfItem item)
    {
      return (item is PdfBoolean);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfDate
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to a Date.</returns>
    public static bool IsDate(this PdfItem item)
    {
      return (item is PdfDate);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfLiteral
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to a Literal.</returns>
    public static bool IsLiteral(this PdfItem item)
    {
      return (item is PdfLiteral);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfName
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to a Name.</returns>
    public static bool IsName(this PdfItem item)
    {
      return (item is PdfName);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfNull
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to Null.</returns>
    public static bool IsNull(this PdfItem item)
    {
      return (item is PdfNull);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfNumber
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to Number.</returns>
    public static bool IsNumber(this PdfItem item)
    {
      return (item is PdfNumber);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfObject
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to Object.</returns>
    public static bool IsObject(this PdfItem item)
    {
      return (item is PdfObject);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfArray
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to Array.</returns>
    public static bool IsArray(this PdfItem item)
    {
      return (item is PdfArray);
    }
    
    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfRectangle
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to Rectangle.</returns>
    public static bool IsRectangle(this PdfItem item)
    {
      return (item is PdfRectangle);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfReference
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to Reference.</returns>
    public static bool IsReference(this PdfItem item)
    {
      return (item is PdfReference);
    }

    /// <summary>
    /// Checks to see if the PdfItem can be represented as a PdfString
    /// </summary>
    /// <param name="item">The item to inspect.</param>
    /// <returns>True if the item can be converted to String.</returns>
    public static bool IsString(this PdfItem item)
    {
      return (item is PdfString);
    }
  }
}