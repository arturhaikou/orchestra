using Microsoft.EntityFrameworkCore;
using Orchestra.Application.Common.Interfaces;

namespace Orchestra.Infrastructure.Persistence;

public class EfCoreDatabaseMigrator : IDatabaseMigrator
{
    private readonly AppDbContext _dbContext;

    public EfCoreDatabaseMigrator(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken = default)
        => await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

    public Task MigrateAsync(CancellationToken cancellationToken = default)
        => _dbContext.Database.MigrateAsync(cancellationToken);
}
