using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S2S.Persistence.IdentityData.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTierHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SubscriptionTier",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            // Migrate old SubscriptionTier values: 0→1 (Free), 1→2 (Premium)
            migrationBuilder.Sql("""
                UPDATE [Users] SET [SubscriptionTier] = CASE [SubscriptionTier]
                    WHEN 0 THEN 1
                    WHEN 1 THEN 2
                    ELSE 1
                END
                WHERE [SubscriptionTier] IN (0, 1)
                """);

            migrationBuilder.CreateTable(
                name: "UserTierHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OldTier = table.Column<int>(type: "int", nullable: false),
                    NewTier = table.Column<int>(type: "int", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTierHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTierHistories_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserTierHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTierHistories_ChangedByUserId",
                table: "UserTierHistories",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTierHistories_UserId",
                table: "UserTierHistories",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTierHistories");

            migrationBuilder.AlterColumn<int>(
                name: "SubscriptionTier",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
