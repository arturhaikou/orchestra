using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models
{
    public class JiraStatusCategory
    {
        [JsonPropertyName("colorName")]
        public string ColorName { get; set; }

        [JsonPropertyName("key")]
        public string Key { get; set; }
    }
}