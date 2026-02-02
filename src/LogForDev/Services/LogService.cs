using LogForDev.Models;
using System.Text;

namespace LogForDev.Services;

public interface ILogService
{
    Task<Guid> InsertLogAsync(LogEntry log);
    Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs);
    Task<List<LogEntry>> GetLogsAsync(LogQueryParams query);
    Task<LogStats> GetStatsAsync();
    Task<List<string>> GetAppNamesAsync();
}

public class LogService : ILogService
{
    private readonly IClickHouseService _clickHouse;
    private readonly ILogger<LogService> _logger;

    public LogService(IClickHouseService clickHouse, ILogger<LogService> logger)
    {
        _clickHouse = clickHouse;
        _logger = logger;
    }

    public async Task<Guid> InsertLogAsync(LogEntry log)
    {
        var sql = $@"
            INSERT INTO logs (id, timestamp, level, app_name, message, metadata, trace_id, span_id, host, environment)
            VALUES (
                '{log.Id}',
                now64(3),
                '{log.Level}',
                '{EscapeString(log.AppName)}',
                '{EscapeString(log.Message)}',
                '{EscapeString(log.Metadata ?? "{}")}',
                '{EscapeString(log.TraceId ?? "")}',
                '{EscapeString(log.SpanId ?? "")}',
                '{EscapeString(log.Host ?? "")}',
                '{EscapeString(log.Environment)}'
            )";

        await _clickHouse.ExecuteAsync(sql);
        return log.Id;
    }

    public async Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs)
    {
        var logList = logs.ToList();
        if (!logList.Any()) return 0;

        var sb = new StringBuilder();
        sb.AppendLine("INSERT INTO logs (id, timestamp, level, app_name, message, metadata, trace_id, span_id, host, environment) VALUES");
        
        var values = logList.Select(log => 
            $"('{log.Id}', now64(3), '{log.Level}', '{EscapeString(log.AppName)}', '{EscapeString(log.Message)}', " +
            $"'{EscapeString(log.Metadata ?? "{}")}', '{EscapeString(log.TraceId ?? "")}', '{EscapeString(log.SpanId ?? "")}', " +
            $"'{EscapeString(log.Host ?? "")}', '{EscapeString(log.Environment)}')");
        
        sb.AppendLine(string.Join(",\n", values));

        await _clickHouse.ExecuteAsync(sb.ToString());
        return logList.Count;
    }

    public async Task<List<LogEntry>> GetLogsAsync(LogQueryParams query)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SELECT id, timestamp, level, app_name, message, metadata, trace_id, span_id, host, environment FROM logs WHERE 1=1");

        if (query.Level.HasValue)
            sb.AppendLine($"AND level = '{query.Level.Value}'");
        
        if (!string.IsNullOrEmpty(query.AppName))
            sb.AppendLine($"AND app_name = '{EscapeString(query.AppName)}'");
        
        if (!string.IsNullOrEmpty(query.Search))
            sb.AppendLine($"AND message ILIKE '%{EscapeString(query.Search)}%'");
        
        if (query.From.HasValue)
            sb.AppendLine($"AND timestamp >= '{query.From.Value:yyyy-MM-dd HH:mm:ss}'");
        
        if (query.To.HasValue)
            sb.AppendLine($"AND timestamp <= '{query.To.Value:yyyy-MM-dd HH:mm:ss}'");

        sb.AppendLine("ORDER BY timestamp DESC");
        sb.AppendLine($"LIMIT {query.PageSize} OFFSET {(query.Page - 1) * query.PageSize}");

        return await _clickHouse.QueryAsync(sb.ToString(), reader => new LogEntry
        {
            Id = reader.GetGuid(0),
            Timestamp = reader.GetDateTime(1),
            Level = Enum.Parse<LogLevel>(reader.GetString(2), true),
            AppName = reader.GetString(3),
            Message = reader.GetString(4),
            Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
            TraceId = reader.IsDBNull(6) ? null : reader.GetString(6),
            SpanId = reader.IsDBNull(7) ? null : reader.GetString(7),
            Host = reader.IsDBNull(8) ? null : reader.GetString(8),
            Environment = reader.GetString(9)
        });
    }

    public async Task<LogStats> GetStatsAsync()
    {
        // Total logs in last 24 hours
        var totalSql = "SELECT count() FROM logs WHERE timestamp > now() - INTERVAL 24 HOUR";
        var totalLogs = await _clickHouse.QueryAsync(totalSql, r => r.GetInt64(0));

        // Error count
        var errorSql = "SELECT count() FROM logs WHERE timestamp > now() - INTERVAL 24 HOUR AND level = 'Error'";
        var errorCount = await _clickHouse.QueryAsync(errorSql, r => r.GetInt64(0));

        // Warning count
        var warningSql = "SELECT count() FROM logs WHERE timestamp > now() - INTERVAL 24 HOUR AND level = 'Warning'";
        var warningCount = await _clickHouse.QueryAsync(warningSql, r => r.GetInt64(0));

        // Logs per minute (last hour)
        var rpmSql = "SELECT count() / 60.0 FROM logs WHERE timestamp > now() - INTERVAL 1 HOUR";
        var logsPerMinute = await _clickHouse.QueryAsync(rpmSql, r => r.GetDouble(0));

        // Top apps
        var topAppsSql = @"
            SELECT app_name, count() as cnt, countIf(level = 'Error') as errors 
            FROM logs 
            WHERE timestamp > now() - INTERVAL 24 HOUR 
            GROUP BY app_name 
            ORDER BY cnt DESC 
            LIMIT 10";
        var topApps = await _clickHouse.QueryAsync(topAppsSql, r => new AppStats
        {
            AppName = r.GetString(0),
            LogCount = r.GetInt64(1),
            ErrorCount = r.GetInt64(2)
        });

        return new LogStats
        {
            TotalLogs = totalLogs.FirstOrDefault(),
            ErrorCount = errorCount.FirstOrDefault(),
            WarningCount = warningCount.FirstOrDefault(),
            LogsPerMinute = logsPerMinute.FirstOrDefault(),
            TopApps = topApps
        };
    }

    public async Task<List<string>> GetAppNamesAsync()
    {
        var sql = "SELECT DISTINCT app_name FROM logs ORDER BY app_name";
        return await _clickHouse.QueryAsync(sql, r => r.GetString(0));
    }

    private static string EscapeString(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");
    }
}
