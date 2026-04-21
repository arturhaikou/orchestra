using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropWorkspaceModelsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceModels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ModelName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PullProgress = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceModels", x => x.Id);
                    table.CheckConstraint("CK_WorkspaceModels_PullProgress", "\"PullProgress\" IS NULL OR (\"PullProgress\" >= 0 AND \"PullProgress\" <= 100)");
                    table.ForeignKey(
                        name: "FK_WorkspaceModels_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceModels_Status",
                table: "WorkspaceModels",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceModels_WorkspaceId_ModelName",
                table: "WorkspaceModels",
                columns: new[] { "WorkspaceId", "ModelName" });
        }
    }
}
