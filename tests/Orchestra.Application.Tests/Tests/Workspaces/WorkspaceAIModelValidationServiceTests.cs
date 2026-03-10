using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using Orchestra.Application.Workspaces.Services;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Application.Common.Exceptions;

namespace Orchestra.Application.Tests.Tests.Workspaces;

/// <summary>
/// Unit tests for WorkspaceAIModelValidationService.
/// Tests validation logic against the available models returned by IAIModelListService.
/// </summary>
public class WorkspaceAIModelValidationServiceTests
{
    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_DoesNotThrow_WhenBothModelIdFieldsAreNull()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert - should not throw
        await validationService.ValidateAIModelIdentifiersAsync(
            aiSummarizationModelId: null,
            customerSatisfactionAnalysisModelId: null);

        // Verify that the AI model list service was NOT called
        // (optimization: skip provider call if no models to validate)
        await mockService.DidNotReceive().GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_DoesNotThrow_WhenBothModelIdFieldsAreEmpty()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert - should not throw
        await validationService.ValidateAIModelIdentifiersAsync(
            aiSummarizationModelId: "",
            customerSatisfactionAnalysisModelId: "  ");

        // Verify that the AI model list service was NOT called
        await mockService.DidNotReceive().GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_DoesNotThrow_WhenAllModelsExist()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        mockService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(
                new[] { "gpt-4o", "gpt-4o-mini", "llama3.2", "mistral" } as IReadOnlyList<string>));

        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert - should not throw
        await validationService.ValidateAIModelIdentifiersAsync(
            aiSummarizationModelId: "gpt-4o",
            customerSatisfactionAnalysisModelId: "llama3.2");

        // Verify the service was called once
        await mockService.Received(1).GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_ThrowsException_WhenSummarizationModelDoesNotExist()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        mockService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(
                new[] { "gpt-4o", "gpt-4o-mini" } as IReadOnlyList<string>));

        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidAIModelIdentifierException>(
            () => validationService.ValidateAIModelIdentifiersAsync(
                aiSummarizationModelId: "unknown-model",
                customerSatisfactionAnalysisModelId: "gpt-4o"));

        // Verify the exception contains the invalid model
        Assert.Single(ex.InvalidModelsByFeature);
        Assert.True(ex.InvalidModelsByFeature.ContainsKey("AI Summarization"));
        Assert.Equal("unknown-model", ex.InvalidModelsByFeature["AI Summarization"]);
    }

    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_ThrowsException_WhenSatisfactionModelDoesNotExist()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        mockService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(
                new[] { "gpt-4o", "gpt-4o-mini" } as IReadOnlyList<string>));

        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidAIModelIdentifierException>(
            () => validationService.ValidateAIModelIdentifiersAsync(
                aiSummarizationModelId: "gpt-4o",
                customerSatisfactionAnalysisModelId: "bad-model"));

        // Verify the exception contains the invalid model
        Assert.Single(ex.InvalidModelsByFeature);
        Assert.True(ex.InvalidModelsByFeature.ContainsKey("Customer Satisfaction Analysis"));
        Assert.Equal("bad-model", ex.InvalidModelsByFeature["Customer Satisfaction Analysis"]);
    }

    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_ThrowsException_ReportingBothInvalidModels()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        mockService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(
                new[] { "gpt-4o", "gpt-4o-mini" } as IReadOnlyList<string>));

        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidAIModelIdentifierException>(
            () => validationService.ValidateAIModelIdentifiersAsync(
                aiSummarizationModelId: "unknown-1",
                customerSatisfactionAnalysisModelId: "unknown-2"));

        // Verify both invalid models are reported
        Assert.Equal(2, ex.InvalidModelsByFeature.Count);
        Assert.True(ex.InvalidModelsByFeature.ContainsKey("AI Summarization"));
        Assert.True(ex.InvalidModelsByFeature.ContainsKey("Customer Satisfaction Analysis"));
        Assert.Equal("unknown-1", ex.InvalidModelsByFeature["AI Summarization"]);
        Assert.Equal("unknown-2", ex.InvalidModelsByFeature["Customer Satisfaction Analysis"]);
    }

    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_SkipsValidation_WhenOnlyOneModelIsProvided()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        mockService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(
                new[] { "gpt-4o" } as IReadOnlyList<string>));

        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert - should not throw (only summarization model is provided, it exists)
        await validationService.ValidateAIModelIdentifiersAsync(
            aiSummarizationModelId: "gpt-4o",
            customerSatisfactionAnalysisModelId: null);

        // Verify the service was called
        await mockService.Received(1).GetAvailableModelsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_PropagatesHttpRequestException_WhenProviderUnreachable()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        mockService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Failed to reach AI provider"));

        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => validationService.ValidateAIModelIdentifiersAsync(
                aiSummarizationModelId: "gpt-4o",
                customerSatisfactionAnalysisModelId: null));

        Assert.Contains("Failed to reach AI provider", ex.Message);
    }

    [Fact]
    public async Task ValidateAIModelIdentifiersAsync_PropagatesInvalidOperationException_WhenProviderMisconfigured()
    {
        // Arrange
        var mockService = Substitute.For<IAIModelListService>();
        mockService.GetAvailableModelsAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Missing AI provider credentials"));

        var validationService = new WorkspaceAIModelValidationService(mockService);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => validationService.ValidateAIModelIdentifiersAsync(
                aiSummarizationModelId: "gpt-4o",
                customerSatisfactionAnalysisModelId: null));

        Assert.Contains("Missing AI provider credentials", ex.Message);
    }
}
