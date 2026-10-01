using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenMenu.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MenuItemTranslationCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "MenuItemTranslations",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "MenuItemTranslations");
        }
    }
}
