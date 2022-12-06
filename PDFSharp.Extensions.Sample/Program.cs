using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PDFSharp.Extensions.Sample
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            ExtractImages(args);
        }

        private static void ExtractImages(string[] args)
        {
            var filename = args.FirstOrDefault() ?? Path.Combine("res", "Sample.pdf");
            Console.WriteLine("Processing file: {0}", filename);

            using (var document = PdfReader.Open(filename, PdfDocumentOpenMode.Import))
            {
                var pageIndex = 0;
                foreach (PdfPage page in document.Pages)
                {
                    var imageIndex = 0;
                    foreach (var image in page.GetImages())
                    {
                        var currPage = pageIndex + 1;
                        var currImg = imageIndex + 1;
                        Console.WriteLine("\r\nExtracting image {1} from page {0}", currPage, currImg);

                        var pre = Path.GetFileNameWithoutExtension(filename);
                        var path = string.Format(@"{2} {0:00000000}-{1:000}.png", currPage, currImg, pre);
                        image.Save(path, ImageFormat.Png);
                        imageIndex++;
                    }
                    pageIndex++;
                }
            }
        }
    }
}