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
    /// Retrieves enabled tool actions matching the given IDs.
    /// Only returns actions where <c>IsEnabled = true</c>, enforcing the destructive
    /// opt-in gate: non-opted-in MCP tool actions are excluded because they have
    /// <c>IsEnabled = false</c> and will be absent from the result.
    /// </summary>
    /// <param name="ids">The tool action IDs to look up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Enabled tool actions matching the provided IDs. Missing or disabled IDs are absent from the result.</returns>
    Task<List<ToolAction>> GetEnabledByIdsAsync(
        List<Guid> ids,
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

    Task AddRangeAsync(
        IEnumerable<ToolAction> toolActions,
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

    Task UpdateRangeAsync(
        List<ToolAction> toolActions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tool actions matching the given method names.
    /// Used by the template availability resolver to map method names to database IDs.
    /// </summary>
    /// <param name="methodNames">The method names to look up.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Tool actions matching the provided method names.</returns>
    Task<List<ToolAction>> GetByMethodNamesAsync(
        List<string> methodNames,
        CancellationToken cancellationToken = default);

    Task<List<ToolAction>> GetActiveByIntegrationIdAsync(
        Guid integrationId,
        CancellationToken cancellationToken = default);

    Task<List<ToolAction>> GetByIntegrationIdAsync(Guid integrationId, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, int>> CountActiveByIntegrationIdsAsync(
        List<Guid> integrationIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tool actions matching the given action names (snake_case identifiers).
    /// Used by the template availability resolver to look up actions by their public name.
    /// </summary>
    /// <param name="names">The action names to look up (e.g., "review_merge_request").</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>Tool actions matching the provided names.</returns>
    Task<List<ToolAction>> GetByNamesAsync(
        List<string> names,
        CancellationToken cancellationToken = default);

    Task<ToolAction?> FindByToolCategoryIdAndMethodNameAsync(
        Guid toolCategoryId,
        string methodName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> DeactivateByIntegrationIdAsync(Guid integrationId, CancellationToken cancellationToken = default);
}