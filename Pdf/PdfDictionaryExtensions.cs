using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using PdfSharp.Pdf.Advanced;

// ReSharper disable once CheckNamespace
namespace PdfSharp.Pdf
{
  /// <summary>
  /// Extension methods for the PdfSharp library PdfDictionary object for exporting images.
  /// </summary>
  public static class PdfDictionaryExtensions
  {
    /// <summary>
    /// Helper method for inspecting the contents of the dictionary / dumping the contents to the specified output.
    /// </summary>
    /// <param name="dictionary">The dictionary to dump.</param>
    /// <param name="output">The optional output method. If not provided, then the output will be directed to standard output.</param>
    private static void Dump(this PdfDictionary dictionary, Action<PdfName, PdfItem> output = null)
    {
      if (dictionary == null) return;
      // If not output method was specified, write to the console.
      if (output == null) output = (n, i) => Console.WriteLine("{0}={1}", n, i);

      foreach (var element in dictionary.Elements) {
        output(new PdfName(element.Key), element.Value);
      }
    }

    /// <summary>
    /// Checks to see if the specified dictionary contains an image.
    /// </summary>
    /// <param name="dictionary">The dictionary to inspect.</param>
    /// <returns>True if the dictionary contains an image, false if otherwise.</returns>
    public static bool IsImage(this PdfDictionary dictionary)
    {
      return ((dictionary != null) &&
              (dictionary.Elements.GetString("/Type") == "/XObject") &&
              (dictionary.Elements.GetString("/Subtype") == "/Image"));
    }

    /// <summary>
    /// Retrieves the specifed dictionary object as an object encoded with DCTDecode filter (JPEG).
    /// </summary>
    /// <param name="dictionary">The dictionary to extract the object from.</param>
    /// <returns>The image retrieve from the dictionary. If not found or an invalid image, then null is returned.</returns>
    private static Image ImageFromDCTDecode(PdfDictionary dictionary)
    {
      // DCTDecode a lossy filter based on the JPEG standard
      // We can just load directly from the stream.
      MemoryStream stream = new MemoryStream(dictionary.Stream.Value);
      return (Bitmap.FromStream(stream));
    }

    /// <summary>
    /// Gets the PixelFormat for the specified bits per pixel (bpp) or color depth.
    /// </summary>
    /// <param name="bitsPerPixel"></param>
    /// <param name="isIndexed">Optional parameters indicating if the bits are indexed. If indexed, then bitsPerPixel must be less than or equal to 8. Defaults to false.</param>
    /// <returns></returns>
    private static PixelFormat GetPixelFormat(int bitsPerPixel, bool isIndexed = false)
    {
      // The number of bits used to represent each color component. 
      // Only a single value may be speciﬁed; the number of bits is the same for all color components. 
      // Valid values are 1, 2, 4, and 8.
      switch(bitsPerPixel) {
        case 1:
          if (isIndexed) { return (PixelFormat.Format1bppIndexed);}
          break;
        case 2:
          return (PixelFormat.Format4bppIndexed);
        case 4:
          if (isIndexed) { return (PixelFormat.Format4bppIndexed); }
          break;
        case 8:
          if (isIndexed) return (PixelFormat.Format8bppIndexed);
          return (PixelFormat.Format24bppRgb); // 8 bits per component x 3 (R,G,B) = 24
      }

      throw new ArgumentException(String.Format("The specified pixel depth '{0}' is not supported.", bitsPerPixel), "bitsPerPixel");
    }

