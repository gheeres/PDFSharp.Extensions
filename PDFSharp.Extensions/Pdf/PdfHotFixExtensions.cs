using System.IO.Compression;
using System.IO;
using PdfSharpCore.Pdf;

namespace PDFSharp.Extensions.Pdf
{
    internal static class PdfHotFixExtensions
    {
        public static byte[] RepeatUnFilter(this PdfDictionary.PdfStream dictStream)
        {
            var input = dictStream.Value;
            if (input == null)
                return null;

            using var archive = new MemoryStream(input);
            archive.Position = 2; // Skip header

            using var output = new MemoryStream();
            using var deflate = new DeflateStream(archive, CompressionMode.Decompress);
            deflate.CopyTo(output);

            return output.ToArray();
        }
    }
}