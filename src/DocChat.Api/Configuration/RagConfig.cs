namespace DocChat.Api.Configuration
{
    public sealed class RagConfig
    {
        public const string SectionName = "RagSettings";

        public string QdrantHost { get; init; } = "localhost";

        public int QdrantPort { get; init; } = 6334;

        public string CollectionName { get; init; } = "documents";

        public string ChunkingModel { get; init; } = "gpt-4o-mini";

        public string EmbeddingModel { get; init; } = "text-embedding-3-small";

        public ulong EmbeddingVectorSize { get; init; } = 1536;

        public int MaxChunkCharacters { get; init; } = 1800;

        public int MaxChunkingInputCharacters { get; init; } = 12000;
    }
}
