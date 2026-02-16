using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateJiraTypeToCloud : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set existing Jira integrations to Cloud type by default
            // Provider is stored as string in database, JIRA is the enum name
            // JiraType = 0 is Cloud
            migrationBuilder.Sql(
                "UPDATE \"Integrations\" SET \"JiraType\" = 0 WHERE \"Provider\" = 'JIRA';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
