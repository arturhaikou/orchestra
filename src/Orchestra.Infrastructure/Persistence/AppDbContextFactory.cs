using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orchestra.Infrastructure.Persistence
{
    /// <summary>
    /// Design-time factory for creating AppDbContext instances.
    /// Required for EF Core CLI commands (migrations, etc.) when using Aspire.
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            // Use PostgreSQL with a default local connection string for design-time operations.
            // The actual connection string will be overridden at runtime via Aspire configuration.
            optionsBuilder.UseNpgsql(
                "Host=localhost;Port=5432;Database=orchestra;Username=postgres;Password=password");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
