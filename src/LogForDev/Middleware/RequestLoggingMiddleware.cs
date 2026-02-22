using LogForDev.Models;
using LogForDev.Services;
using System.Diagnostics;

namespace LogForDev.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAppLogService _appLogService;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, IAppLogService appLogService, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _appLogService = appLogService;
        _logger = logger;
    }

    /// <summary>
    /// Truncates stack traces to first 5 lines to prevent internal path disclosure in logs.
    /// </summary>
    private static string TruncateStackTrace(string exception)
    {
        var lines = exception.Split('\n');
        if (lines.Length <= 5)
            return exception;
        return string.Join('\n', lines.Take(5)) + "\n... (truncated)";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "/";
        var queryString = context.Request.QueryString.Value;
        // Mask sensitive query parameters (apiKey, token, key, secret) to prevent credential leaks in logs
        if (!string.IsNullOrEmpty(queryString))
        {
            path += System.Text.RegularExpressions.Regex.Replace(
                queryString,
                @"(apiKey|token|key|secret)=[^&]*",
                "$1=***",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
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

            if (statusCode >= 500 || exception != null)
                _logger.LogError(exception != null ? new Exception(exception) : null, "{Message}", message);
            else if (statusCode >= 400)
                _logger.LogWarning("{Message}", message);
            else
                _logger.LogInformation("{Message}", message);

            _appLogService.Enqueue(new AppLogEntry
            {
                Level = level,
                Category = "HTTP",
                Message = message,
                Exception = exception != null ? TruncateStackTrace(exception) : null,
                RequestMethod = method,
                RequestPath = path,
                StatusCode = statusCode,
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
        }
    }
}
