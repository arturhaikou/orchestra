namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Defines the contract for scanning assemblies to discover tool categories and actions
/// decorated with custom attributes, and seeding them into the database.
/// </summary>
public interface IToolScanningService
{
    /// <summary>
    /// Scans all loaded assemblies for types with [ToolCategory] and methods with [ToolAction] attributes,
    /// then upserts the discovered tools into the database globally.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A summary of scanned categories and actions.</returns>
    /// <remarks>
    /// This operation is idempotent and can safely be run multiple times.
    /// Existing tools will be updated with current attribute metadata.
    /// Tools are global and filtered by workspace integrations during agent assignment.
    /// </remarks>
    Task<ToolScanResult> ScanAndSeedToolsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a tool scanning operation.
/// </summary>
public class ToolScanResult
{
    /// <summary>
    /// Gets the number of tool categories discovered and processed.
    /// </summary>
    public int CategoriesProcessed { get; set; }

    /// <summary>
    /// Gets the number of tool actions discovered and processed.
    /// </summary>
    public int ActionsProcessed { get; set; }

    /// <summary>
    /// Gets the list of errors encountered during scanning (non-fatal).
    /// </summary>
    public List<string> Errors { get; init; } = new();
}