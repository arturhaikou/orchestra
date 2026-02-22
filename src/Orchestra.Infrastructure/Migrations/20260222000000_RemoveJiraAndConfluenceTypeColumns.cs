using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveJiraAndConfluenceTypeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JiraType",
                table: "Integrations");

            migrationBuilder.DropColumn(
                name: "ConfluenceType",
                table: "Integrations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JiraType",
                table: "Integrations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfluenceType",
                table: "Integrations",
                type: "integer",
                nullable: true);
        }
    }
}
