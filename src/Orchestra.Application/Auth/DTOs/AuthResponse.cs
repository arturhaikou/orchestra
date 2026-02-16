namespace Orchestra.Application.Auth.DTOs;

public record AuthResponse(string Token, UserDto User);