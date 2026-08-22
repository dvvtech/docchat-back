using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SemanticChunkerNET;

var builder = Kernel.CreateBuilder();
builder.AddOpenAIEmbeddingGenerator(
    modelId: "text-embedding-3-small", // или ваша модель
    apiKey: "ВАШ_API_КЛЮЧ"
);
var kernel = builder.Build();
var embeddingGenerator = kernel.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

// 2. Создание чанкера
// tokenLimit: лимит токенов вашей модели эмбеддингов
var semanticChunker = new SemanticChunker(embeddingGenerator, tokenLimit: 512);

// 3. Нарезка текста
string inputText = "Ваш длинный текст для нарезки...";
IList<Chunk> chunks = await semanticChunker.CreateChunksAsync(inputText);

// 4. Использование полученных чанков
foreach (var chunk in chunks)
{
    Console.WriteLine($"Чанк {chunk.Id}: {chunk.Text.Length} символов, {chunk.Embedding.Vector.Length} измерений");
}

// 5. Сохранение чанков с векторами в Qdrant
const string qdrantHost = "localhost";
const int qdrantPort = 6334;
const string collectionName = "documents";
const ulong vectorSize = 1536; // размерность text-embedding-3-small
const string documentId = "example-doc-001";

var qdrant = new QdrantClient(qdrantHost, qdrantPort);

if (!await qdrant.CollectionExistsAsync(collectionName))
{
    await qdrant.CreateCollectionAsync(
        collectionName,
        new VectorParams
        {
            Size = vectorSize,
            Distance = Distance.Cosine
        });
}

var points = chunks.Select((chunk, index) => new PointStruct
{
    Id = new PointId { Uuid = Guid.NewGuid().ToString() },
    Vectors = chunk.Embedding.Vector.ToArray(),
    Payload =
    {
        ["documentId"] = documentId,
        ["chunkIndex"] = index,
        ["text"] = chunk.Text,
        ["characterCount"] = chunk.Text.Length,
        ["uploadedAtUtc"] = DateTimeOffset.UtcNow.ToString("O")
    }
}).ToArray();

await qdrant.UpsertAsync(collectionName, points);

Console.WriteLine($"Сохранено {points.Length} точек в коллекцию '{collectionName}'.");