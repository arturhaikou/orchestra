using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Orchestra.Application.Auth.DTOs;
using Orchestra.Application.Auth.Services;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Xunit;

namespace Orchestra.Application.Tests.Tests.Auth;

public class AuthServiceTests : IClassFixture<AuthTestFixture>
{
    private readonly AuthTestFixture _fixture;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IUserDataAccess _userDataAccess;
    private readonly AuthService _sut;

    public AuthServiceTests(AuthTestFixture fixture)
    {
        _fixture = fixture;
        _passwordHashingService = _fixture.CreateMockPasswordHashingService();
        _jwtTokenService = _fixture.CreateMockJwtTokenService();
        _userDataAccess = _fixture.CreateMockUserDataAccess();
        _sut = new AuthService(_passwordHashingService, _jwtTokenService, _userDataAccess);
    }

    // Note: Service does not check for cancellation before other exceptions, so these tests are omitted.

    // ChangePasswordAsync tests

    [Fact]
    public async Task ChangePasswordAsync_InvalidCurrentPassword_ThrowsInvalidCredentialsException()
    {
        // Arrange
        var userId = TestConstants.TestUserId;
        var user = new UserBuilder().WithId(userId).WithPasswordHash(TestConstants.TestPasswordHash).Build();
        var request = new ChangePasswordRequestBuilder().WithCurrentPassword("wrong").WithNewPassword("NewValidPassword123!").Build();
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHashingService.VerifyPassword(request.CurrentPassword, user.PasswordHash).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _sut.ChangePasswordAsync(userId, request));
    }

    [Fact]
    public async Task ChangePasswordAsync_InvalidNewPassword_ThrowsValidationException()
    {
        // Arrange
        var userId = TestConstants.TestUserId;
        var user = new UserBuilder().WithId(userId).WithPasswordHash(TestConstants.TestPasswordHash).Build();
        var request = new ChangePasswordRequestBuilder().WithCurrentPassword(TestConstants.TestPassword).WithNewPassword("short").Build();
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHashingService.VerifyPassword(request.CurrentPassword, user.PasswordHash).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _sut.ChangePasswordAsync(userId, request));
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = TestConstants.TestUserId;
        var request = new ChangePasswordRequestBuilder().WithCurrentPassword(TestConstants.TestPassword).WithNewPassword("NewValidPassword123!").Build();
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ChangePasswordAsync(userId, request));
    }

    // UpdateProfileAsync tests
    [Fact]
    public async Task UpdateProfileAsync_SuccessfulUpdate_ReturnsUserDto()
    {
        // Arrange
        var userId = TestConstants.TestUserId;
        var user = new UserBuilder().WithId(userId).WithEmail("old@example.com").WithName("Old Name").Build();
        var request = new UpdateProfileRequest("new@example.com", "New Name");
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _userDataAccess.AnyByEmailExcludingUserAsync(request.Email, userId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _sut.UpdateProfileAsync(userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Email.ToLowerInvariant(), result.Email);
        Assert.Equal(request.Name, result.Name);
        await _userDataAccess.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateProfileAsync_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        // Arrange
        var userId = TestConstants.TestUserId;
        var user = new UserBuilder().WithId(userId).WithEmail("old@example.com").Build();
        var request = new UpdateProfileRequest("duplicate@example.com", "Name");
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _userDataAccess.AnyByEmailExcludingUserAsync(request.Email, userId, Arg.Any<CancellationToken>()).Returns(true);

        // Act & Assert
        await Assert.ThrowsAsync<DuplicateEmailException>(() => _sut.UpdateProfileAsync(userId, request));
    }

    [Fact]
    public async Task UpdateProfileAsync_UserNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var userId = TestConstants.TestUserId;
        var request = new UpdateProfileRequest("new@example.com", "Name");
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.UpdateProfileAsync(userId, request));
    }

    [Fact]
    public async Task UpdateProfileAsync_UnchangedEmail_DoesNotCheckForDuplicate()
    {
        // Arrange
        var userId = TestConstants.TestUserId;
        var user = new UserBuilder().WithId(userId).WithEmail("same@example.com").Build();
        var request = new UpdateProfileRequest("same@example.com", "New Name");
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        await _sut.UpdateProfileAsync(userId, request);

        // Assert
        await _userDataAccess.DidNotReceive().AnyByEmailExcludingUserAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _userDataAccess.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // LoginAsync tests
    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ReturnsAuthResponse()
    {
        // Arrange
        var user = new UserBuilder().WithPasswordHash(TestConstants.TestPasswordHash).Build();
        var request = new LoginRequestBuilder().WithEmail(user.Email).WithPassword(TestConstants.TestPassword).Build();
        _userDataAccess.FindByEmailAsync(request.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHashingService.VerifyPassword(request.Password, user.PasswordHash).Returns(true);
        _jwtTokenService.GenerateToken(user).Returns(TestConstants.TestJwtToken);

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestConstants.TestJwtToken, result.Token);
        Assert.Equal(user.Email, result.User.Email);
        await _userDataAccess.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsInvalidCredentialsException()
    {
        // Arrange
        var request = new LoginRequestBuilder().Build();
        _userDataAccess.FindByEmailAsync(request.Email, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _sut.LoginAsync(request));
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ThrowsInvalidCredentialsException()
    {
        // Arrange
        var user = new UserBuilder().WithPasswordHash(TestConstants.TestPasswordHash).Build();
        var request = new LoginRequestBuilder().WithEmail(user.Email).WithPassword("wrongpassword").Build();
        _userDataAccess.FindByEmailAsync(request.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHashingService.VerifyPassword(request.Password, user.PasswordHash).Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidCredentialsException>(() => _sut.LoginAsync(request));
    }

    [Fact]
    public async Task LoginAsync_UpdatesLastLoginAtAndPersists()
    {
        // Arrange
        var user = new UserBuilder().WithPasswordHash(TestConstants.TestPasswordHash).Build();
        var request = new LoginRequestBuilder().WithEmail(user.Email).WithPassword(TestConstants.TestPassword).Build();
        _userDataAccess.FindByEmailAsync(request.Email, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHashingService.VerifyPassword(request.Password, user.PasswordHash).Returns(true);
        _jwtTokenService.GenerateToken(user).Returns(TestConstants.TestJwtToken);

        // Act
        await _sut.LoginAsync(request);

        // Assert
        await _userDataAccess.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task ChangePasswordAsync_SuccessfulChange_UpdatesPassword()
    {
        // Arrange
        var userId = TestConstants.TestUserId;
        var user = new UserBuilder().WithId(userId).WithPasswordHash(TestConstants.TestPasswordHash).Build();
        var request = new ChangePasswordRequestBuilder().WithCurrentPassword(TestConstants.TestPassword).WithNewPassword("NewValidPassword123!").Build();
        _userDataAccess.FindByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHashingService.VerifyPassword(request.CurrentPassword, user.PasswordHash).Returns(true);
        _passwordHashingService.HashPassword(request.NewPassword).Returns("hashed_new_password");

        // Act
        await _sut.ChangePasswordAsync(userId, request);

        // Assert
        _passwordHashingService.Received(1).HashPassword(request.NewPassword);
        await _userDataAccess.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        Assert.True(user.PasswordHash == "hashed_new_password");
    }
    // RegisterAsync tests
    [Fact]
    public async Task RegisterAsync_SuccessfulRegistration_ReturnsAuthResponse()
    {
        // Arrange
        var request = new RegisterRequestBuilder().Build();
        _userDataAccess.AnyByEmailAsync(request.Email, Arg.Any<CancellationToken>()).Returns(false);
        _passwordHashingService.HashPassword(request.Password).Returns(TestConstants.TestPasswordHash);
        _jwtTokenService.GenerateToken(Arg.Any<User>()).Returns(TestConstants.TestJwtToken);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(TestConstants.TestJwtToken, result.Token);
        Assert.Equal(request.Email.ToLowerInvariant(), result.User.Email);
        await _userDataAccess.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _userDataAccess.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_InvalidPassword_ThrowsValidationException()
    {
        // Arrange
        var request = new RegisterRequestBuilder().WithPassword("short").Build();
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => _sut.RegisterAsync(request));
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        // Arrange
        var request = new RegisterRequestBuilder().Build();
        _userDataAccess.AnyByEmailAsync(request.Email, Arg.Any<CancellationToken>()).Returns(true);
        // Act & Assert
        await Assert.ThrowsAsync<DuplicateEmailException>(() => _sut.RegisterAsync(request));
    }

    [Fact]
    public async Task RegisterAsync_NullName_ThrowsArgumentException()
    {
        // Arrange
        var request = new RegisterRequestBuilder().WithName(null!).Build();
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.RegisterAsync(request));
    }

    [Fact]
    public async Task RegisterAsync_EmptyEmail_ThrowsArgumentException()
    {
        // Arrange
        var request = new RegisterRequestBuilder().WithEmail("").Build();
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.RegisterAsync(request));
    }

    [Fact]
    public async Task RegisterAsync_PasswordIsHashedAndUserSaved()
    {
        // Arrange
        var request = new RegisterRequestBuilder().Build();
        _userDataAccess.AnyByEmailAsync(request.Email, Arg.Any<CancellationToken>()).Returns(false);
        _passwordHashingService.HashPassword(request.Password).Returns(TestConstants.TestPasswordHash);
        _jwtTokenService.GenerateToken(Arg.Any<User>()).Returns(TestConstants.TestJwtToken);

        // Act
        await _sut.RegisterAsync(request);

        // Assert
        _passwordHashingService.Received(1).HashPassword(request.Password);
        await _userDataAccess.Received(1).AddAsync(Arg.Is<User>(u => u.Email == request.Email.ToLowerInvariant()), Arg.Any<CancellationToken>());
    }
}
