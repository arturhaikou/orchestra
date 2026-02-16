using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models
{
    public class JiraTicketPriority
    {
        public string Name { get; set; }

        [JsonPropertyName("statusColor")]
        public string StatusColor { get; set; }

        public string Id { get; set; }
    }
}