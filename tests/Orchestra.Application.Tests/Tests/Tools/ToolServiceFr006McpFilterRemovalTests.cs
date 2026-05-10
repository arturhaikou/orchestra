using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tools.Services;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Domain.Interfaces;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tools;

public class ToolServiceFr006McpFilterRemovalTests
{
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IWorkspaceAuthorizationService _authService = Substitute.For<IWorkspaceAuthorizationService>();

    private ToolService CreateSut() => new(
        _toolCategoryDataAccess,
        _toolActionDataAccess,
        _agentToolActionDataAccess,
        _integrationDataAccess,
        _agentDataAccess,
        _authService);

    // ---------------------------------------------------------------
    // Scenario 4: Non-MCP integrations unaffected
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAvailableToolsAsync_WithActiveJiraIntegration_PassesJiraProviderToCategoryQuery()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        var jiraIntegration = new IntegrationBuilder()
            .WithWorkspaceId(workspaceId)
            .WithProvider(ProviderType.JIRA)
            .WithIsActive(true)
            .Build();

        var jiraCategory = new ToolCategoryBuilder()
            .WithProviderType(ProviderType.JIRA)
            .WithIsActive(true)
            .Build();

        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns([jiraIntegration]);

        _toolCategoryDataAccess
            .GetByProviderTypesAsync(
                Arg.Is<List<ProviderType>>(types => types.Contains(ProviderType.JIRA)),
                Arg.Any<CancellationToken>())
            .Returns([jiraCategory]);

        _toolActionDataAccess.GetByCategoryIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ToolAction>());

        var sut = CreateSut();

        var result = await sut.GetAvailableToolsAsync(userId, workspaceId);

        Assert.Single(result);
        Assert.Equal(ProviderType.JIRA.ToString(), result[0].ProviderType);

        await _toolCategoryDataAccess.Received(1).GetByProviderTypesAsync(
            Arg.Is<List<ProviderType>>(types => types.Contains(ProviderType.JIRA)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAvailableToolsAsync_WithNoActiveIntegrations_ReturnsInternalCategoryOnly()
    {
        var userId = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();

        _integrationDataAccess.GetByWorkspaceIdAsync(workspaceId, Arg.Any<CancellationToken>())
            .Returns(new List<Domain.Entities.Integration>());

        _toolCategoryDataAccess
            .GetByProviderTypesAsync(
                Arg.Is<List<ProviderType>>(types => types.Contains(ProviderType.INTERNAL)),
                Arg.Any<CancellationToken>())
            .Returns(new List<ToolCategory>());

        var sut = CreateSut();

        var result = await sut.GetAvailableToolsAsync(userId, workspaceId);

        await _toolCategoryDataAccess.Received(1).GetByProviderTypesAsync(
            Arg.Is<List<ProviderType>>(types => types.Contains(ProviderType.INTERNAL) && types.Count == 1),
            Arg.Any<CancellationToken>());
    }
}
