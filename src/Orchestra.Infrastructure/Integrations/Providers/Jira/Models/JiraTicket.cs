using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models
{
    public class JiraTicket
    {
        public string Id { get; set; }

        public string Key { get; set; }

        [JsonPropertyName("fields")]
        public JiraFields Fields { get; set; }
    }
}