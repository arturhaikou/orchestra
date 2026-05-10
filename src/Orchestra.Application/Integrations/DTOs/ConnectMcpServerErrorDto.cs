namespace Orchestra.Application.Integrations.DTOs;

public sealed record ConnectMcpServerErrorDto(
    string ErrorCode,
    string Message
);
