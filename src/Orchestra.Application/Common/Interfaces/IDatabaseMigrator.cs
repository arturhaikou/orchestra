namespace Orchestra.Application.Common.Interfaces;

/// <summary>
/// Abstracts EF Core database migration operations to enable unit testing
/// of the DatabaseMigrationWorker without a real relational database provider.
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>
    /// Returns the names of all migrations that are defined in the assembly
    /// but have not yet been applied to the target database.
    /// </summary>
    Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies all pending migrations to the target database.
    /// </summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
