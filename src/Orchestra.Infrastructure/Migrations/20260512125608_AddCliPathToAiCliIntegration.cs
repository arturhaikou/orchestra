using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCliPathToAiCliIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CliPath",
                table: "AiCliIntegrations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CliPath",
                table: "AiCliIntegrations");
        }
    }
}
