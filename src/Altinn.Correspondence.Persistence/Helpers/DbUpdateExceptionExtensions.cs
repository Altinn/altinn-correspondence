using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Altinn.Correspondence.Persistence.Helpers;

public static class DbUpdateExceptionExtensions
{
    // PostgreSQL error code for unique_violation
    private const string UniqueViolationSqlState = "23505";

    public static bool IsPostgresUniqueViolation(this DbUpdateException exception)
    {
        for (var ex = exception.InnerException; ex is not null; ex = ex.InnerException)
        {
            if (ex is PostgresException postgresException && postgresException.SqlState == UniqueViolationSqlState)
            {
                return true;
            }

            var sqlState = ex.Data["SqlState"]?.ToString();
            if (sqlState == UniqueViolationSqlState)
            {
                return true;
            }
        }

        return false;
    }
}
