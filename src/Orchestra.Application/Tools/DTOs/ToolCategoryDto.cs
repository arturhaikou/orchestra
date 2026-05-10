namespace Orchestra.Application.Tools.DTOs;

public record ToolCategoryDto(
    Guid Id,
    string Name,
    string Description,
    string ProviderType,
    List<ToolActionDto> Actions,
    string Source = "native",
    Guid? IntegrationId = null,
    bool IsMcpCategory = false,
    string? TransportType = null,
    bool? IntegrationConnected = null
);