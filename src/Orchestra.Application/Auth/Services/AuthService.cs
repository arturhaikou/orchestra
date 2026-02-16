using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Validators;

namespace Orchestra.Application.Auth.Services;

public class AuthService : IAuthService
{
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserDataAccess _userDataAccess;

    public AuthService(
        IPasswordHashingService passwordHashingService,
        IJwtTokenService jwtTokenService,
        IUserDataAccess userDataAccess)
    {
        _passwordHashingService = passwordHashingService;
        _jwtTokenService = jwtTokenService;
        _userDataAccess = userDataAccess;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        // Validate password
        var (isValid, error) = PasswordValidator.ValidatePassword(request.Password);
        if (!isValid)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["Password"] = new[] { error! }
            };
            throw new ValidationException(errors);
        }

        // Check if email already exists
        var emailExists = await _userDataAccess.AnyByEmailAsync(
            request.Email, cancellationToken);
        if (emailExists)
        {
            throw new DuplicateEmailException(request.Email);
        }

        // Hash password
        var passwordHash = _passwordHashingService.HashPassword(request.Password);

        // Create user
        var user = User.Create(request.Email.ToLowerInvariant(), request.Name, passwordHash);

        // Save to database
        await _userDataAccess.AddAsync(user, cancellationToken);
        await _userDataAccess.SaveChangesAsync(cancellationToken);

        // Generate JWT token
        var token = _jwtTokenService.GenerateToken(user);

        // Return response
        var userDto = new UserDto(user.Id.ToString(), user.Email, user.Name);
        return new AuthResponse(token, userDto);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Query database for user by email and IsActive
        var user = await _userDataAccess.FindByEmailAsync(
            request.Email, cancellationToken);

        // If user is null, throw InvalidCredentialsException
        if (user == null)
        {
            throw new InvalidCredentialsException();
        }

        // Verify password
        var isPasswordValid = _passwordHashingService.VerifyPassword(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            throw new InvalidCredentialsException();
        }

        // Call user.RecordLogin() to update LastLoginAt
        user.RecordLogin();

        // Persist changes
        await _userDataAccess.SaveChangesAsync(cancellationToken);

        // Generate JWT token
        var token = _jwtTokenService.GenerateToken(user);

        // Map user to UserDto and create AuthResponse
        var userDto = new UserDto(user.Id.ToString(), user.Email, user.Name);
        return new AuthResponse(token, userDto);
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userDataAccess.FindByIdAsync(userId, cancellationToken);
        
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found");
        
        // Check for duplicate email only if email has changed
        var normalizedEmail = request.Email.ToLowerInvariant();
        if (user.Email != normalizedEmail)
        {
            var emailExists = await _userDataAccess.AnyByEmailExcludingUserAsync(
                request.Email, userId, cancellationToken);
            
            if (emailExists)
                throw new DuplicateEmailException(request.Email);
        }
        
        // Use domain method to update profile
        user.UpdateProfile(normalizedEmail, request.Name);
        
        await _userDataAccess.SaveChangesAsync(cancellationToken);
        
        return new UserDto(user.Id.ToString(), user.Email, user.Name);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        // Load user
        var user = await _userDataAccess.FindByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException($"User with ID {userId} not found");

        // Verify current password
        var isCurrentPasswordValid = _passwordHashingService.VerifyPassword(
            request.CurrentPassword, 
            user.PasswordHash);
        
        if (!isCurrentPasswordValid)
            throw new InvalidCredentialsException();

        // Validate new password strength
        var (isValid, error) = PasswordValidator.ValidatePassword(request.NewPassword);
        if (!isValid)
        {
            var errors = new Dictionary<string, string[]>
            {
                ["NewPassword"] = new[] { error! }
            };
            throw new ValidationException(errors);
        }

        // Hash new password
        var newPasswordHash = _passwordHashingService.HashPassword(request.NewPassword);

        // Update user entity
        user.UpdatePassword(newPasswordHash);

        // Persist changes
        await _userDataAccess.SaveChangesAsync(cancellationToken);
    }
}