    /// <summary>
    /// Retrieves the specifed dictionary object as an object encoded with FlateDecode filter.
    /// </summary>
    /// <remarks>
    /// FlateDecode a commonly used filter based on the zlib/deflate algorithm (a.k.a. gzip, but not zip) 
    /// defined in RFC 1950 and RFC 1951; introduced in PDF 1.2; it can use one of two groups of predictor 
    /// functions for more compact zlib/deflate compression: Predictor 2 from the TIFF 6.0 specification 
    /// and predictors (filters) from the PNG specification (RFC 2083)
    /// </remarks>
    /// <param name="dictionary">The dictionary to extract the object from.</param>
    /// <returns>The image retrieve from the dictionary. If not found or an invalid image, then null is returned.</returns>
    private static Image ImageFromFlateDecode(PdfDictionary dictionary)
    {
      PdfDictionaryImageMetaData imageData = new PdfDictionaryImageMetaData(dictionary);

      // FlateDecode can be either indexed or a traditional ColorSpace
      bool isIndexed = imageData.ColorSpace.IsIndexed;
      PixelFormat format = GetPixelFormat(imageData.BitsPerPixel, isIndexed);

      Bitmap bitmap = new Bitmap(imageData.Width, imageData.Height, format);

      // If indexed, retrieve and assign the color palette for the item.
      if ((isIndexed) && (imageData.ColorSpace.IsRGB)) bitmap.Palette = ((PdfIndexedRGBColorSpace) imageData.ColorSpace).ToColorPalette();

      // If not an indexed color, the .NET image component expects pixels to be in BGR order. However, our PDF stream is in RGB order.
      byte[] stream = (format == PixelFormat.Format24bppRgb) ? ConvertRGBStreamToBGR(dictionary.Stream.UnfilteredValue) : dictionary.Stream.UnfilteredValue;

      BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, imageData.Width, imageData.Height), ImageLockMode.WriteOnly, format);
      // We can't just copy the bytes directly; the BitmapData .NET class has a stride (padding) associated with it. 
      int length = (int)Math.Ceiling(bitmapData.Width * format.GetBitsPerPixel() / 8.0);
      for (int y = 0, height = bitmapData.Height; y < height; y++) {
        int offset = y * length;
        Marshal.Copy(stream, offset, bitmapData.Scan0 + (y * bitmapData.Stride), length);
      }
      bitmap.UnlockBits(bitmapData);

