using DocChat.Api.Services;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;

namespace DocChat.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly AiAgentService _aiAgentService;

        public WeatherForecastController(AiAgentService aiAgentService)
        {
            _aiAgentService = aiAgentService;
        }

        private static readonly string[] Summaries =
        [
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        ];

        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get(CancellationToken cancellationToken)
        {
            var chatMessages = new List<ChatMessage>();

             await _aiAgentService.ProcessQueryAsync(chatMessages, "привет, что умеешь?", cancellationToken);

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}
