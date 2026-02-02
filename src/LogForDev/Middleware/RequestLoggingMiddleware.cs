using LogForDev.Models;
using LogForDev.Services;
using System.Diagnostics;

namespace LogForDev.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppLogService _appLogService;

    public RequestLoggingMiddleware(RequestDelegate next, IAppLogService appLogService)
    {
        _next = next;
        _appLogService = appLogService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "/";
        var method = context.Request.Method;

        // Skip logging for static files
        if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") ||
            path.StartsWith("/images") || path.StartsWith("/favicon"))
        {
            await _next(context);
            return;
        }

        string? exception = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            exception = ex.ToString();
            throw;
        }
        finally
        {
            sw.Stop();
            var statusCode = context.Response.StatusCode;
            var level = statusCode >= 500 ? "Error" : statusCode >= 400 ? "Warning" : "Information";

            if (exception != null) level = "Error";

            var message = $"{method} {path} -> {statusCode} ({sw.ElapsedMilliseconds}ms)";

            _appLogService.Enqueue(new AppLogEntry
            {
                Level = level,
                Category = "HTTP",
                Message = message,
                Exception = exception,
                RequestMethod = method,
                RequestPath = path,
                StatusCode = statusCode,
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
        }
    }
}
