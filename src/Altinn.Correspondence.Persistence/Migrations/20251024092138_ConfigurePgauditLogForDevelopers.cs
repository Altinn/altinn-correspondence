using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurePgauditLogForDevelopers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $do$
                BEGIN
                    -- First, try to create the pgaudit extension
                    BEGIN
                        CREATE EXTENSION IF NOT EXISTS pgaudit;
                        RAISE NOTICE 'PGAUDIT extension created successfully';
                    EXCEPTION
                        WHEN OTHERS THEN
                            RAISE NOTICE 'PGAUDIT extension could not be created: %', SQLERRM;
                    END;
                    
                    -- Then, try to set pgaudit.log = 'all' for Altinn-30-Correspondence-Test-Developers (test environments)
                    BEGIN
                        ALTER ROLE ""Altinn-30-Correspondence-Test-Developers"" SET pgaudit.log = 'all';
                        RAISE NOTICE 'Set pgaudit.log for Altinn-30-Correspondence-Test-Developers';
                    EXCEPTION WHEN undefined_object THEN
                        RAISE NOTICE 'Role Altinn-30-Correspondence-Test-Developers does not exist, skipping';
                    END;
                    
                    -- Try to set pgaudit.log = 'all' for Altinn-30-Correspondence-Prod-Developers (production environments)
                    BEGIN
                        ALTER ROLE ""Altinn-30-Correspondence-Prod-Developers"" SET pgaudit.log = 'all';
                        RAISE NOTICE 'Set pgaudit.log for Altinn-30-Correspondence-Prod-Developers';
                    EXCEPTION WHEN undefined_object THEN
                        RAISE NOTICE 'Role Altinn-30-Correspondence-Prod-Developers does not exist, skipping';
                    END;
                EXCEPTION
                    WHEN OTHERS THEN
                        -- Log the error but don't fail the migration
                        RAISE NOTICE 'Could not configure pgaudit for developer roles: %', SQLERRM;
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
                    -- Reset pgaudit.log configuration for Altinn-30-Correspondence-Test-Developers
                    BEGIN
                        ALTER ROLE ""Altinn-30-Correspondence-Test-Developers"" RESET pgaudit.log;
                        RAISE NOTICE 'Reset pgaudit.log for Altinn-30-Correspondence-Test-Developers';
                    EXCEPTION WHEN undefined_object THEN
                        RAISE NOTICE 'Role Altinn-30-Correspondence-Test-Developers does not exist, skipping';
                    END;
                    
                    -- Reset pgaudit.log configuration for Altinn-30-Correspondence-Prod-Developers
                    BEGIN
                        ALTER ROLE ""Altinn-30-Correspondence-Prod-Developers"" RESET pgaudit.log;
                        RAISE NOTICE 'Reset pgaudit.log for Altinn-30-Correspondence-Prod-Developers';
                    EXCEPTION WHEN undefined_object THEN
                        RAISE NOTICE 'Role Altinn-30-Correspondence-Prod-Developers does not exist, skipping';
                    END;
                    
                    -- Note: We don't drop the pgaudit extension in Down() as it might be used by other parts of the system
                    -- If you need to remove it, do it manually: DROP EXTENSION IF EXISTS pgaudit;
                EXCEPTION
                    WHEN OTHERS THEN
                        -- Log the error but don't fail the migration
                        RAISE NOTICE 'Could not reset pgaudit.log for developer roles: %', SQLERRM;
                END
                $do$;
            ");
        }
    }
}
