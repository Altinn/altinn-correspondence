using Altinn.Authorization.ProblemDetails;
using Altinn.Correspondence.Common.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Altinn.Correspondence.API.Filters;

public class ClientErrorLoggingFilter(ILogger<ClientErrorLoggingFilter> logger) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult { Value: AltinnProblemDetails problemDetails } objectResult)
            return;

        var statusCode = objectResult.StatusCode ?? problemDetails.Status ?? 0;
        if (statusCode < 400 || statusCode >= 500)
            return;

        var errorCode = problemDetails.Extensions.TryGetValue("errorCode", out var code) ? code : null;
        var path = context.HttpContext.Request.Path.ToString().SanitizeForLogging();
        var method = context.HttpContext.Request.Method.SanitizeForLogging();

        if (problemDetails.Extensions.TryGetValue("errors", out var errorsObj)
            && errorsObj is Dictionary<string, string[]> validationErrors
            && validationErrors.Count > 0)
        {
            var errors = string.Join("; ", validationErrors.SelectMany(e => e.Value.Select(msg => $"{e.Key.SanitizeForLogging()}: {msg.SanitizeForLogging()}")));
            logger.LogWarning(
                "Client error {StatusCode} on {Method} {Path}: Validation errors: {Errors}",
                statusCode, method, path, errors);
            return;
        }

        logger.LogWarning(
            "Client error {StatusCode} on {Method} {Path}: [{ErrorCode}] {Detail}",
            statusCode, method, path, errorCode, problemDetails.Detail?.SanitizeForLogging());
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
