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
    private readonly LogForDevOptions _options;
    private readonly ILogger<LogsController> _logger;

    public LogsController(
        ILogService logService, 
        IOptions<LogForDevOptions> options,
        ILogger<LogsController> logger)
    {
        _logService = logService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Send a single log entry
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<LogResponse>> PostLog([FromBody] LogEntryRequest request)
    {
        if (!ValidateApiKey())
            return Unauthorized(new LogResponse { Success = false, Error = "Invalid API key" });

        try
        {
            var logEntry = request.ToLogEntry();
            
            // Get client IP as host if not provided
            if (string.IsNullOrEmpty(logEntry.Host))
                logEntry.Host = HttpContext.Connection.RemoteIpAddress?.ToString();

            var id = await _logService.InsertLogAsync(logEntry);
            
            return Ok(new LogResponse { Success = true, Id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert log");
            return StatusCode(500, new LogResponse { Success = false, Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Send multiple log entries at once
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<LogResponse>> PostBatch([FromBody] BatchLogRequest request)
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
            });

            var count = await _logService.InsertBatchAsync(logEntries);
            
            return Ok(new LogResponse { Success = true, Count = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert batch logs");
            return StatusCode(500, new LogResponse { Success = false, Error = "Internal server error" });
        }
    }

    /// <summary>
    /// Query logs with filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<LogEntry>>> GetLogs([FromQuery] LogQueryParams query)
    {
        if (!ValidateApiKey())
            return Unauthorized();

        try
        {
            var logs = await _logService.GetLogsAsync(query);
            return Ok(logs);
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

    private bool ValidateApiKey()
    {
        // Check header
        if (Request.Headers.TryGetValue("X-API-Key", out var apiKey))
        {
            return apiKey == _options.ApiKey;
        }
        
        // Check query parameter (for testing)
        if (Request.Query.TryGetValue("apiKey", out var queryApiKey))
        {
            return queryApiKey == _options.ApiKey;
        }

        return false;
    }
}
