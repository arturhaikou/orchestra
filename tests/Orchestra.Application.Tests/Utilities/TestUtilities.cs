using Orchestra.Domain.Enums;

namespace Orchestra.Application.Tests.Utilities;

/// <summary>
/// Common test constants used across test suites.
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Well-known test workspace ID used for multi-tenancy validation.
    /// </summary>
    public static readonly Guid TestWorkspaceId = new("12345678-1234-1234-1234-123456789012");

    /// <summary>
    /// Well-known test user ID.
    /// </summary>
    public static readonly Guid TestUserId = new("87654321-4321-4321-4321-210987654321");

    /// <summary>
    /// Well-known test agent ID.
    /// </summary>
    public static readonly Guid TestAgentId = new("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Well-known test ticket ID.
    /// </summary>
    public static readonly Guid TestTicketId = new("22222222-2222-2222-2222-222222222222");

    /// <summary>
    /// Well-known test integration ID.
    /// </summary>
    public static readonly Guid TestIntegrationId = new("33333333-3333-3333-3333-333333333333");

    /// <summary>
    /// Well-known test tool category ID.
    /// </summary>
    public static readonly Guid TestToolCategoryId = new("44444444-4444-4444-4444-444444444444");

    /// <summary>
    /// Well-known test tool action ID.
    /// </summary>
    public static readonly Guid TestToolActionId = new("55555555-5555-5555-5555-555555555555");

    /// <summary>
    /// Valid test email address.
    /// </summary>
    public const string TestEmail = "test@example.com";

    /// <summary>
    /// Valid test password.
    /// </summary>
    public const string TestPassword = "ValidPassword123!";

    /// <summary>
    /// Valid test password hash.
    /// </summary>
    public const string TestPasswordHash = "hashed_valid_password_123";

    /// <summary>
    /// Test JWT token value.
    /// </summary>
    public const string TestJwtToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IlRlc3QgVXNlciIsImlhdCI6MTUxNjIzOTAyMn0.signature";

    /// <summary>
    /// Default danger level for test tool actions.
    /// </summary>
    public static readonly DangerLevel DefaultDangerLevel = DangerLevel.Safe;

    /// <summary>
    /// Default agent status for tests.
    /// </summary>
    public static readonly AgentStatus DefaultAgentStatus = AgentStatus.Idle;

    /// <summary>
    /// Default provider type for tests.
    /// </summary>
    public static readonly ProviderType DefaultProviderType = ProviderType.JIRA;

    /// <summary>
    /// Default integration type for tests.
    /// </summary>
    public static readonly IntegrationType DefaultIntegrationType = IntegrationType.TRACKER;
}

/// <summary>
/// Helper class for workspace-scoped testing, ensuring multi-tenancy boundaries.
/// </summary>
public class WorkspaceScopeHelper
{
    /// <summary>
    /// Validates that a collection of entities all belong to the same workspace.
    /// </summary>
    /// <param name="entities">The entities to validate.</param>
    /// <param name="expectedWorkspaceId">The expected workspace ID.</param>
    /// <returns>True if all entities belong to the expected workspace.</returns>
    public static bool AreAllInWorkspace(IEnumerable<Agent> entities, Guid expectedWorkspaceId)
    {
        return entities.All(e => e.WorkspaceId == expectedWorkspaceId);
    }

    /// <summary>
    /// Validates that a collection of ticket entities all belong to the same workspace.
    /// </summary>
    public static bool AreAllInWorkspace(IEnumerable<Ticket> entities, Guid expectedWorkspaceId)
    {
        return entities.All(e => e.WorkspaceId == expectedWorkspaceId);
    }

    /// <summary>
    /// Validates that a collection of integration entities all belong to the same workspace.
    /// </summary>
    public static bool AreAllInWorkspace(IEnumerable<Integration> entities, Guid expectedWorkspaceId)
    {
        return entities.All(e => e.WorkspaceId == expectedWorkspaceId);
    }

    /// <summary>
    /// Validates that an entity belongs to the specified workspace.
    /// </summary>
    public static bool IsInWorkspace(Agent entity, Guid expectedWorkspaceId)
    {
        return entity.WorkspaceId == expectedWorkspaceId;
    }

    /// <summary>
    /// Validates that a ticket belongs to the specified workspace.
    /// </summary>
    public static bool IsInWorkspace(Ticket entity, Guid expectedWorkspaceId)
    {
        return entity.WorkspaceId == expectedWorkspaceId;
    }

