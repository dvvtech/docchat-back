using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.IO.Compression;
using DocChat.Api.Configuration;
using DocChat.Api.Exceptions;
using DocChat.Api.Services.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace DocChat.Api.Services
{
    public sealed class DocumentTextExtractor : IDocumentTextExtractor
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".doc",
            ".docx",
            ".txt",
        };

        private readonly RagConfig _ragConfig;

        public DocumentTextExtractor(IOptions<RagConfig> ragConfig)
        {
            _ragConfig = ragConfig.Value;
        }

        public async IAsyncEnumerable<string> ExtractTextPagesAsync(
            IFormFile file,
            [EnumeratorCancellation] CancellationToken ct)
        {
            var extension = Path.GetExtension(file.FileName);
            if (!SupportedExtensions.Contains(extension))
            {
                throw new UnsupportedFileTypeException($"File extension '{extension}' is not supported.");
            }

            await using var stream = file.OpenReadStream();

            switch (extension.ToLowerInvariant())
            {
                case ".pdf":
                    foreach (var page in ExtractPdfPages(stream))
                        yield return page;
                    break;

                case ".docx":
                    foreach (var page in ExtractDocxPages(stream))
                        yield return page;
                    break;

                case ".doc":
                    foreach (var page in ExtractDocPages(stream))
                        yield return page;
                    break;

                case ".txt":
                    await foreach (var page in ExtractTxtPagesAsync(stream, ct))
                        yield return page;
                    break;
            }
        }

        private IEnumerable<string> ExtractPdfPages(Stream stream)
        {
            using var document = PdfDocument.Open(stream);
            var batch = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                batch.AppendLine(page.Text);
                batch.AppendLine();

                if (batch.Length >= _ragConfig.MaxChunkingInputCharacters)
                {
                    yield return batch.ToString();
                    batch.Clear();
                }
            }

            if (batch.Length > 0)
                yield return batch.ToString();
        }

        private IEnumerable<string> ExtractDocxPages(Stream stream)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var batch = new StringBuilder();

            foreach (var entry in archive.Entries.Where(IsWordTextEntry))
            {
                using var entryStream = entry.Open();
                var xml = XDocument.Load(entryStream);

                foreach (var paragraph in xml.Descendants().Where(e => e.Name.LocalName == "p"))
                {
                    var text = string.Concat(
                        paragraph
                            .Descendants()
                            .Where(e => e.Name.LocalName == "t")
                            .Select(e => e.Value));

                    if (string.IsNullOrWhiteSpace(text))
                        continue;

                    batch.AppendLine(text);

                    if (batch.Length >= _ragConfig.MaxChunkingInputCharacters)
                    {
                        yield return batch.ToString();
                        batch.Clear();
                    }
                }
            }

            if (batch.Length > 0)
                yield return batch.ToString();
        }

        private IEnumerable<string> ExtractDocPages(Stream stream)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();

            var unicodeRuns = Regex.Matches(
                Encoding.Unicode.GetString(bytes), @"[\p{L}\p{N}\p{P}\p{Zs}\t\r\n]{3,}")
                .Select(m => Regex.Replace(m.Value, @"[ \t]{2,}", " ").Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v));

            var asciiRuns = Regex.Matches(
                Encoding.UTF8.GetString(bytes), @"[\p{L}\p{N}\p{P}\p{Zs}\t\r\n]{5,}")
                .Select(m => Regex.Replace(m.Value, @"[ \t]{2,}", " ").Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v));

            var text = string.Join(Environment.NewLine,
                unicodeRuns.Sum(r => r.Length) >= asciiRuns.Sum(r => r.Length)
                    ? unicodeRuns
                    : asciiRuns);

            foreach (var page in SplitByLength(text, _ragConfig.MaxChunkingInputCharacters))
                yield return page;
        }

        private static async IAsyncEnumerable<string> ExtractTxtPagesAsync(
            Stream stream,
            [EnumeratorCancellation] CancellationToken ct)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true, leaveOpen: false);

            var batch = new StringBuilder();
            var buffer = new char[4096];

            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var charsRead = await reader.ReadAsync(buffer, ct);
                if (charsRead == 0)
                    break;

                batch.Append(buffer, 0, charsRead);

                if (batch.Length >= 12000)
                {
                    yield return batch.ToString();
                    batch.Clear();
                }
            }

            if (batch.Length > 0)
                yield return batch.ToString();
        }

        private static bool IsWordTextEntry(ZipArchiveEntry entry)
        {
            return entry.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)
                || (entry.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase)
                    && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                || (entry.FullName.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase)
                    && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> SplitByLength(string text, int maxLength)
        {
            for (var i = 0; i < text.Length; i += maxLength)
                yield return text.Substring(i, Math.Min(maxLength, text.Length - i));
        }
    }
}
