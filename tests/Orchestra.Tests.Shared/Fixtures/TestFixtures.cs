using System;
using Microsoft.Extensions.Logging;
using Orchestra.Domain;
using Orchestra.Application;
using Orchestra.Infrastructure;

namespace Orchestra.Tests.Shared.Fixtures
{
    public class TestFixture : IDisposable
    {
        /// <summary>
        /// Gets a generic logger substitute for testing.
        /// </summary>
        protected ILogger<T> GetLoggerSubstitute<T>() where T : class
        {
            return Substitute.For<ILogger<T>>();
        }

        /// <summary>
        /// Disposes of any test resources.
        /// </summary>
        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    public class ServiceTestFixture<T> : TestFixture where T : class
    {
        /// <summary>
        /// Gets a substitute (mock) logger for the service.
        /// </summary>
        protected ILogger<T> Logger { get; }

        /// <summary>
        /// Initializes a new instance of ServiceTestFixture.
        /// </summary>
        public ServiceTestFixture()
        {
            Logger = GetLoggerSubstitute<T>();
        }
    }

    /// <summary>
    /// Specialized fixture for authentication-related service testing.
    /// </summary>
    public class AuthTestFixture : ServiceTestFixture<IAuthService>
    {
        public IJwtTokenService CreateMockJwtTokenService() => Substitute.For<IJwtTokenService>();
        public IPasswordHashingService CreateMockPasswordHashingService() => Substitute.For<IPasswordHashingService>();
        public IUserDataAccess CreateMockUserDataAccess() => Substitute.For<IUserDataAccess>();
        public IWorkspaceService CreateMockWorkspaceService() => Substitute.For<IWorkspaceService>();
    }

    /// <summary>
    /// Specialized fixture for agent-related service testing.
    /// </summary>
    public class AgentTestFixture : ServiceTestFixture<IAgentService>
    {
        public IAgentDataAccess CreateMockAgentDataAccess() => Substitute.For<IAgentDataAccess>();
        public IWorkspaceAuthorizationService CreateMockWorkspaceAuthorizationService() => Substitute.For<IWorkspaceAuthorizationService>();
        public IToolService CreateMockToolService() => Substitute.For<IToolService>();
    }

    /// <summary>
    /// Specialized fixture for ticket-related service testing.
    /// </summary>
    public class TicketTestFixture : ServiceTestFixture<ITicketService>
    {
        public ITicketDataAccess CreateMockTicketDataAccess() => Substitute.For<ITicketDataAccess>();
        public IWorkspaceAuthorizationService CreateMockWorkspaceAuthorizationService() => Substitute.For<IWorkspaceAuthorizationService>();
        public ITicketProviderFactory CreateMockTicketProviderFactory() => Substitute.For<ITicketProviderFactory>();
        public ITicketMappingService CreateMockTicketMappingService() => Substitute.For<ITicketMappingService>();
    }

    /// <summary>
    /// Specialized fixture for integration-related service testing.
    /// </summary>
    public class IntegrationTestFixture : ServiceTestFixture<IIntegrationService>
    {
        public IIntegrationDataAccess CreateMockIntegrationDataAccess() => Substitute.For<IIntegrationDataAccess>();
        public IWorkspaceAuthorizationService CreateMockWorkspaceAuthorizationService() => Substitute.For<IWorkspaceAuthorizationService>();
    }

    /// <summary>
    /// Specialized fixture for tool-related service testing.
    /// </summary>
    public class ToolTestFixture : ServiceTestFixture<IToolService>
    {
        public IToolActionDataAccess CreateMockToolActionDataAccess() => Substitute.For<IToolActionDataAccess>();
        public IToolCategoryDataAccess CreateMockToolCategoryDataAccess() => Substitute.For<IToolCategoryDataAccess>();
        public IAgentToolActionDataAccess CreateMockAgentToolActionDataAccess() => Substitute.For<IAgentToolActionDataAccess>();
        public IToolValidationService CreateMockToolValidationService() => Substitute.For<IToolValidationService>();
        public IToolScanningService CreateMockToolScanningService() => Substitute.For<IToolScanningService>();
    }

    /// <summary>
    /// Specialized fixture for workspace-related service testing.
    /// </summary>
    public class WorkspaceTestFixture : ServiceTestFixture<IWorkspaceService>
    {
        public IWorkspaceDataAccess CreateMockWorkspaceDataAccess() => Substitute.For<IWorkspaceDataAccess>();
        public IWorkspaceAuthorizationService CreateMockWorkspaceAuthorizationService() => Substitute.For<IWorkspaceAuthorizationService>();
        public IUserDataAccess CreateMockUserDataAccess() => Substitute.For<IUserDataAccess>();
    }
}
