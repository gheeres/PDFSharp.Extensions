using System;
using SixLabors.ImageSharp;
using System.IO;
using System.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Drawing;
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
            const SearchOption o = SearchOption.AllDirectories;
            var files = Directory.GetFiles(root, "*.pdf", o);
            foreach (var file in files)
                try
                {
                    ExtractImages(output, file);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"{file} -> {e.Message}");
                }

            var doc = files.FirstOrDefault(f => f.Contains("sample"));
            ExtractDocument(output, doc);

            var imgs = Directory.GetFiles(root, "*.png", o);
            CombineImages(output, imgs);
        }

        private static void CombineImages(string output, string[] filenames)
        {
            var images = filenames.Select(Image.Load).ToArray();

            var name = Path.GetFileNameWithoutExtension(filenames.First());
            var path = Path.Combine(output, $"{name}.pdf");
            using (PdfDocument pdf = images.First().ToPdf())
                pdf.Save(path);

            name = Path.GetFileNameWithoutExtension(filenames.Last());
            path = Path.Combine(output, $"{name}.pdf");
            using (PdfDocument pdf = images.ToPdf())
                pdf.Save(path);
        }

        private static void ExtractDocument(string output, string filename)
        {
            using (var document = PdfReader.Open(filename, PdfDocumentOpenMode.Import))
            {
                using var image = document.GetImages().Single();
                var name = Path.GetFileNameWithoutExtension(filename);
                var path = Path.Combine(output, $"{name}.png");
                image.SaveAsPng(path);

                var page = document.Pages[0];

                var elements = page.Elements;
                var array = elements.Values.OfType<PdfArray>().Single();
                Console.WriteLine($" {nameof(PdfArray)} " +
                                  $"{nameof(PdfArrayExtensions.IsEmpty)} " +
                                  $"= {array.IsEmpty()}");
                array.Dump();

                var resources = page.Resources.Elements;
                var dict = resources.Values.OfType<PdfDictionary>().First();
                dict.Dump();
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