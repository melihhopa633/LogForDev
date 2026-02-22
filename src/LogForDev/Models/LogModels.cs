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

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    [JsonIgnore]
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}

public class LogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string AppName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public string? ExceptionType { get; set; }
    public string? ExceptionMessage { get; set; }
    public string? ExceptionStacktrace { get; set; }
    public string? Source { get; set; }
    public string? RequestMethod { get; set; }
    public string? RequestPath { get; set; }
    public int StatusCode { get; set; }
    public double DurationMs { get; set; }
    public string? UserId { get; set; }
    public string? TraceId { get; set; }
    public string? SpanId { get; set; }
    public string? Host { get; set; }
    public string Environment { get; set; } = "production";
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
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

    [JsonPropertyName("exceptionType")]
    public string? ExceptionType { get; set; }

    [JsonPropertyName("exceptionMessage")]
    public string? ExceptionMessage { get; set; }

    [JsonPropertyName("exceptionStacktrace")]
    public string? ExceptionStacktrace { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("requestMethod")]
    public string? RequestMethod { get; set; }

    [JsonPropertyName("requestPath")]
    public string? RequestPath { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("durationMs")]
    public double DurationMs { get; set; }

    [JsonPropertyName("userId")]
    public string? UserId { get; set; }

    public LogEntry ToLogEntry()
    {
        return new LogEntry
        {
            Level = Enum.TryParse<LogLevel>(Level, true, out var level) ? level : LogLevel.Info,
            Message = Message,
            AppName = AppName,
            Metadata = Metadata != null ? System.Text.Json.JsonSerializer.Serialize(Metadata) : null,
            ExceptionType = ExceptionType,
            ExceptionMessage = ExceptionMessage,
            ExceptionStacktrace = ExceptionStacktrace,
            Source = Source,
            RequestMethod = RequestMethod,
            RequestPath = RequestPath,
            StatusCode = StatusCode,
            DurationMs = DurationMs,
            UserId = UserId,
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
    public string? Levels { get; set; }
    public string? AppName { get; set; }
    public string? Search { get; set; }
    public string? Environment { get; set; }
    public string? TraceId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ExceptionType { get; set; }
    public string? Source { get; set; }
    public string? UserId { get; set; }
    public string? RequestMethod { get; set; }
    public int? StatusCodeMin { get; set; }
    public int? StatusCodeMax { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    private int _page = 1;
    public int Page
    {
        get => _page;
        set => _page = Math.Max(value, 1);
    }

    private int _pageSize = 50;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, 500);
    }
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

    private int _page = 1;
    public int Page
    {
        get => _page;
        set => _page = Math.Max(value, 1);
    }

    private int _pageSize = 50;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Clamp(value, 1, 500);
    }
}

public class CreateProjectRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("expiryDays")]
    public int? ExpiryDays { get; set; }
}

public class UpdateProjectRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

// Log Aggregation Models
public class LogPattern
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("level")]
    public LogLevel Level { get; set; }

    [JsonPropertyName("appName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("firstSeen")]
    public DateTime FirstSeen { get; set; }

    [JsonPropertyName("lastSeen")]
    public DateTime LastSeen { get; set; }

    [JsonPropertyName("sampleMessage")]
    public string SampleMessage { get; set; } = string.Empty;
}

public class LogPatternQueryParams
{
    public LogLevel? Level { get; set; }
    public string? Levels { get; set; }
    public string? AppName { get; set; }
    public int Hours { get; set; } = 24;
    public int MinCount { get; set; } = 2;
    public int Limit { get; set; } = 50;
}

// Trace Correlation Models
public class TraceTimeline
{
    [JsonPropertyName("traceId")]
    public string TraceId { get; set; } = string.Empty;

    [JsonPropertyName("logs")]
    public List<TraceLogEntry> Logs { get; set; } = new();

    [JsonPropertyName("totalDurationMs")]
    public double TotalDurationMs { get; set; }

    [JsonPropertyName("services")]
    public List<string> Services { get; set; } = new();

    [JsonPropertyName("hasErrors")]
    public bool HasErrors { get; set; }
}

public class TraceLogEntry
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("level")]
    public LogLevel Level { get; set; }

    [JsonPropertyName("appName")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public string? Metadata { get; set; }

    [JsonPropertyName("exceptionType")]
    public string? ExceptionType { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("offsetMs")]
    public double OffsetMs { get; set; }
}
