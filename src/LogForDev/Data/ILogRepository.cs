using LogForDev.Models;

namespace LogForDev.Data;

public interface ILogRepository
{
    Task<Guid> InsertAsync(LogEntry log, CancellationToken cancellationToken = default);
    Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs, CancellationToken cancellationToken = default);
    Task<PagedResult<LogEntry>> GetPagedAsync(LogQueryParams query, CancellationToken cancellationToken = default);
    Task<long> CountAsync(LogQueryParams query, CancellationToken cancellationToken = default);
    Task<LogStats> GetStatsAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetAppNamesAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetEnvironmentsAsync(CancellationToken cancellationToken = default);
    Task<List<LogPattern>> GetPatternsAsync(LogPatternQueryParams query, CancellationToken cancellationToken = default);
    Task<TraceTimeline?> GetTraceTimelineAsync(string traceId, CancellationToken cancellationToken = default);
    Task DeleteLogsAsync(int? olderThanDays = null, CancellationToken cancellationToken = default);
}
