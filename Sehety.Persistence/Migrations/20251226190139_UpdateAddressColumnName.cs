using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S2S.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAddressColumnName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Address_Street",
                table: "Users",
                newName: "Street");

            migrationBuilder.RenameColumn(
                name: "Address_Country",
                table: "Users",
                newName: "Country");

            migrationBuilder.RenameColumn(
                name: "Address_City",
                table: "Users",
                newName: "City");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Street",
                table: "Users",
                newName: "Address_Street");

            migrationBuilder.RenameColumn(
                name: "Country",
                table: "Users",
                newName: "Address_Country");

            migrationBuilder.RenameColumn(
                name: "City",
                table: "Users",
                newName: "Address_City");
        }
    }
}
