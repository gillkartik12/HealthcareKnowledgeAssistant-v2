namespace HealthcareKnowledgeAssistant.Configuration
{
    public class RedisSettings
    {
        public const string SectionName = "Redis";

        public bool Enabled { get; set; } = true;

        public string ConnectionString { get; set; }
            = "localhost:6379";

        public int DefaultTtlMinutes { get; set; } = 10;
    }
}
