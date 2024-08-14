using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Schema_Permissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
            @$"
                        DO $do$
                        DECLARE
                            role_count INTEGER;
                        BEGIN
                            SELECT COUNT(*) INTO role_count FROM pg_roles WHERE rolname = 'azure_pg_admin';

                            IF role_count > 0 THEN
                                GRANT ALL ON SCHEMA correspondence TO azure_pg_admin;
                                GRANT ALL ON ALL TABLES IN SCHEMA correspondence TO azure_pg_admin;
                                GRANT ALL ON ALL SEQUENCES IN SCHEMA correspondence TO azure_pg_admin;
                                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT ALL ON TABLES TO azure_pg_admin;
                                ALTER DEFAULT PRIVILEGES IN SCHEMA correspondence GRANT ALL ON SEQUENCES TO azure_pg_admin;
                            END IF;
                        END
                        $do$;
                        "
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
