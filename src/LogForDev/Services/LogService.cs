using LogForDev.Models;
using System.Text;

namespace LogForDev.Services;

public interface ILogService
{
    Task<Guid> InsertLogAsync(LogEntry log);
    Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs);
    Task<PagedResult<LogEntry>> GetLogsAsync(LogQueryParams query);
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

    public async Task<PagedResult<LogEntry>> GetLogsAsync(LogQueryParams query)
    {
        var where = new StringBuilder("WHERE 1=1");

        if (query.Level.HasValue)
            where.AppendLine($" AND level = '{query.Level.Value}'");

        if (!string.IsNullOrEmpty(query.AppName))
            where.AppendLine($" AND app_name = '{EscapeString(query.AppName)}'");

        if (!string.IsNullOrEmpty(query.Search))
            where.AppendLine($" AND message ILIKE '%{EscapeString(query.Search)}%'");

        if (query.From.HasValue)
            where.AppendLine($" AND timestamp >= '{query.From.Value:yyyy-MM-dd HH:mm:ss}'");

        if (query.To.HasValue)
            where.AppendLine($" AND timestamp <= '{query.To.Value:yyyy-MM-dd HH:mm:ss}'");

        var whereClause = where.ToString();

        // Count query
        var countSql = $"SELECT count() FROM logs {whereClause}";
        var countResult = await _clickHouse.QueryAsync(countSql, r => Convert.ToInt64(r.GetValue(0)));
        var totalCount = countResult.FirstOrDefault();

        // Data query
        var dataSql = $"SELECT id, timestamp, level, app_name, message, metadata, trace_id, span_id, host, environment FROM logs {whereClause} ORDER BY timestamp DESC LIMIT {query.PageSize} OFFSET {(query.Page - 1) * query.PageSize}";

        var data = await _clickHouse.QueryAsync(dataSql, reader => new LogEntry
        {
            Id = reader.GetGuid(0),
            Timestamp = reader.GetDateTime(1),
            Level = Enum.Parse<Models.LogLevel>(reader.GetString(2), true),
            AppName = reader.GetString(3),
            Message = reader.GetString(4),
            Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
            TraceId = reader.IsDBNull(6) ? null : reader.GetString(6),
            SpanId = reader.IsDBNull(7) ? null : reader.GetString(7),
            Host = reader.IsDBNull(8) ? null : reader.GetString(8),
            Environment = reader.GetString(9)
        });

        return new PagedResult<LogEntry>
        {
            Data = data,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<LogStats> GetStatsAsync()
    {
        // Total logs in last 24 hours
        var totalSql = "SELECT count() FROM logs WHERE timestamp > now() - INTERVAL 24 HOUR";
        var totalLogs = await _clickHouse.QueryAsync(totalSql, r => Convert.ToInt64(r.GetValue(0)));

        // Error count
        var errorSql = "SELECT count() FROM logs WHERE timestamp > now() - INTERVAL 24 HOUR AND level = 'Error'";
        var errorCount = await _clickHouse.QueryAsync(errorSql, r => Convert.ToInt64(r.GetValue(0)));

        // Warning count
        var warningSql = "SELECT count() FROM logs WHERE timestamp > now() - INTERVAL 24 HOUR AND level = 'Warning'";
        var warningCount = await _clickHouse.QueryAsync(warningSql, r => Convert.ToInt64(r.GetValue(0)));

        // Logs per minute (last hour)
        var rpmSql = "SELECT count() / 60.0 FROM logs WHERE timestamp > now() - INTERVAL 1 HOUR";
        var logsPerMinute = await _clickHouse.QueryAsync(rpmSql, r => Convert.ToDouble(r.GetValue(0)));

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
            LogCount = Convert.ToInt64(r.GetValue(1)),
            ErrorCount = Convert.ToInt64(r.GetValue(2))
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
