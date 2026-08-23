using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SemanticChunkerNET;
using System.Text;

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
    Id = new PointId { Uuid = Guid.NewGuid().ToString() },//documentId + ":" + chunkIndex
    Vectors = chunk.Embedding.Vector.ToArray(),
    Payload =
    {
        ["documentId"] = documentId,
        ["chunkIndex"] = index,
        ["text"] = chunk.Text,
        ["indexedAtUtc"] = DateTimeOffset.UtcNow.ToString("O")
    }
}).ToArray();

await qdrant.UpsertAsync(collectionName, points);

Console.WriteLine($"Сохранено {points.Length} точек в коллекцию '{collectionName}'.");

// 6. Поиск по документу
string query = "что искать в документе";
var queryEmbedding = (await embeddingGenerator.GenerateAsync([query])).First().Vector.ToArray();

var searchResults = await qdrant.SearchAsync(
    collectionName,
    queryEmbedding,
    limit: 10,
    scoreThreshold: 0.7f);//порог релевантности

Console.WriteLine($"Найдено {searchResults.Count} результатов по запросу '{query}':");
foreach (var result in searchResults)
{
    Console.WriteLine($"  Score={result.Score:F4}: {result.Payload["text"].StringValue}");
}

// 7. Reranker (LLM-ранжирование через OpenAI)  есть еще способ cross-encoder
bool useReranker = true;
if (useReranker)
{
    var reranked = await RerankWithLlmAsync(query, searchResults, "ВАШ_API_КЛЮЧ");

    Console.WriteLine($"\nРезультаты после reranking по запросу '{query}':");
    foreach (var (point, score) in reranked)
    {
        Console.WriteLine($"  RerankScore={score:F4}: {point.Payload["text"].StringValue}");
    }
}

async Task<IReadOnlyList<(ScoredPoint Point, double Score)>> RerankWithLlmAsync(
    string query,
    IReadOnlyList<ScoredPoint> candidates,
    string apiKey)
{
    if (candidates.Count == 0)
    {
        return Array.Empty<(ScoredPoint, double)>();
    }

    var chatClient = new OpenAI.Chat.ChatClient("gpt-4o-mini", apiKey);

    var prompt = new StringBuilder();
    prompt.AppendLine("You are a search result reranker. Score each passage's relevance to the query from 0.0 to 1.0.");
    prompt.AppendLine($"Query: {query}");
    for (var i = 0; i < candidates.Count; i++)
    {
        prompt.AppendLine($"[{i}] {candidates[i].Payload["text"].StringValue}");
    }
    prompt.AppendLine("Return ONLY valid JSON: an array of objects {\"index\": <int>, \"score\": <number 0.0-1.0>}, sorted by score descending.");

    var completion = await chatClient.CompleteChatAsync(
        [new OpenAI.Chat.UserChatMessage(prompt.ToString())],
        new OpenAI.Chat.ChatCompletionOptions { Temperature = 0f });

    var content = StripJsonFence(completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty);

    var reranked = new List<(ScoredPoint, double)>();
    try
    {
        using var document = System.Text.Json.JsonDocument.Parse(content);
        if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("index", out var indexEl) &&
                    element.TryGetProperty("score", out var scoreEl) &&
                    indexEl.ValueKind == System.Text.Json.JsonValueKind.Number &&
                    scoreEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    var index = indexEl.GetInt32();
                    if (index >= 0 && index < candidates.Count)
                    {
                        reranked.Add((candidates[index], scoreEl.GetDouble()));
                    }
                }
            }
        }
    }
    catch (System.Text.Json.JsonException)
    {
        return candidates.Select(c => (c, (double)c.Score)).ToArray();
    }

    return reranked.Count > 0
        ? reranked
        : candidates.Select(c => (c, (double)c.Score)).ToArray();
}

string StripJsonFence(string content)
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