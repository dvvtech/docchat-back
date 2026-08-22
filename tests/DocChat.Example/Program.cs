using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
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