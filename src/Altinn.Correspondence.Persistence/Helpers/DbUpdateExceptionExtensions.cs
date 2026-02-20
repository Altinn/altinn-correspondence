using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Persistence.Helpers;

public static class DbUpdateExceptionExtensions
{
    // PostgreSQL error code for unique_violation
    private const string UniqueViolationSqlState = "23505";

    public static bool IsPostgresUniqueViolation(this DbUpdateException exception)
    {
        var sqlState = exception.InnerException?.Data?["SqlState"]?.ToString();
        return sqlState == UniqueViolationSqlState;
    }
}
