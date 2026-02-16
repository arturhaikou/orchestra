using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models
{
    public class JiraSearchResponse
    {
        [JsonPropertyName("issues")]
        public List<JiraTicket> Tickets { get; set; }

        [JsonPropertyName("isLast")]
        public bool IsLast { get; set; }

        [JsonPropertyName("nextPageToken")]
        public string NextPageToken { get; set; }
    }
}