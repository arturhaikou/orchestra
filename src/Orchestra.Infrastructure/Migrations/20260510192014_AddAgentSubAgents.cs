using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentSubAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentSubAgents",
                columns: table => new
                {
                    ParentAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubAgentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentSubAgents", x => new { x.ParentAgentId, x.SubAgentId });
                    table.ForeignKey(
                        name: "FK_AgentSubAgents_Agents_ParentAgentId",
                        column: x => x.ParentAgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentSubAgents_Agents_SubAgentId",
                        column: x => x.SubAgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentSubAgents_SubAgentId",
                table: "AgentSubAgents",
                column: "SubAgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentSubAgents");
        }
    }
}
