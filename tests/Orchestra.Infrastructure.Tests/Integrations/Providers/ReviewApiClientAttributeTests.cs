using System.Reflection;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;
using Orchestra.Infrastructure.Tools.Attributes;

namespace Orchestra.Infrastructure.Tests.Integrations.Providers;

/// <summary>
/// Verifies the FR-02 structural guarantee: review API client methods carry no [ToolAction]
/// attribute and their declaring interfaces carry no [ToolCategory] attribute, ensuring
/// ToolScanningService never discovers, seeds, or exposes these methods as catalogue entries.
/// </summary>
public class ReviewApiClientAttributeTests
{
    // ── IGitHubApiClient — interface-level check ─────────────────────────────

    [Fact]
    public void IGitHubApiClient_HasNoToolCategoryAttribute()
    {
        // IGitHubApiClient must not be decorated with [ToolCategory].
        // ToolScanningService only reflects over types bearing [ToolCategory].
        // If this type had [ToolCategory], the scanner would inspect its methods.
        var attr = typeof(IGitHubApiClient).GetCustomAttribute<ToolCategoryAttribute>();

        Assert.Null(attr);
    }

    // ── IGitHubApiClient — per-method checks ─────────────────────────────────

    [Theory]
    [InlineData("GetPullRequestDiffAsync")]
    [InlineData("GetPullRequestFilesAsync")]
    [InlineData("GetPullRequestReviewCommentsAsync")]
    [InlineData("SubmitPullRequestReviewAsync")]
    [InlineData("GetFileContentAsync")]
    public void IGitHubApiClient_ReviewMethod_HasNoToolActionAttribute(string methodName)
    {
        // Each review method must not carry [ToolAction].
        // Absence of [ToolAction] means ToolScanningService skips the method
        // entirely, so no ToolAction row is ever seeded for it.
        var method = typeof(IGitHubApiClient).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Null(method!.GetCustomAttribute<ToolActionAttribute>());
    }

    // ── IGitLabApiClient — interface-level check ─────────────────────────────

    [Fact]
    public void IGitLabApiClient_HasNoToolCategoryAttribute()
    {
        var attr = typeof(IGitLabApiClient).GetCustomAttribute<ToolCategoryAttribute>();

        Assert.Null(attr);
    }

    // ── IGitLabApiClient — per-method checks ─────────────────────────────────

    [Theory]
    [InlineData("GetMergeRequestDiffAsync")]
    [InlineData("GetMergeRequestChangesAsync")]
    [InlineData("CreateMergeRequestDiscussionAsync")]
    [InlineData("ApproveMergeRequestAsync")]
    public void IGitLabApiClient_ReviewMethod_HasNoToolActionAttribute(string methodName)
    {
        var method = typeof(IGitLabApiClient).GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Null(method!.GetCustomAttribute<ToolActionAttribute>());
    }
}
