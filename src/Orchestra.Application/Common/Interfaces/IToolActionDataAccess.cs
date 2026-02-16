using Orchestra.Domain.Entities;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Defines the contract for ToolAction data access operations.
/// This interface abstracts the data access layer for tool actions,
/// enabling querying by category IDs using explicit joins without navigation properties.
/// </summary>
public interface IToolActionDataAccess
{
    /// <summary>
    /// Retrieves tool actions for multiple categories.
    /// </summary>
    /// <param name="categoryIds">List of category IDs to retrieve actions for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of tool actions belonging to the specified categories, ordered by name.</returns>
    Task<List<ToolAction>> GetByCategoryIdsAsync(
        List<Guid> categoryIds, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tool actions for a single category.
    /// </summary>
    /// <param name="categoryId">The category ID to retrieve actions for.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of tool actions belonging to the specified category, ordered by name.</returns>
    Task<List<ToolAction>> GetByCategoryIdAsync(
        Guid categoryId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single tool action by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the tool action.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The tool action if found; otherwise, null.</returns>
    Task<ToolAction?> GetByIdAsync(
        Guid id, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new tool action to the database.
    /// </summary>
    /// <param name="toolAction">The tool action entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(
        ToolAction toolAction, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tool action in the database.
    /// </summary>
    /// <param name="toolAction">The tool action entity with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(
        ToolAction toolAction, 
        CancellationToken cancellationToken = default);
}