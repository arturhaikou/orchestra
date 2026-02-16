using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models
{
    public class JiraTicketStatus
    {
        public string Name { get; set; }

        [JsonPropertyName("statusCategory")]
        public JiraStatusCategory StatusCategory { get; set; }
    }
}