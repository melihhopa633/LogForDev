using System.Collections.Concurrent;
using LogForDev.Models;

namespace LogForDev.Services;

public interface ILogBufferService
{
    void Enqueue(LogEntry entry);
    void EnqueueBatch(IEnumerable<LogEntry> entries);
}

public class LogBufferService : BackgroundService, ILogBufferService
{
    private readonly ConcurrentQueue<LogEntry> _queue = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LogBufferService> _logger;

    private const int MaxBatchSize = 100;
    private const int FlushIntervalMs = 1000;

    public LogBufferService(IServiceScopeFactory scopeFactory, ILogger<LogBufferService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(LogEntry entry)
    {
        _queue.Enqueue(entry);
    }

    public void EnqueueBatch(IEnumerable<LogEntry> entries)
    {
        foreach (var entry in entries)
            _queue.Enqueue(entry);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Log buffer service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(FlushIntervalMs, stoppingToken);
                await FlushAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing log buffer");
            }
        }

        // Final flush on shutdown
        await FlushAsync();
        _logger.LogInformation("Log buffer service stopped");
    }

    private async Task FlushAsync()
    {
        if (_queue.IsEmpty) return;

        var batch = new List<LogEntry>(MaxBatchSize);
        while (batch.Count < MaxBatchSize && _queue.TryDequeue(out var entry))
        {
            batch.Add(entry);
        }

        if (batch.Count == 0) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var logService = scope.ServiceProvider.GetRequiredService<ILogService>();
            await logService.InsertBatchAsync(batch);
            _logger.LogDebug("Flushed {Count} logs to ClickHouse", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to flush {Count} logs, re-queuing", batch.Count);
            // Re-queue failed entries
            foreach (var entry in batch)
                _queue.Enqueue(entry);
        }
    }
}
