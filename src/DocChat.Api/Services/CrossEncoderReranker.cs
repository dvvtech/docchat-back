using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DocChat.Api.Configuration;
using DocChat.Api.Models.Documents;
using DocChat.Api.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace DocChat.Api.Services;

public sealed class CrossEncoderReranker : IDocumentReranker
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly CrossEncoderConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CrossEncoderReranker> _logger;

    public CrossEncoderReranker(
        IOptions<CrossEncoderConfig> config,
        IOptions<ProxyConfig> proxyConfig,
        ILogger<CrossEncoderReranker> logger)
    {
        _config = config.Value;
        _logger = logger;

        var handler = proxyConfig.Value.TryCreateHttpHandler(logger);
        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(_config.TimeoutSeconds, 1, 300));

        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        }
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

        try
        {
            var request = new RerankRequest
            {
                Model = _config.Model,
                Query = query,
                Documents = candidates.Select(candidate => candidate.Text).ToArray(),
                TopN = candidates.Count,
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(request, SerializerOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(_config.BaseUrl, content, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<RerankResponse>(stream, SerializerOptions, ct);

            var results = payload?.Results;
            if (results is null || results.Count == 0)
            {
                _logger.LogWarning("Cross-encoder returned no results. Falling back to vector similarity scores.");
                return Fallback(candidates);
            }

            var reranked = results
                .Where(r => r.Index >= 0 && r.Index < candidates.Count)
                .Select(r => candidates[r.Index] with { Score = r.RelevanceScore })
                .ToArray();

            if (reranked.Length == 0)
            {
                _logger.LogWarning("Cross-encoder returned no valid indexes. Falling back to vector similarity scores.");
                return Fallback(candidates);
            }

            return reranked
                .OrderByDescending(item => item.Score)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cross-encoder reranking failed. Falling back to vector similarity scores.");
            return Fallback(candidates);
        }
    }

    private static IReadOnlyList<SearchResult> Fallback(IReadOnlyList<SearchResult> candidates)
    {
        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();
    }

    private sealed class RerankRequest
    {
        public string Model { get; init; } = string.Empty;

        public string Query { get; init; } = string.Empty;

        public IReadOnlyList<string> Documents { get; init; } = [];

        public int TopN { get; init; }
    }

    private sealed class RerankResponse
    {
        public List<RerankResult>? Results { get; init; }
    }

    private sealed class RerankResult
    {
        public int Index { get; init; }

        public double RelevanceScore { get; init; }
    }
}
