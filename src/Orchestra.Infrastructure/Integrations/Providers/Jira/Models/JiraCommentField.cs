namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models
{
    public class JiraCommentField
    {
        public JiraComment[] Comments { get; set; }

        public int Total { get; set; }
    }

    public class JiraComment
    {
        public string Id { get; set; }

        public dynamic Body { get; set; }

        public JiraAuthor Author { get; set; }
    }

    public class JiraAuthor
    {
        public string DisplayName { get; set; }
    }
}