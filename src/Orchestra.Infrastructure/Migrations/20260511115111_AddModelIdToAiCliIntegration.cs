using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModelIdToAiCliIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModelId",
                table: "AiCliIntegrations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "AiCliIntegrations");
        }
    }
}
