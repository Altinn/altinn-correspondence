using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InstallPgStatStatements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $do$
                BEGIN
                    -- Try to create the extension
                    CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
                EXCEPTION
                    WHEN OTHERS THEN
                        -- Log the error but don't fail the migration
                        RAISE NOTICE 'pg_stat_statements could not be activated: %', SQLERRM;
                END
                $do$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $do$
                BEGIN
                    -- Try to remove the extension
                    DROP EXTENSION IF EXISTS pg_stat_statements;
                EXCEPTION
                    WHEN OTHERS THEN
                        -- Log the error but don't fail the migration
                        RAISE NOTICE 'pg_stat_statements cleanup failed: %', SQLERRM;
                END
                $do$;
            ");
        }
    }
}