      return (bitmap);
    }

    /// <summary>
    /// Converts an RGB ordered stream to BGR ordering. 
    /// </summary>
    /// <remarks>
    /// A PDF /DeviceRGB stream is stored in RGB ordering, however the .NET Image libraries expect BGR ordering.
    /// </remarks>
    /// <param name="stream">The input stream to reorder. The input array will be modified inline by this procedure.</param>
    /// <returns>Return the modified input stream.</returns>
    private static byte[] ConvertRGBStreamToBGR(byte[] stream)
    {
      if (stream == null) return(null);

      for (int x = 0, length = stream.Length; x < length; x += 3) {
        byte red = stream[x];

        stream[x] = stream[x+2];
        stream[x+2] = red;
      }
      return (stream);
    }

    /// <summary>
    /// Retrieves the image from the specified dictionary.
    /// </summary>
    /// <param name="dictionary">The dictionary to extract the image from.</param>
    /// <returns>Returns the image if valid, otherwise if the dictionary does not contain a valid image, then null is returned.</returns>
    public static Image ToImage(this PdfDictionary dictionary)
    {
      if (! IsImage(dictionary)) return(null);

      // Create a dictionary mapping (i.e. switch statement) to handle the different filter types.
      // Setup a default action, "noAction" if the dictionary entry is not found which will return a null image.
      Func<PdfDictionary, Image> noAction = (d) => null;
      IDictionary<string,Func<PdfDictionary, Image>> map = new Dictionary<string, Func<PdfDictionary, Image>>() {
        { "/DCTDecode", ImageFromDCTDecode },
        { "/FlateDecode", ImageFromFlateDecode }
      }; 
      
      string filter = dictionary.Elements.GetName("/Filter");
      var action = map.ContainsKey(filter) ? map[filter] : noAction;
      return (action.Invoke(dictionary));
    }

    #region ColorSpace
    /// <summary>
    /// Internal class for working with a colorspace.
    /// </summary>
    abstract class PdfColorSpace
    {
      /// <summary>Checks to see if the colorspace supports CYMK colors.</summary>
      public virtual bool IsCMYK
      {
        get { return (false); }
      }

      /// <summary>Checks to see if the colorspace supports RGB colors.</summary>
      public virtual bool IsRGB
      {
        get { return (false); }
      }

      /// <summary>Checks to see if the colorspace is an indexed colorspace.</summary>
      public virtual bool IsIndexed
      {
        get { return(false); }
      }
    }

    /// <summary>
    /// Internal class for working with an indexed colorspace.
    /// </summary>
    abstract class PdfIndexedColorSpace : PdfColorSpace
    {
      private int _colors = 0;

      /// <summary>The number of colors.</summary>
      public int Colors
      {
        get { return(_colors + 1); }
        set { _colors = value; }
      }

      /// <summary>Checks to see if the colorspace is an indexed colorspace.</summary>
      public override bool IsIndexed
      {
        get { return (true); }
      }

      /// <param name="colors">The number of colors.</param>
      protected PdfIndexedColorSpace(int colors)
      {
        Colors = colors;
      }

      /// <summary>
      /// Retrieves the raw data for the colorspace.
      /// </summary>
      /// <returns>The raw data from the PdfArray of PdfReference.</returns>
      protected virtual IEnumerable<byte> GetRawPalette(PdfItem item)
      {
        // The palette data is directly imbedded.
        if (item.IsArray()) return(GetRawPalette(item as PdfArray));
        if (item.IsReference()) return (GetRawPalette(item as PdfReference));
      
        throw new ArgumentException("The specified palette information was incorrect.", "item");
      }

      /// <summary>
      /// Retrieves the raw data for the colorspace.
      /// </summary>
      /// <returns>The raw data from the PdfArray of PdfReference.</returns>
      protected virtual IEnumerable<byte> GetRawPalette(PdfArray array)
      {
        if (array == null) throw new ArgumentNullException("array", "The indexed color array was null.");

        foreach(var item in array.Elements) {
          var number = (item as PdfInteger);
          if (number == null) yield return(0);
          yield return (Convert.ToByte(number.Value));
        }
      }

      /// <summary>
      /// Retrieves the raw data for the colorspace.
      /// </summary>
      /// <returns>The raw data from the PdfArray of PdfReference.</returns>
      protected virtual IEnumerable<byte> GetRawPalette(PdfReference reference)
      {
        if (reference == null) throw new ArgumentNullException("reference", "The reference index was null.");

        var value = (reference.Value as PdfDictionary);
        if (value == null) return(new byte[0]);
        return (value.Stream.UnfilteredValue);
      }

      /// <summary>
      /// Converts the specified Pdf colorspace to a Color space.
      /// </summary>
      /// <returns>The ColorPalette representing the raw palette.</returns>
      public abstract ColorPalette ToColorPalette();
    }

    /// <summary>
    /// Internal class for working with an indexed RGB colorspace.
    /// </summary>
    class PdfIndexedRGBColorSpace : PdfIndexedColorSpace
    {
      private readonly PdfItem _colorSpace;
      protected Color[] _palette;

      /// <summary>The color palette for the colorspace.</summary>
      public virtual Color[] Palette
      {
        get { return (_palette ?? (_palette = GetColorPalette().ToArray())); }
      }

      /// <summary>Checks to see if the colorspace supports RGB colors.</summary>
      public override bool IsRGB
      {
        get { return (true); }
      }

      /// <param name="colorSpace">The pdfItem representing the color space.</param>
      /// <param name="colors">The number of colors.</param>
      public PdfIndexedRGBColorSpace(PdfItem colorSpace, int colors): base(colors)
      {
        if (colorSpace == null) throw new ArgumentNullException("colorSpace", "The colorspace must be specified when creating a PdfIndexedRGBColorSpace.");
        _colorSpace = colorSpace;
      }

      /// <summary>
      /// Retrieves the color palette.
      /// </summary>
      /// <returns>The color pallete for the indexed image.</returns>
      protected virtual IEnumerable<Color> GetColorPalette()
      {
        int offset = 3;
        byte[] values = GetRawPalette(_colorSpace).ToArray();
        for (int color = 0, length = Colors; color < length; color++) {
          yield return (Color.FromArgb(values[color * offset], values[(color * offset) + 1], values[(color * offset) + 2]));
        }
      }

      /// <summary>
      /// Converts the specified Pdf colorspace to a Color space.
      /// </summary>
      /// <returns>The ColorPalette representing the raw palette.</returns>
      public override ColorPalette ToColorPalette()
      {
        // Unfortunately ColorPalette is a sealed object so we have two options:
        //
        // 1.) Create an Image object with with correct PixelFormat and then keep
        //     a reference to the ColorPalette that was created for the image, overridding
        //     the colors.
        // 2.) Use reflection to access the protected constructor.
        //
        // We'll choose option 2 since it just feels "right", but is "hacky"
        Type type = typeof(ColorPalette);
        ColorPalette colorPalette = (ColorPalette) Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                                                                            null, new object[] { Colors }, CultureInfo.InvariantCulture);
        for (int color = 0, length = Palette.Length; color < length; color++) {
          colorPalette.Entries[color] = Palette[color];
        }
        return (colorPalette);

      }
    }

    /// <summary>
    /// Internal class for working with an RGB colorspace.
    /// </summary>
    class PdfRGBColorSpace: PdfColorSpace
    {
      /// <summary>Checks to see if the colorspace supports RGB colors.</summary>
      public override bool IsRGB
      {
        get { return (true); }
      }
    }

    /// <summary>
    /// Internal class for working with a CMYK colorspace.
    /// </summary>
    class PdfCMYKColorSpace : PdfColorSpace
    {
      /// <summary>Checks to see if the colorspace supports CMYKcolors.</summary>
      public override bool IsCMYK
      {
        get { return (true); }
      }
    }

    /// <summary>
    /// Internal class for extracting colorspace information from a dictionary.
    /// </summary>
    class PdfDictionaryColorSpace
    {
      /// <summary>
      /// Get's the colorspace name for the specified item.
      /// </summary>
      /// <param name="colorSpace">The colorspace to inspect.</param>
      /// <returns>The PdfName of the specified item.</returns>
      private static string GetColorSpaceName(PdfItem colorSpace)
      {
        if (colorSpace == null) return(null);
        if (colorSpace.IsArray()) return (GetColorSpaceName(((PdfArray)colorSpace).Elements[0]));
        if (colorSpace.IsName()) return (((PdfName) colorSpace).Value);
        if (colorSpace.IsReference()) {
          PdfReference reference = (colorSpace as PdfReference);
          if (reference != null) {
            return (GetColorSpaceName(reference.Value));
          }
        }
        return (null);
      }

      /// <summary>
      /// Parse the colorspace information from the specified colorspace item.
      /// </summary>
      /// <param name="colorSpace">The external color space object to parse.</param>
      public static PdfColorSpace Parse(PdfItem colorSpace)
      {
        if (colorSpace == null)  throw new ArgumentNullException("colorSpace", "The provided color space was null.");

        Func<PdfItem, PdfColorSpace> defaultAction = (a) => { throw new Exception(String.Format("An unsupported colorspace '{0}' was provided.", GetColorSpaceName(a))); };
        IDictionary<string, Func<PdfItem, PdfColorSpace>> map = new Dictionary<string, Func<PdfItem, PdfColorSpace>> {
          // An Indexed color space is deﬁned by a four-element array, as follows:
          //
          //   /Indexed base hival lookup
          //
          // base - an array or name that identiﬁes the base color space in which the values in the color table are to be interpreted
          // hival - an integer that speciﬁes the maximum valid index value. I
          // lookup - The color table is deﬁned by the lookup parameter, which can be either a stream or (in PDF 1.2) a string
          { "/Indexed", (a) => {
            if (a == null) throw new ArgumentNullException("a", "The indexed colorspace cannot be null.");

            var array = (a as PdfArray) ?? (a.Get() as PdfArray);
            if (array == null) throw new ArgumentException("a", "The indexed colorspace not specified as an array.");
            
            PdfItem colorBase = array.Elements[1].Get(); // base
            int colors = array.Elements.GetInteger(2);   // hival
            PdfItem lookup = array.Elements[3];          // lookup
            
            // We currently only support DeviceRGB indexed color spaces.
            if ((colorBase.IsName() && ((PdfName) colorBase).Value == "/DeviceRGB")) {
              return (new PdfIndexedRGBColorSpace(lookup, colors));
            }
            throw new NotImplementedException(String.Format("The indexed colorspace '{0}' is not supported.", GetColorSpaceName(colorBase)));
          } }, 
          // Standard RGB Colorspace
          { "/DeviceRGB", (a) => new PdfRGBColorSpace() },
          // Standard CMYK Colorspace
          { "/DeviceCMYK", (a) => {
            throw new NotImplementedException("CMYK encoded images are not supported.");
            return (new PdfCMYKColorSpace());
          } },
        };

        string name = GetColorSpaceName(colorSpace);
        var action = (map.ContainsKey(name) ? map[name] : defaultAction);
        return(action.Invoke(colorSpace));
      }
    }
    #endregion ColorSpace

    #region Shared Image MetaData
    /// <summary>
    /// Internal class for extracting meta data from a dictionary.
    /// </summary>
    class PdfDictionaryImageMetaData
    {
      /// <summary>The total length or size of the image data.</summary>
      public int Length { get; set; }

      /// <summary>The height of the stored image.</summary>
      public int Height { get; set; }

      /// <summary>The width of the stored image.</summary>
      public int Width { get; set; }

      /// <summary>The number of bits used to represent 1 pixel in the image. Commonly abbreviated as "bpp".</summary>
      public int BitsPerPixel { get; set; }

      /// <summary>The colorspace information for the image.</summary>
      public PdfColorSpace ColorSpace { get; set; }

      /// <param name="dictionary">The dictionary object o parse.</param>
      public PdfDictionaryImageMetaData(PdfDictionary dictionary)
      {
        Initialize(dictionary);
      }

      /// <summary>
      /// Initializes the item based on the specified PdfDictionary.
      /// </summary>
      /// <param name="dictionary">The dictionary to use for initialization.</param>
      private void Initialize(PdfDictionary dictionary)
      {
        if (dictionary == null) throw new ArgumentNullException("dictionary", "The PDF dictionary item to extract image meta data from was null.");
        if (!dictionary.IsImage()) throw new ArgumentException("The specified dictionary does not represent an image.", "dictionary");

        Height = dictionary.Elements.GetInteger("/Height");
        Width = dictionary.Elements.GetInteger("/Width");
        BitsPerPixel = dictionary.Elements.GetInteger("/BitsPerComponent");
        Length = dictionary.Elements.GetInteger("/Length");

        PdfItem colorSpace = null;
        if (dictionary.Elements.TryGetValue("/ColorSpace", out colorSpace)) {
          ColorSpace = PdfDictionaryColorSpace.Parse(colorSpace);
        }
        else ColorSpace = new PdfRGBColorSpace(); // Default to RGB Color Space
      }

      /// <summary>
      /// Returns a string that represents the current object.
      /// </summary>
      /// <returns>
      /// A string that represents the current object.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public override string ToString()
      {
        Func<IEnumerable<string>> palette = () => ((PdfIndexedRGBColorSpace)ColorSpace).Palette.Select((c,i) => String.Format("[{0:000}]{1:x2}{2:x2}{3:x2}{4:x2}", i, c.A, c.R, c.G, c.B));
        return (String.Format("{0}x{1} @ {2}bpp{3}", Width, Height, BitsPerPixel, 
                               (ColorSpace is PdfIndexedRGBColorSpace) ? String.Format(" /Indexed({0}): {1}", ((PdfIndexedColorSpace) ColorSpace).Colors,
                                                                                                              String.Join(", ", palette.Invoke())) : null));
      }
    }
    #endregion Shared Image MetaData
  }
}
