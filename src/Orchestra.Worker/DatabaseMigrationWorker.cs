using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Infrastructure.Persistence;

namespace Orchestra.Worker;

/// <summary>
/// Background worker responsible for applying database migrations and seeding tools on startup.
/// Implements fail-fast behavior to prevent application startup with incompatible database schema.
/// </summary>
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

        using var scope = _serviceProvider.CreateScope();
        
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            _logger.LogInformation("Checking for pending database migrations...");
            
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count());
                await dbContext.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Database migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("Database is up to date, no pending migrations");
            }

            // Seed tool categories and actions after migrations complete
            _logger.LogInformation("Starting tool seeding...");
            var toolScanningService = scope.ServiceProvider.GetRequiredService<IToolScanningService>();
            var result = await toolScanningService.ScanAndSeedToolsAsync(cancellationToken);
            
            if (result.Errors.Any())
            {
                _logger.LogWarning(
                    "Tool seeding completed with {ErrorCount} error(s). Categories: {Categories}, Actions: {Actions}",
                    result.Errors.Count,
                    result.CategoriesProcessed,
                    result.ActionsProcessed);
                
                foreach (var error in result.Errors)
                {
                    _logger.LogWarning("  Tool seeding error: {Error}", error);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Tool seeding completed successfully. Categories: {Categories}, Actions: {Actions}",
                    result.CategoriesProcessed,
                    result.ActionsProcessed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while applying database migrations or seeding tools");
            throw; // Fail fast - prevent Worker from starting with incompatible database
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Database migration worker stopped");
        return Task.CompletedTask;
    }
}
