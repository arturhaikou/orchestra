using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Defines the contract for user data access operations.
/// This interface abstracts the data access layer, allowing for decoupling
/// from specific persistence implementations like Entity Framework Core.
/// </summary>
public interface IUserDataAccess
{
    /// <summary>
    /// Finds a user by their unique identifier.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a user by their email address. The search is case-insensitive.
    /// </summary>
    /// <param name="email">The email address of the user.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any user exists with the specified email address. The search is case-insensitive.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if a user with the email exists; otherwise, false.</returns>
    Task<bool> AnyByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any user exists with the specified email address, excluding a specific user.
    /// This is useful for update scenarios to check for duplicate emails while excluding the current user.
    /// The search is case-insensitive.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <param name="excludeUserId">The ID of the user to exclude from the check.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if another user with the email exists; otherwise, false.</returns>
    Task<bool> AnyByEmailExcludingUserAsync(string email, Guid excludeUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new user to the data store.
    /// </summary>
    /// <param name="user">The user to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task AddAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing user in the data store.
    /// </summary>
    /// <param name="user">The user to update.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all changes made in the current context to the data store.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}