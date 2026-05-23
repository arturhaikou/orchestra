using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubAgentTrackingToJobSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgentId",
                table: "JobSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentName",
                table: "JobSteps",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentStepId",
                table: "JobSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobSteps_ParentStepId",
                table: "JobSteps",
                column: "ParentStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobSteps_ParentStepId",
                table: "JobSteps");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "JobSteps");

            migrationBuilder.DropColumn(
                name: "AgentName",
                table: "JobSteps");

            migrationBuilder.DropColumn(
                name: "ParentStepId",
                table: "JobSteps");
        }
    }
}
