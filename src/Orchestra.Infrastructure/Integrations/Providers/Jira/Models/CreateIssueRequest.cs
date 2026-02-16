using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Orchestra.Infrastructure.Integrations.Providers.Jira.Models
{
    public class CreateIssueRequest
    {
        public CreateIssueFields Fields { get; set; }
    }

    public class CreateIssueFields
    {
        public string Summary { get; set; }

        public object Description { get; set; }

        public IssueTypeField Issuetype { get; set; }

        public ProjectField Project { get; set; }

        public ParentField Parent { get; set; }
    }

    public class IssueTypeField
    {
        public string Id { get; set; }
    }

    public class ProjectField
    {
        public string Id { get; set; }
    }

    public class  ParentField
    {
        public string Key { get; set; }
    }

    public class DescriptionField
    {
        public List<ContentField> Content { get; set; }
        public string Type { get; set; } = "doc";
        public int Version { get; set; } = 1;
    }

    public class ContentField
    {
        public List<ContentBodyField> Content { get; set; }
        public string Type { get; set; } = "paragraph";

    }

    public class ContentBodyField
    {
        public string Text { get; set; }
        public string Type { get; set; } = "text";
    }
}