using System.Text;
using System.Text.Json;
using DocChat.Api.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocChat.Api.Services
{
    public sealed class LlmDocumentChunker
    {
        private readonly RagConfig _ragConfig;
        private readonly OpenAiClientFactory _openAiClientFactory;
        private readonly ILogger<LlmDocumentChunker> _logger;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private ChatClient? _chatClient;
        private bool _initialized;

        public LlmDocumentChunker(
            IOptions<RagConfig> ragConfig,
            OpenAiClientFactory openAiClientFactory,
            ILogger<LlmDocumentChunker> logger)
        {
            _ragConfig = ragConfig.Value;
            _openAiClientFactory = openAiClientFactory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<string>> ChunkAsync(string text, CancellationToken ct)
        {
            var normalizedText = NormalizeText(text);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return Array.Empty<string>();
            }

            await EnsureInitializedAsync(ct);

            var chunks = new List<string>();
            foreach (var fragment in SplitByLength(normalizedText, _ragConfig.MaxChunkingInputCharacters))
            {
                var fragmentChunks = await ChunkFragmentWithLlmAsync(fragment, ct);
                chunks.AddRange(fragmentChunks);
            }

            return chunks.Count > 0
                ? chunks
                : SplitByLength(normalizedText, _ragConfig.MaxChunkCharacters).ToArray();
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(ct);
            try
            {
                if (_initialized) return;

                var openAi = _openAiClientFactory.CreateClient();
                _chatClient = openAi.GetChatClient(_ragConfig.ChunkingModel);
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task<IReadOnlyList<string>> ChunkFragmentWithLlmAsync(string fragment, CancellationToken ct)
        {
            var messages = new ChatMessage[]
            {
                new SystemChatMessage(
                    "You split documents into RAG chunks. Return only valid JSON: an array of strings. " +
                    "Each chunk must be self-contained, preserve the source language, and be no longer than " +
                    $"{_ragConfig.MaxChunkCharacters} characters. Do not summarize and do not add comments."),
                new UserChatMessage($"Document text:\n{fragment}")
            };

            var options = new ChatCompletionOptions
            {
                Temperature = 0f,
            };

            try
            {
                var completion = await _chatClient!.CompleteChatAsync(messages, options, ct);
                var content = completion.Value.Content.FirstOrDefault()?.Text;
                var chunks = ParseChunks(content);

                return chunks.Count > 0
                    ? chunks
                    : SplitByLength(fragment, _ragConfig.MaxChunkCharacters).ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to split document fragment with LLM. Falling back to local chunking.");
                return SplitByLength(fragment, _ragConfig.MaxChunkCharacters).ToArray();
            }
        }

        private static IReadOnlyList<string> ParseChunks(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return Array.Empty<string>();
            }

            var json = StripMarkdownJsonFence(content.Trim());

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<string>();
                }

                return document.RootElement
                    .EnumerateArray()
                    .Where(element => element.ValueKind == JsonValueKind.String)
                    .Select(element => NormalizeText(element.GetString() ?? string.Empty))
                    .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
                    .ToArray();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }

        private static string StripMarkdownJsonFence(string content)
        {
            if (!content.StartsWith("```", StringComparison.Ordinal))
            {
                return content;
            }

            var firstNewLine = content.IndexOf('\n');
            var lastFence = content.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine < 0 || lastFence <= firstNewLine)
            {
                return content;
            }

            return content[(firstNewLine + 1)..lastFence].Trim();
        }

        private static string NormalizeText(string text)
        {
            var lines = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join('\n', lines);
        }

        private static IEnumerable<string> SplitByLength(string text, int maxLength)
        {
            if (text.Length <= maxLength)
            {
                yield return text;
                yield break;
            }

            var current = new StringBuilder();
            foreach (var paragraph in text.Split('\n'))
            {
                if (paragraph.Length > maxLength)
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }

                    for (var i = 0; i < paragraph.Length; i += maxLength)
                    {
                        yield return paragraph.Substring(i, Math.Min(maxLength, paragraph.Length - i));
                    }

                    continue;
                }

                if (current.Length + paragraph.Length + 1 > maxLength && current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append('\n');
                }

                current.Append(paragraph);
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }
    }
}
