using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.IO.Compression;
using UglyToad.PdfPig;

namespace DocChat.Api.Services
{
    public sealed class DocumentTextExtractor
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc",
            ".docx",
            ".txt",
        };

        public async Task<string> ExtractTextAsync(IFormFile file, CancellationToken ct)
        {
            var extension = Path.GetExtension(file.FileName);
            if (!SupportedExtensions.Contains(extension))
            {
                throw new NotSupportedException($"File extension '{extension}' is not supported.");
            }

            await using var stream = file.OpenReadStream();
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => ExtractPdf(stream),
                ".doc" => ExtractDoc(stream),
                ".docx" => ExtractDocx(stream),
                ".txt" => await ExtractTxtAsync(stream, ct),
                _ => throw new NotSupportedException($"File extension '{extension}' is not supported.")
            };
        }

        private static string ExtractPdf(Stream stream)
        {
            using var document = PdfDocument.Open(stream);
            var builder = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                builder.AppendLine(page.Text);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string ExtractDoc(Stream stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();

            var unicodeText = ExtractPrintableRuns(Encoding.Unicode.GetString(bytes), minRunLength: 3);
            var asciiText = ExtractPrintableRuns(Encoding.UTF8.GetString(bytes), minRunLength: 5);

            return unicodeText.Length >= asciiText.Length ? unicodeText : asciiText;
        }

        private static string ExtractDocx(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var builder = new StringBuilder();

            foreach (var entry in archive.Entries.Where(IsWordTextEntry))
            {
                using var entryStream = entry.Open();
                var xml = XDocument.Load(entryStream);
                foreach (var paragraph in xml.Descendants().Where(element => element.Name.LocalName == "p"))
                {
                    var paragraphText = string.Concat(
                        paragraph
                            .Descendants()
                            .Where(element => element.Name.LocalName == "t")
                            .Select(element => element.Value));

                    if (!string.IsNullOrWhiteSpace(paragraphText))
                    {
                        builder.AppendLine(paragraphText);
                    }
                }
            }

            return builder.ToString();
        }

        private static async Task<string> ExtractTxtAsync(Stream stream, CancellationToken ct)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
            return await reader.ReadToEndAsync(ct);
        }

        private static bool IsWordTextEntry(ZipArchiveEntry entry)
        {
            return entry.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
                || (entry.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                || (entry.FullName.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        }

        private static string ExtractPrintableRuns(string text, int minRunLength)
        {
            var runs = Regex.Matches(text, @"[\p{L}\p{N}\p{P}\p{Zs}\t\r\n]{" + minRunLength + @",}")
                .Select(match => Regex.Replace(match.Value, @"[ \t]{2,}", " ").Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value));

            return string.Join(Environment.NewLine, runs);
        }
    }
}
