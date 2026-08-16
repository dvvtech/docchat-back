using System.Security.Cryptography;
using System.Text;
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
            CancellationToken ct,
            int chunkIndexOffset = 0)
        {
            if (chunks.Count != embeddings.Count)
            {
                throw new InvalidOperationException("Chunks and embeddings count mismatch.");
            }

            await EnsureCollectionAsync(ct);

            var uploadedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            var points = chunks.Select((chunk, index) =>
            {
                var chunkIndex = chunkIndexOffset + index;

                return new PointStruct
                {
                    Id = new PointId { Uuid = CreateStablePointId(documentId, chunkIndex).ToString() },
                    Vectors = embeddings[index],
                    Payload =
                    {
                        ["documentId"] = documentId,
                        ["fileName"] = fileName,
                        ["chunkIndex"] = chunkIndex,
                        ["text"] = chunk,
                        ["characterCount"] = chunk.Length,
                        ["uploadedAtUtc"] = uploadedAtUtc
                    }
                };
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

            var searchLimit = (ulong)Math.Clamp(
                topK * Math.Max(1, _ragConfig.RetrievalCandidateMultiplier),
                topK,
                100);

            var results = await _qdrantClient.SearchAsync(
                _ragConfig.CollectionName,
                queryVector,
                limit: searchLimit,
                cancellationToken: ct);

            return results
                .Where(point => point.Score >= _ragConfig.MinSimilarityScore)
                .Take(topK)
                .Select(ToSearchResult)
                .ToArray();
        }

        public async Task<IReadOnlyList<SearchResult>> GetChunksAsync(
            int maxChunks,
            CancellationToken ct)
        {
            await EnsureCollectionAsync(ct);

            var chunks = new List<SearchResult>(Math.Min(maxChunks, 1024));
            PointId? offset = null;

            while (chunks.Count < maxChunks)
            {
                ct.ThrowIfCancellationRequested();

                var limit = (uint)Math.Min(256, maxChunks - chunks.Count);
                var response = await _qdrantClient.ScrollAsync(
                    _ragConfig.CollectionName,
                    limit: limit,
                    offset: offset,
                    cancellationToken: ct);

                if (response.Result.Count == 0)
                {
                    break;
                }

                chunks.AddRange(response.Result.Select(ToSearchResult));
                offset = response.NextPageOffset;

                if (offset is null)
                {
                    break;
                }
            }

            return chunks;
        }

        private static SearchResult ToSearchResult(ScoredPoint point)
        {
            return new SearchResult(
                point.Payload["documentId"].StringValue,
                point.Payload["fileName"].StringValue,
                (int)point.Payload["chunkIndex"].IntegerValue,
                point.Payload["text"].StringValue,
                point.Score
            );
        }

        private static SearchResult ToSearchResult(RetrievedPoint point)
        {
            return new SearchResult(
                point.Payload["documentId"].StringValue,
                point.Payload["fileName"].StringValue,
                (int)point.Payload["chunkIndex"].IntegerValue,
                point.Payload["text"].StringValue,
                0d
            );
        }

        private static Guid CreateStablePointId(string documentId, int chunkIndex)
        {
            var bytes = Encoding.UTF8.GetBytes($"{documentId}:{chunkIndex}");
            var hash = SHA256.HashData(bytes);
            return new Guid(hash[..16]);
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
