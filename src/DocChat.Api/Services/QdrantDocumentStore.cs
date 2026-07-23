using DocChat.Api.Configuration;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace DocChat.Api.Services
{
    public sealed class QdrantDocumentStore
    {
        private readonly RagConfig _ragConfig;
        private readonly QdrantClient _qdrantClient;
        private readonly SemaphoreSlim _collectionLock = new(1, 1);
        private bool _collectionReady;

        public QdrantDocumentStore(IOptions<RagConfig> ragConfig)
        {
            _ragConfig = ragConfig.Value;
            _qdrantClient = new QdrantClient(_ragConfig.QdrantHost, _ragConfig.QdrantPort);
        }

        public async Task SaveChunksAsync(
            string documentId,
            string fileName,
            IReadOnlyList<string> chunks,
            IReadOnlyList<float[]> embeddings,
            CancellationToken ct)
        {
            if (chunks.Count != embeddings.Count)
            {
                throw new InvalidOperationException("Chunks and embeddings count mismatch.");
            }

            await EnsureCollectionAsync(ct);

            var points = chunks.Select((chunk, index) => new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString() },
                Vectors = embeddings[index],
                Payload =
                {
                    ["documentId"] = documentId,
                    ["fileName"] = fileName,
                    ["chunkIndex"] = index,
                    ["text"] = chunk,
                    ["characterCount"] = chunk.Length,
                    ["uploadedAtUtc"] = DateTimeOffset.UtcNow.ToString("O")
                }
            }).ToArray();

            await _qdrantClient.UpsertAsync(_ragConfig.CollectionName, points, cancellationToken: ct);
        }

        public async Task DeleteDocumentAsync(string documentId, CancellationToken ct)
        {
            await EnsureCollectionAsync(ct);

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "documentId",
                            Match = new Match { Keyword = documentId }
                        }
                    }
                }
            };

            await _qdrantClient.DeleteAsync(_ragConfig.CollectionName, filter, cancellationToken: ct);
        }

        public async Task<IReadOnlyList<SearchResult>> SearchAsync(
            float[] queryVector,
            int topK,
            CancellationToken ct)
        {
            await EnsureCollectionAsync(ct);

            var results = await _qdrantClient.SearchAsync(
                _ragConfig.CollectionName,
                queryVector,
                limit: (ulong)topK,
                cancellationToken: ct);

            return results.Select(point => new SearchResult(
                point.Payload["documentId"].StringValue,
                point.Payload["fileName"].StringValue,
                (int)point.Payload["chunkIndex"].IntegerValue,
                point.Payload["text"].StringValue,
                point.Score
            )).ToArray();
        }

        private async Task EnsureCollectionAsync(CancellationToken ct)
        {
            if (_collectionReady) return;

            await _collectionLock.WaitAsync(ct);
            try
            {
                if (_collectionReady) return;

                if (!await _qdrantClient.CollectionExistsAsync(_ragConfig.CollectionName, cancellationToken: ct))
                {
                    await _qdrantClient.CreateCollectionAsync(
                        _ragConfig.CollectionName,
                        new VectorParams
                        {
                            Size = _ragConfig.EmbeddingVectorSize,
                            Distance = Distance.Cosine
                        },
                        cancellationToken: ct);
                }

                _collectionReady = true;
            }
            finally
            {
                _collectionLock.Release();
            }
        }
    }

    public sealed record SearchResult(
        string DocumentId,
        string FileName,
        int ChunkIndex,
        string Text,
        double Score);
}
