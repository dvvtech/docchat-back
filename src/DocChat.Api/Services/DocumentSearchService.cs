using System.Text;
using DocChat.Api.Configuration;
using DocChat.Api.Models.Documents;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocChat.Api.Services;

public sealed class DocumentSearchService
{
    private readonly ILogger<DocumentSearchService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private ChatClient? _chatClient;
    private bool _initialized;

    private readonly AiConfig _aiConfig;
    private readonly OpenAiClientFactory _openAiClientFactory;
    private readonly DocumentEmbeddingService _embeddingService;
    private readonly QdrantDocumentStore _documentStore;
    private readonly RagConfig _ragConfig;

    public DocumentSearchService(
        IOptions<AiConfig> aiConfig,
        IOptions<RagConfig> ragConfig,
        OpenAiClientFactory openAiClientFactory,
        DocumentEmbeddingService embeddingService,
        QdrantDocumentStore documentStore,
        ILogger<DocumentSearchService> logger)
    {
        _aiConfig = aiConfig.Value;
        _ragConfig = ragConfig.Value;
        _openAiClientFactory = openAiClientFactory;
        _embeddingService = embeddingService;
        _documentStore = documentStore;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var topK = Math.Clamp(request.TopK > 0 ? request.TopK : 5, 1, 20);

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, ct);

        var results = await _documentStore.SearchAsync(queryEmbedding, topK, ct);

        if (results.Count == 0)
        {
            return new SearchResponse(
                "No sufficiently relevant documents found for your query.",
                Array.Empty<SearchResultDto>());
        }

        var sources = results.Select(r => new SearchResultDto(
            r.DocumentId,
            r.FileName,
            r.ChunkIndex,
            r.Text,
            r.Score
        )).ToArray();

        var context = BuildContext(sources);

        var systemPrompt = """
            You answer questions using only the provided document excerpts.

            Rules:
            - If the excerpts do not contain enough information, say that the documents do not contain enough information.
            - Do not invent facts, names, dates, numbers, or document details.
            - Cite every factual claim that comes from the excerpts using [filename#chunkIndex].
            - Prefer a concise answer in the same language as the user's question.

            Context:
            """ + context;

        await EnsureInitializedAsync(ct);

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(request.Query)
        };

        ChatCompletion completion;
        try
        {
            var options = new ChatCompletionOptions { Temperature = 0f };
            var result = await _chatClient!.CompleteChatAsync(messages, options, ct);
            completion = result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API error during search");
            throw;
        }

        var answer = completion.Content.FirstOrDefault()?.Text
            ?? "The model returned an empty answer.";

        return new SearchResponse(answer, sources);
    }

    private string BuildContext(IReadOnlyList<SearchResultDto> sources)
    {
        var maxContextCharacters = Math.Max(1000, _ragConfig.MaxContextCharacters);
        var builder = new StringBuilder(capacity: Math.Min(maxContextCharacters, 16_384));

        foreach (var source in sources)
        {
            var block = $"""
                [Source: {source.FileName}#{source.ChunkIndex}]
                {source.Text.Trim()}

                """;

            var remaining = maxContextCharacters - builder.Length;
            if (remaining <= 0)
            {
                break;
            }

            if (block.Length > remaining)
            {
                if (builder.Length == 0)
                {
                    builder.Append(block[..remaining]);
                }

                break;
            }

            builder.Append(block);
        }

        return builder.ToString();
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            var openAi = _openAiClientFactory.CreateClient();
            _chatClient = openAi.GetChatClient(_aiConfig.Model);

            _initialized = true;
            _logger.LogInformation("DocumentSearchService initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DocumentSearchService");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }
}
