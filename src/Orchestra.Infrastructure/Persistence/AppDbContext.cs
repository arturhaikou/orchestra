using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Orchestra.Domain.Entities;

namespace Orchestra.Infrastructure.Persistence
{
    public class AppDbContext : DbContext, IDataProtectionKeyContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; } = null!;

        public DbSet<Workspace> Workspaces { get; set; } = null!;

        public DbSet<UserWorkspace> UserWorkspaces { get; set; } = null!;

        public DbSet<Agent> Agents { get; set; } = null!;

        public DbSet<AgentToolAction> AgentToolActions { get; set; } = null!;

        public DbSet<AgentSubAgent> AgentSubAgents { get; set; } = null!;

        public DbSet<Integration> Integrations => Set<Integration>();

        public DbSet<McpServer> McpServers { get; set; } = null!;

        public DbSet<AgentMcpTool> AgentMcpTools { get; set; } = null!;

        public DbSet<Ticket> Tickets { get; set; } = null!;

        public DbSet<TicketStatus> TicketStatuses { get; set; } = null!;

        public DbSet<TicketPriority> TicketPriorities { get; set; } = null!;

        public DbSet<TicketComment> TicketComments { get; set; } = null!;

        public DbSet<ToolCategory> ToolCategories { get; set; } = null!;

        public DbSet<ToolAction> ToolActions { get; set; } = null!;

        public DbSet<AIProviderConfiguration> AIProviderConfigurations { get; set; } = null!;

        public DbSet<AiCliIntegration> AiCliIntegrations { get; set; } = null!;

        public DbSet<Skill> Skills { get; set; } = null!;

        public DbSet<AgentSkill> AgentSkills { get; set; } = null!;

        public DbSet<AgentCliSkill> AgentCliSkills { get; set; } = null!;

        public DbSet<SkillFolder> SkillFolders { get; set; } = null!;

        public DbSet<AgentSkillFolder> AgentSkillFolders { get; set; } = null!;

        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        public DbSet<Job> Jobs => Set<Job>();

        public DbSet<JobStep> JobSteps => Set<JobStep>();

        public DbSet<AgentQuestion> AgentQuestions => Set<AgentQuestion>();

        public DbSet<AgentConversationSnapshot> AgentConversationSnapshots => Set<AgentConversationSnapshot>();

        public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();

        public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

        public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();

        public DbSet<WorkflowStepExecution> WorkflowStepExecutions => Set<WorkflowStepExecution>();

        public DbSet<WorkflowStepSystemTool> WorkflowStepSystemTools => Set<WorkflowStepSystemTool>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            if (Database.ProviderName?.Contains("InMemory") != true)
                modelBuilder.HasPostgresExtension("citext");

            modelBuilder.ApplyConfiguration(new Configurations.UserConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.WorkspaceConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.UserWorkspaceConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentToolActionConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentSubAgentConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.IntegrationConfiguration(Database.ProviderName?.Contains("InMemory") == true));
            modelBuilder.ApplyConfiguration(new Configurations.McpServerConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentMcpToolConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TicketConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TicketStatusConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TicketPriorityConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.TicketCommentConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.ToolCategoryConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.ToolActionConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AIProviderConfigurationConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AiCliIntegrationConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.SkillConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentSkillConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentCliSkillConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.SkillFolderConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentSkillFolderConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.JobConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.JobStepConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentQuestionConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.AgentConversationSnapshotConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.WorkflowDefinitionConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.WorkflowStepConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.WorkflowExecutionConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.WorkflowStepExecutionConfiguration());
            modelBuilder.ApplyConfiguration(new Configurations.WorkflowStepSystemToolConfiguration());
        }
    }
}