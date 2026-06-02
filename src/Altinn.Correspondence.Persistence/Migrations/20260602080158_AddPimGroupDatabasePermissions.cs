using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPimGroupDatabasePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
            @"
                DO $do$
                DECLARE
                    db_reader_role_name CONSTANT text := 'correspondence-prod-db-reader';
                    db_writer_role_name CONSTANT text := 'correspondence-prod-db-writer';
                    role_name text;
                BEGIN
                    FOREACH role_name IN ARRAY ARRAY[db_reader_role_name, db_writer_role_name]
                    LOOP
                        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                            BEGIN
                                PERFORM * FROM pgaadauth_create_principal(role_name, false, false);
                                RAISE NOTICE 'Created AAD principal for %', role_name;
                            EXCEPTION
                                WHEN undefined_function THEN
                                    EXECUTE format('CREATE ROLE %I', role_name);
                                    RAISE NOTICE 'pgaadauth_create_principal missing, created local role %', role_name;
                                WHEN duplicate_object THEN
                                    RAISE NOTICE 'Role % already exists, skipping', role_name;
                                WHEN OTHERS THEN
                                    RAISE NOTICE 'Could not create role %: %', role_name, SQLERRM;
                            END;
                        END IF;
                    END LOOP;
                END
                $do$;

                GRANT CONNECT, TEMPORARY ON DATABASE correspondence TO ""correspondence-prod-db-reader"", ""correspondence-prod-db-writer"";

                GRANT USAGE ON SCHEMA correspondence TO ""correspondence-prod-db-reader"", ""correspondence-prod-db-writer"";
                GRANT SELECT ON ALL TABLES IN SCHEMA correspondence TO ""correspondence-prod-db-reader"";
                GRANT SELECT ON ALL SEQUENCES IN SCHEMA correspondence TO ""correspondence-prod-db-reader"";
                GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA correspondence TO ""correspondence-prod-db-reader"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT SELECT ON TABLES TO ""correspondence-prod-db-reader"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT SELECT ON SEQUENCES TO ""correspondence-prod-db-reader"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT EXECUTE ON FUNCTIONS TO ""correspondence-prod-db-reader"";

                GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
                    ON ALL TABLES IN SCHEMA correspondence TO ""correspondence-prod-db-writer"";
                GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA correspondence TO ""correspondence-prod-db-writer"";
                GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA correspondence TO ""correspondence-prod-db-writer"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence
                    GRANT SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON TABLES TO ""correspondence-prod-db-writer"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence
                    GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO ""correspondence-prod-db-writer"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT EXECUTE ON FUNCTIONS TO ""correspondence-prod-db-writer"";
            "
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
            @"
                REVOKE CONNECT, TEMPORARY ON DATABASE correspondence FROM ""correspondence-prod-db-reader"", ""correspondence-prod-db-writer"";

                REVOKE USAGE ON SCHEMA correspondence FROM ""correspondence-prod-db-reader"", ""correspondence-prod-db-writer"";
                REVOKE SELECT ON ALL TABLES IN SCHEMA correspondence FROM ""correspondence-prod-db-reader"";
                REVOKE SELECT ON ALL SEQUENCES IN SCHEMA correspondence FROM ""correspondence-prod-db-reader"";
                REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA correspondence FROM ""correspondence-prod-db-reader"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence REVOKE SELECT ON TABLES FROM ""correspondence-prod-db-reader"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence REVOKE SELECT ON SEQUENCES FROM ""correspondence-prod-db-reader"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence REVOKE EXECUTE ON FUNCTIONS FROM ""correspondence-prod-db-reader"";

                REVOKE SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER
                    ON ALL TABLES IN SCHEMA correspondence FROM ""correspondence-prod-db-writer"";
                REVOKE USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA correspondence FROM ""correspondence-prod-db-writer"";
                REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA correspondence FROM ""correspondence-prod-db-writer"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence
                    REVOKE SELECT, INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON TABLES FROM ""correspondence-prod-db-writer"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence
                    REVOKE USAGE, SELECT, UPDATE ON SEQUENCES FROM ""correspondence-prod-db-writer"";
                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence REVOKE EXECUTE ON FUNCTIONS FROM ""correspondence-prod-db-writer"";
            "
            );
        }
    }
}
