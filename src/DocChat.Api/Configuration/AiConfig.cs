namespace DocChat.Api.Configuration
{
    public class AiConfig
    {
        public const string SectionName = "AiSettings";

        public string ApiKey { get; set; }

        public string Model { get; set; }
    }
}
