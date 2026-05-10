namespace Orchestra.Application.Integrations.DTOs;

public sealed record SaveMcpServerErrorDto(
    string ErrorCode,
    string Message
);
