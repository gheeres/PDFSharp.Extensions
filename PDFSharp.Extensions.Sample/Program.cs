using System;
using SixLabors.ImageSharp;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;

namespace PDFSharp.Extensions.Sample
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var root = args.FirstOrDefault() ?? Path.Combine("res");
            var output = Directory.CreateDirectory("out").Name;
            var files = Directory.GetFiles(root, "*.pdf", SearchOption.AllDirectories);
            foreach (var file in files)
                try
                {
                    ExtractImages(output, file);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"{file} -> {e.Message}");
                }
        }

        private static void ExtractImages(string output, string filename)
        {
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
                        path = Path.Combine(output, path);
                        image.SaveAsPng(path);
                        imageIndex++;
                    }
                    pageIndex++;
                }
            }
        }
    }
}