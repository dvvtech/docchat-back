using DocChat.Api.Configuration;
using DocChat.Api.Services;

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
            _builder.Services.AddSingleton<AiAgentService>();
            _builder.Services.AddSingleton<DocumentTextExtractor>();
            _builder.Services.AddSingleton<LlmDocumentChunker>();
            _builder.Services.AddSingleton<DocumentEmbeddingService>();
            _builder.Services.AddSingleton<QdrantDocumentStore>();
            _builder.Services.AddSingleton<DocumentSearchService>();
            _builder.Services.AddScoped<DocumentIngestionService>();
        }
    }
}
