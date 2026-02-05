using LogForDev.Models;

namespace LogForDev.Data;

public interface ILogRepository
{
    Task<Guid> InsertAsync(LogEntry log);
    Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs);
    Task<PagedResult<LogEntry>> GetPagedAsync(LogQueryParams query);
    Task<long> CountAsync(LogQueryParams query);
    Task<LogStats> GetStatsAsync();
    Task<List<string>> GetAppNamesAsync();
    Task<List<string>> GetEnvironmentsAsync();
}
