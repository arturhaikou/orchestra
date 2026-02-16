using System.ComponentModel;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;

namespace Orchestra.Infrastructure.Tools.Services;

// [ToolCategory("GitHub", ProviderType.GITHUB, "Interact with GitHub repositories")]
public interface IGitHubToolService
{
    // [ToolAction("get_pr", "Get pull request details", DangerLevel.Safe)]
    // [Description("Retrieve detailed information about a GitHub pull request")]
    Task<object> GetPullRequestAsync();

    // [ToolAction("get_issue", "Get GitHub issue details", DangerLevel.Safe)]
    // [Description("Retrieve detailed information about a GitHub issue")]
    Task<object> GetIssueAsync();
}