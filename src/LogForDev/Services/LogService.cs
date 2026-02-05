using LogForDev.Data;
using LogForDev.Models;

namespace LogForDev.Services;

public interface ILogService
{
    Task<Guid> InsertLogAsync(LogEntry log);
    Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs);
    Task<PagedResult<LogEntry>> GetLogsAsync(LogQueryParams query);
    Task<LogStats> GetStatsAsync();
    Task<List<string>> GetAppNamesAsync();
    Task<List<string>> GetEnvironmentsAsync();
    Task<List<LogPattern>> GetPatternsAsync(LogPatternQueryParams query);
    Task<TraceTimeline?> GetTraceTimelineAsync(string traceId);
}

public class LogService : ILogService
{
    private readonly ILogRepository _repository;
    private readonly ILogger<LogService> _logger;

    public LogService(ILogRepository repository, ILogger<LogService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Guid> InsertLogAsync(LogEntry log)
    {
        try
        {
            return await _repository.InsertAsync(log);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert log for app {AppName}", log.AppName);
            throw;
        }
    }

    public async Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs)
    {
        try
        {
            return await _repository.InsertBatchAsync(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert batch logs");
            throw;
        }
    }

    public async Task<PagedResult<LogEntry>> GetLogsAsync(LogQueryParams query)
    {
        try
        {
            return await _repository.GetPagedAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get logs");
            throw;
        }
    }

    public async Task<LogStats> GetStatsAsync()
    {
        try
        {
            return await _repository.GetStatsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stats");
            throw;
        }
    }

    public async Task<List<string>> GetAppNamesAsync()
    {
        try
        {
            return await _repository.GetAppNamesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get app names");
            throw;
        }
    }

    public async Task<List<string>> GetEnvironmentsAsync()
    {
        try
        {
            return await _repository.GetEnvironmentsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environments");
            throw;
        }
    }

    public async Task<List<LogPattern>> GetPatternsAsync(LogPatternQueryParams query)
    {
        try
        {
            return await _repository.GetPatternsAsync(query);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get patterns");
            throw;
        }
    }

    public async Task<TraceTimeline?> GetTraceTimelineAsync(string traceId)
    {
        try
        {
            return await _repository.GetTraceTimelineAsync(traceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get trace timeline for {TraceId}", traceId);
            throw;
        }
    }
}
