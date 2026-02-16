using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;

namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Defines the contract for tool category data access operations.
/// This interface abstracts the data access layer, allowing for decoupling
/// from specific persistence implementations like Entity Framework Core.
/// </summary>
public interface IToolCategoryDataAccess
{
    /// <summary>
    /// Retrieves all tool categories across all workspaces.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of all tool categories.</returns>
    Task<List<ToolCategory>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves tool categories filtered by provider types.
    /// </summary>
    /// <param name="providerTypes">List of provider types to filter by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of tool categories matching the specified provider types.</returns>
    Task<List<ToolCategory>> GetByProviderTypesAsync(
        List<ProviderType> providerTypes, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single tool category by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the tool category.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The tool category if found; otherwise, null.</returns>
    Task<ToolCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single tool category by its name.
    /// </summary>
    /// <param name="name">The name of the tool category.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The tool category if found; otherwise, null.</returns>
    Task<ToolCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new tool category to the database.
    /// </summary>
    /// <param name="toolCategory">The tool category entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(ToolCategory toolCategory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing tool category in the database.
    /// </summary>
    /// <param name="toolCategory">The tool category entity with updated values.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateAsync(ToolCategory toolCategory, CancellationToken cancellationToken = default);
}