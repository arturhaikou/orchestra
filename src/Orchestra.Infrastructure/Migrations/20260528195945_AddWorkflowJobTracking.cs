using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowJobTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowJobId",
                table: "WorkflowExecutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentJobId",
                table: "Jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowExecutionId",
                table: "Jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ParentJobId",
                table: "Jobs",
                column: "ParentJobId");

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_Jobs_ParentJobId",
                table: "Jobs",
                column: "ParentJobId",
                principalTable: "Jobs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_Jobs_ParentJobId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ParentJobId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "WorkflowJobId",
                table: "WorkflowExecutions");

            migrationBuilder.DropColumn(
                name: "ParentJobId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "WorkflowExecutionId",
                table: "Jobs");
        }
    }
}
