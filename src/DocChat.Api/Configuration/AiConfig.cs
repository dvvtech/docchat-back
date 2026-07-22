namespace DocChat.Api.Configuration
{
    public class AiConfig
    {
        public const string SectionName = "AiSettings";

        public string ApiKey { get; set; } = string.Empty;

        public string Model { get; set; } = "gpt-4o";
    }
}
