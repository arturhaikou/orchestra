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

        /// <summary>
        /// Total number of issues matching the JQL query (returned by Jira v2 and v3).
        /// Used by the On-Premise client to compute <see cref="IsLast"/> because Jira Data Center
        /// / Server does not return a native isLast field.
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }

        /// <summary>
        /// Zero-based index of the first result in this page (returned by Jira v2 and v3).
        /// Together with <see cref="Total"/> and the actual result count, used to compute
        /// <see cref="IsLast"/> for On-Premise Jira instances.
        /// </summary>
        [JsonPropertyName("startAt")]
        public int StartAt { get; set; }
    }
}