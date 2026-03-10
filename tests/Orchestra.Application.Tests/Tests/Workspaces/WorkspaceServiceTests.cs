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
using Orchestra.Tests.Shared.Builders;

namespace Orchestra.Application.Tests.Tests.Workspaces;

public class WorkspaceServiceTests
{
    [Fact]
    public async Task CreateWorkspaceAsync_CreatesWorkspaceWithBothAiFlags_WhenEnabled()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "AI-Enabled Workspace",
            IsAiSummarizationEnabled = true,
            IsCustomerSatisfactionAnalysisEnabled = true
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var createdWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("AI-Enabled Workspace")
            .WithIsAiSummarizationEnabled(true)
            .WithIsCustomerSatisfactionAnalysisEnabled(true)
            .Build();

        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(createdWorkspace));

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        var service = new WorkspaceService(mockDataAccess, mockValidationService);

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
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "Standard Workspace"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var createdWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("Standard Workspace")
            .WithIsAiSummarizationEnabled(false)
            .WithIsCustomerSatisfactionAnalysisEnabled(false)
            .Build();

        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(createdWorkspace));

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        var service = new WorkspaceService(mockDataAccess, mockValidationService);

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
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "Partial AI Workspace",
            IsAiSummarizationEnabled = true,
            IsCustomerSatisfactionAnalysisEnabled = null
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var createdWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("Partial AI Workspace")
            .WithIsAiSummarizationEnabled(true)
            .WithIsCustomerSatisfactionAnalysisEnabled(false)
            .Build();

        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(createdWorkspace));

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        var service = new WorkspaceService(mockDataAccess, mockValidationService);

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
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = true,
            IsCustomerSatisfactionAnalysisEnabled = true
        };

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        var service = new WorkspaceService(mockDataAccess, mockValidationService);

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
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = false,
            IsCustomerSatisfactionAnalysisEnabled = null
        };

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        var service = new WorkspaceService(mockDataAccess, mockValidationService);

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
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "New Workspace Name"
        };

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        var service = new WorkspaceService(mockDataAccess, mockValidationService);

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
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, nonOwnerId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "Hack Attempt",
            IsAiSummarizationEnabled = true
        };

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        var service = new WorkspaceService(mockDataAccess, mockValidationService);

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
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, nonMemberId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "New Name",
            IsAiSummarizationEnabled = true
        };

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        var service = new WorkspaceService(mockDataAccess, mockValidationService);

        // Act & Assert
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(
            () => service.UpdateWorkspaceAsync(nonMemberId, workspace.Id, updateRequest)
        );
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ThrowsInvalidAIModelIdentifierException_WhenAiSummarizationModelIsInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "Test Workspace",
            IsAiSummarizationEnabled = true,
            AiSummarizationModelId = "unknown-model"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        
        // Simulate validation failure for the invalid model
        mockValidationService
            .ValidateAIModelIdentifiersAsync(
                Arg.Is<string?>(m => m == "unknown-model"),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidAIModelIdentifierException(
                new Dictionary<string, string> { { "AI Summarization", "unknown-model" } })));

        var service = new WorkspaceService(mockDataAccess, mockValidationService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidAIModelIdentifierException>(
            () => service.CreateWorkspaceAsync(userId, createRequest));

        Assert.Single(ex.InvalidModelsByFeature);
        Assert.Equal("unknown-model", ex.InvalidModelsByFeature["AI Summarization"]);

        // Verify that CreateAsync was NOT called (no persistence on validation failure)
        await mockDataAccess.DidNotReceive().CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_ThrowsInvalidAIModelIdentifierException_WhenBothModelsAreInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "Test Workspace",
            IsAiSummarizationEnabled = true,
            AiSummarizationModelId = "bad-model-1",
            IsCustomerSatisfactionAnalysisEnabled = true,
            CustomerSatisfactionAnalysisModelId = "bad-model-2"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        
        // Simulate validation failure for both models
        mockValidationService
            .ValidateAIModelIdentifiersAsync(
                Arg.Is<string?>(m => m == "bad-model-1"),
                Arg.Is<string?>(m => m == "bad-model-2"),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidAIModelIdentifierException(
                new Dictionary<string, string>
                {
                    { "AI Summarization", "bad-model-1" },
                    { "Customer Satisfaction Analysis", "bad-model-2" }
                })));

        var service = new WorkspaceService(mockDataAccess, mockValidationService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidAIModelIdentifierException>(
            () => service.CreateWorkspaceAsync(userId, createRequest));

        Assert.Equal(2, ex.InvalidModelsByFeature.Count);
        Assert.Equal("bad-model-1", ex.InvalidModelsByFeature["AI Summarization"]);
        Assert.Equal("bad-model-2", ex.InvalidModelsByFeature["Customer Satisfaction Analysis"]);

        // Verify that CreateAsync was NOT called
        await mockDataAccess.DidNotReceive().CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateWorkspaceAsync_SucceedsAndPersists_WhenModelValidationPasses()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var createRequest = new CreateWorkspaceRequest
        {
            Name = "Valid Workspace",
            IsAiSummarizationEnabled = true,
            AiSummarizationModelId = "gpt-4o",
            IsCustomerSatisfactionAnalysisEnabled = true,
            CustomerSatisfactionAnalysisModelId = "llama3.2"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        
        // Validation succeeds (does not throw)
        mockValidationService
            .ValidateAIModelIdentifiersAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var expectedWorkspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithName("Valid Workspace")
            .WithIsAiSummarizationEnabled(true)
            .WithAiSummarizationModelId("gpt-4o")
            .WithIsCustomerSatisfactionAnalysisEnabled(true)
            .WithCustomerSatisfactionAnalysisModelId("llama3.2")
            .Build();

        mockDataAccess.CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>())
            .Returns(x => Task.FromResult((Workspace)x[0]));

        var service = new WorkspaceService(mockDataAccess, mockValidationService);

        // Act
        var result = await service.CreateWorkspaceAsync(userId, createRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Valid Workspace", result.Name);
        Assert.True(result.IsAiSummarizationEnabled);
        Assert.Equal("gpt-4o", result.AiSummarizationModelId);
        Assert.True(result.IsCustomerSatisfactionAnalysisEnabled);
        Assert.Equal("llama3.2", result.CustomerSatisfactionAnalysisModelId);

        // Verify validation was called
        await mockValidationService.Received(1).ValidateAIModelIdentifiersAsync(
            Arg.Is<string?>(m => m == "gpt-4o"),
            Arg.Is<string?>(m => m == "llama3.2"),
            Arg.Any<CancellationToken>());

        // Verify CreateAsync was called
        await mockDataAccess.Received(1).CreateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_ThrowsInvalidAIModelIdentifierException_WhenModelIsInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithAiSummarizationModelId("gpt-4o")
            .WithIsAiSummarizationEnabled(true)
            .Build();

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = true,
            AiSummarizationModelId = "unknown-new-model"
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        
        // Validation fails
        mockValidationService
            .ValidateAIModelIdentifiersAsync(
                Arg.Is<string?>(m => m == "unknown-new-model"),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidAIModelIdentifierException(
                new Dictionary<string, string> { { "AI Summarization", "unknown-new-model" } })));

        var service = new WorkspaceService(mockDataAccess, mockValidationService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidAIModelIdentifierException>(
            () => service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest));

        // Verify that UpdateAsync was NOT called
        await mockDataAccess.DidNotReceive().UpdateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_SkipsValidation_WhenNoModelIdFieldsAreProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithAiSummarizationModelId("gpt-4o")
            .WithIsAiSummarizationEnabled(true)
            .Build();

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = "Updated Name",
            IsAiSummarizationEnabled = false
            // No model ID fields provided
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        mockValidationService
            .ValidateAIModelIdentifiersAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new WorkspaceService(mockDataAccess, mockValidationService);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.Name);
        Assert.False(result.IsAiSummarizationEnabled);
        // Model ID should be preserved (partial-update semantics)
        Assert.Equal("gpt-4o", result.AiSummarizationModelId);

        // Verify validation was called with null/empty for the unmodified fields
        await mockValidationService.Received(1).ValidateAIModelIdentifiersAsync(
            Arg.Is<string?>(m => m == null || m == ""),
            Arg.Is<string?>(m => m == null || m == ""),
            Arg.Any<CancellationToken>());

        // Verify UpdateAsync was called
        await mockDataAccess.Received(1).UpdateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateWorkspaceAsync_SucceedsAndPreservesPriorModel_WhenOnlyFlagIsChanged()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithOwnerId(userId)
            .WithAiSummarizationModelId("gpt-4o")
            .WithIsAiSummarizationEnabled(true)
            .WithIsCustomerSatisfactionAnalysisEnabled(false)
            .Build();

        var updateRequest = new UpdateWorkspaceRequest
        {
            Name = workspace.Name,
            IsAiSummarizationEnabled = false
            // No model ID fields provided - should be skipped
        };

        var mockDataAccess = Substitute.For<IWorkspaceDataAccess>();
        mockDataAccess.GetByIdAsync(workspace.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Workspace?>(workspace));
        mockDataAccess.IsUserMemberAsync(workspace.Id, userId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var mockValidationService = Substitute.For<IWorkspaceAIModelValidationService>();
        mockValidationService
            .ValidateAIModelIdentifiersAsync(
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new WorkspaceService(mockDataAccess, mockValidationService);

        // Act
        var result = await service.UpdateWorkspaceAsync(userId, workspace.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsAiSummarizationEnabled);
        // Model ID should remain unchanged
        Assert.Equal("gpt-4o", result.AiSummarizationModelId);

        // Verify UpdateAsync was called
        await mockDataAccess.Received(1).UpdateAsync(Arg.Any<Workspace>(), Arg.Any<CancellationToken>());
    }
}
