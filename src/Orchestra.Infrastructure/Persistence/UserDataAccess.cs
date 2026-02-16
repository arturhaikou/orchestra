using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence
{
    /// <summary>
    /// Provides data access operations for User entities using Entity Framework Core.
    /// </summary>
    public class UserDataAccess : IUserDataAccess
    {
        private readonly AppDbContext _context;

        public UserDataAccess(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Finds a user by their unique identifier.
        /// </summary>
        /// <param name="userId">The unique identifier of the user.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The user if found; otherwise, null.</returns>
        public async Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
        }

        /// <summary>
        /// Finds a user by their email address. The search is case-insensitive.
        /// </summary>
        /// <param name="email">The email address of the user.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The user if found; otherwise, null.</returns>
        public async Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() && u.IsActive, cancellationToken);
        }

        /// <summary>
        /// Checks if any user exists with the specified email address (case-insensitive).
        /// </summary>
        public async Task<bool> AnyByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AnyAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);
        }

        /// <summary>
        /// Checks if any user (excluding the specified user ID) exists with the given email address (case-insensitive).
        /// Used for update scenarios to prevent duplicate emails.
        /// </summary>
        /// <param name="email">The email address to check.</param>
        /// <param name="excludeUserId">The ID of the user to exclude from the check.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>True if another user with the email exists; otherwise, false.</returns>
        public async Task<bool> AnyByEmailExcludingUserAsync(string email, Guid excludeUserId, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AnyAsync(u => u.Email.ToLower() == email.ToLower() && u.Id != excludeUserId, 
                         cancellationToken);
        }

        /// <summary>
        /// Adds a new user to the database context.
        /// Call SaveChangesAsync to persist changes.
        /// </summary>
        /// <param name="user">The user to add.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _context.Users.Add(user);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Marks user entity as modified for explicit update tracking.
        /// Call SaveChangesAsync to persist changes.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Persists all pending changes to the database.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The number of state entries written to the database.</returns>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
    }
}