using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LogForDev.Models;
using LogForDev.Services;

namespace LogForDev.Controllers;

/// <summary>
/// API endpoints for sending and querying logs
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
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
    /// <param name="request">The log entry to send</param>
    /// <returns>Response with the created log ID</returns>
    /// <response code="200">Log successfully queued</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(LogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LogResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(LogResponse), StatusCodes.Status500InternalServerError)]
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
    /// Send multiple log entries at once (batch insert)
    /// </summary>
    /// <param name="request">Batch of log entries to send</param>
    /// <returns>Response with the count of inserted logs</returns>
    /// <response code="200">Logs successfully queued</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("batch")]
    [ProducesResponseType(typeof(LogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LogResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(LogResponse), StatusCodes.Status500InternalServerError)]
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
    /// Query logs with filters and pagination
    /// </summary>
    /// <param name="query">Filter parameters (level, appName, search, from, to, page, pageSize)</param>
    /// <returns>Paginated list of log entries</returns>
    /// <response code="200">Returns the filtered logs</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Get log statistics (last 24 hours)
    /// </summary>
    /// <returns>Statistics including total logs, errors, warnings, and top apps</returns>
    /// <response code="200">Returns the statistics</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(LogStats), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Get list of registered application names
    /// </summary>
    /// <returns>List of unique app names that have sent logs</returns>
    /// <response code="200">Returns the app names</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("apps")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    /// Get list of environments
    /// </summary>
    /// <returns>List of unique environment names</returns>
    /// <response code="200">Returns the environment names</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("environments")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<string>>> GetEnvironments()
    {
        if (!ValidateApiKey())
            return Unauthorized();

        try
        {
            var envs = await _logService.GetEnvironmentsAsync();
            return Ok(envs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environments");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get log patterns (aggregated similar logs)
    /// </summary>
    /// <param name="query">Query parameters for pattern matching</param>
    /// <returns>List of log patterns with counts</returns>
    /// <response code="200">Returns the log patterns</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("patterns")]
    [ProducesResponseType(typeof(List<LogPattern>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<LogPattern>>> GetPatterns([FromQuery] LogPatternQueryParams query)
    {
        if (!ValidateApiKey())
            return Unauthorized();

        try
        {
            var patterns = await _logService.GetPatternsAsync(query);
            return Ok(patterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get patterns");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get trace timeline (all logs for a trace ID)
    /// </summary>
    /// <param name="traceId">The trace ID to query</param>
    /// <returns>Timeline of logs in the trace</returns>
    /// <response code="200">Returns the trace timeline</response>
    /// <response code="404">Trace not found</response>
    /// <response code="401">Invalid or missing API key</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("trace/{traceId}")]
    [ProducesResponseType(typeof(TraceTimeline), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TraceTimeline>> GetTraceTimeline(string traceId)
    {
        if (!ValidateApiKey())
            return Unauthorized();

        try
        {
            var timeline = await _logService.GetTraceTimelineAsync(traceId);
            if (timeline == null)
                return NotFound(new { error = "Trace not found" });

            return Ok(timeline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trace timeline for {TraceId}", traceId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Query internal LogForDev application logs
    /// </summary>
    /// <param name="query">Filter parameters for internal logs</param>
    /// <returns>Paginated list of internal application log entries</returns>
    /// <response code="200">Returns the internal logs</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("app")]
    [ProducesResponseType(typeof(PagedResult<AppLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
        // Allow requests from same origin (browser UI)
        var referer = Request.Headers["Referer"].ToString();
        var host = $"{Request.Scheme}://{Request.Host}";
        if (!string.IsNullOrEmpty(referer) && referer.StartsWith(host))
        {
            return true;
        }

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
