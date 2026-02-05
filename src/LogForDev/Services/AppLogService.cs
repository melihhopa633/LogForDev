using LogForDev.Models;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace LogForDev.Services;

public interface IAppLogService
{
    void Enqueue(AppLogEntry log);
    Task<PagedResult<AppLogEntry>> GetLogsAsync(AppLogQueryParams query);
}

public class AppLogService : BackgroundService, IAppLogService
{
    private readonly IClickHouseService _clickHouse;
    private readonly ILogger<AppLogService> _logger;
    private readonly ConcurrentQueue<AppLogEntry> _queue = new();
    private const int MaxBatchSize = 50;
    private const int FlushIntervalMs = 2000;

    public AppLogService(IClickHouseService clickHouse, ILogger<AppLogService> logger)
    {
        _clickHouse = clickHouse;
        _logger = logger;
    }

    public void Enqueue(AppLogEntry log) => _queue.Enqueue(log);

    public async Task<PagedResult<AppLogEntry>> GetLogsAsync(AppLogQueryParams query)
    {
        var where = new StringBuilder("WHERE 1=1");

        if (!string.IsNullOrEmpty(query.Level))
            where.AppendLine($" AND level = '{EscapeString(query.Level)}'");

        if (!string.IsNullOrEmpty(query.Search))
            where.AppendLine($" AND message ILIKE '%{EscapeString(query.Search)}%'");

        if (query.From.HasValue)
            where.AppendLine($" AND timestamp >= '{query.From.Value:yyyy-MM-dd HH:mm:ss}'");

        if (query.To.HasValue)
            where.AppendLine($" AND timestamp <= '{query.To.Value:yyyy-MM-dd HH:mm:ss}'");

        var whereClause = where.ToString();

        var countSql = $"SELECT count() FROM app_logs {whereClause}";
        var countResult = await _clickHouse.QueryAsync(countSql, r => Convert.ToInt64(r.GetValue(0)));
        var totalCount = countResult.FirstOrDefault();

        var dataSql = $@"SELECT id, timestamp, level, category, message, exception,
            request_method, request_path, status_code, duration_ms
            FROM app_logs {whereClause}
            ORDER BY timestamp DESC
            LIMIT {query.PageSize} OFFSET {(query.Page - 1) * query.PageSize}";

        var data = await _clickHouse.QueryAsync(dataSql, r => new AppLogEntry
        {
            Id = r.GetGuid(0),
            Timestamp = r.GetDateTime(1),
            Level = r.GetString(2),
            Category = r.GetString(3),
            Message = r.GetString(4),
            Exception = r.IsDBNull(5) ? null : r.GetString(5),
            RequestMethod = r.IsDBNull(6) ? null : r.GetString(6),
            RequestPath = r.IsDBNull(7) ? null : r.GetString(7),
            StatusCode = Convert.ToInt32(r.GetValue(8)),
            DurationMs = Convert.ToDouble(r.GetValue(9))
        });

        return new PagedResult<AppLogEntry>
        {
            Data = data,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(FlushIntervalMs, stoppingToken);
            await FlushAsync();
        }
        await FlushAsync();
    }

    private async Task FlushAsync()
    {
        if (_queue.IsEmpty) return;

        var batch = new List<AppLogEntry>();
        while (batch.Count < MaxBatchSize && _queue.TryDequeue(out var log))
            batch.Add(log);

        if (batch.Count == 0) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("INSERT INTO app_logs (id, timestamp, level, category, message, exception, request_method, request_path, status_code, duration_ms) VALUES");

            var values = batch.Select(log =>
                $"('{log.Id}', now64(3), '{EscapeString(log.Level)}', '{EscapeString(log.Category)}', " +
                $"'{EscapeString(log.Message)}', '{EscapeString(log.Exception ?? "")}', " +
                $"'{EscapeString(log.RequestMethod ?? "")}', '{EscapeString(log.RequestPath ?? "")}', " +
                $"{log.StatusCode}, {log.DurationMs.ToString("F2", CultureInfo.InvariantCulture)})");

            sb.AppendLine(string.Join(",\n", values));
            await _clickHouse.ExecuteAsync(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} app logs", batch.Count);
            foreach (var log in batch)
                _queue.Enqueue(log);
        }
    }

    private static string EscapeString(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");
    }
}
