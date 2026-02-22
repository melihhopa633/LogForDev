using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LogForDev.Core;
using LogForDev.Data;
using LogForDev.Models;
using LogForDev.Services;
using LogForDev.Extensions;

namespace LogForDev.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LogsController : ControllerBase
{
    private readonly ILogRepository _logRepository;
    private readonly ILogBufferService _buffer;
    private readonly IProjectService _projectService;
    private readonly IAppLogService _appLogService;
    private readonly ILogger<LogsController> _logger;

    public LogsController(
        ILogRepository logRepository,
        ILogBufferService buffer,
        IProjectService projectService,
        IAppLogService appLogService,
        ILogger<LogsController> logger)
    {
        _logRepository = logRepository;
        _buffer = buffer;
        _projectService = projectService;
        _appLogService = appLogService;
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
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult<PagedResult<LogEntry>>> GetLogs([FromQuery] LogQueryParams query, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _logRepository.GetPagedAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query logs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("stats")]
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult<LogStats>> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            var stats = await _logRepository.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stats");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("apps")]
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult<List<string>>> GetApps(CancellationToken cancellationToken)
    {
        try
        {
            var apps = await _logRepository.GetAppNamesAsync(cancellationToken);
            return Ok(apps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get apps");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("environments")]
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult<List<string>>> GetEnvironments(CancellationToken cancellationToken)
    {
        try
        {
            var envs = await _logRepository.GetEnvironmentsAsync(cancellationToken);
            return Ok(envs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environments");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("projects")]
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
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
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult<List<LogPattern>>> GetPatterns([FromQuery] LogPatternQueryParams query, CancellationToken cancellationToken)
    {
        try
        {
            var patterns = await _logRepository.GetPatternsAsync(query, cancellationToken);
            return Ok(patterns);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get patterns");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpGet("trace/{traceId}")]
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult<TraceTimeline>> GetTraceTimeline(string traceId, CancellationToken cancellationToken)
    {
        try
        {
            var timeline = await _logRepository.GetTraceTimelineAsync(traceId, cancellationToken);
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
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult> DeleteLogs([FromQuery] int? olderThanDays, CancellationToken cancellationToken)
    {
        try
        {
            await _logRepository.DeleteLogsAsync(olderThanDays, cancellationToken);
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
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
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
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
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
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
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
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult<PagedResult<AppLogEntry>>> GetAppLogs([FromQuery] AppLogQueryParams query, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _appLogService.GetLogsAsync(query, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query app logs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    [HttpDelete("app")]
    [Authorize(Policy = AppConstants.Auth.DashboardOnlyPolicy)]
    public async Task<ActionResult> DeleteAppLogs([FromQuery] int? olderThanDays, CancellationToken cancellationToken)
    {
        try
        {
            await _appLogService.DeleteLogsAsync(olderThanDays, cancellationToken);
            var msg = olderThanDays.HasValue ? $"App logs older than {olderThanDays} days deleted" : "All app logs deleted";
            return Ok(new { success = true, message = msg });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete app logs");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
