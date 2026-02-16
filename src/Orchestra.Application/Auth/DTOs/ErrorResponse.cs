namespace Orchestra.Application.Auth.DTOs;

public record ErrorResponse(string Message, string? Detail = null);