    /// <summary>
    /// Validates that an integration belongs to the specified workspace.
    /// </summary>
    public static bool IsInWorkspace(Integration entity, Guid expectedWorkspaceId)
    {
        return entity.WorkspaceId == expectedWorkspaceId;
    }
}

/// <summary>
/// Helper class providing common mock data provider implementations for testing.
/// </summary>
public class MockDataProvider
{
    /// <summary>
    /// Creates a mock agent data access that returns the provided agents.
    /// </summary>
    public static IAgentDataAccess AgentDataAccessWithData(params Agent[] agents)
    {
        var mock = Substitute.For<IAgentDataAccess>();
        mock.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(args => Task.FromResult(agents.FirstOrDefault(a => a.Id == (Guid)args[0])));
        return mock;
    }

    /// <summary>
    /// Creates a mock ticket data access that returns the provided tickets.
    /// </summary>
    public static ITicketDataAccess TicketDataAccessWithData(params Ticket[] tickets)
    {
        var mock = Substitute.For<ITicketDataAccess>();
        // Note: Specific method needs to be checked from ITicketDataAccess interface
        return mock;
    }

    /// <summary>
    /// Creates a mock user data access that returns the provided users.
    /// </summary>
    public static IUserDataAccess UserDataAccessWithData(params User[] users)
    {
        var mock = Substitute.For<IUserDataAccess>();
        // Note: Specific method needs to be checked from IUserDataAccess interface
        return mock;
    }

    /// <summary>
    /// Creates a mock workspace data access that returns the provided workspaces.
    /// </summary>
    public static IWorkspaceDataAccess WorkspaceDataAccessWithData(params Workspace[] workspaces)
    {
        var mock = Substitute.For<IWorkspaceDataAccess>();
        mock.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(args => Task.FromResult(workspaces.FirstOrDefault(w => w.Id == (Guid)args[0])));
        return mock;
    }

    /// <summary>
    /// Creates a mock integration data access that returns the provided integrations.
    /// </summary>
    public static IIntegrationDataAccess IntegrationDataAccessWithData(params Integration[] integrations)
    {
        var mock = Substitute.For<IIntegrationDataAccess>();
        mock.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(args => Task.FromResult(integrations.FirstOrDefault(i => i.Id == (Guid)args[0])));
        return mock;
    }
}

/// <summary>
/// Helper class providing extension methods for common test assertion patterns.
/// </summary>
public static class AssertionExtensions
{
    /// <summary>
    /// Asserts that an entity belongs to the expected workspace.
    /// </summary>
    public static void ShouldBeInWorkspace(this Agent agent, Guid expectedWorkspaceId)
    {
        Assert.Equal(expectedWorkspaceId, agent.WorkspaceId);
    }

    /// <summary>
    /// Asserts that a ticket belongs to the expected workspace.
    /// </summary>
    public static void ShouldBeInWorkspace(this Ticket ticket, Guid expectedWorkspaceId)
    {
        Assert.Equal(expectedWorkspaceId, ticket.WorkspaceId);
    }

    /// <summary>
    /// Asserts that an integration belongs to the expected workspace.
    /// </summary>
    public static void ShouldBeInWorkspace(this Integration integration, Guid expectedWorkspaceId)
    {
        Assert.Equal(expectedWorkspaceId, integration.WorkspaceId);
    }

    /// <summary>
    /// Asserts that an agent has a specific status.
    /// </summary>
    public static void ShouldHaveStatus(this Agent agent, AgentStatus expectedStatus)
    {
        Assert.Equal(expectedStatus, agent.Status);
    }

    /// <summary>
    /// Asserts that a ticket is internal.
    /// </summary>
    public static void ShouldBeInternal(this Ticket ticket)
    {
        Assert.True(ticket.IsInternal);
    }

    /// <summary>
    /// Asserts that a ticket is external.
    /// </summary>
    public static void ShouldBeExternal(this Ticket ticket)
    {
        Assert.False(ticket.IsInternal);
    }

    /// <summary>
    /// Asserts that a user is active.
    /// </summary>
    public static void ShouldBeActive(this User user)
    {
        Assert.True(user.IsActive);
    }

    /// <summary>
    /// Asserts that a user is inactive.
    /// </summary>
    public static void ShouldBeInactive(this User user)
    {
        Assert.False(user.IsActive);
    }

    /// <summary>
    /// Asserts that an integration is connected.
    /// </summary>
    public static void ShouldBeConnected(this Integration integration)
    {
        Assert.True(integration.Connected);
    }

    /// <summary>
    /// Asserts that an integration is disconnected.
    /// </summary>
    public static void ShouldBeDisconnected(this Integration integration)
    {
        Assert.False(integration.Connected);
    }
}
