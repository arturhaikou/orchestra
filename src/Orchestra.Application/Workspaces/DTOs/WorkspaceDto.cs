namespace Orchestra.Application.Workspaces.DTOs;

public record WorkspaceDto(
    string Id,
    string Name,
    bool IsAiSummarizationEnabled,
    bool IsCustomerSatisfactionAnalysisEnabled,
    string? AiSummarizationModelId,
    string? CustomerSatisfactionAnalysisModelId,
    string? DefaultModelId,
    string OwnerId);