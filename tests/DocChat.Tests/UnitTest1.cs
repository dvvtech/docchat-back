using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace DocChat.Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task QdrantWriteReadExample()
        {
            var client = new QdrantClient("localhost", 6333);

            const string collectionName = "test_collection";

            // Create collection if not exists
            if (!await client.CollectionExistsAsync(collectionName))
            {
                await client.CreateCollectionAsync(collectionName, new VectorParams
                {
                    Size = 4,
                    Distance = Distance.Cosine
                });
            }

            // Write a point with a vector and payload
            var pointId = new PointId { Uuid = Guid.NewGuid().ToString() };
            await client.UpsertAsync(collectionName, new[]
            {
                new PointStruct
                {
                    Id = pointId,
                    Vectors = new float[] { 0.1f, 0.2f, 0.3f, 0.4f },
                    Payload = { ["key"] = "hello qdrant", ["number"] = 42 }
                }
            });

            // Read the point back by ID
            var retrieved = await client.RetrieveAsync(collectionName, [pointId]);
            Assert.NotEmpty(retrieved);
            Assert.Equal("hello qdrant", retrieved[0].Payload["key"].StringValue);
            Assert.Equal(42, retrieved[0].Payload["number"].IntegerValue);

            // Cleanup: delete the collection
            await client.DeleteCollectionAsync(collectionName);
        }
    }
}
