using System.ComponentModel.DataAnnotations;

namespace Orchestra.Application.Auth.DTOs;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);