using NSubstitute;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Tools.Services;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Tools;

// FR-005: GetAvailableToolsAsync — Per-action Transport and IntegrationName badge fields
public class ToolServiceGetAvailableToolsActionBadgeTests
{
    private readonly IToolCategoryDataAccess _toolCategoryDataAccess = Substitute.For<IToolCategoryDataAccess>();
    private readonly IToolActionDataAccess _toolActionDataAccess = Substitute.For<IToolActionDataAccess>();
    private readonly IAgentToolActionDataAccess _agentToolActionDataAccess = Substitute.For<IAgentToolActionDataAccess>();
    private readonly IIntegrationDataAccess _integrationDataAccess = Substitute.For<IIntegrationDataAccess>();
    private readonly IAgentDataAccess _agentDataAccess = Substitute.For<IAgentDataAccess>();
    private readonly IWorkspaceAuthorizationService _workspaceAuthorizationService = Substitute.For<IWorkspaceAuthorizationService>();
    private readonly ToolService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _workspaceId = Guid.NewGuid();

    public ToolServiceGetAvailableToolsActionBadgeTests()
    {
        _sut = new ToolService(
            _toolCategoryDataAccess,
            _toolActionDataAccess,
            _agentToolActionDataAccess,
            _integrationDataAccess,
            _agentDataAccess,
            _workspaceAuthorizationService);
    }

    [Fact]
    public async Task GetAvailableToolsAsync_WithNativeAction_ActionDtoTransportIsNull()
    {
        var nativeCategory = new ToolCategoryBuilder()
            .WithProviderType(ProviderType.INTERNAL)
            .Build();

        var nativeAction = new ToolActionBuilder()
            .WithToolCategoryId(nativeCategory.Id)
            .WithIsEnabled(true)
            .Build();

        _workspaceAuthorizationService
            .EnsureUserIsMemberAsync(_userId, _workspaceId, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _integrationDataAccess
            .GetByWorkspaceIdAsync(_workspaceId, Arg.Any<CancellationToken>())
            .Returns([]);
        _toolCategoryDataAccess
            .GetByProviderTypesAsync(Arg.Any<List<ProviderType>>(), Arg.Any<CancellationToken>())
            .Returns([nativeCategory]);
        _toolActionDataAccess
            .GetByCategoryIdsAsync(Arg.Any<List<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([nativeAction]);

        var result = await _sut.GetAvailableToolsAsync(_userId, _workspaceId);

        var actionDto = result.Single().Actions.Single();
        Assert.Null(actionDto.Transport);
        Assert.Null(actionDto.IntegrationName);
    }
}
