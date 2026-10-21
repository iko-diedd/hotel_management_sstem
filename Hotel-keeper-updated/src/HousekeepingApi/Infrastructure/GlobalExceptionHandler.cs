using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousekeepingApi.Infrastructure;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title, detail) = ex switch
        {
            ApiException api => (api.StatusCode, Title(api.StatusCode), api.Message),
            DbUpdateConcurrencyException => (409, "Concurrency conflict",
                "The record was modified by someone else. Reload it and try again."),
            DbUpdateException => (409, "Conflict",
                "The change conflicts with existing data (for example a duplicate value or a related record)."),
            _ => (500, "Server error", "An unexpected error occurred.")
        };

        if (status >= 500) _logger.LogError(ex, "Unhandled exception");
        else _logger.LogInformation("Request failed with {Status}: {Message}", status, ex.Message);

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = ctx.Request.Path
        }, ct);
        return true;
    }

    private static string Title(int s) => s switch
    {
        400 => "Bad request", 401 => "Unauthorized", 403 => "Forbidden", 404 => "Not found", 409 => "Conflict", _ => "Error"
    };
}
