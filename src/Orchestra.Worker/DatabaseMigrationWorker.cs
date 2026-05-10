using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Worker;

public class DatabaseMigrationWorker : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMigrationWorker> _logger;

    public DatabaseMigrationWorker(
        IServiceProvider serviceProvider,
        ILogger<DatabaseMigrationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database migration worker started");

        await using var scope = _serviceProvider.CreateAsyncScope();

        try
        {
            await ApplyPendingMigrationsAsync(scope.ServiceProvider, cancellationToken);
            await SeedToolsAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while applying database migrations or seeding tools");
            throw;
        }
    }

    private async Task ApplyPendingMigrationsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var migrator = serviceProvider.GetRequiredService<IDatabaseMigrator>();

        _logger.LogInformation("Checking for pending database migrations...");

        var pendingMigrations = await migrator.GetPendingMigrationsAsync(cancellationToken);
        if (!pendingMigrations.Any())
        {
            _logger.LogInformation("Database is up to date, no pending migrations");
            return;
        }

        _logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count());
        await migrator.MigrateAsync(cancellationToken);
        _logger.LogInformation("Database migrations applied successfully");
    }

    private async Task SeedToolsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting tool seeding...");
        var toolScanningService = serviceProvider.GetRequiredService<IToolScanningService>();
        var result = await toolScanningService.ScanAndSeedToolsAsync(cancellationToken);

        if (result.Errors.Any())
            LogSeedingWarnings(result);
        else
            LogSeedingSuccess(result);
    }

    private void LogSeedingWarnings(ToolScanResult result)
    {
        _logger.LogWarning(
            "Tool seeding completed with {ErrorCount} error(s). Categories: {Categories}, Actions: {Actions}",
            result.Errors.Count,
            result.CategoriesProcessed,
            result.ActionsProcessed);

        foreach (var error in result.Errors)
            _logger.LogWarning("Tool seeding error: {Error}", error);
    }

    private void LogSeedingSuccess(ToolScanResult result)
    {
        _logger.LogInformation(
            "Tool seeding completed successfully. Categories: {Categories}, Actions: {Actions}",
            result.CategoriesProcessed,
            result.ActionsProcessed);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database migration worker stopped");
        return Task.CompletedTask;
    }
}
