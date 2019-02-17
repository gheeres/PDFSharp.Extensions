using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using BitMiracle.LibTiff.Classic;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Filters;

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
    /// <param name="colorSpace">The colorspace of the image.</param>
    /// <param name="bitsPerPixel">The number of bits per pixel.</param>
    /// <param name="isIndexed">Optional parameters indicating if the bits are indexed. If indexed, then bitsPerPixel must be less than or equal to 8. Defaults to false.</param>
    /// <returns>The pixel format to read the data from.</returns>
    private static PixelFormat GetPixelFormat(PdfColorSpace colorSpace, int bitsPerPixel, bool isIndexed = false)
    {
      // The number of bits used to represent each color component. 
      // Only a single value may be speciﬁed; the number of bits is the same for all color components. 
      // Valid values are 1, 2, 4, and 8.
      switch(bitsPerPixel) {
        case 1:
          return (PixelFormat.Format1bppIndexed);
        case 2:
          return (PixelFormat.Format4bppIndexed);
        case 4:
          if (isIndexed) { return (PixelFormat.Format4bppIndexed); }
          break;
        case 8:
          if ((isIndexed) || (colorSpace is PdfGrayColorSpace)) return (PixelFormat.Format8bppIndexed);
          return (PixelFormat.Format24bppRgb); // 8 bits per component x 3 (R,G,B) = 24
      }

      throw new ArgumentException(String.Format("The specified pixel depth '{0}' is not supported.", bitsPerPixel), "bitsPerPixel");
    }

    /// <summary>
    /// Writes the specified TIFF tag data to the stream.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="tag">The <see cref="TiffTag"/> to write.</param>
    /// <param name="type">The <see cref="TiffType"/> being written.</param>
    /// <param name="count">The number of values being written.</param>
    /// <param name="value">The actual value to be written.</param>
    private static void WriteTiffTag(Stream stream, TiffTag tag, TiffType type, uint count, uint value)
    {
      if (stream == null) return;

      stream.Write(BitConverter.GetBytes((uint)tag), 0, 2);
      stream.Write(BitConverter.GetBytes((uint)type), 0, 2);
      stream.Write(BitConverter.GetBytes(count), 0, 4);
      stream.Write(BitConverter.GetBytes(value), 0, 4);
    }

    /// <summary>
    /// Prepends a proper TIFF image header to the the CCITTFaxDecode image data.
    /// </summary>
    /// <param name="imageData">The metadata about the image.</param>
    /// <param name="image">The original compressed image.</param>
    /// <returns>A properly formatted TIFF header and the compressed image data.</returns>
    private static byte[] GetTiffImageBufferFromCCITTFaxDecode(PdfDictionaryImageMetaData imageData, byte[] image)
    {
      const short TIFF_BIGENDIAN = 0x4d4d;
      const short TIFF_LITTLEENDIAN = 0x4949;

      const int ifd_length = 10;
      const int header_length = 10 + (ifd_length * 12 + 4);
      using (MemoryStream buffer = new MemoryStream(header_length + image.Length)) {
        // TIFF Header
        buffer.Write(BitConverter.GetBytes(BitConverter.IsLittleEndian ? TIFF_LITTLEENDIAN : TIFF_BIGENDIAN), 0, 2); // tiff_magic (big/little endianness)
        buffer.Write(BitConverter.GetBytes((uint)42), 0, 2);         // tiff_version
        buffer.Write(BitConverter.GetBytes((uint)8), 0, 4);          // first_ifd (Image file directory) / offset
        buffer.Write(BitConverter.GetBytes((uint)ifd_length), 0, 2); // ifd_length, number of tags (ifd entries)

        // Dictionary should be in order based on the TiffTag value
        WriteTiffTag(buffer, TiffTag.SUBFILETYPE, TiffType.LONG, 1, 0);
        WriteTiffTag(buffer, TiffTag.IMAGEWIDTH, TiffType.LONG, 1, (uint)imageData.Width);
        WriteTiffTag(buffer, TiffTag.IMAGELENGTH, TiffType.LONG, 1, (uint)imageData.Height);
        WriteTiffTag(buffer, TiffTag.BITSPERSAMPLE, TiffType.SHORT, 1, (uint)imageData.BitsPerPixel);
        WriteTiffTag(buffer, TiffTag.COMPRESSION, TiffType.SHORT, 1, (uint) Compression.CCITTFAX4); // CCITT Group 4 fax encoding.
        WriteTiffTag(buffer, TiffTag.PHOTOMETRIC, TiffType.SHORT, 1, 0); // WhiteIsZero
        WriteTiffTag(buffer, TiffTag.STRIPOFFSETS, TiffType.LONG, 1, header_length);
        WriteTiffTag(buffer, TiffTag.SAMPLESPERPIXEL, TiffType.SHORT, 1, 1);
        WriteTiffTag(buffer, TiffTag.ROWSPERSTRIP, TiffType.LONG, 1, (uint)imageData.Height);
        WriteTiffTag(buffer, TiffTag.STRIPBYTECOUNTS, TiffType.LONG, 1, (uint)image.Length);

        // Next IFD Offset
        buffer.Write(BitConverter.GetBytes((uint)0), 0, 4);

        buffer.Write(image, 0, image.Length);
        return(buffer.GetBuffer());
      }
    }

    /// <summary>
    /// Retrieves the specifed dictionary object as an object encoded with CCITTFaxDecode filter (TIFF).
    /// </summary>
    /// <param name="dictionary">The dictionary to extract the object from.</param>
    /// <returns>The image retrieve from the dictionary. If not found or an invalid image, then null is returned.</returns>
    private static Image ImageFromCCITTFaxDecode(PdfDictionary dictionary)
    {
      Image image = null;
      PdfDictionaryImageMetaData imageData = new PdfDictionaryImageMetaData(dictionary);

      PixelFormat format = GetPixelFormat(imageData.ColorSpace, imageData.BitsPerPixel, true);
      Bitmap bitmap = new Bitmap(imageData.Width, imageData.Height, format);

      // Determine if BLACK=1, create proper indexed color palette.
      CCITTFaxDecodeParameters ccittFaxDecodeParameters = new CCITTFaxDecodeParameters(dictionary.Elements["/DecodeParms"].Get() as PdfDictionary);
      if (ccittFaxDecodeParameters.BlackIs1) bitmap.Palette = PdfIndexedColorSpace.CreateColorPalette(Color.Black, Color.White);
      else bitmap.Palette = PdfIndexedColorSpace.CreateColorPalette(Color.White, Color.Black);

      using (MemoryStream stream = new MemoryStream(GetTiffImageBufferFromCCITTFaxDecode(imageData, dictionary.Stream.Value))) {
        using (Tiff tiff = Tiff.ClientOpen("<INLINE>", "r", stream, new TiffStream())) {
          if (tiff == null) return (null);

          int stride = tiff.ScanlineSize();
          byte[] buffer = new byte[stride];
          for (int i = 0; i < imageData.Height; i++) {
            tiff.ReadScanline(buffer, i);

            Rectangle imgRect = new Rectangle(0, i, imageData.Width, 1);
            BitmapData imgData = bitmap.LockBits(imgRect, ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
            Marshal.Copy(buffer, 0, imgData.Scan0, buffer.Length);
            bitmap.UnlockBits(imgData);
          }
        }
      }
      return (bitmap);
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
      PixelFormat format = GetPixelFormat(imageData.ColorSpace, imageData.BitsPerPixel, isIndexed);

      Bitmap bitmap = new Bitmap(imageData.Width, imageData.Height, format);

      // If indexed, retrieve and assign the color palette for the item.
      if ((isIndexed) && (imageData.ColorSpace.IsRGB)) bitmap.Palette = ((PdfIndexedRGBColorSpace) imageData.ColorSpace).ToColorPalette();
      else if (imageData.ColorSpace is PdfGrayColorSpace) bitmap.Palette = ((PdfGrayColorSpace)imageData.ColorSpace).ToColorPalette(imageData.BitsPerPixel);

      // If not an indexed color, the .NET image component expects pixels to be in BGR order. However, our PDF stream is in RGB order.
      byte[] stream = (format == PixelFormat.Format24bppRgb) ? ConvertRGBStreamToBGR(dictionary.Stream.UnfilteredValue) : dictionary.Stream.UnfilteredValue;

      BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, imageData.Width, imageData.Height), ImageLockMode.WriteOnly, format);
      // We can't just copy the bytes directly; the BitmapData .NET class has a stride (padding) associated with it. 
      int bitsPerPixel = ((((int)format >> 8) & 0xFF));
      int length = (int)Math.Ceiling(bitmapData.Width * bitsPerPixel / 8.0);
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

    private static PdfDictionary ProcessFilters(PdfDictionary dictionary)
    {
      PdfDictionary result;

      // Create a dictionary mapping (i.e. switch statement) to process the expected filters.
      var map = new Dictionary<string, Func<byte[], byte[]>>() {
        { "/FlateDecode", (d) => {
          var decoder = new FlateDecode();
          return (decoder.Decode(d));
        } }
      };

      // Get all of the filters.
      var filters = ((PdfArray)dictionary.Elements["/Filter"])
                                         .Elements.Where(e => e.IsName())
                                         .Select(e => ((PdfName)e).Value)
                                         .ToList();
      // If only one filter in array. Just rewrite the /Filter
      if (filters.Count == 1) {
        result = dictionary.Clone();
        result.Elements["/Filter"] = new PdfName(filters[0]);
        return (result);
      }
      
      // Process each filter in order. The last filter should be the actual encoded image.
      byte[] data = dictionary.Stream.Value;
      for(int index = 0; index < (filters.Count - 1); index++) {
        if (! map.ContainsKey(filters[index])) {
          throw new NotSupportedException(String.Format("Encountered embedded image with multiple filters: \"{0}\". Unable to process the filter: \"{1}\".",
                                                        String.Join(",", filters), filters[index]));
        }
        data = map[filters[index]].Invoke(data);
      }

      result = new PdfDictionary();
      result.Elements.Add("/Filter", new PdfName(filters.Last()));
      foreach (var element in dictionary.Elements.Where(e => !String.Equals(e.Key, "/Filter", StringComparison.OrdinalIgnoreCase))) {
        result.Elements.Add(element.Key, element.Value);
      }
      result.CreateStream(data);

      return(result);
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
      var map = new Dictionary<string, Func<PdfDictionary, Image>>() {
        { "/CCITTFaxDecode", ImageFromCCITTFaxDecode },
        { "/DCTDecode", ImageFromDCTDecode },
        { "/FlateDecode", ImageFromFlateDecode }
      };

      string filter = null;
      var element = dictionary.Elements["/Filter"];
      if (element.IsName()) filter = ((PdfName)element).Value;
      else if (element.IsArray()) return(ToImage(ProcessFilters(dictionary)));

      var action = map.ContainsKey(filter ?? String.Empty) ? map[filter] : noAction;
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
      private int _colors;

      /// <summary>The number of colors.</summary>
      public int Colors
      {
        get { return(_colors + 1); }
        private set { _colors = value; }
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
        if (item is PdfString pdfString) return new RawEncoding().GetBytes(pdfString.Value);
      
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
      /// Creates an empty color palette with the required number of colors.
      /// </summary>
      /// <param name="colors">The number of colors to create the palette with.</param>
      /// <returns>An empty <see cref="ColorPalette"/> with the required number of colors.</returns>
      public static ColorPalette CreateColorPalette(int colors)
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
        return ((ColorPalette)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.CreateInstance,
                                                      null, new object[] { colors }, CultureInfo.InvariantCulture));
      }

      /// <summary>
      /// Creates an color palette filled with the specified colors.
      /// </summary>
      /// <param name="colors">The colors to fill / create the palette with.</param>
      /// <returns>An empty <see cref="ColorPalette"/> with the required number of colors.</returns>
      public static ColorPalette CreateColorPalette(params Color[] colors)
      {
        return (CreateColorPalette((IEnumerable<Color>)colors));
      }

      /// <summary>
      /// Creates an color palette filled with the specified colors.
      /// </summary>
      /// <param name="colors">The colors to fill / create the palette with.</param>
      /// <returns>An empty <see cref="ColorPalette"/> with the required number of colors.</returns>
      public static ColorPalette CreateColorPalette(IEnumerable<Color> colors)
      {
        ColorPalette palette = CreateColorPalette(colors.Count());
        Parallel.ForEach(colors, (color, state, index) => {
          palette.Entries[index] = color;
        });
        return (palette);
      }

      /// <summary>
      /// Creates an empty color palette with the required number of colors.
      /// </summary>
      /// <returns>An empty <see cref="ColorPalette"/> with the required number of colors.</returns>
      protected virtual ColorPalette CreateColorPalette()
      {
        return (CreateColorPalette(Colors));
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
        ColorPalette colorPalette = CreateColorPalette();
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
    /// Internal class for working with a Gray colorspace
    /// </summary>
    class PdfGrayColorSpace : PdfRGBColorSpace
    {
      public PdfGrayColorSpace()
      {
      }

      /// <summary>
      /// Converts the specified Pdf colorspace to a Color space.
      /// </summary>
      /// <returns>The ColorPalette representing the raw palette.</returns>
      public ColorPalette ToColorPalette(int bitsPerPixel)
      {
        if (bitsPerPixel > 8) throw new ArgumentException("bitsPerPixel", "Can't support grayscale image with more than 8 bits per pixel.");

        int colors = bitsPerPixel > 1 ? Convert.ToInt32(Math.Pow(2, bitsPerPixel)) : bitsPerPixel + 1;
        ColorPalette palette = PdfIndexedRGBColorSpace.CreateColorPalette(colors);

        Parallel.For(0, colors, (color) => {
          int gray = (int) Math.Floor((256f - 1)/ (colors - 1) * color);
          palette.Entries[color] = Color.FromArgb(gray, gray, gray);
        });
        return (palette);
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
          // Gray Colorspace
          { "/DeviceGray", (a) => new PdfGrayColorSpace() },
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

    #region Optional Decode Parameters
    class CCITTFaxDecodeParameters
    {
      private const string _K = "/K";
      private const string END_OF_LINE = "/EndOfLine";
      private const string ENCODED_BYTE_ALIGN = "/EncodedByteAlign";
      private const string COLUMNS = "/Columns";
      private const string ROWS = "/Rows";
      private const string END_OF_BLOCK = "/EndOfBlock";
      private const string BLACK_IS_1 = "/BlackIs1";
      private const string DAMAGED_ROWS_BEFORE_ERROR = "/DamagedRowsBeforeError";

      private int _columns = 1728;
      private bool _endOfBlock = true;

      /// <summary>
      /// A code identifying the encoding scheme used: 
      /// 
      ///  <0 = Pure two-dimensional encoding (Group 4) 
      ///   0 = Pure one-dimensional encoding (Group 3, 1-D) 
      ///  >0 = Mixed one- and two-dimensional encoding (Group 3, 2-D), 
      ///       in which a line encoded one-dimensionally can be followed 
      ///       by at most K − 1 lines encoded two-dimensionally 
      /// 
      /// The filter distinguishes among negative, zero, and positive values of 
      /// K to determine how to interpret the encoded data; however, it does 
      /// not distinguish between different positive K values. Default value: 0
      /// </summary>
      public int K { get; set; }

      /// <summary>
      /// A flag indicating whether end-of-line bit patterns are required to be 
      /// present in the encoding. The CCITTFaxDecode filter always accepts 
      /// end-of-line bit patterns, but requires them only if EndOfLine is true. 
      /// Default value: false.
      /// </summary>
      public bool EndOfLine { get; set; }

      /// <summary>
      /// A flag indicating whether the filter expects extra 0 bits before each
      /// encoded line so that the line begins on a byte boundary. If true, the
      /// filter skips over encoded bits to begin decoding each line at a byte
      /// boundary. Iffalse, the filter does not expect extra bits in the 
      /// encoded representation. Default value: false.
      /// </summary>
      public bool EncodedByteAlign { get; set; }

      /// <summary>
      /// The width of the image in pixels. If the value is not a multiple of 8,
      /// the filter adjusts the width of the unencoded image to the next 
      /// multiple of 8 so that each line starts on a byte boundary. 
      /// Default value: 1728
      /// </summary>
      public int Columns
      {
        get { return(_columns); }
        set { _columns = value; }
      }

      /// <summary>
      /// The height of the image in scan lines. If the value is 0 or absent, the
      /// image’s height is not predetermined, and the encoded data must be
      /// terminated by an end-of-block bit pattern or by the end of the filter’s 
      /// data. Default value: 0.
      /// </summary>
      public int Rows { get; set; }

      /// <summary>
      /// A flag indicating whether the filter expects the encoded data to be 
      /// terminated by an end-of-block pattern, overriding the Rows parameter.
      /// If false, the filter stops when it has decoded the number of lines 
      /// indicated by Rows or when its data has been exhausted, whichever occurs first.
      /// The end-of-block patter is the CCITT end-of-facsimile-block (EOFB) or
      /// return-to-contorl (RTC_ appropriate for the K parameter. 
      /// Default value: true.
      /// </summary>
      public bool EndOfBlock
      {
        get { return(_endOfBlock); }
        set { _endOfBlock = value; }
      }

      /// <summary>
      /// A flag indicating whether 1 bits are to be interpreted as black pixels 
      /// and 0 bits as white pixels, the reverse of the normal PDF convention 
      /// for image data. Default value: false.
      /// </summary>
      public bool BlackIs1 { get; set; }

      /// <summary>
      /// The number of damaged rows of data to be tolerated before an error
      /// occurs. This entry applies only if EndOfLine is true and K is 
      /// non-negative. Tolerating a damaged row means locating its end in the
      /// encoded data by searching for an EndOfLine pattern and then 
      /// substituting decoded data from the previous row if the previous row
      /// was not damaged, or a white scan line if the previous row was also
      /// damaged. Default value: 0.
      /// </summary>
      public int DamagedRowsBeforeError { get; set; }

      public CCITTFaxDecodeParameters()
      {
      }

      /// <param name="dictionary">The dictionary element to parse / retrieve.</param>
      public CCITTFaxDecodeParameters(PdfDictionary dictionary):this()
      {
        if (dictionary == null) return;

        if (dictionary.Elements.ContainsKey(_K)) {
          K = dictionary.Elements.GetInteger(_K);
        }
        if (dictionary.Elements.ContainsKey(END_OF_LINE)) {
          EndOfLine = dictionary.Elements.GetBoolean(END_OF_LINE);
        }
        if (dictionary.Elements.ContainsKey(ENCODED_BYTE_ALIGN)) {
          EncodedByteAlign = dictionary.Elements.GetBoolean(ENCODED_BYTE_ALIGN);
        }
        if (dictionary.Elements.ContainsKey(COLUMNS)) {
          Columns = dictionary.Elements.GetInteger(COLUMNS);
        }
        if (dictionary.Elements.ContainsKey(ROWS)) {
          Rows = dictionary.Elements.GetInteger(ROWS);
        }
        if (dictionary.Elements.ContainsKey(END_OF_BLOCK)) {
          EndOfBlock = dictionary.Elements.GetBoolean(END_OF_BLOCK);
        }
        if (dictionary.Elements.ContainsKey(BLACK_IS_1)) {
          BlackIs1 = dictionary.Elements.GetBoolean(BLACK_IS_1);
        }
        if (dictionary.Elements.ContainsKey(DAMAGED_ROWS_BEFORE_ERROR)) {
          DamagedRowsBeforeError = dictionary.Elements.GetInteger(DAMAGED_ROWS_BEFORE_ERROR);
        }
      }
    }
    #endregion
  }
}
