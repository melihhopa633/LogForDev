using System.Text.Json.Serialization;

namespace LogForDev.Models;

public enum LogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5
}

public class LogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string AppName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? Host { get; set; }
    public string Environment { get; set; } = "production";
}

public class LogEntryRequest
{
    [JsonPropertyName("level")]
    public string Level { get; set; } = "info";
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
    
    [JsonPropertyName("appName")]
    public string AppName { get; set; } = string.Empty;
    
    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }
    
    [JsonPropertyName("traceId")]
    public string? TraceId { get; set; }
    
    [JsonPropertyName("spanId")]
    public string? SpanId { get; set; }
    
    [JsonPropertyName("host")]
    public string? Host { get; set; }
    
    [JsonPropertyName("environment")]
    public string? Environment { get; set; }

    public LogEntry ToLogEntry()
    {
        return new LogEntry
        {
            Level = Enum.TryParse<LogLevel>(Level, true, out var level) ? level : LogLevel.Info,
            Message = Message,
            AppName = AppName,
            Metadata = Metadata != null ? System.Text.Json.JsonSerializer.Serialize(Metadata) : null,
            TraceId = TraceId,
            SpanId = SpanId,
            Host = Host,
            Environment = Environment ?? "production"
        };
    }
}

public class BatchLogRequest
{
    [JsonPropertyName("logs")]
    public List<LogEntryRequest> Logs { get; set; } = new();
}

public class LogResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    [JsonPropertyName("id")]
    public Guid? Id { get; set; }
    
    [JsonPropertyName("count")]
    public int? Count { get; set; }
    
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class LogQueryParams
{
    public LogLevel? Level { get; set; }
    public string? AppName { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class PagedResult<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();

    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public class LogStats
{
    public long TotalLogs { get; set; }
    public long ErrorCount { get; set; }
    public long WarningCount { get; set; }
    public double LogsPerMinute { get; set; }
    public List<AppStats> TopApps { get; set; } = new();
}

public class AppStats
{
    public string AppName { get; set; } = string.Empty;
    public long LogCount { get; set; }
    public long ErrorCount { get; set; }
}

public class AppLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Information";
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public int StatusCode { get; set; }
    public double DurationMs { get; set; }
}

public class AppLogQueryParams
{
    public string? Level { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
