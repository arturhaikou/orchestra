namespace Orchestra.Application.Tools.DTOs;

/// <summary>
/// Response DTO for Tool Category with associated actions.
/// Includes hierarchical child collection of actions to reduce API roundtrips.
/// </summary>
public record ToolCategoryDto(
    Guid Id,
    string Name,
    string Description,
    string ProviderType,
    List<ToolActionDto> Actions
);