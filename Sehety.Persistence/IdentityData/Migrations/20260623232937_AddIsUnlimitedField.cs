using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S2S.Persistence.IdentityData.Migrations
{
    /// <inheritdoc />
    public partial class AddIsUnlimitedField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUnlimited",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsUnlimited",
                table: "Users");
        }
    }
}
