using System.Text.Json.Serialization;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models
{
    public class JiraFields
    {
        public string Summary { get; set; }

        public dynamic Description { get; set; }

        public JiraCommentField Comment { get; set; }

        public JiraTicketStatus Status { get; set; }

        public JiraTicketPriority Priority { get; set; }

        public ProjectField Project { get; set; }

        [JsonPropertyName("assignee")]
        public JiraAssignee? Assignee { get; set; }

        [JsonPropertyName("issuetype")]
        public IssueType? Issuetype { get; set; }
    }
}