using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveModelIdFromAiCliIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "AiCliIntegrations");

            migrationBuilder.AddColumn<string>(
                name: "ReasoningEffort",
                table: "Agents",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReasoningEffort",
                table: "Agents");

            migrationBuilder.AddColumn<string>(
                name: "ModelId",
                table: "AiCliIntegrations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
