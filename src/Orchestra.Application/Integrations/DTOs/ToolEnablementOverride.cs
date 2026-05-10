namespace Orchestra.Application.Integrations.DTOs;

public record ToolEnablementOverride(
    string ToolName,
    bool IsEnabled
);
