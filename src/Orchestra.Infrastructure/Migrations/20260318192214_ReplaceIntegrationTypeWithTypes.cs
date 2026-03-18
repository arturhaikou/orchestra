using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestra.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIntegrationTypeWithTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop the index on the old Type column
            migrationBuilder.DropIndex(
                name: "IX_Integrations_Type",
                table: "Integrations");

            // Step 2: Add the new Types column as nullable first so existing rows don't violate NOT NULL
            migrationBuilder.AddColumn<string[]>(
                name: "Types",
                table: "Integrations",
                type: "text[]",
                nullable: true);

            // Step 3: Copy every existing row's Type value into Types as a single-element array
            migrationBuilder.Sql(@"
        UPDATE ""Integrations""
        SET ""Types"" = ARRAY[""Type""::text]
        WHERE ""Type"" IS NOT NULL;

        UPDATE ""Integrations""
        SET ""Types"" = '{}'
        WHERE ""Type"" IS NULL;
    ");

            // Step 4: Make Types NOT NULL now that all rows have a value
            migrationBuilder.AlterColumn<string[]>(
                name: "Types",
                table: "Integrations",
                type: "text[]",
                nullable: false,
                defaultValue: new string[] { },
                oldClrType: typeof(string[]),
                oldType: "text[]",
                oldNullable: true);

            // Step 5: Drop the old scalar Type column
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Integrations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Step 1: Re-add the Type column as nullable
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Integrations",
                type: "text",
                nullable: true);

            // Step 2: Restore Type from the first element of Types
            migrationBuilder.Sql(@"
        UPDATE ""Integrations""
        SET ""Type"" = ""Types""[1]
        WHERE array_length(""Types"", 1) > 0;
    ");

            // Step 3: Make Type NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "Integrations",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            // Step 4: Drop Types
            migrationBuilder.DropColumn(
                name: "Types",
                table: "Integrations");

            // Step 5: Recreate the index on Type
            migrationBuilder.CreateIndex(
                name: "IX_Integrations_Type",
                table: "Integrations",
                column: "Type");
        }
    }
}
