using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Altinn.Correspondence.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ActivateCron : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Create test table
                CREATE TABLE IF NOT EXISTS test_migration_table (
                    id SERIAL PRIMARY KEY,
                    name VARCHAR(100),
                    created_at TIMESTAMP DEFAULT NOW()
                );
                
                -- Insert test data
                INSERT INTO test_migration_table (name) VALUES 
                    ('Test Row 1'),
                    ('Test Row 2'),
                    ('Test Row 3'),
                    ('Test Row 4'),
                    ('Test Row 5');
                
                RAISE NOTICE 'Test table created with 5 rows';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS test_migration_table;");
        }
    }
}
