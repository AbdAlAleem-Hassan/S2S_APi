using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace S2S.Persistence.IdentityData.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUsageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                // Drop IsUnlimited if it exists (legacy column), with its default constraint
                migrationBuilder.Sql("""
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = N'IsUnlimited')
                    BEGIN
                        DECLARE @df_unlim nvarchar(200)
                        SELECT @df_unlim = name FROM sys.default_constraints
                            WHERE parent_object_id = OBJECT_ID(N'[Users]')
                            AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[Users]'), N'IsUnlimited', 'ColumnId')
                        IF @df_unlim IS NOT NULL
                            EXEC('ALTER TABLE [Users] DROP CONSTRAINT [' + @df_unlim + ']')
                        ALTER TABLE [Users] DROP COLUMN [IsUnlimited]
                    END
                    """);

                // Convert SubscriptionTier from nvarchar to int if it exists as string,
                // otherwise add it as int.
                migrationBuilder.Sql("""
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = N'SubscriptionTier')
                    BEGIN
                        DECLARE @colType nvarchar(20)
                        SELECT @colType = t.name
                        FROM sys.columns c
                        JOIN sys.types t ON c.system_type_id = t.system_type_id
                        WHERE c.object_id = OBJECT_ID(N'[Users]') AND c.name = N'SubscriptionTier'

                        IF @colType IN ('nvarchar', 'varchar')
                        BEGIN
                            EXEC('ALTER TABLE [Users] ADD [SubscriptionTier_int] int NULL')
                            EXEC('UPDATE [Users] SET [SubscriptionTier_int] = CASE [SubscriptionTier]
                                WHEN ''Premium'' THEN 1
                                WHEN ''Enterprise'' THEN 2
                                ELSE 0
                            END')
                            DECLARE @df_subs nvarchar(200)
                            SELECT @df_subs = name FROM sys.default_constraints
                                WHERE parent_object_id = OBJECT_ID(N'[Users]')
                                AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[Users]'), N'SubscriptionTier', 'ColumnId')
                            IF @df_subs IS NOT NULL
                                EXEC('ALTER TABLE [Users] DROP CONSTRAINT [' + @df_subs + ']')
                            EXEC('ALTER TABLE [Users] DROP COLUMN [SubscriptionTier]')
                            EXEC sp_rename N'[Users].[SubscriptionTier_int]', N'SubscriptionTier', 'COLUMN'
                        END
                        ELSE
                        BEGIN
                            EXEC('ALTER TABLE [Users] ALTER COLUMN [SubscriptionTier] int NOT NULL')
                        END
                        EXEC('ALTER TABLE [Users] ADD CONSTRAINT DF_Users_SubscriptionTier DEFAULT 0 FOR [SubscriptionTier]')
                    END
                    ELSE
                    BEGIN
                        EXEC('ALTER TABLE [Users] ADD [SubscriptionTier] int NOT NULL DEFAULT 0')
                    END
                    """);
            }

            migrationBuilder.CreateTable(
                name: "UserUsages",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WindowStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    QuotaType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserUsages", x => new { x.UserId, x.WindowStart });
                });

            // Final safety net: ensure SubscriptionTier is NOT NULL
            if (migrationBuilder.ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
            {
                migrationBuilder.Sql("""
                    IF EXISTS (SELECT 1 FROM sys.columns
                        WHERE object_id = OBJECT_ID(N'[Users]') AND name = N'SubscriptionTier' AND is_nullable = 1)
                        ALTER TABLE [Users] ALTER COLUMN [SubscriptionTier] int NOT NULL
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserUsages");
        }
    }
}
