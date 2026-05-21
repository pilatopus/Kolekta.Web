using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kolekta.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCardPopularity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Popularity",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Popularity",
                table: "Cards");
        }
    }
}
