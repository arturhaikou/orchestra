using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Integrations.Providers.GitHub;
using Orchestra.Infrastructure.Integrations.Providers.GitLab;
using Orchestra.Infrastructure.Tools;
using Orchestra.Infrastructure.Tools.Services;
using Orchestra.Tests.Shared.Builders;
using Orchestra.Tests.Shared.Fixtures;

namespace Orchestra.Infrastructure.Tests.Tools;

public class CodeReviewOrchestrationServiceTests : ServiceTestFixture<CodeReviewOrchestrationService>
{
    private readonly IChatClientResolver _chatClientResolver = Substitute.For<IChatClientResolver>();
    private readonly IGitHubApiClientFactory _gitHubApiClientFactory = Substitute.For<IGitHubApiClientFactory>();
    private readonly IGitLabApiClientFactory _gitLabApiClientFactory = Substitute.For<IGitLabApiClientFactory>();
    private readonly IGitHubApiClient _gitHubApiClient = Substitute.For<IGitHubApiClient>();
    private readonly IGitLabApiClient _gitLabApiClient = Substitute.For<IGitLabApiClient>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();

    private readonly CodeReviewOrchestrationService _sut;

    private static readonly Guid TestWorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string TestIntegrationId = "22222222-2222-2222-2222-222222222222";
    private const string TestPrNumber = "42";
    private const string TestMrIid = "7";
    private const string TestModelIdentifier = "gpt-4o";

    public CodeReviewOrchestrationServiceTests()
    {
        _gitHubApiClientFactory.CreateClient(Arg.Any<Integration>()).Returns(_gitHubApiClient);
        _gitLabApiClientFactory.CreateClient(Arg.Any<Integration>()).Returns(_gitLabApiClient);
        _chatClientResolver
            .ResolveChatClientAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("ChatClientAgent cannot be instantiated in unit tests."));

        _sut = new CodeReviewOrchestrationService(
            _chatClientResolver,
            _gitHubApiClientFactory,
            _gitLabApiClientFactory,
            Logger);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 1 — GitHub integration: CreateClient is called with the
    // resolved integration before agent instantiation starts.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewAsync_GitHub_CallsGitHubApiClientFactory_WithResolvedIntegration()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        // Act — the call will ultimately fail at ChatClientAgent construction (IChatClient throws),
        // but IGitHubApiClientFactory.CreateClient must have been called before that point.
        var result = await _sut.ReviewAsync(
            ProviderType.GITHUB,
            TestWorkspaceId,
            TestIntegrationId,
            TestPrNumber,
            TestModelIdentifier,
            projectPrinciples: null,
            integration);

