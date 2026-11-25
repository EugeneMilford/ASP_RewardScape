using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SpendWell.Api.Common;

/// <summary>
/// Turns exceptions into consistent ProblemDetails responses so controllers stay thin.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string title;

        switch (exception)
        {
            case NotFoundException:
                status = StatusCodes.Status404NotFound;
                title = "Not found";
                break;
            case BusinessRuleException:
                status = StatusCodes.Status400BadRequest;
                title = "Business rule violated";
                break;
            case ConflictException:
                status = StatusCodes.Status409Conflict;
                title = "Conflict";
                break;
            case DbUpdateConcurrencyException:
                status = StatusCodes.Status409Conflict;
                title = "Concurrent update";
                break;
            default:
                status = StatusCodes.Status500InternalServerError;
                title = "Server error";
                break;
        }

        string detail;
        if (exception is DbUpdateConcurrencyException)
        {
            detail = "Someone else changed this record at the same moment. Please try again.";
        }
        else if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
            detail = "An unexpected error occurred.";
        }
        else
        {
            detail = exception.Message;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
