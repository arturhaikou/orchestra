using Orchestra.Domain.Enums;

namespace Orchestra.Application.Agents.DTOs;

public record ResolvedToolAction(
    Guid ToolActionId,
    string MethodName,
    ProviderType ProviderType
);
