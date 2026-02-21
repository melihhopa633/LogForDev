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

    public async Task<Guid> InsertAsync(LogEntry log, CancellationToken cancellationToken = default)
    {
        var projectId = log.ProjectId?.ToString() ?? "00000000-0000-0000-0000-000000000000";
        var sql = $@"
            INSERT INTO {TableName} (id, timestamp, level, app_name, message, metadata,
                exception_type, exception_message, exception_stacktrace, source,
                request_method, request_path, status_code, duration_ms, user_id,
                trace_id, span_id, host, environment, project_id, project_name)
            VALUES (
                '{log.Id}',
                now64(3),
                '{log.Level}',
                '{ClickHouseStringHelper.Escape(log.AppName)}',
                '{ClickHouseStringHelper.Escape(log.Message)}',
                '{ClickHouseStringHelper.Escape(log.Metadata ?? "{}")}',
                '{ClickHouseStringHelper.Escape(log.ExceptionType ?? "")}',
                '{ClickHouseStringHelper.Escape(log.ExceptionMessage ?? "")}',
                '{ClickHouseStringHelper.Escape(log.ExceptionStacktrace ?? "")}',
                '{ClickHouseStringHelper.Escape(log.Source ?? "")}',
                '{ClickHouseStringHelper.Escape(log.RequestMethod ?? "")}',
                '{ClickHouseStringHelper.Escape(log.RequestPath ?? "")}',
                {log.StatusCode},
                {log.DurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                '{ClickHouseStringHelper.Escape(log.UserId ?? "")}',
                '{ClickHouseStringHelper.Escape(log.TraceId ?? "")}',
                '{ClickHouseStringHelper.Escape(log.SpanId ?? "")}',
                '{ClickHouseStringHelper.Escape(log.Host ?? "")}',
                '{ClickHouseStringHelper.Escape(log.Environment)}',
                '{projectId}',
                '{ClickHouseStringHelper.Escape(log.ProjectName ?? "")}'
            )";

        await _clickHouse.ExecuteAsync(sql, cancellationToken: cancellationToken);
        return log.Id;
    }

    public async Task<int> InsertBatchAsync(IEnumerable<LogEntry> logs, CancellationToken cancellationToken = default)
    {
        var logList = logs.ToList();
        if (!logList.Any()) return 0;

        var sql = new StringBuilder();
        sql.AppendLine($"INSERT INTO {TableName} (id, timestamp, level, app_name, message, metadata, exception_type, exception_message, exception_stacktrace, source, request_method, request_path, status_code, duration_ms, user_id, trace_id, span_id, host, environment, project_id, project_name) VALUES");

        var values = logList.Select(log =>
        {
            var projectId = log.ProjectId?.ToString() ?? "00000000-0000-0000-0000-000000000000";
            return $"('{log.Id}', now64(3), '{log.Level}', '{ClickHouseStringHelper.Escape(log.AppName)}', '{ClickHouseStringHelper.Escape(log.Message)}', " +
                $"'{ClickHouseStringHelper.Escape(log.Metadata ?? "{}")}', " +
                $"'{ClickHouseStringHelper.Escape(log.ExceptionType ?? "")}', '{ClickHouseStringHelper.Escape(log.ExceptionMessage ?? "")}', '{ClickHouseStringHelper.Escape(log.ExceptionStacktrace ?? "")}', " +
                $"'{ClickHouseStringHelper.Escape(log.Source ?? "")}', '{ClickHouseStringHelper.Escape(log.RequestMethod ?? "")}', '{ClickHouseStringHelper.Escape(log.RequestPath ?? "")}', " +
                $"{log.StatusCode}, {log.DurationMs.ToString(System.Globalization.CultureInfo.InvariantCulture)}, '{ClickHouseStringHelper.Escape(log.UserId ?? "")}', " +
                $"'{ClickHouseStringHelper.Escape(log.TraceId ?? "")}', '{ClickHouseStringHelper.Escape(log.SpanId ?? "")}', " +
                $"'{ClickHouseStringHelper.Escape(log.Host ?? "")}', '{ClickHouseStringHelper.Escape(log.Environment)}', " +
                $"'{projectId}', '{ClickHouseStringHelper.Escape(log.ProjectName ?? "")}')";
        });

        sql.AppendLine(string.Join(",\n", values));

        await _clickHouse.ExecuteAsync(sql.ToString(), cancellationToken: cancellationToken);
        return logList.Count;
    }

    public async Task<PagedResult<LogEntry>> GetPagedAsync(LogQueryParams query, CancellationToken cancellationToken = default)
    {
        var queryBuilder = BuildQueryFromParams(query);

        var totalCount = await CountAsync(query, cancellationToken);

        var dataSql = queryBuilder
            .Select("id", "timestamp", "level", "app_name", "message", "metadata",
                "exception_type", "exception_message", "exception_stacktrace", "source",
                "request_method", "request_path", "status_code", "duration_ms", "user_id",
                "trace_id", "span_id", "host", "environment", "project_id", "project_name")
            .OrderByDesc("timestamp")
            .Paginate(query.Page, query.PageSize)
            .BuildSelect();

        var data = await _clickHouse.QueryAsync(dataSql, MapLogEntry, cancellationToken);

        return new PagedResult<LogEntry>
        {
            Data = data,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<long> CountAsync(LogQueryParams query, CancellationToken cancellationToken = default)
    {
        var queryBuilder = BuildQueryFromParams(query);
        var countSql = queryBuilder.BuildCount();
        var result = await _clickHouse.QueryAsync(countSql, r => Convert.ToInt64(r.GetValue(0)), cancellationToken);
        return result.FirstOrDefault();
    }

    public async Task<LogStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalSql = $"SELECT count() FROM {TableName} WHERE timestamp > now() - INTERVAL 24 HOUR";
        var totalLogs = await _clickHouse.QueryAsync(totalSql, r => Convert.ToInt64(r.GetValue(0)), cancellationToken);

        var errorSql = $"SELECT count() FROM {TableName} WHERE timestamp > now() - INTERVAL 24 HOUR AND level = 'Error'";
        var errorCount = await _clickHouse.QueryAsync(errorSql, r => Convert.ToInt64(r.GetValue(0)), cancellationToken);

        var warningSql = $"SELECT count() FROM {TableName} WHERE timestamp > now() - INTERVAL 24 HOUR AND level = 'Warning'";
        var warningCount = await _clickHouse.QueryAsync(warningSql, r => Convert.ToInt64(r.GetValue(0)), cancellationToken);

        var rpmSql = $"SELECT count() / 60.0 FROM {TableName} WHERE timestamp > now() - INTERVAL 1 HOUR";
        var logsPerMinute = await _clickHouse.QueryAsync(rpmSql, r => Convert.ToDouble(r.GetValue(0)), cancellationToken);

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
        }, cancellationToken);

        return new LogStats
        {
            TotalLogs = totalLogs.FirstOrDefault(),
            ErrorCount = errorCount.FirstOrDefault(),
            WarningCount = warningCount.FirstOrDefault(),
            LogsPerMinute = logsPerMinute.FirstOrDefault(),
            TopApps = topApps
        };
    }

    public async Task<List<string>> GetAppNamesAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT DISTINCT app_name FROM {TableName} ORDER BY app_name";
        return await _clickHouse.QueryAsync(sql, r => r.GetString(0), cancellationToken);
    }

    public async Task<List<string>> GetEnvironmentsAsync(CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT DISTINCT environment FROM {TableName} WHERE environment != '' ORDER BY environment";
        return await _clickHouse.QueryAsync(sql, r => r.GetString(0), cancellationToken);
    }

    public async Task<List<LogPattern>> GetPatternsAsync(LogPatternQueryParams query, CancellationToken cancellationToken = default)
    {
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
            sql += $" AND app_name = '{ClickHouseStringHelper.Escape(query.AppName)}'";
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
        }, cancellationToken);
    }

    public async Task<TraceTimeline?> GetTraceTimelineAsync(string traceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(traceId))
            return null;

        var sql = $@"
            SELECT id, timestamp, level, app_name, message, metadata, exception_type, source
            FROM {TableName}
            WHERE trace_id = '{ClickHouseStringHelper.Escape(traceId)}'
            ORDER BY timestamp ASC
            LIMIT 500";

        var logs = await _clickHouse.QueryAsync(sql, r => new TraceLogEntry
        {
            Id = r.GetGuid(0),
            Timestamp = r.GetDateTime(1),
            Level = Enum.Parse<Models.LogLevel>(r.GetString(2), true),
            AppName = r.GetString(3),
            Message = r.GetString(4),
            Metadata = r.IsDBNull(5) ? null : r.GetString(5),
            ExceptionType = r.IsDBNull(6) ? null : r.GetString(6),
            Source = r.IsDBNull(7) ? null : r.GetString(7)
        }, cancellationToken);

        if (!logs.Any())
            return null;

        var firstTimestamp = logs.First().Timestamp;
        var lastTimestamp = logs.Last().Timestamp;

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

    public async Task DeleteLogsAsync(int? olderThanDays = null, CancellationToken cancellationToken = default)
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
        await _clickHouse.ExecuteAsync(sql, cancellationToken: cancellationToken);
        _logger.LogInformation("Logs deleted: {Mode}", olderThanDays.HasValue ? $"older than {olderThanDays} days" : "all");
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

        if (!string.IsNullOrEmpty(query.ExceptionType))
        {
            builder.Where("exception_type", query.ExceptionType);
        }

        if (!string.IsNullOrEmpty(query.Source))
        {
            builder.Where("source", query.Source);
        }

        if (!string.IsNullOrEmpty(query.UserId))
        {
            builder.Where("user_id", query.UserId);
        }

        if (!string.IsNullOrEmpty(query.RequestMethod))
        {
            builder.Where("request_method", query.RequestMethod);
        }

        if (query.StatusCodeMin.HasValue)
        {
            builder.Where("status_code", ">=", query.StatusCodeMin.Value.ToString());
        }

        if (query.StatusCodeMax.HasValue)
        {
            builder.Where("status_code", "<=", query.StatusCodeMax.Value.ToString());
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
        // Column order: id(0), timestamp(1), level(2), app_name(3), message(4), metadata(5),
        // exception_type(6), exception_message(7), exception_stacktrace(8), source(9),
        // request_method(10), request_path(11), status_code(12), duration_ms(13), user_id(14),
        // trace_id(15), span_id(16), host(17), environment(18), project_id(19), project_name(20)
        var projectId = reader.IsDBNull(19) ? (Guid?)null : reader.GetGuid(19);
        if (projectId == Guid.Empty) projectId = null;

        return new LogEntry
        {
            Id = reader.GetGuid(0),
            Timestamp = reader.GetDateTime(1),
            Level = Enum.Parse<Models.LogLevel>(reader.GetString(2), true),
            AppName = reader.GetString(3),
            Message = reader.GetString(4),
            Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
            ExceptionType = reader.IsDBNull(6) ? null : reader.GetString(6),
            ExceptionMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
            ExceptionStacktrace = reader.IsDBNull(8) ? null : reader.GetString(8),
            Source = reader.IsDBNull(9) ? null : reader.GetString(9),
            RequestMethod = reader.IsDBNull(10) ? null : reader.GetString(10),
            RequestPath = reader.IsDBNull(11) ? null : reader.GetString(11),
            StatusCode = Convert.ToInt32(reader.GetValue(12)),
            DurationMs = Convert.ToDouble(reader.GetValue(13)),
            UserId = reader.IsDBNull(14) ? null : reader.GetString(14),
            TraceId = reader.IsDBNull(15) ? null : reader.GetString(15),
            SpanId = reader.IsDBNull(16) ? null : reader.GetString(16),
            Host = reader.IsDBNull(17) ? null : reader.GetString(17),
            Environment = reader.GetString(18),
            ProjectId = projectId,
            ProjectName = reader.IsDBNull(20) ? null : reader.GetString(20)
        };
    }
}
