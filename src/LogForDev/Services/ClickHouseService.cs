using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;
using Microsoft.Extensions.Options;
using LogForDev.Models;

namespace LogForDev.Services;

public interface IClickHouseService
{
    Task InitializeAsync();
    Task<(bool Success, string? Error)> TestConnectionAsync();
    Task<ClickHouseConnection> GetConnectionAsync();
    Task ExecuteAsync(string sql, object? parameters = null);
    Task<List<T>> QueryAsync<T>(string sql, Func<System.Data.IDataReader, T> mapper);
}

public class ClickHouseService : IClickHouseService
{
    private readonly ClickHouseOptions _options;
    private readonly ILogger<ClickHouseService> _logger;

    public ClickHouseService(IOptions<ClickHouseOptions> options, ILogger<ClickHouseService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync()
    {
        try
        {
            await using var connection = new ClickHouseConnection(_options.DefaultConnectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ClickHouse connection test failed");
            return (false, ex.Message);
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Connect to default DB first to create our database
            await using var defaultConn = new ClickHouseConnection(_options.DefaultConnectionString);
            await defaultConn.OpenAsync();

            await using var createDbCmd = defaultConn.CreateCommand();
            createDbCmd.CommandText = $"CREATE DATABASE IF NOT EXISTS {_options.Database}";
            await createDbCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Database '{Database}' ensured on {Host}:{Port}", _options.Database, _options.Host, _options.Port);

            // Now connect to our database and create the table
            await using var connection = await GetConnectionAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS logs (
                    id UUID DEFAULT generateUUIDv4(),
                    timestamp DateTime64(3) DEFAULT now64(3),
                    level Enum8('Trace'=0, 'Debug'=1, 'Info'=2, 'Warning'=3, 'Error'=4, 'Fatal'=5),
                    app_name LowCardinality(String),
                    message String,
                    metadata String DEFAULT '{{}}'  ,
                    trace_id String DEFAULT '',
                    span_id String DEFAULT '',
                    host LowCardinality(String) DEFAULT '',
                    environment LowCardinality(String) DEFAULT 'production',
                    created_at DateTime DEFAULT now()
                )
                ENGINE = MergeTree()
                PARTITION BY toYYYYMM(timestamp)
                ORDER BY (app_name, level, timestamp)
                TTL created_at + INTERVAL 30 DAY
                SETTINGS index_granularity = 8192";
            await cmd.ExecuteNonQueryAsync();

            // Create app_logs table for internal application logs
            await using var appLogsCmd = connection.CreateCommand();
            appLogsCmd.CommandText = $@"
                CREATE TABLE IF NOT EXISTS app_logs (
                    id UUID DEFAULT generateUUIDv4(),
                    timestamp DateTime64(3) DEFAULT now64(3),
                    level LowCardinality(String),
                    category LowCardinality(String),
                    message String,
                    exception String DEFAULT '',
                    request_method LowCardinality(String) DEFAULT '',
                    request_path String DEFAULT '',
                    status_code UInt16 DEFAULT 0,
                    duration_ms Float64 DEFAULT 0,
                    created_at DateTime DEFAULT now()
                )
                ENGINE = MergeTree()
                PARTITION BY toYYYYMM(timestamp)
                ORDER BY (level, timestamp)
                TTL created_at + INTERVAL 30 DAY
                SETTINGS index_granularity = 8192";
            await appLogsCmd.ExecuteNonQueryAsync();

            _logger.LogInformation("ClickHouse initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ClickHouse");
            throw;
        }
    }

    public async Task<ClickHouseConnection> GetConnectionAsync()
    {
        var connection = new ClickHouseConnection(_options.ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task ExecuteAsync(string sql, object? parameters = null)
    {
        await using var connection = await GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<T>> QueryAsync<T>(string sql, Func<System.Data.IDataReader, T> mapper)
    {
        var results = new List<T>();
        await using var connection = await GetConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(mapper(reader));
        }

        return results;
    }
}
