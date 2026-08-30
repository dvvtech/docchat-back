using System.Text;
using DocChat.Api.Configuration;
using DocChat.Api.Models.Documents;
using DocChat.Api.Services.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocChat.Api.Services;

public sealed class DocumentSearchService : IDocumentSearchService
{
    private readonly AiConfig _aiConfig;
    private readonly RagConfig _ragConfig;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentVectorStore _documentStore;
    private readonly IDocumentReranker _reranker;
    private readonly ChatClient _chatClient;

    public DocumentSearchService(
        IOptions<AiConfig> aiConfig,
        IOptions<RagConfig> ragConfig,
        OpenAiClientFactory openAiClientFactory,
        IEmbeddingService embeddingService,
        IDocumentVectorStore documentStore,
        IDocumentReranker reranker)
    {
        _aiConfig = aiConfig.Value;
        _ragConfig = ragConfig.Value;
        _embeddingService = embeddingService;
        _documentStore = documentStore;
        _reranker = reranker;
        _chatClient = openAiClientFactory.CreateClient().GetChatClient(_aiConfig.Model);
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var topK = Math.Clamp(request.TopK > 0 ? request.TopK : 5, 1, 20);

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, ct);

        var candidateLimit = Math.Clamp(
            topK * Math.Max(1, _ragConfig.RetrievalCandidateMultiplier),
            topK,
            100);

        var results = await _documentStore.SearchAsync(queryEmbedding, candidateLimit, ct);

        if (results.Count == 0)
        {
            return new SearchResponse(
                "No sufficiently relevant documents found for your query.",
                Array.Empty<SearchResultDto>());
        }

        results = await _reranker.RerankAsync(request.Query, results, ct);

        var sources = results
            .Take(topK)
            .Select(r => new SearchResultDto(
                r.DocumentId,
                r.FileName,
                r.ChunkIndex,
                r.Text,
                r.Score
            ))
            .ToArray();

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

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(request.Query)
        };

        var options = new ChatCompletionOptions { Temperature = 0f };
        var completion = await _chatClient.CompleteChatAsync(messages, options, ct);

        var answer = completion.Value.Content.FirstOrDefault()?.Text
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
}
