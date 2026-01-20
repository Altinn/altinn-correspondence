using System.Diagnostics;
using System.Net;
using Altinn.Authorization.ProblemDetails;
using Altinn.Correspondence.Application;
using Microsoft.AspNetCore.Mvc;

namespace Altinn.Correspondence.API.Helpers;

public static class ProblemDetailsHelper
{
    private static readonly ProblemDescriptorFactory _factory = ProblemDescriptorFactory.New("CORR");
    private static readonly ValidationErrorDescriptorFactory _validationFactory
        = ValidationErrorDescriptorFactory.New("CORR");

    private static readonly Dictionary<HttpStatusCode, (string Type, string Title)> StatusCodeMappings = new()
    {
        { HttpStatusCode.BadRequest, ("https://tools.ietf.org/html/rfc9110#section-15.5.1", "Bad Request") },
        { HttpStatusCode.Unauthorized, ("https://tools.ietf.org/html/rfc9110#section-15.5.2", "Unauthorized") },
        { HttpStatusCode.Forbidden, ("https://tools.ietf.org/html/rfc9110#section-15.5.4", "Forbidden") },
        { HttpStatusCode.NotFound, ("https://tools.ietf.org/html/rfc9110#section-15.5.5", "Not Found") },
        { HttpStatusCode.Conflict, ("https://tools.ietf.org/html/rfc9110#section-15.5.10", "Conflict") },
        { HttpStatusCode.UnprocessableEntity, ("https://tools.ietf.org/html/rfc9110#section-15.5.21", "Unprocessable Entity") },
        { HttpStatusCode.UnavailableForLegalReasons, ("https://tools.ietf.org/html/rfc7725#section-3", "Unavailable For Legal Reasons") },
        { HttpStatusCode.InternalServerError, ("https://tools.ietf.org/html/rfc9110#section-15.6.1", "Internal Server Error") },
        { HttpStatusCode.BadGateway, ("https://tools.ietf.org/html/rfc9110#section-15.6.3", "Bad Gateway") },
    };

    public static ObjectResult ToProblemResult(Error error)
    {
        var descriptor = _factory.Create((uint)error.ErrorCode, error.StatusCode, error.Message);
        var problemDetails = descriptor.ToProblemDetails();

        if (StatusCodeMappings.TryGetValue(error.StatusCode, out var mapping))
        {
            problemDetails.Type = mapping.Type;
            problemDetails.Title = mapping.Title;
        }

        var traceId = Activity.Current?.Id;
        if (!string.IsNullOrEmpty(traceId))
        {
            problemDetails.Extensions["traceId"] = traceId;
        }

        problemDetails.Extensions["errorCode"] = error.ErrorCode;

        return new ObjectResult(problemDetails)
        {
            StatusCode = (int)error.StatusCode
        };
    }

    public static ObjectResult ToValidationProblemResult(ActionContext context)
    {
        var validationErrors = new List<AltinnValidationError>();
        
        // for each key-value pair build a list of validation errors
        foreach (var kvp in context.ModelState)
        {
            if (kvp.Value?.Errors == null || kvp.Value.Errors.Count == 0)
                continue;
            
            foreach (var error in kvp.Value.Errors)
            {
                var descriptor = _validationFactory.Create(0, error.ErrorMessage);
                var validationError = descriptor.ToValidationError(kvp.Key);
                validationErrors.Add(validationError);
            }
        }

        // Create AltinnValidationProblemDetails with the array of validation errors
        var problemDetails = new AltinnValidationProblemDetails(validationErrors.ToArray());
        // Keep the old errors to remain backwards compatible
        problemDetails.Extensions["errors"] = validationErrors;
        if (StatusCodeMappings.TryGetValue(HttpStatusCode.BadRequest, out var mapping))
        {
            problemDetails.Type = mapping.Type;
            problemDetails.Title = mapping.Title;
        }

        var traceId = Activity.Current?.Id;
        if (!string.IsNullOrEmpty(traceId))
        {
            problemDetails.Extensions["traceId"] = traceId;
        }

        return new ObjectResult(problemDetails);
    }
}
