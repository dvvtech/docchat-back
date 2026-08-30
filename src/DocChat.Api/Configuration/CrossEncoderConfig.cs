namespace DocChat.Api.Configuration;

public sealed class CrossEncoderConfig
{
    public const string SectionName = "CrossEncoderSettings";

    public string BaseUrl { get; init; } = "https://api.jina.ai/v1/rerank";

    public string Model { get; init; } = "jina-reranker-v2-base-multilingual";

    public string ApiKey { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 30;
}
