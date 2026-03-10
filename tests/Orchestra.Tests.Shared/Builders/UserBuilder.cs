using Bogus;

namespace Orchestra.Tests.Shared.Builders;

/// <summary>
/// Fluent builder for creating User test entities with sensible defaults.
/// </summary>
public class UserBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _email = new Faker().Internet.Email();
    private string _name = new Faker().Name.FullName();
    private string _passwordHash = "hashed_password_" + Guid.NewGuid();
    private bool _isActive = true;

    /// <summary>
    /// Sets the user ID.
    /// </summary>
    public UserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the user email address.
    /// </summary>
    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    /// <summary>
    /// Sets the user name.
    /// </summary>
    public UserBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// Sets the password hash.
    /// </summary>
    public UserBuilder WithPasswordHash(string passwordHash)
    {
        _passwordHash = passwordHash;
        return this;
    }

    /// <summary>
    /// Sets whether the user is active.
    /// </summary>
    public UserBuilder AsActive(bool active = true)
    {
        _isActive = active;
        return this;
    }

    /// <summary>
    /// Builds the User entity.
    /// </summary>
    public User Build()
    {
        return User.Create(_email, _name, _passwordHash);
    }

    /// <summary>
    /// Creates an active user with typical configuration.
    /// </summary>
    public static User ActiveUser()
    {
        return new UserBuilder()
            .AsActive(true)
            .Build();
    }

    /// <summary>
    /// Creates an inactive user.
    /// </summary>
    public static User InactiveUser()
    {
        return new UserBuilder()
            .AsActive(false)
            .Build();
    }

    /// <summary>
    /// Creates a user with a specific email.
    /// </summary>
    public static User UserWithEmail(string email)
    {
        return new UserBuilder()
            .WithEmail(email)
            .Build();
    }
}
