using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPimGroupDatabasePermissions : Migration
    {
        /// <inheritdoc />

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $do$
                DECLARE
                    db_reader_role_name CONSTANT text := 'correspondence-prod-db-read';
                    db_writer_role_name CONSTANT text := 'correspondence-prod-db-write';
                    reader_exists boolean;
                    writer_exists boolean;
                BEGIN
                    SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = db_reader_role_name) INTO reader_exists;
                    SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = db_writer_role_name) INTO writer_exists;

                    ----------------------------------------------------------------
                    -- READER role
                    ----------------------------------------------------------------
                    IF reader_exists THEN
                        GRANT CONNECT, TEMPORARY ON DATABASE correspondence TO ""correspondence-prod-db-read"";

                        GRANT USAGE ON SCHEMA correspondence TO ""correspondence-prod-db-read"";
                        GRANT SELECT ON ALL TABLES IN SCHEMA correspondence TO ""correspondence-prod-db-read"";
                        GRANT SELECT ON ALL SEQUENCES IN SCHEMA correspondence TO ""correspondence-prod-db-read"";
                        GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA correspondence TO ""correspondence-prod-db-read"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT SELECT ON TABLES TO ""correspondence-prod-db-read"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT SELECT ON SEQUENCES TO ""correspondence-prod-db-read"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT EXECUTE ON FUNCTIONS TO ""correspondence-prod-db-read"";

                        ALTER ROLE ""correspondence-prod-db-read"" SET pgaudit.log = 'all';

                        RAISE NOTICE 'Granted privileges and configured pgaudit for correspondence-prod-db-read';
                    ELSE
                        RAISE NOTICE 'Role correspondence-prod-db-read does not exist, skipping';
                    END IF;

                    ----------------------------------------------------------------
                    -- WRITER role
                    ----------------------------------------------------------------
                    IF writer_exists THEN
                        GRANT CONNECT, TEMPORARY ON DATABASE correspondence TO ""correspondence-prod-db-write"";

                        GRANT USAGE ON SCHEMA correspondence TO ""correspondence-prod-db-write"";
                        GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
                            ON ALL TABLES IN SCHEMA correspondence TO ""correspondence-prod-db-write"";
                        GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA correspondence TO ""correspondence-prod-db-write"";
                        GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA correspondence TO ""correspondence-prod-db-write"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence
                            GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON TABLES TO ""correspondence-prod-db-write"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence
                            GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO ""correspondence-prod-db-write"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT EXECUTE ON FUNCTIONS TO ""correspondence-prod-db-write"";
                        GRANT CREATE ON SCHEMA correspondence TO ""correspondence-prod-db-write"";

                        ALTER ROLE ""correspondence-prod-db-write"" SET pgaudit.log = 'all';

                        RAISE NOTICE 'Granted privileges and configured pgaudit for correspondence-prod-db-write';
                    ELSE
                        RAISE NOTICE 'Role correspondence-prod-db-write does not exist, skipping';
                    END IF;
                END
                $do$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $do$
                DECLARE
                    db_reader_role_name CONSTANT text := 'correspondence-prod-db-read';
                    db_writer_role_name CONSTANT text := 'correspondence-prod-db-write';
                    reader_exists boolean;
                    writer_exists boolean;
                BEGIN
                    SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = db_reader_role_name) INTO reader_exists;
                    SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = db_writer_role_name) INTO writer_exists;

                    ----------------------------------------------------------------
                    -- READER role
                    ----------------------------------------------------------------
                    IF reader_exists THEN
                        ALTER ROLE ""correspondence-prod-db-read"" RESET pgaudit.log;

                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence REVOKE SELECT ON TABLES FROM ""correspondence-prod-db-read"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence REVOKE SELECT ON SEQUENCES FROM ""correspondence-prod-db-read"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence REVOKE EXECUTE ON FUNCTIONS FROM ""correspondence-prod-db-read"";

                        REVOKE SELECT ON ALL TABLES IN SCHEMA correspondence FROM ""correspondence-prod-db-read"";
                        REVOKE SELECT ON ALL SEQUENCES IN SCHEMA correspondence FROM ""correspondence-prod-db-read"";
                        REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA correspondence FROM ""correspondence-prod-db-read"";
                        REVOKE USAGE ON SCHEMA correspondence FROM ""correspondence-prod-db-read"";

                        REVOKE CONNECT, TEMPORARY ON DATABASE correspondence FROM ""correspondence-prod-db-read"";

                        RAISE NOTICE 'Revoked privileges and reset pgaudit for correspondence-prod-db-read';
                    ELSE
                        RAISE NOTICE 'Role correspondence-prod-db-read does not exist, skipping';
                    END IF;

                    ----------------------------------------------------------------
                    -- WRITER role
                    ----------------------------------------------------------------
                    IF writer_exists THEN
                        ALTER ROLE ""correspondence-prod-db-write"" RESET pgaudit.log;

                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence
                            REVOKE SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON TABLES FROM ""correspondence-prod-db-write"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence
                            REVOKE USAGE, SELECT, UPDATE ON SEQUENCES FROM ""correspondence-prod-db-write"";
                        ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence REVOKE EXECUTE ON FUNCTIONS FROM ""correspondence-prod-db-write"";

                        REVOKE CREATE ON SCHEMA correspondence FROM ""correspondence-prod-db-write"";
                        REVOKE SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
                            ON ALL TABLES IN SCHEMA correspondence FROM ""correspondence-prod-db-write"";
                        REVOKE USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA correspondence FROM ""correspondence-prod-db-write"";
                        REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA correspondence FROM ""correspondence-prod-db-write"";
                        REVOKE USAGE ON SCHEMA correspondence FROM ""correspondence-prod-db-write"";

                        REVOKE CONNECT, TEMPORARY ON DATABASE correspondence FROM ""correspondence-prod-db-write"";

                        RAISE NOTICE 'Revoked privileges and reset pgaudit for correspondence-prod-db-write';
                    ELSE
                        RAISE NOTICE 'Role correspondence-prod-db-write does not exist, skipping';
                    END IF;
                END
                $do$;
            ");
        }
    }
}
