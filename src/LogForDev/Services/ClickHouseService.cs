using ClickHouse.Client.ADO;
using ClickHouse.Client.Utility;
using Microsoft.Extensions.Options;
using LogForDev.Models;

namespace LogForDev.Services;

public interface IClickHouseService
{
    Task InitializeAsync();
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

    public async Task InitializeAsync()
    {
        try
        {
            await using var connection = await GetConnectionAsync();
            _logger.LogInformation("ClickHouse connection established to {Host}:{Port}", _options.Host, _options.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to ClickHouse");
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
