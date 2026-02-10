using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LogForDev.Models;
using LogForDev.Services;
using LogForDev.Extensions;

namespace LogForDev.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly ILogService _logService;
    private readonly ILogBufferService _buffer;
    private readonly IProjectService _projectService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(
        ILogService logService,
        ILogBufferService buffer,
        IProjectService projectService,
        ILogger<LogsController> logger)
    {
        _logService = logService;
        _buffer = buffer;
        _projectService = projectService;
        _logger = logger;
    }

    [HttpPost]
    public ActionResult<LogResponse> PostLog([FromBody] LogEntryRequest request)
    {
        try
        {
            var logEntry = request.ToLogEntry();
            var project = HttpContext.GetProject();

            if (string.IsNullOrEmpty(logEntry.Host))
                logEntry.Host = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (project != null)
            {
                logEntry.ProjectId = project.Id;
                logEntry.ProjectName = project.Name;
            }

            _buffer.Enqueue(logEntry);

            return Ok(new LogResponse { Success = true, Id = logEntry.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue log");
            return StatusCode(500, new LogResponse { Success = false, Error = "Internal server error" });
        }
    }

    [HttpPost("batch")]
    public ActionResult<LogResponse> PostBatch([FromBody] BatchLogRequest request)
    {
        try
        {
            var project = HttpContext.GetProject();
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();

            var logEntries = request.Logs.Select(r =>
            {
                var entry = r.ToLogEntry();
                if (string.IsNullOrEmpty(entry.Host))
                    entry.Host = clientIp;
                if (project != null)
                {
                    entry.ProjectId = project.Id;
                    entry.ProjectName = project.Name;
                }
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

    [HttpGet]
    public async Task<ActionResult<PagedResult<LogEntry>>> GetLogs([FromQuery] LogQueryParams query)
    {
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

    [HttpGet("stats")]
    public async Task<ActionResult<LogStats>> GetStats()
    {
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

    [HttpGet("apps")]
    public async Task<ActionResult<List<string>>> GetApps()
    {
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

    [HttpGet("environments")]
    public async Task<ActionResult<List<string>>> GetEnvironments()
    {
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

    [HttpGet("projects")]
    [AllowAnonymous]
    public async Task<ActionResult<List<Project>>> GetProjects()
    {
        try
        {
            var projects = await _projectService.GetAllProjectsAsync();
            return Ok(projects);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get projects");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("patterns")]
    public async Task<ActionResult<List<LogPattern>>> GetPatterns([FromQuery] LogPatternQueryParams query)
    {
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

    [HttpGet("trace/{traceId}")]
    public async Task<ActionResult<TraceTimeline>> GetTraceTimeline(string traceId)
    {
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

    [HttpDelete]
    [AllowAnonymous]
    public async Task<ActionResult> DeleteLogs([FromQuery] int? olderThanDays = null)
    {
        try
        {
            await _logService.DeleteLogsAsync(olderThanDays);
            var msg = olderThanDays.HasValue ? $"Logs older than {olderThanDays} days deleted" : "All logs deleted";
            return Ok(new { success = true, message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete logs");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    [HttpPost("projects")]
    [AllowAnonymous]
    public async Task<ActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, error = "Project name is required" });

            var apiKey = $"lfdev_{Guid.NewGuid():N}";
            var project = await _projectService.CreateProjectAsync(request.Name, apiKey, request.ExpiryDays);
            return Ok(new { success = true, project, apiKey });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project");
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    [HttpPut("projects/{id}")]
    [AllowAnonymous]
    public async Task<ActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { success = false, error = "Project name is required" });

            var result = await _projectService.UpdateProjectAsync(id, request.Name);
            if (!result)
                return StatusCode(500, new { success = false, error = "Failed to update project" });

            return Ok(new { success = true, message = "Project updated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update project {ProjectId}", id);
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    [HttpDelete("projects/{id}")]
    [AllowAnonymous]
    public async Task<ActionResult> DeleteProject(Guid id)
    {
        try
        {
            var result = await _projectService.DeleteProjectAsync(id);
            if (!result)
                return StatusCode(500, new { success = false, error = "Failed to delete project" });

            return Ok(new { success = true, message = "Project deleted" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete project {ProjectId}", id);
            return StatusCode(500, new { success = false, error = "Internal server error" });
        }
    }

    [HttpGet("app")]
    [AllowAnonymous]
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
}
