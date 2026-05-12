using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCliIntegrationIdToAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AiCliIntegrationId",
                table: "Agents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agents_AiCliIntegrationId",
                table: "Agents",
                column: "AiCliIntegrationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_AiCliIntegrations_AiCliIntegrationId",
                table: "Agents",
                column: "AiCliIntegrationId",
                principalTable: "AiCliIntegrations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agents_AiCliIntegrations_AiCliIntegrationId",
                table: "Agents");

            migrationBuilder.DropIndex(
                name: "IX_Agents_AiCliIntegrationId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "AiCliIntegrationId",
                table: "Agents");
        }
    }
}
