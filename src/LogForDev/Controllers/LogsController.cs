using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LogForDev.Models;
using LogForDev.Services;

namespace LogForDev.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly ILogBufferService _buffer;
    private readonly LogForDevOptions _options;
    private readonly ILogger<LogsController> _logger;

    public LogsController(
        ILogService logService,
        ILogBufferService buffer,
        IOptions<LogForDevOptions> options,
        ILogger<LogsController> logger)
    {
        _logService = logService;
        _buffer = buffer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Send a single log entry
    /// </summary>
    [HttpPost]
    public ActionResult<LogResponse> PostLog([FromBody] LogEntryRequest request)
    {
        if (!ValidateApiKey())
            return Unauthorized(new LogResponse { Success = false, Error = "Invalid API key" });

        try
        {
            var logEntry = request.ToLogEntry();

            if (string.IsNullOrEmpty(logEntry.Host))
                logEntry.Host = HttpContext.Connection.RemoteIpAddress?.ToString();

            _buffer.Enqueue(logEntry);

            return Ok(new LogResponse { Success = true, Id = logEntry.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue log");
            return StatusCode(500, new LogResponse { Success = false, Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Send multiple log entries at once
    /// </summary>
    [HttpPost("batch")]
    public ActionResult<LogResponse> PostBatch([FromBody] BatchLogRequest request)
    {
        if (!ValidateApiKey())
            return Unauthorized(new LogResponse { Success = false, Error = "Invalid API key" });

        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var logEntries = request.Logs.Select(r =>
            {
                var entry = r.ToLogEntry();
                if (string.IsNullOrEmpty(entry.Host))
                    entry.Host = clientIp;
                return entry;
            }).ToList();

            _buffer.EnqueueBatch(logEntries);

            return Ok(new LogResponse { Success = true, Count = logEntries.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue batch logs");
            return StatusCode(500, new LogResponse { Success = false, Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Query logs with filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<LogEntry>>> GetLogs([FromQuery] LogQueryParams query)
    {
        if (!ValidateApiKey())
            return Unauthorized();

        try
        {
            var result = await _logService.GetLogsAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query logs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get log statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<LogStats>> GetStats()
    {
        if (!ValidateApiKey())
            return Unauthorized();

        try
        {
            var stats = await _logService.GetStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stats");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get list of registered app names
    /// </summary>
    [HttpGet("apps")]
    public async Task<ActionResult<List<string>>> GetApps()
    {
        if (!ValidateApiKey())
            return Unauthorized();

        try
        {
            var apps = await _logService.GetAppNamesAsync();
            return Ok(apps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get apps");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Query internal application logs
    /// </summary>
    [HttpGet("app")]
    public async Task<ActionResult<PagedResult<AppLogEntry>>> GetAppLogs([FromQuery] AppLogQueryParams query)
    {
        try
        {
            var appLogService = HttpContext.RequestServices.GetRequiredService<IAppLogService>();
            var result = await appLogService.GetLogsAsync(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query app logs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private bool ValidateApiKey()
    {
        if (Request.Headers.TryGetValue("X-API-Key", out var apiKey))
        {
            return apiKey == _options.ApiKey;
        }

        if (Request.Query.TryGetValue("apiKey", out var queryApiKey))
        {
            return queryApiKey == _options.ApiKey;
        }

        return false;
    }
}
