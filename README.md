PDFSharp.Extensions
=========

The following are extension methods for PDFSharp to support and simplify some common 
operations.

This project started due to a project requiring for images that where scanned into a PDF to be extracted to allow for portions of the embedded images to be permanently redacted. Most of the solutions and partial bits of code were incomplete or didn't cover my test cases and needs. Originally, I solve the problem using the iTextSharp library but the licensing model for that project prohibited me from continued use and development.

Licensed under the MIT license.

---------------------------------------

Image Utilities
-----------
Extension methods are provided for extracting images from an entire document, 
individual pages or specific images. Currently only RGB encoded images (/DeviceRGB) 
are supported with either /DCTDecode or /FlatEncode encoding. /Indexed colorspaces 
are also supported for /FlatEncode images including 1bpp images (black & white).

All images are extracted as System.Drawing.Image obects which can then be saved or 
manipulated as necessary.

__Example__

```
string filename = @"My.Sample.pdf";
Console.WriteLine("Processing file: {0}", filename);
using (PdfDocument document = PdfReader.Open(filename, PdfDocumentOpenMode.Import)) {
  int pageIndex = 0;
  foreach (PdfPage page in document.Pages) {
    int imageIndex = 0;
    foreach(Image image in page.GetImages()) {
      Console.WriteLine("\r\nExtracting image {1} from page {0}", pageIndex + 1, imageIndex + 1);
      
      // Save the file images to disk in the current directory.
      image.Save(String.Format(@"{0:00000000}-{1:000}.png", pageIndex + 1, imageIndex + 1, Path.GetFileName(filename)), ImageFormat.Png);
      imageIndex++;
    }
    pageIndex++;
  }
}
```

__Notes__

If you find a PDF file that contains an encoded image, which is a /DeviceRGB 
colorspace, that doesn't extract correctly, please send submit a issue and attach
the offending PDF file or send via email. 

Please do not complain about PDF files that are unable to be processed because 
they are iref encoded. This is a limitation of the PDFSharp libraries and on the
product roadmap for implementation.

Helpers
-----------
A helper class has been created for a PdfDictionary object that will allow you to 
quickly inspect to see if the dictionary is an image. A number of other helper 
extension methods have been created for PdfItem and other base classes for common
inspection related tasks.

```
PdfDictionary item;
if (item.IsImage()) {
  Image image = item.ToImage();
}
```
