using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConditionStepSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "WorkflowSteps",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "WorkflowSteps",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FalseNextStepId",
                table: "WorkflowSteps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StepType",
                table: "WorkflowSteps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "TrueNextStepId",
                table: "WorkflowSteps",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "FalseNextStepId",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "StepType",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "TrueNextStepId",
                table: "WorkflowSteps");

            migrationBuilder.AlterColumn<Guid>(
                name: "AgentId",
                table: "WorkflowSteps",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
