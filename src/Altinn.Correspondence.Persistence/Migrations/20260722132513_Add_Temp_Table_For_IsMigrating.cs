using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Temp_Table_For_IsMigrating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE ""correspondence"".""TempTableIsMigratingCorrespondences"" (
                    LIKE ""correspondence"".""Correspondences""
                );
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE ""correspondence"".""TempTableIsMigratingCorrespondences""
                    ADD PRIMARY KEY (""Id"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS ""correspondence"".""TempTableIsMigratingCorrespondences"";
            ");
        }
    }
}
