using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Application.Helpers;

public static class DbUpdateExceptionExtensions
{
    // PostgreSQL error code for unique_violation
    private const string UniqueViolationSqlState = "23505";
    private const string ForeignKeyViolationSqlState = "23503";

    public static bool IsPostgresUniqueViolation(this DbUpdateException exception)
    {
        var sqlState = exception.InnerException?.Data?["SqlState"]?.ToString();
        return sqlState == UniqueViolationSqlState;
    }

    public static bool IsPostgresForeignKeyViolation(this DbUpdateException exception)
    {
        var sqlState = exception.InnerException?.Data?["SqlState"]?.ToString();
        return sqlState == ForeignKeyViolationSqlState;
    }
}