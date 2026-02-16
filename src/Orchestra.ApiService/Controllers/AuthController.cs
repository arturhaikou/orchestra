using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using System.Security.Claims;

namespace Orchestra.ApiService.Controllers;

[ApiController]
[Route("v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new ErrorResponse("Validation failed", string.Join(", ", ex.Errors.SelectMany(e => e.Value))));
        }
        catch (DuplicateEmailException)
        {
            return Conflict(new ErrorResponse("Email already exists"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }

    [Authorize]
    [HttpPatch("profile")]
    [ProducesResponseType(typeof(UserDto), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 409)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    public async Task<ActionResult<UserDto>> UpdateProfile(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ErrorResponse("Invalid user token"));
            }

            var response = await _authService.UpdateProfileAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new ErrorResponse(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Extract user ID from token claims
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(new ErrorResponse("Invalid user token"));
            }

            // Call service to change password
            await _authService.ChangePasswordAsync(userId, request, cancellationToken);
            
            // Return success
            return NoContent();
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new ErrorResponse(ex.Message));
        }
        catch (ValidationException ex)
        {
            var errorDetails = string.Join(", ", ex.Errors.Values.SelectMany(v => v));
            return BadRequest(new ErrorResponse(ex.Message, errorDetails));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (Exception)
        {
            // Log the exception (if logger is available)
            return StatusCode(500, new ErrorResponse("An unexpected error occurred"));
        }
    }
}