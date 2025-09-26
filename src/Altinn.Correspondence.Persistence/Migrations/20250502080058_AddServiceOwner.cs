using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceOwners",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceOwners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StorageProviders",
                schema: "correspondence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    StorageResourceName = table.Column<string>(type: "text", nullable: false),
                    ServiceOwnerId = table.Column<string>(type: "text", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    ServiceOwnerEntityId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StorageProviders_ServiceOwners_ServiceOwnerEntityId",
                        column: x => x.ServiceOwnerEntityId,
                        principalSchema: "correspondence",
                        principalTable: "ServiceOwners",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_StorageProviders_ServiceOwnerEntityId",
                schema: "correspondence",
                table: "StorageProviders",
                column: "ServiceOwnerEntityId");

            var sql = @"
                CREATE OR REPLACE FUNCTION initialize_service_owner(
                    service_owner_id TEXT,
                    service_owner_name TEXT
                )
                RETURNS INT AS $$
                DECLARE
                    jobId INT;
                    request_json TEXT;
                    parameter_types_array TEXT[];
                    parameter_types_string TEXT;
                BEGIN
                    -- Create the service owner request JSON
                    request_json := json_build_object(
                        'ServiceOwnerId', service_owner_id,
                        'ServiceOwnerName', service_owner_name
                    )::text;

                    -- Define the parameter types
                    parameter_types_array := ARRAY[
                        'Altinn.Correspondence.Application.InitializeServiceOwner.InitializeServiceOwnerRequest, Altinn.Correspondence.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null',
                        'System.Security.Claims.ClaimsPrincipal, System.Security.Claims, Version=9.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a',
                        'System.Threading.CancellationToken, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e'
                    ];

                    -- Format parameter types array to JSON string with proper escaping
                    parameter_types_string := format('[""%s"",""%s"",""%s""]',
                        replace(parameter_types_array[1], '""', '\""'),
                        replace(parameter_types_array[2], '""', '\""'),
                        replace(parameter_types_array[3], '""', '\""')
                    );

                    -- Insert into hangfire.job
                    INSERT INTO hangfire.job (invocationdata, arguments, createdat, statename, stateid)
                    VALUES (
                        -- Create the invocationdata JSON
                        json_build_object(
                            'Type', 'Altinn.Correspondence.Application.InitializeServiceOwner.InitializeServiceOwnerHandler, Altinn.Correspondence.Application, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null',
                            'Method', 'Process',
                            'Arguments', format('[""%s"",null,null]',
                                replace(request_json, '""', '\""') -- Properly escape quotes
                            ),
                            'ParameterTypes', parameter_types_string
                        ),

                        -- Create the arguments as a proper JSON array with the first element as a JSON string
                        to_jsonb(ARRAY[request_json, NULL, NULL]),

                        now(),
                        'Enqueued',
                        NULL
                    )
                    RETURNING id INTO jobId;

                    -- Insert into hangfire.jobqueue
                    INSERT INTO hangfire.jobqueue (jobid, queue, fetchedat)
                    VALUES (jobId, 'default', NULL);

                    RETURN jobId;
                END;
                $$ LANGUAGE plpgsql;
                ";

            migrationBuilder.Sql(sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StorageProviders",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "ServiceOwners",
                schema: "correspondence");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS initialize_service_owner;");
        }
    }
}
