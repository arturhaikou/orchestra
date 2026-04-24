using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TemplateIdentifier",
                table: "Agents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateVersion",
                table: "Agents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agents_WorkspaceId_TemplateIdentifier",
                table: "Agents",
                columns: new[] { "WorkspaceId", "TemplateIdentifier" },
                unique: true,
                filter: "\"TemplateIdentifier\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Agents_WorkspaceId_TemplateIdentifier",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "TemplateIdentifier",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "Agents");
        }
    }
}
