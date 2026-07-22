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

    public DocumentSearchService(
        IOptions<AiConfig> aiConfig,
        OpenAiClientFactory openAiClientFactory,
        DocumentEmbeddingService embeddingService,
        QdrantDocumentStore documentStore,
        ILogger<DocumentSearchService> logger)
    {
        _aiConfig = aiConfig.Value;
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
                "No relevant documents found for your query.",
                Array.Empty<SearchResultDto>());
        }

        var context = string.Join("\n\n", results.Select((r, i) =>
            $"[Source {i + 1}] Document: {r.FileName}, Chunk: {r.ChunkIndex}\n{r.Text}"));

        var systemPrompt = """
            You are a helpful assistant that answers questions based on the provided document excerpts.
            Use only the information from the provided context to answer the question.
            If the context doesn't contain enough information to answer the question, say so.
            Always cite the source filenames and chunk indices when referencing specific information.

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
            var result = await _chatClient!.CompleteChatAsync(messages, cancellationToken: ct);
            completion = result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI API error during search");
            throw;
        }

        var answer = completion.Content[0].Text;

        var sources = results.Select(r => new SearchResultDto(
            r.DocumentId,
            r.FileName,
            r.ChunkIndex,
            r.Text,
            r.Score
        )).ToArray();

        return new SearchResponse(answer, sources);
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
