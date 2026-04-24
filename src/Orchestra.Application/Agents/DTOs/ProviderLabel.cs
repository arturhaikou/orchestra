using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.DTOs;

public record ProviderLabel(
    ProviderType ProviderType,
    string Label
);
