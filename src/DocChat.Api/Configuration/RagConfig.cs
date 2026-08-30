namespace DocChat.Api.Configuration
{
    public sealed class RagConfig
    {
        public const string SectionName = "RagSettings";

        public string QdrantHost { get; init; } = "localhost";

        public int QdrantPort { get; init; } = 6334;

        public string CollectionName { get; init; } = "documents";

        public string EmbeddingModel { get; init; } = "text-embedding-3-small";

        public ulong EmbeddingVectorSize { get; init; } = 1536;

        public int SemanticChunkerTokenLimit { get; init; } = 512;

        public bool UseReranker { get; init; }

        public string RerankerModel { get; init; } = "gpt-4o-mini";

        public int MaxChunkingInputCharacters { get; init; } = 12000;

        public int RetrievalCandidateMultiplier { get; init; } = 4;

        public double MinSimilarityScore { get; init; } = 0.25;

        public int MaxContextCharacters { get; init; } = 12000;
    }
}
