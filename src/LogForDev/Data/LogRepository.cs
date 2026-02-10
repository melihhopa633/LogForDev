using LogForDev.Models;
using LogForDev.Services;
using System.Text;

namespace LogForDev.Data;

public class LogRepository : ILogRepository
{
    private readonly IClickHouseService _clickHouse;
    private readonly ILogger<LogRepository> _logger;
    private const string TableName = "logs";

    public LogRepository(IClickHouseService clickHouse, ILogger<LogRepository> logger)
    {
        _clickHouse = clickHouse;
        _logger = logger;
    }

    public async Task<Guid> InsertAsync(LogEntry log)
    {
        var projectId = log.ProjectId?.ToString() ?? "00000000-0000-0000-0000-000000000000";
        var sql = $@"
            INSERT INTO {TableName} (id, timestamp, level, app_name, message, metadata, trace_id, span_id, host, environment, project_id, project_name)
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
                '{EscapeString(log.Environment)}',
                '{projectId}',
                '{EscapeString(log.ProjectName ?? "")}'
            )";

        await _clickHouse.ExecuteAsync(sql);
        return log.Id;
    }

    public async Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs)
    {
        var logList = logs.ToList();
        if (!logList.Any()) return 0;

        var sql = new StringBuilder();
        sql.AppendLine($"INSERT INTO {TableName} (id, timestamp, level, app_name, message, metadata, trace_id, span_id, host, environment, project_id, project_name) VALUES");

        var values = logList.Select(log =>
        {
            var projectId = log.ProjectId?.ToString() ?? "00000000-0000-0000-0000-000000000000";
            return $"('{log.Id}', now64(3), '{log.Level}', '{EscapeString(log.AppName)}', '{EscapeString(log.Message)}', " +
                $"'{EscapeString(log.Metadata ?? "{}")}', '{EscapeString(log.TraceId ?? "")}', '{EscapeString(log.SpanId ?? "")}', " +
                $"'{EscapeString(log.Host ?? "")}', '{EscapeString(log.Environment)}', " +
                $"'{projectId}', '{EscapeString(log.ProjectName ?? "")}')";
        });

        sql.AppendLine(string.Join(",\n", values));

        await _clickHouse.ExecuteAsync(sql.ToString());
        return logList.Count;
    }

    public async Task<PagedResult<LogEntry>> GetPagedAsync(LogQueryParams query)
    {
        var queryBuilder = BuildQueryFromParams(query);

        // Count query
        var totalCount = await CountAsync(query);

        // Data query
        var dataSql = queryBuilder
            .Select("id", "timestamp", "level", "app_name", "message", "metadata", "trace_id", "span_id", "host", "environment", "project_id", "project_name")
            .OrderByDesc("timestamp")
            .Paginate(query.Page, query.PageSize)
            .BuildSelect();

        var data = await _clickHouse.QueryAsync(dataSql, MapLogEntry);

        return new PagedResult<LogEntry>
        {
            Data = data,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<long> CountAsync(LogQueryParams query)
    {
        var queryBuilder = BuildQueryFromParams(query);
        var countSql = queryBuilder.BuildCount();
        var result = await _clickHouse.QueryAsync(countSql, r => Convert.ToInt64(r.GetValue(0)));
        return result.FirstOrDefault();
    }

    public async Task<LogStats> GetStatsAsync()
    {
        // Total logs in last 24 hours
        var totalSql = $"SELECT count() FROM {TableName} WHERE timestamp > now() - INTERVAL 24 HOUR";
        var totalLogs = await _clickHouse.QueryAsync(totalSql, r => Convert.ToInt64(r.GetValue(0)));

        // Error count
        var errorSql = $"SELECT count() FROM {TableName} WHERE timestamp > now() - INTERVAL 24 HOUR AND level = 'Error'";
        var errorCount = await _clickHouse.QueryAsync(errorSql, r => Convert.ToInt64(r.GetValue(0)));

        // Warning count
        var warningSql = $"SELECT count() FROM {TableName} WHERE timestamp > now() - INTERVAL 24 HOUR AND level = 'Warning'";
        var warningCount = await _clickHouse.QueryAsync(warningSql, r => Convert.ToInt64(r.GetValue(0)));

        // Logs per minute (last hour)
        var rpmSql = $"SELECT count() / 60.0 FROM {TableName} WHERE timestamp > now() - INTERVAL 1 HOUR";
        var logsPerMinute = await _clickHouse.QueryAsync(rpmSql, r => Convert.ToDouble(r.GetValue(0)));

        // Top apps
        var topAppsSql = $@"
            SELECT app_name, count() as cnt, countIf(level = 'Error') as errors
            FROM {TableName}
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
        var sql = $"SELECT DISTINCT app_name FROM {TableName} ORDER BY app_name";
        return await _clickHouse.QueryAsync(sql, r => r.GetString(0));
    }

    public async Task<List<string>> GetEnvironmentsAsync()
    {
        var sql = $"SELECT DISTINCT environment FROM {TableName} WHERE environment != '' ORDER BY environment";
        return await _clickHouse.QueryAsync(sql, r => r.GetString(0));
    }

    private ClickHouseQueryBuilder BuildQueryFromParams(LogQueryParams query)
    {
        var builder = new ClickHouseQueryBuilder(TableName);

        if (query.Level.HasValue)
        {
            builder.Where("level", query.Level.Value.ToString());
        }

        if (!string.IsNullOrEmpty(query.Levels))
        {
            var levelList = query.Levels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            builder.WhereIn("level", levelList);
        }

        if (!string.IsNullOrEmpty(query.AppName))
        {
            builder.Where("app_name", query.AppName);
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            builder.WhereLike("message", query.Search);
        }

        if (!string.IsNullOrEmpty(query.Environment))
        {
            builder.Where("environment", query.Environment);
        }

        if (!string.IsNullOrEmpty(query.TraceId))
        {
            builder.Where("trace_id", query.TraceId);
        }

        if (query.ProjectId.HasValue)
        {
            builder.Where("project_id", query.ProjectId.Value.ToString());
        }

        if (query.From.HasValue && query.To.HasValue)
        {
            builder.WhereBetween("timestamp",
                query.From.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                query.To.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        else
        {
            if (query.From.HasValue)
            {
                builder.Where("timestamp", ">=", query.From.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            if (query.To.HasValue)
            {
                builder.Where("timestamp", "<=", query.To.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            }
        }

        return builder;
    }

    private static LogEntry MapLogEntry(System.Data.IDataReader reader)
    {
        var projectId = reader.IsDBNull(10) ? (Guid?)null : reader.GetGuid(10);
        if (projectId == Guid.Empty) projectId = null;

        return new LogEntry
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
            Environment = reader.GetString(9),
            ProjectId = projectId,
            ProjectName = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }

    private static string EscapeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");
    }

    public async Task<List<LogPattern>> GetPatternsAsync(LogPatternQueryParams query)
    {
        // ClickHouse aggregation query to find similar log patterns
        // Groups by: first 50 chars of message (normalized) + level + app_name
        var sql = $@"
            SELECT
                replaceRegexpAll(substring(message, 1, 100), '[0-9]+', '*') as pattern,
                count() as cnt,
                level,
                app_name,
                min(timestamp) as first_seen,
                max(timestamp) as last_seen,
                any(message) as sample_message
            FROM {TableName}
            WHERE timestamp > now() - INTERVAL {query.Hours} HOUR";

        if (query.Level.HasValue)
        {
            sql += $" AND level = '{query.Level.Value}'";
        }

        if (!string.IsNullOrEmpty(query.Levels))
        {
            var levelList = query.Levels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var quotedLevels = string.Join(",", levelList.Select(l => $"'{l}'"));
            sql += $" AND level IN ({quotedLevels})";
        }

        if (!string.IsNullOrEmpty(query.AppName))
        {
            sql += $" AND app_name = '{EscapeString(query.AppName)}'";
        }

        sql += $@"
            GROUP BY pattern, level, app_name
            HAVING cnt >= {query.MinCount}
            ORDER BY cnt DESC
            LIMIT {query.Limit}";

        return await _clickHouse.QueryAsync(sql, r => new LogPattern
        {
            Pattern = r.GetString(0),
            Count = Convert.ToInt64(r.GetValue(1)),
            Level = Enum.Parse<Models.LogLevel>(r.GetString(2), true),
            AppName = r.GetString(3),
            FirstSeen = r.GetDateTime(4),
            LastSeen = r.GetDateTime(5),
            SampleMessage = r.GetString(6)
        });
    }

    public async Task<TraceTimeline?> GetTraceTimelineAsync(string traceId)
    {
        if (string.IsNullOrEmpty(traceId))
            return null;

        var sql = $@"
            SELECT id, timestamp, level, app_name, message, metadata
            FROM {TableName}
            WHERE trace_id = '{EscapeString(traceId)}'
            ORDER BY timestamp ASC
            LIMIT 500";

        var logs = await _clickHouse.QueryAsync(sql, r => new TraceLogEntry
        {
            Id = r.GetGuid(0),
            Timestamp = r.GetDateTime(1),
            Level = Enum.Parse<Models.LogLevel>(r.GetString(2), true),
            AppName = r.GetString(3),
            Message = r.GetString(4),
            Metadata = r.IsDBNull(5) ? null : r.GetString(5)
        });

        if (!logs.Any())
            return null;

        var firstTimestamp = logs.First().Timestamp;
        var lastTimestamp = logs.Last().Timestamp;

        // Calculate offset from first log
        foreach (var log in logs)
        {
            log.OffsetMs = (log.Timestamp - firstTimestamp).TotalMilliseconds;
        }

        return new TraceTimeline
        {
            TraceId = traceId,
            Logs = logs,
            TotalDurationMs = (lastTimestamp - firstTimestamp).TotalMilliseconds,
            Services = logs.Select(l => l.AppName).Distinct().ToList(),
            HasErrors = logs.Any(l => l.Level >= Models.LogLevel.Error)
        };
    }

    public async Task DeleteLogsAsync(int? olderThanDays = null)
    {
        string sql;
        if (olderThanDays.HasValue && olderThanDays.Value > 0)
        {
            sql = $"ALTER TABLE {TableName} DELETE WHERE timestamp < now() - INTERVAL {olderThanDays.Value} DAY";
        }
        else
        {
            sql = $"TRUNCATE TABLE {TableName}";
        }
        await _clickHouse.ExecuteAsync(sql);
        _logger.LogInformation("Logs deleted: {Mode}", olderThanDays.HasValue ? $"older than {olderThanDays} days" : "all");
    }
}
