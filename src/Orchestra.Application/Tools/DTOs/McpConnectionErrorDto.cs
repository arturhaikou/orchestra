namespace Orchestra.Application.Tools.DTOs;

public record McpConnectionErrorDto(
    string ServerUrl,
    string ErrorMessage,
    string ErrorCode,
    bool ToolsModified
);
