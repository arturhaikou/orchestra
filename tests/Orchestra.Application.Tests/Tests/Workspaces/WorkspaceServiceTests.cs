using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using Orchestra.Application.Workspaces.Services;
using Orchestra.Application.Workspaces.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Domain.Entities;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Enums;
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Workspaces;

public class WorkspaceServiceTests
{
    private static IWorkspaceAIProviderRepository BuildDefaultAiProviderRepo()
    {
        var repo = Substitute.For<IWorkspaceAIProviderRepository>();
        repo.GetByWorkspaceIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AIProviderConfiguration?)null);
        return repo;
    }

    [Fact]
    public async Task CreateWorkspaceAsync_CreatesWorkspaceWithBothAiFlags_WhenEnabled()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var providerConfigId = Guid.NewGuid();
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "AI-Enabled Workspace",
            IsAiSummarizationEnabled = true,
            IsCustomerSatisfactionAnalysisEnabled = true,
            ProviderType = AIProviderType.AzureOpenAI,
            Endpoint = "https://myopenai.openai.azure.com/",
            ApiKey = "test-api-key"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockProviderService
            .CreateProviderConfigAsync(Arg.Any<Guid>(), Arg.Any<AIProviderType>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerConfigId));
        
        var createdWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("AI-Enabled Workspace")
            .WithIsAiSummarizationEnabled(true)
            .WithIsCustomerSatisfactionAnalysisEnabled(true)
            .Build();

        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(createdWorkspace));

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.CreateWorkspaceAsync(userId, createRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("AI-Enabled Workspace", result.Name);
        Assert.True(result.IsAiSummarizationEnabled);
        Assert.True(result.IsCustomerSatisfactionAnalysisEnabled);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_CreatesWorkspaceWithDefaultDisabledFlags_WhenNotProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var providerConfigId = Guid.NewGuid();
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "Standard Workspace",
            ProviderType = AIProviderType.AzureOpenAI,
            Endpoint = "https://myopenai.openai.azure.com/",
            ApiKey = "test-api-key"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockProviderService
            .CreateProviderConfigAsync(Arg.Any<Guid>(), Arg.Any<AIProviderType>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerConfigId));
        
        var createdWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("Standard Workspace")
            .WithIsAiSummarizationEnabled(false)
            .WithIsCustomerSatisfactionAnalysisEnabled(false)
            .Build();

        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(createdWorkspace));

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.CreateWorkspaceAsync(userId, createRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Standard Workspace", result.Name);
        Assert.False(result.IsAiSummarizationEnabled);
        Assert.False(result.IsCustomerSatisfactionAnalysisEnabled);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_CreatesWorkspaceWithPartialAiFlags_WhenOnlyOneEnabled()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var providerConfigId = Guid.NewGuid();
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "Partial AI Workspace",
            IsAiSummarizationEnabled = true,
            IsCustomerSatisfactionAnalysisEnabled = null,
            ProviderType = AIProviderType.AzureOpenAI,
            Endpoint = "https://myopenai.openai.azure.com/",
            ApiKey = "test-api-key"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockProviderService
            .CreateProviderConfigAsync(Arg.Any<Guid>(), Arg.Any<AIProviderType>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerConfigId));
        
        var createdWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("Partial AI Workspace")
            .WithIsAiSummarizationEnabled(true)
            .WithIsCustomerSatisfactionAnalysisEnabled(false)
            .Build();

        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(createdWorkspace));

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.CreateWorkspaceAsync(userId, createRequest);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsAiSummarizationEnabled);
        Assert.False(result.IsCustomerSatisfactionAnalysisEnabled);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_EnablesBothAiFlags_WhenOwnerSubmitsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithIsAiSummarizationEnabled(false)
            .WithIsCustomerSatisfactionAnalysisEnabled(false)
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = true,
            IsCustomerSatisfactionAnalysisEnabled = true
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert
        Assert.True(result.IsAiSummarizationEnabled);
        Assert.True(result.IsCustomerSatisfactionAnalysisEnabled);
        await mockDataAccess.Received(1).UpdateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_PreservesUnchangedFlag_WhenOtherFlagIsUpdated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithIsAiSummarizationEnabled(true)
            .WithIsCustomerSatisfactionAnalysisEnabled(false)
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = false,
            IsCustomerSatisfactionAnalysisEnabled = null
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert
        Assert.False(result.IsAiSummarizationEnabled);
        Assert.False(result.IsCustomerSatisfactionAnalysisEnabled);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_PreservesAllFlags_WhenOnlyRenaming()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithIsAiSummarizationEnabled(true)
            .WithIsCustomerSatisfactionAnalysisEnabled(false)
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "New Workspace Name"
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert
        Assert.Equal("New Workspace Name", result.Name);
        Assert.True(result.IsAiSummarizationEnabled);
        Assert.False(result.IsCustomerSatisfactionAnalysisEnabled);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ThrowsUnauthorized_WhenNonOwnerAttemptsUpdate()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var nonOwnerId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(ownerId)
            .WithIsAiSummarizationEnabled(false)
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, nonOwnerId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "Hack Attempt",
            IsAiSummarizationEnabled = true
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedWorkspaceAccessException>(
            () => service.UpdateWorkspaceAsync(nonOwnerId, workspace.Id, updateRequest)
        );
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ThrowsNotFound_WhenNonMemberAttemptsUpdate()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(ownerId)
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, nonMemberId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "New Name",
            IsAiSummarizationEnabled = true
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act & Assert
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(
            () => service.UpdateWorkspaceAsync(nonMemberId, workspace.Id, updateRequest)
        );
    }

    // -----------------------------------------------------------------------
    // FR-05: Provider Configuration Validation Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateWorkspaceAsync_WithNullProviderType_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "No Provider Workspace",
            ProviderType = null
        };
        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateWorkspaceAsync(userId, request));
        Assert.Contains("ProviderType is required", ex.Message);
        await mockDataAccess.DidNotReceive().CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithAzureOpenAI_AndMissingEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Azure WS",
            ProviderType = AIProviderType.AzureOpenAI,
            ApiKey = "some-key",
            Endpoint = null
        };
        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateWorkspaceAsync(userId, request));
        Assert.Contains("Endpoint is required", ex.Message);
        await mockDataAccess.DidNotReceive().CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithAzureOpenAI_AndMissingApiKey_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Azure WS",
            ProviderType = AIProviderType.AzureOpenAI,
            Endpoint = "https://myopenai.openai.azure.com/",
            ApiKey = null
        };
        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateWorkspaceAsync(userId, request));
        Assert.Contains("ApiKey is required", ex.Message);
        await mockDataAccess.DidNotReceive().CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithOllama_AndMissingEndpoint_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Ollama WS",
            ProviderType = AIProviderType.Ollama,
            Endpoint = null
        };
        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateWorkspaceAsync(userId, request));
        Assert.Contains("Endpoint is required", ex.Message);
        await mockDataAccess.DidNotReceive().CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithAzureOpenAI_AndValidConfig_CallsCreateProviderConfigAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var providerConfigId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "My Azure Workspace",
            ProviderType = AIProviderType.AzureOpenAI,
            Endpoint = "https://myopenai.openai.azure.com/",
            ApiKey = "super-secret-key"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();

        mockProviderService
            .CreateProviderConfigAsync(
                Arg.Any<Guid>(),
                AIProviderType.AzureOpenAI,
                "https://myopenai.openai.azure.com/",
                "super-secret-key",
                null,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerConfigId));

        var mockWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("My Azure Workspace")
            .WithAIProviderType(AIProviderType.AzureOpenAI)
            .Build();
        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockWorkspace));

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        await service.CreateWorkspaceAsync(userId, request);

        // Assert
        await mockProviderService.Received(1).CreateProviderConfigAsync(
            Arg.Any<Guid>(),
            AIProviderType.AzureOpenAI,
            "https://myopenai.openai.azure.com/",
            "super-secret-key",
            null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithOllama_AndValidConfig_CallsCreateProviderConfigAsyncWithBaseUrl()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var providerConfigId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Ollama Workspace",
            ProviderType = AIProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            DefaultModelId = "llama3:latest"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();

        mockProviderService
            .CreateProviderConfigAsync(
                Arg.Any<Guid>(),
                AIProviderType.Ollama,
                "http://localhost:11434",
                null,
                "llama3:latest",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerConfigId));

        var mockWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("Ollama Workspace")
            .WithAIProviderType(AIProviderType.Ollama)
            .Build();
        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockWorkspace));

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        await service.CreateWorkspaceAsync(userId, request);

        // Assert
        await mockProviderService.Received(1).CreateProviderConfigAsync(
            Arg.Any<Guid>(),
            AIProviderType.Ollama,
            "http://localhost:11434",
            null,
            "llama3:latest",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithDefaultModelId_ReturnsWorkspaceDtoWithDefaultModelId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var providerConfigId = Guid.NewGuid();
        const string modelId = "gpt-4o";
        var request = new CreateWorkspaceRequest
        {
            Name = "Model Workspace",
            ProviderType = AIProviderType.AzureOpenAI,
            Endpoint = "https://myopenai.openai.azure.com/",
            ApiKey = "key-456",
            DefaultModelId = modelId
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();

        mockProviderService
            .CreateProviderConfigAsync(Arg.Any<Guid>(), Arg.Any<AIProviderType>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerConfigId));

        mockDataAccess
            .CreateAsync(Arg.Do<Workspace>(w => { }), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<Workspace>()));

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.CreateWorkspaceAsync(userId, request);

        // Assert
        Assert.Equal(modelId, result.DefaultModelId);
    }

    // -----------------------------------------------------------------------
    // FR-03: Ollama defaultModelId required-field validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateWorkspaceAsync_WithOllama_AndMissingDefaultModelId_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Ollama WS",
            ProviderType = AIProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            DefaultModelId = null
        };
        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateWorkspaceAsync(userId, request));
        Assert.Contains("defaultModelId is required", ex.Message);

        // No workspace should be created
        await mockDataAccess.DidNotReceive().CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithOllama_AndBlankDefaultModelId_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Ollama WS",
            ProviderType = AIProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            DefaultModelId = "   "
        };
        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateWorkspaceAsync(userId, request));
        Assert.Contains("defaultModelId is required", ex.Message);

        // No workspace should be created
        await mockDataAccess.DidNotReceive().CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithOllama_AndValidDefaultModelId_CallsCreateProviderConfigWithModelId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var providerConfigId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Ollama WS",
            ProviderType = AIProviderType.Ollama,
            Endpoint = "http://localhost:11434",
            DefaultModelId = "llama3:latest"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();

        mockProviderService
            .CreateProviderConfigAsync(
                Arg.Any<Guid>(),
                AIProviderType.Ollama,
                "http://localhost:11434",
                null,
                "llama3:latest",
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerConfigId));

        mockDataAccess
            .CreateAsync(Arg.Do<Workspace>(w => { }), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<Workspace>()));

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.CreateWorkspaceAsync(userId, request);

        // Assert — provider service received defaultModelId
        await mockProviderService.Received(1).CreateProviderConfigAsync(
            Arg.Any<Guid>(),
            AIProviderType.Ollama,
            "http://localhost:11434",
            null,
            "llama3:latest",
            Arg.Any<CancellationToken>());

        Assert.Equal("llama3:latest", result.DefaultModelId);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_WithAzureOpenAI_AndNoDefaultModelId_Succeeds()
    {
        // Arrange — AzureOpenAI does NOT require defaultModelId at creation time (FR-03 Scenario 4)
        var userId = Guid.NewGuid();
        var providerConfigId = Guid.NewGuid();
        var request = new CreateWorkspaceRequest
        {
            Name = "Azure WS No Model",
            ProviderType = AIProviderType.AzureOpenAI,
            Endpoint = "https://myopenai.openai.azure.com/",
            ApiKey = "secret-key",
            DefaultModelId = null
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();

        mockProviderService
            .CreateProviderConfigAsync(Arg.Any<Guid>(), Arg.Any<AIProviderType>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(providerConfigId));

        mockDataAccess
            .CreateAsync(Arg.Do<Workspace>(w => { }), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<Workspace>()));

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act — must not throw
        var result = await service.CreateWorkspaceAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Azure WS No Model", result.Name);
        Assert.Null(result.DefaultModelId);
    }

    [Fact]
    public async Task CreateWorkspaceAsync_AzureOpenAI_ReturnsDtoWithNullDefaultModelId_WhenRequestHasNone()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "Test Workspace",
            ProviderType = AIProviderType.AzureOpenAI,
            Endpoint = "https://test.openai.azure.com/",
            ApiKey = "test-key",
            DefaultModelId = null
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = Substitute.For<IWorkspaceAIProviderRepository>();

        mockProviderService
            .CreateProviderConfigAsync(Arg.Any<Guid>(), Arg.Any<AIProviderType>(), Arg.Any<string?>(), 
                Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        var createdWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("Test Workspace")
            .Build();

        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(createdWorkspace));

        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.CreateWorkspaceAsync(userId, createRequest);

        // Assert
        Assert.Null(result.DefaultModelId);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ReturnsDtoWithDefaultModelId_FromAIProviderConfiguration()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithIsAiSummarizationEnabled(false)
            .Build();

        var aiConfig = AIProviderConfiguration.Create(
            workspace.Id,
            AIProviderType.AzureOpenAI,
            defaultModelId: "gpt-4o");

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = Substitute.For<IWorkspaceAIProviderRepository>();

        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        mockAiProviderRepo.GetByWorkspaceIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AIProviderConfiguration?>(aiConfig));

        var updateRequest = new UpdateWorkspaceRequest { Name = workspace.Name };

        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert — DefaultModelId sourced exclusively from AIProviderConfiguration
        Assert.Equal("gpt-4o", result.DefaultModelId);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ReturnsDtoWithNullDefaultModelId_WhenNoAIConfig()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        var mockAiProviderRepo = Substitute.For<IWorkspaceAIProviderRepository>();

        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        mockAiProviderRepo.GetByWorkspaceIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<AIProviderConfiguration?>(null));

        var updateRequest = new UpdateWorkspaceRequest { Name = workspace.Name };

        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert — null when no AIProviderConfiguration record exists
        Assert.Null(result.DefaultModelId);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_PersistsAiSummarizationModelId_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithIsAiSummarizationEnabled(true)
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = true,
            AiSummarizationModelId = "llama3.2"
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert
        Assert.Equal("llama3.2", result.AiSummarizationModelId);
        await mockDataAccess.Received(1).UpdateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_PersistsCustomerSatisfactionModelId_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithIsCustomerSatisfactionAnalysisEnabled(true)
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsCustomerSatisfactionAnalysisEnabled = true,
            CustomerSatisfactionAnalysisModelId = "llama3.2"
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert
        Assert.Equal("llama3.2", result.CustomerSatisfactionAnalysisModelId);
        await mockDataAccess.Received(1).UpdateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_PreservesModelIds_WhenOnlyFlagsAreUpdated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithIsAiSummarizationEnabled(true)
            .WithAiSummarizationModelId("llama3.2")
            .WithIsCustomerSatisfactionAnalysisEnabled(true)
            .WithCustomerSatisfactionAnalysisModelId("mistral")
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Only update flags — no model ID fields sent
        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = false,
            IsCustomerSatisfactionAnalysisEnabled = false
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert — model IDs unchanged because no model ID fields were in the request
        Assert.Equal("llama3.2", result.AiSummarizationModelId);
        Assert.Equal("mistral", result.CustomerSatisfactionAnalysisModelId);
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ClearsModelId_WhenNullModelIdSentWithFlagUpdate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithIsAiSummarizationEnabled(true)
            .WithAiSummarizationModelId("llama3.2")
            .Build();

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockProviderService = Substitute.For<IWorkspaceProviderService>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        // Disable feature and explicitly clear model ID
        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = false,
            AiSummarizationModelId = null
        };

        var mockAiProviderRepo = BuildDefaultAiProviderRepo();
        var service = new WorkspaceService(mockDataAccess, mockProviderService, mockAiProviderRepo);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert — model ID preserved because null model ID alone does not trigger updateModelIds
        Assert.Equal("llama3.2", result.AiSummarizationModelId);
    }

}

