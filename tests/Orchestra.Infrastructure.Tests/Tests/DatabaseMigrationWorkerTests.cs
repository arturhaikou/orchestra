using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute.ExceptionExtensions;
using Orchestra.Application.Common.Interfaces;
using Orchestra.Worker;

namespace Orchestra.Infrastructure.Tests.Tests;

/// <summary>
/// Unit tests for <see cref="DatabaseMigrationWorker"/>.
///
/// STRUCTURAL NOTE FOR IMPLEMENTATION AGENT:
/// These tests mock <see cref="IDatabaseMigrator"/> via the service provider rather than
/// using <c>AppDbContext.Database</c> directly. <c>DatabaseFacade.GetPendingMigrationsAsync</c>
/// and <c>DatabaseFacade.MigrateAsync</c> are EF Core relational-only extension methods that
/// cannot be mocked with NSubstitute and throw on the InMemory provider.
///
/// ACTION REQUIRED: Refactor <c>DatabaseMigrationWorker</c> to resolve <see cref="IDatabaseMigrator"/>
/// from the scoped <c>IServiceProvider</c> instead of calling <c>AppDbContext.Database.*</c>
/// directly. Register a concrete <c>EfCoreDatabaseMigrator</c> (wrapping <c>AppDbContext.Database</c>)
/// in the DI container. This will make all tests below pass (green).
/// </summary>
public class DatabaseMigrationWorkerTests
{
    private readonly IServiceProvider _rootServiceProvider = Substitute.For<IServiceProvider>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IServiceScope _scope = Substitute.For<IServiceScope>();
    private readonly IServiceProvider _scopedServiceProvider = Substitute.For<IServiceProvider>();
    private readonly IDatabaseMigrator _databaseMigrator = Substitute.For<IDatabaseMigrator>();
    private readonly IToolScanningService _toolScanningService = Substitute.For<IToolScanningService>();
    private readonly ILogger<DatabaseMigrationWorker> _logger = Substitute.For<ILogger<DatabaseMigrationWorker>>();
    private readonly DatabaseMigrationWorker _sut;

    public DatabaseMigrationWorkerTests()
    {
        _rootServiceProvider.GetService(typeof(IServiceScopeFactory)).Returns(_scopeFactory);
        _scopeFactory.CreateScope().Returns(_scope);
        _scope.ServiceProvider.Returns(_scopedServiceProvider);

        _scopedServiceProvider.GetService(typeof(IDatabaseMigrator)).Returns(_databaseMigrator);
        _scopedServiceProvider.GetService(typeof(IToolScanningService)).Returns(_toolScanningService);

        _sut = new DatabaseMigrationWorker(_rootServiceProvider, _logger);
    }

    // -------------------------------------------------------------------------
    // Scenario 1: Fresh DB — pending migrations present → MigrateAsync is called
    // Covers FR-001 BDD Scenario 1: "exactly one migration is applied"
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_WithPendingMigrations_CallsMigrateAsyncOnce()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(["20260509000000_ConsolidatedBaseline"]);

        _toolScanningService
            .ScanAndSeedToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new ToolScanResult());

        await _sut.StartAsync(CancellationToken.None);

        await _databaseMigrator.Received(1).MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WithPendingMigrations_CallsScanAndSeedAfterMigration()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(["20260509000000_ConsolidatedBaseline"]);

        _toolScanningService
            .ScanAndSeedToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new ToolScanResult());

        await _sut.StartAsync(CancellationToken.None);

        await _toolScanningService.Received(1).ScanAndSeedToolsAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 2: Database already up to date — MigrateAsync is skipped,
    // seeding still runs (idempotent re-run)
    // Covers FR-001 BDD Scenario 2
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_WithNoPendingMigrations_DoesNotCallMigrateAsync()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<string>());

        _toolScanningService
            .ScanAndSeedToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new ToolScanResult());

        await _sut.StartAsync(CancellationToken.None);

        await _databaseMigrator.DidNotReceive().MigrateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WithNoPendingMigrations_StillRunsToolSeeding()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<string>());

        _toolScanningService
            .ScanAndSeedToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new ToolScanResult());

        await _sut.StartAsync(CancellationToken.None);

        await _toolScanningService.Received(1).ScanAndSeedToolsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WithNoPendingMigrations_CompletesWithoutException()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<string>());

        _toolScanningService
            .ScanAndSeedToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new ToolScanResult());

        var exception = await Record.ExceptionAsync(() => _sut.StartAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    // -------------------------------------------------------------------------
    // Scenario 3: MigrateAsync throws — exception propagates (fail-fast)
    // Covers FR-001 BDD Scenario 3: existing data preserved via fail-fast
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_WhenMigrateAsyncThrows_PropagatesException()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(["20260509000000_ConsolidatedBaseline"]);

        _databaseMigrator
            .MigrateAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Migration failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAsync_WhenMigrateAsyncThrows_NeverCallsToolSeeding()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(["20260509000000_ConsolidatedBaseline"]);

        _databaseMigrator
            .MigrateAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Migration failed"));

        await Record.ExceptionAsync(() => _sut.StartAsync(CancellationToken.None));

        await _toolScanningService.DidNotReceive().ScanAndSeedToolsAsync(Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Scenario 4: Tool seeding returns non-fatal errors → no exception thrown
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_WhenSeedingHasErrors_CompletesWithoutException()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<string>());

        _toolScanningService
            .ScanAndSeedToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new ToolScanResult
            {
                CategoriesProcessed = 3,
                ActionsProcessed = 10,
                Errors = ["Category X failed to seed"]
            });

        var exception = await Record.ExceptionAsync(() => _sut.StartAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task StartAsync_WhenSeedingHasErrors_TaskIsCompletedSuccessfully()
    {
        _databaseMigrator
            .GetPendingMigrationsAsync(Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<string>());

        _toolScanningService
            .ScanAndSeedToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new ToolScanResult
            {
                Errors = ["Category X failed to seed"]
            });

        var task = _sut.StartAsync(CancellationToken.None);
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }

    // -------------------------------------------------------------------------
    // Scenario 5: StopAsync is a synchronous no-op
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StopAsync_Always_CompletesWithoutException()
    {
        var exception = await Record.ExceptionAsync(() => _sut.StopAsync(CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task StopAsync_Always_ReturnsCompletedTask()
    {
        var task = _sut.StopAsync(CancellationToken.None);
        await task;

        Assert.True(task.IsCompletedSuccessfully);
    }
}
