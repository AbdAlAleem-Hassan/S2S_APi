using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S2S.Persistence.IdentityData.Migrations
{
    /// <inheritdoc />
    public partial class AddFCMTokenColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FcmToken",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FcmToken",
                table: "Users");
        }
    }
}
