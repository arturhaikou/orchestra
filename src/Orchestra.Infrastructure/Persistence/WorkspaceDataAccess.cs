using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Orchestra.Application.Common.Exceptions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence
{
    /// <summary>
    /// Provides data access operations for Workspace entities using Entity Framework Core.
    /// </summary>
    public class WorkspaceDataAccess : IWorkspaceDataAccess
    {
        private readonly AppDbContext _context;

        public WorkspaceDataAccess(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Creates a workspace and adds the owner to UserWorkspaces in a transaction.
        /// </summary>
        /// <param name="workspace">The workspace to create.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The created workspace.</returns>
        public async Task<Workspace> CreateAsync(Workspace workspace, CancellationToken cancellationToken = default)
        {
            _context.Workspaces.Add(workspace);
            var userWorkspace = UserWorkspace.Create(workspace.OwnerId, workspace.Id);
            _context.UserWorkspaces.Add(userWorkspace);
            await _context.SaveChangesAsync(cancellationToken);

            return workspace;
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

        /// <summary>
        /// Retrieves all active workspaces where the specified user is a member.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of workspaces ordered alphabetically by name.</returns>
        public async Task<List<Workspace>> GetUserWorkspacesAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.UserWorkspaces
                .Where(uw => uw.UserId == userId)
                .Join(
                    _context.Workspaces,
                    uw => uw.WorkspaceId,
                    w => w.Id,
                    (uw, w) => w
                )
                .Where(w => w.IsActive)
                .OrderBy(w => w.Name)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Checks if a user is a member of the specified workspace.
        /// </summary>
        /// <param name="userId">The user ID to check.</param>
        /// <param name="workspaceId">The workspace ID to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the user is a member of the workspace, false otherwise.</returns>
        public async Task<bool> IsMemberAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return await _context.UserWorkspaces
                .AnyAsync(uw => uw.UserId == userId && uw.WorkspaceId == workspaceId, cancellationToken);
        }

        /// <summary>
        /// Checks if a user is a member of the specified workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace ID to check.</param>
        /// <param name="userId">The user ID to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the user is a member of the workspace, false otherwise.</returns>
        public async Task<bool> IsUserMemberAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.UserWorkspaces
                .AnyAsync(uw => uw.UserId == userId && uw.WorkspaceId == workspaceId, cancellationToken);
        }

        /// <summary>
        /// Retrieves a workspace by its ID.
        /// </summary>
        /// <param name="id">The ID of the workspace.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The workspace if found, otherwise null.</returns>
        public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Workspaces
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
        }

        /// <summary>
        /// Updates a workspace in the database.
        /// </summary>
        /// <param name="workspace">The workspace to update.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken = default)
        {
            _context.Workspaces.Update(workspace);
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Deletes a workspace by its unique identifier.
        /// Cascade deletes all related UserWorkspaces due to foreign key configuration.
        /// </summary>
        /// <param name="id">The workspace ID to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="WorkspaceNotFoundException">Thrown when workspace is not found.</exception>
        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var workspace = await _context.Workspaces
                .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

            if (workspace == null)
            {
                throw new WorkspaceNotFoundException(id);
            }

            _context.Workspaces.Remove(workspace);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}