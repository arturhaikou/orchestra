using Microsoft.EntityFrameworkCore;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<Workspace> Workspaces { get; set; } = null!;

        public DbSet<UserWorkspace> UserWorkspaces { get; set; } = null!;

        public DbSet<Agent> Agents { get; set; } = null!;

        public DbSet<AgentToolAction> AgentToolActions { get; set; } = null!;

        public DbSet<Integration> Integrations => Set<Integration>();

        public DbSet<Ticket> Tickets { get; set; } = null!;

        public DbSet<TicketStatus> TicketStatuses { get; set; } = null!;

        public DbSet<TicketPriority> TicketPriorities { get; set; } = null!;

        public DbSet<TicketComment> TicketComments { get; set; } = null!;

        public DbSet<ToolCategory> ToolCategories { get; set; } = null!;

        public DbSet<ToolAction> ToolActions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new Configurations.UserConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.WorkspaceConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.UserWorkspaceConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentToolActionConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.IntegrationConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TicketConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TicketStatusConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TicketPriorityConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TicketCommentConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.ToolCategoryConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.ToolActionConfiguration());
        }
    }
}