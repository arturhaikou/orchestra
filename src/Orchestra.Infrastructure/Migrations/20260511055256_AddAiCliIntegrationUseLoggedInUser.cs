using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAiCliIntegrationUseLoggedInUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EncryptedCredential",
                table: "AiCliIntegrations",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "UseLoggedInUser",
                table: "AiCliIntegrations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UseLoggedInUser",
                table: "AiCliIntegrations");

            migrationBuilder.AlterColumn<string>(
                name: "EncryptedCredential",
                table: "AiCliIntegrations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
