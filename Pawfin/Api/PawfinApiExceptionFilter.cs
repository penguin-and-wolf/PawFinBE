using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>
/// Logs unexpected Pawfin API errors and returns a traceable JSON error response.
/// </summary>
public sealed class PawfinApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<PawfinApiExceptionFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PawfinApiExceptionFilter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public PawfinApiExceptionFilter(ILogger<PawfinApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        _logger.LogError(
            context.Exception,
            "Pawfin API request {Method} {Path} failed. TraceId: {TraceId}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path,
            traceId);

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Pawfin request failed",
            Detail = "The server could not complete this request. Use the traceId to find its details in the Jellyfin server log.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["errorCode"] = "pawfin_internal_error";

        context.Result = new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        context.ExceptionHandled = true;
    }
}
