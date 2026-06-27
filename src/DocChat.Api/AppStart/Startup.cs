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

            _builder.Services.AddControllers();
        }
    }
}
