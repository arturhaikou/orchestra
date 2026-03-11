namespace Orchestra.Application.Workspaces.DTOs;

public record UpdateWorkspaceRequest
{
    public required string Name { get; init; }
    public bool? IsAiSummarizationEnabled { get; init; }
    public bool? IsCustomerSatisfactionAnalysisEnabled { get; init; }
    public string? AiSummarizationModelId { get; init; }
    public string? CustomerSatisfactionAnalysisModelId { get; init; }
}