        // Assert
        _gitHubApiClientFactory.Received(1).CreateClient(integration);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 2 — GitLab integration: CreateClient is called with the
    // resolved integration before agent instantiation starts.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewAsync_GitLab_CallsGitLabApiClientFactory_WithResolvedIntegration()
    {
        // Arrange
        var integration = IntegrationBuilder.GitLabIntegration();

        // Act
        var result = await _sut.ReviewAsync(
            ProviderType.GITLAB,
            TestWorkspaceId,
            TestIntegrationId,
            TestMrIid,
            TestModelIdentifier,
            projectPrinciples: null,
            integration);

        // Assert
        _gitLabApiClientFactory.Received(1).CreateClient(integration);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 4 — Sub-agent inherits parent's model override.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewAsync_WithNonNullModelIdentifier_PassesModelToResolver()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        // Act
        await _sut.ReviewAsync(
            ProviderType.GITHUB,
            TestWorkspaceId,
            TestIntegrationId,
            TestPrNumber,
            modelIdentifier: TestModelIdentifier,
            projectPrinciples: null,
            integration);

        // Assert
        await _chatClientResolver.Received(1)
            .ResolveChatClientAsync(TestModelIdentifier, Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 5 — Sub-agent falls back to system default when model is null.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewAsync_WithNullModelIdentifier_PassesNullToResolver()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        // Act
        await _sut.ReviewAsync(
            ProviderType.GITHUB,
            TestWorkspaceId,
            TestIntegrationId,
            TestPrNumber,
            modelIdentifier: null,
            projectPrinciples: null,
            integration);

        // Assert
        await _chatClientResolver.Received(1)
            .ResolveChatClientAsync(null, Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 8 — Exception during factory call returns error result,
    // no exception propagates to the caller.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewAsync_WhenApiClientFactoryThrows_ReturnsErrorResult_DoesNotPropagate()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();
        _gitHubApiClientFactory
            .CreateClient(Arg.Any<Integration>())
            .Throws(new HttpRequestException("GitHub API unreachable"));

        // Act
        var result = await _sut.ReviewAsync(
            ProviderType.GITHUB,
            TestWorkspaceId,
            TestIntegrationId,
            TestPrNumber,
            TestModelIdentifier,
            projectPrinciples: null,
            integration);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Contains("GitHub API unreachable", result.Error);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Scenario 3 (partial) — Project Principles path does not crash.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReviewAsync_WithProjectPrinciples_DoesNotThrow()
    {
        // Arrange
        var integration = IntegrationBuilder.GitHubIntegration();

        // Act — should not throw, error is caught internally
        var result = await _sut.ReviewAsync(
            ProviderType.GITHUB,
            TestWorkspaceId,
            TestIntegrationId,
            TestPrNumber,
            TestModelIdentifier,
            projectPrinciples: "We follow SOLID. All public methods must have XML docs.",
            integration);

        // Assert — the call still reaches ChatClientResolver (after factory)
        await _chatClientResolver.Received(1)
            .ResolveChatClientAsync(TestModelIdentifier, Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FR-02 — BaseReviewSystemPrompt scope-constraint regression guards.
    // These tests use reflection to verify the Review Scope section is present
    // and complete. They do not test LLM behaviour; they guard against
    // accidental removal of the scope-limiting instructions.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BaseReviewSystemPrompt_ContainsReviewScopeSection_BeforeToolUsageWorkflow()
    {
        // Arrange
        var field = typeof(CodeReviewOrchestrationService)
            .GetField("BaseReviewSystemPrompt", BindingFlags.NonPublic | BindingFlags.Static);
        var prompt = (string)field!.GetValue(null)!;

        // Act
        var scopeIndex = prompt.IndexOf("## Review Scope", StringComparison.Ordinal);
        var workflowIndex = prompt.IndexOf("## Tool Usage Workflow", StringComparison.Ordinal);

        // Assert — section must exist and must precede the Tool Usage Workflow section
        Assert.True(scopeIndex >= 0, "## Review Scope section not found in BaseReviewSystemPrompt.");
        Assert.True(workflowIndex >= 0, "## Tool Usage Workflow section not found in BaseReviewSystemPrompt.");
        Assert.True(scopeIndex < workflowIndex,
            "## Review Scope section must appear before ## Tool Usage Workflow in BaseReviewSystemPrompt.");
    }

    [Fact]
    public void BaseReviewSystemPrompt_ContainsAllFourCategoricalProhibitions()
    {
        // Arrange
        var field = typeof(CodeReviewOrchestrationService)
            .GetField("BaseReviewSystemPrompt", BindingFlags.NonPublic | BindingFlags.Static);
        var prompt = (string)field!.GetValue(null)!;

        // Assert — all four prohibition categories required by FR-02 §2 must be present
        Assert.Contains("General technology recommendations not grounded in the Project Principles", prompt);
        Assert.Contains("availability or compatibility of a framework, runtime, or library", prompt);
        Assert.Contains("Code style opinions", prompt);
        Assert.Contains("Architecture or design-pattern suggestions", prompt);
    }

    [Fact]
    public void BaseReviewSystemPrompt_ContainsNullPrinciplesFallbackInstruction()
    {
        // Arrange
        var field = typeof(CodeReviewOrchestrationService)
            .GetField("BaseReviewSystemPrompt", BindingFlags.NonPublic | BindingFlags.Static);
        var prompt = (string)field!.GetValue(null)!;

        // Assert — the instruction covering the null-ProjectPrinciples edge case (Scenario 5, FR-02) must be present
        Assert.Contains("Project-Specific Principles section is absent", prompt);
        Assert.Contains("restrict your analysis entirely to business logic correctness", prompt);
    }
}
