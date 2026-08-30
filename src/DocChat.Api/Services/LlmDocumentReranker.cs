using System.Text;
using System.Text.Json;
using DocChat.Api.Configuration;
using DocChat.Api.Models.Documents;
using DocChat.Api.Services.Abstractions;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocChat.Api.Services;

public sealed class LlmDocumentReranker : IDocumentReranker
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<LlmDocumentReranker> _logger;

    public LlmDocumentReranker(
        OpenAiClientFactory openAiClientFactory,
        IOptions<RagConfig> ragConfig,
        ILogger<LlmDocumentReranker> logger)
    {
        _chatClient = openAiClientFactory.CreateClient().GetChatClient(ragConfig.Value.RerankerModel);
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchResult>> RerankAsync(
        string query,
        IReadOnlyList<SearchResult> candidates,
        CancellationToken ct)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<SearchResult>();
        }

        var prompt = BuildPrompt(query, candidates);

        string content;
        try
        {
            var completion = await _chatClient.CompleteChatAsync(
                [new UserChatMessage(prompt)],
                new ChatCompletionOptions { Temperature = 0f },
                ct);

            content = completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM reranking failed. Falling back to vector similarity scores.");
            return Fallback(candidates);
        }

        var reranked = ParseScores(content, candidates);
        if (reranked.Count == 0)
        {
            _logger.LogWarning("LLM reranker returned no valid scores. Falling back to vector similarity scores.");
            return Fallback(candidates);
        }

        return reranked
            .OrderByDescending(item => item.Score)
            .ToArray();
    }

    private static string BuildPrompt(string query, IReadOnlyList<SearchResult> candidates)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are a search result reranker. Score each passage's relevance to the query from 0.0 to 1.0.");
        prompt.AppendLine($"Query: {query}");
        for (var i = 0; i < candidates.Count; i++)
        {
            prompt.AppendLine($"[{i}] {candidates[i].Text}");
        }
        prompt.AppendLine("Return ONLY valid JSON: an array of objects {\"index\": <int>, \"score\": <number 0.0-1.0>}, sorted by score descending.");
        return prompt.ToString();
    }

    private static List<SearchResult> ParseScores(string content, IReadOnlyList<SearchResult> candidates)
    {
        var reranked = new List<SearchResult>();

        try
        {
            using var document = JsonDocument.Parse(StripJsonFence(content.Trim()));

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return reranked;
            }

            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("index", out var indexEl) &&
                    element.TryGetProperty("score", out var scoreEl) &&
                    indexEl.ValueKind == JsonValueKind.Number &&
                    scoreEl.ValueKind == JsonValueKind.Number)
                {
                    var index = indexEl.GetInt32();
                    if (index >= 0 && index < candidates.Count)
                    {
                        reranked.Add(candidates[index] with { Score = scoreEl.GetDouble() });
                    }
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }

        return reranked;
    }

    private static string StripJsonFence(string content)
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

    private static IReadOnlyList<SearchResult> Fallback(IReadOnlyList<SearchResult> candidates)
    {
        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();
    }
}
