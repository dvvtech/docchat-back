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
            _builder.Services.Configure<CrossEncoderConfig>(_builder.Configuration.GetSection(CrossEncoderConfig.SectionName));
        }

        private void ConfigureServices()
        {
            var ragConfig = _builder.Configuration
                .GetSection(RagConfig.SectionName)
                .Get<RagConfig>() ?? new RagConfig();

            _builder.Services.AddSingleton<OpenAiClientFactory>();

            _builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            {
                var ragSettings = sp.GetRequiredService<IOptions<RagConfig>>().Value;
                var openAiClientFactory = sp.GetRequiredService<OpenAiClientFactory>();
                return openAiClientFactory
                    .CreateClient()
                    .GetEmbeddingClient(ragSettings.EmbeddingModel)
                    .AsIEmbeddingGenerator();
            });

            _builder.Services.AddSingleton<IDocumentChunker, SemanticDocumentChunker>();
            _builder.Services.AddSingleton<IEmbeddingService, DocumentEmbeddingService>();
            _builder.Services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();
            _builder.Services.AddSingleton<IDocumentVectorStore, QdrantDocumentStore>();

            switch (ragConfig.RerankMethod)
            {
                case RerankMethod.Llm:
                    _builder.Services.AddSingleton<IDocumentReranker, LlmDocumentReranker>();
                    break;

                case RerankMethod.CrossEncoder:
                    _builder.Services.AddSingleton<IDocumentReranker, CrossEncoderReranker>();
                    break;

                case RerankMethod.None:
                default:
                    _builder.Services.AddSingleton<IDocumentReranker, NoOpDocumentReranker>();
                    break;
            }

            _builder.Services.AddSingleton<IDocumentSearchService, DocumentSearchService>();
            _builder.Services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();
        }
    }
}
