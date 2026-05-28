namespace Orchestra.Application.Tools.DTOs;

public record AgentOptionalToolSelectionsDto(IReadOnlyList<string> SelectedMethodNames);

public record SaveAgentOptionalToolsRequest(IReadOnlyList<string> MethodNames);
