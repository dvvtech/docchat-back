using DocChat.Api.Configuration;
using DocChat.Api.Services;
using DocChat.Api.Services.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace DocChat.Api.AppStart
{
    internal sealed class Startup
    {
        private readonly WebApplicationBuilder _builder;

        public Startup(WebApplicationBuilder builder)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        }

        public void Initialize()
        {
            if (_builder.Environment.IsDevelopment())
            {
                _builder.Services.AddSwaggerGen();
            }

            InitConfigs();
            ConfigureServices();

            _builder.Services.AddControllers();
        }

        private void InitConfigs()
        {
            _builder.Services.Configure<AiConfig>(_builder.Configuration.GetSection(AiConfig.SectionName));
            _builder.Services.Configure<ProxyConfig>(_builder.Configuration.GetSection(ProxyConfig.SectionName));
            _builder.Services.Configure<RagConfig>(_builder.Configuration.GetSection(RagConfig.SectionName));
        }

        private void ConfigureServices()
        {
            _builder.Services.AddSingleton<OpenAiClientFactory>();

            _builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            {
                var ragConfig = sp.GetRequiredService<IOptions<RagConfig>>().Value;
                var openAiClientFactory = sp.GetRequiredService<OpenAiClientFactory>();
                return openAiClientFactory
                    .CreateClient()
                    .GetEmbeddingClient(ragConfig.EmbeddingModel)
                    .AsIEmbeddingGenerator();
            });

            _builder.Services.AddSingleton<IDocumentChunker, SemanticDocumentChunker>();
            _builder.Services.AddSingleton<IEmbeddingService, DocumentEmbeddingService>();
            _builder.Services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
            _builder.Services.AddSingleton<IDocumentVectorStore, QdrantDocumentStore>();
            _builder.Services.AddSingleton<IDocumentReranker, LlmDocumentReranker>();
            _builder.Services.AddSingleton<IDocumentSearchService, DocumentSearchService>();
            _builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        }
    }
}
