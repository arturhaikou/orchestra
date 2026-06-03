using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowSystemTools : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveTicketId",
                table: "WorkflowExecutions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkflowStepSystemTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToolIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStepSystemTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStepSystemTools_WorkflowSteps_WorkflowStepId",
                        column: x => x.WorkflowStepId,
                        principalTable: "WorkflowSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_ActiveTicketId",
                table: "WorkflowExecutions",
                column: "ActiveTicketId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStepSystemTools_WorkflowStepId",
                table: "WorkflowStepSystemTools",
                column: "WorkflowStepId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowExecutions_Tickets_ActiveTicketId",
                table: "WorkflowExecutions",
                column: "ActiveTicketId",
                principalTable: "Tickets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowExecutions_Tickets_ActiveTicketId",
                table: "WorkflowExecutions");

            migrationBuilder.DropTable(
                name: "WorkflowStepSystemTools");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowExecutions_ActiveTicketId",
                table: "WorkflowExecutions");

            migrationBuilder.DropColumn(
                name: "ActiveTicketId",
                table: "WorkflowExecutions");
        }
    }
}
