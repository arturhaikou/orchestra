using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Domain.Entities;
using Orchestra.Domain.Enums;
using Orchestra.Infrastructure.Tools.Attributes;

namespace Orchestra.Infrastructure.Tools;

/// <summary>
/// Service for scanning assemblies to discover tool categories and actions
/// decorated with custom attributes, and seeding them into the database.
/// </summary>
public class ToolScanningService : IToolScanningService
{
    private readonly IToolCategoryDataAccess _categoryDataAccess;
    private readonly IToolActionDataAccess _actionDataAccess;
    private readonly ILogger<ToolScanningService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolScanningService"/> class.
    /// </summary>
    /// <param name="categoryDataAccess">Data access for tool categories.</param>
    /// <param name="actionDataAccess">Data access for tool actions.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public ToolScanningService(
        IToolCategoryDataAccess categoryDataAccess,
        IToolActionDataAccess actionDataAccess,
        ILogger<ToolScanningService> logger)
    {
        _categoryDataAccess = categoryDataAccess;
        _actionDataAccess = actionDataAccess;
        _logger = logger;
    }

    /// <summary>
    /// Scans all loaded assemblies for types decorated with [ToolCategory] attribute.
    /// </summary>
    /// <returns>A collection of discovered category types with their attributes.</returns>
    private IEnumerable<(Type Type, ToolCategoryAttribute Attribute)> ScanForToolCategories()
    {
        var discoveredCategories = new List<(Type, ToolCategoryAttribute)>();

        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName?.StartsWith("orchestra", StringComparison.OrdinalIgnoreCase) == true);

            foreach (var assembly in assemblies)
            {
                try
                {
                    var typesWithAttribute = assembly.GetTypes()
                        .Where(t => t.GetCustomAttribute<ToolCategoryAttribute>() != null)
                        .Select(t => (Type: t, Attribute: t.GetCustomAttribute<ToolCategoryAttribute>()!));

                    discoveredCategories.AddRange(typesWithAttribute);
                }
                catch (ReflectionTypeLoadException ex)
                {
                    _logger.LogWarning(ex, "Failed to load types from assembly {AssemblyName}", assembly.FullName);
                }
            }

            _logger.LogInformation("Discovered {Count} tool categories across loaded assemblies", discoveredCategories.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning assemblies for tool categories");
        }

        return discoveredCategories;
    }

    /// <summary>
    /// Scans a tool category type for methods decorated with [ToolAction] attribute.
    /// </summary>
    /// <param name="categoryType">The type to scan for tool actions.</param>
    /// <returns>A collection of discovered action methods with their attributes.</returns>
    private IEnumerable<(MethodInfo Method, ToolActionAttribute Attribute)> ScanForToolActions(Type categoryType)
    {
        try
        {
            var methodsWithAttribute = categoryType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<ToolActionAttribute>() != null)
                .Select(m => (Method: m, Attribute: m.GetCustomAttribute<ToolActionAttribute>()!));

            return methodsWithAttribute.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan methods in type {TypeName}", categoryType.FullName);
            return Enumerable.Empty<(MethodInfo, ToolActionAttribute)>();
        }
    }

    /// <summary>
    /// Upserts a tool category - updates if exists, inserts if new.
    /// </summary>
    /// <param name="categoryType">The type containing the category metadata.</param>
    /// <param name="categoryAttribute">The category attribute with metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The category ID (existing or newly created).</returns>
    private async Task<Guid> UpsertToolCategoryAsync(
        Type categoryType,
        ToolCategoryAttribute categoryAttribute,
        CancellationToken cancellationToken)
    {
        var categoryName = categoryAttribute.Name;
        var serviceClassName = categoryType.FullName ?? categoryType.Name;

        // Check if category exists by name globally
        var existingCategory = await _categoryDataAccess.GetByNameAsync(categoryName, cancellationToken);

        if (existingCategory != null)
        {
            _logger.LogDebug(
                "Updating existing category: {CategoryName} (ID: {CategoryId})",
                categoryName,
                existingCategory.Id);

            // Update the existing category
            existingCategory.Update(
                categoryAttribute.Name,
                categoryAttribute.Description);

            await _categoryDataAccess.UpdateAsync(existingCategory, cancellationToken);

            return existingCategory.Id;
        }
        else
        {
            _logger.LogDebug("Creating new category: {CategoryName}", categoryName);

            // Create new category
            var newCategory = ToolCategory.Create(
                categoryAttribute.Name,
                categoryAttribute.Description,
                categoryAttribute.ProviderType,
                serviceClassName);

            await _categoryDataAccess.AddAsync(newCategory, cancellationToken);

            return newCategory.Id;
        }
    }

    /// <summary>
    /// Finds an existing tool action by category ID and method name.
    /// </summary>
    /// <param name="categoryId">The category ID.</param>
    /// <param name="methodName">The method name to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing action if found; otherwise, null.</returns>
    private async Task<ToolAction?> FindExistingActionAsync(
        Guid categoryId,
        string methodName,
        CancellationToken cancellationToken)
    {
        var actions = await _actionDataAccess.GetByCategoryIdAsync(categoryId, cancellationToken);
        return actions.FirstOrDefault(a => a.MethodName == methodName);
    }

    /// <summary>
    /// Upserts a tool action - updates if exists, inserts if new.
    /// </summary>
    /// <param name="categoryId">The parent category ID.</param>
    /// <param name="method">The method info containing the action metadata.</param>
    /// <param name="actionAttribute">The action attribute with metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The action ID (existing or newly created).</returns>
    private async Task<Guid> UpsertToolActionAsync(
        Guid categoryId,
        MethodInfo method,
        ToolActionAttribute actionAttribute,
        CancellationToken cancellationToken)
    {
        var methodName = method.Name;

        // Check if action exists by category ID + method name
        var existingAction = await FindExistingActionAsync(categoryId, methodName, cancellationToken);

        if (existingAction != null)
        {
            _logger.LogDebug(
                "  Updating existing action: {ActionName} (Method: {MethodName}, ID: {ActionId})",
                actionAttribute.Name,
                methodName,
                existingAction.Id);

            // Update the existing action with latest metadata from attributes
            existingAction.Update(
                actionAttribute.Name,
                actionAttribute.Description,
                actionAttribute.DangerLevel);

            await _actionDataAccess.UpdateAsync(existingAction, cancellationToken);

            return existingAction.Id;
        }
        else
        {
            _logger.LogDebug(
                "  Creating new action: {ActionName} (Method: {MethodName})",
                actionAttribute.Name,
                methodName);

            // Create new action
            var newAction = ToolAction.Create(
                categoryId,
                actionAttribute.Name,
                actionAttribute.Description,
                methodName,
                actionAttribute.DangerLevel);

            await _actionDataAccess.AddAsync(newAction, cancellationToken);

            return newAction.Id;
        }
    }

    /// <inheritdoc />
    public async Task<ToolScanResult> ScanAndSeedToolsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting global tool scanning...");

        var result = new ToolScanResult();

        try
        {
            // Step 1: Scan for tool categories
            var discoveredCategories = ScanForToolCategories();

            foreach (var (categoryType, categoryAttribute) in discoveredCategories)
            {
                try
                {
                    _logger.LogInformation(
                        "Processing category: {CategoryName} ({ProviderType})",
                        categoryAttribute.Name,
                        categoryAttribute.ProviderType);

                    // Step 2: Scan for tool actions within this category
                    var discoveredActions = ScanForToolActions(categoryType);

                    _logger.LogInformation(
                        "  Found {Count} actions for category {CategoryName}",
                        discoveredActions.Count(),
                        categoryAttribute.Name);

                    // Step 3: Upsert tool category
                    var categoryId = await UpsertToolCategoryAsync(
                        categoryType,
                        categoryAttribute,
                        cancellationToken);

                    _logger.LogDebug(
                        "Category {CategoryName} processed with ID: {CategoryId}",
                        categoryAttribute.Name,
                        categoryId);

                    // Step 4: Upsert tool actions
                    foreach (var (method, actionAttribute) in discoveredActions)
                    {
                        try
                        {
                            var actionId = await UpsertToolActionAsync(
                                categoryId,
                                method,
                                actionAttribute,
                                cancellationToken);

                            _logger.LogDebug(
                                "  Action {ActionName} processed with ID: {ActionId}",
                                actionAttribute.Name,
                                actionId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "  Failed to process action {ActionName} in category {CategoryName}",
                                actionAttribute.Name,
                                categoryAttribute.Name);
                            
                            result.Errors.Add($"Failed to process action {actionAttribute.Name}: {ex.Message}");
                        }
                    }

                    result.CategoriesProcessed++;
                    result.ActionsProcessed += discoveredActions.Count();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing category {CategoryName}", categoryAttribute.Name);
                    result.Errors.Add($"Failed to process category {categoryAttribute.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error during tool scanning");
            result.Errors.Add($"Fatal scanning error: {ex.Message}");
        }

        _logger.LogInformation(
            "Tool scanning completed. Categories: {Categories}, Actions: {Actions}, Errors: {Errors}",
            result.CategoriesProcessed,
            result.ActionsProcessed,
            result.Errors.Count);

        return result;
    }
}