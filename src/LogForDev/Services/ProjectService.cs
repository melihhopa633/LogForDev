using System.Collections.Concurrent;
using LogForDev.Models;

namespace LogForDev.Services;

public interface IProjectService
{
    Task<Project?> ValidateApiKeyAsync(string apiKey);
    Task<Project> CreateProjectAsync(string name, string apiKey, int? expiryDays = null);
    Task<List<Project>> GetAllProjectsAsync();
    Task<bool> DeleteProjectAsync(Guid projectId);
    Task RefreshCacheAsync();
}

public class ProjectService : IProjectService
{
    private readonly IClickHouseService _clickHouse;
    private readonly ILogger<ProjectService> _logger;
    private readonly ConcurrentDictionary<string, Project> _cache = new();
    private DateTime _lastRefresh = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

    public ProjectService(IClickHouseService clickHouse, ILogger<ProjectService> logger)
    {
        _clickHouse = clickHouse;
        _logger = logger;
    }

    public async Task<Project?> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            return null;

        // Check cache first
        if (_cache.TryGetValue(apiKey, out var cached))
            return cached.IsExpired ? null : cached;

        // If cache is stale, refresh
        if (DateTime.UtcNow - _lastRefresh > CacheExpiry)
        {
            await RefreshCacheAsync();
            if (_cache.TryGetValue(apiKey, out cached))
                return cached.IsExpired ? null : cached;
        }

        // Direct DB lookup as fallback (new key added recently)
        try
        {
            var projects = await _clickHouse.QueryAsync(
                $"SELECT id, name, api_key, created_at, expires_at FROM projects WHERE api_key = '{EscapeString(apiKey)}' LIMIT 1",
                MapProject);

            var project = projects.FirstOrDefault();
            if (project != null)
            {
                _cache[apiKey] = project;
                return project.IsExpired ? null : project;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate API key from DB");
            return null;
        }
    }

    public async Task<Project> CreateProjectAsync(string name, string apiKey, int? expiryDays = null)
    {
        DateTime? expiresAt = expiryDays.HasValue && expiryDays.Value > 0
            ? DateTime.UtcNow.AddDays(expiryDays.Value)
            : null;

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = name,
            ApiKey = apiKey,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        var expiresAtSql = expiresAt.HasValue
            ? $"'{expiresAt.Value:yyyy-MM-dd HH:mm:ss}'"
            : "NULL";

        var sql = $@"INSERT INTO projects (id, name, api_key, created_at, expires_at)
                     VALUES ('{project.Id}', '{EscapeString(project.Name)}', '{EscapeString(project.ApiKey)}', now(), {expiresAtSql})";

        await _clickHouse.ExecuteAsync(sql);
        _cache[apiKey] = project;

        _logger.LogInformation("Created project '{Name}' with ID {Id}, expires: {Expires}",
            name, project.Id, expiresAt?.ToString("yyyy-MM-dd") ?? "never");
        return project;
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        try
        {
            return await _clickHouse.QueryAsync(
                "SELECT id, name, api_key, created_at, expires_at FROM projects ORDER BY created_at",
                MapProject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get projects from DB");
            return new List<Project>();
        }
    }

    public async Task<bool> DeleteProjectAsync(Guid projectId)
    {
        try
        {
            var sql = $"ALTER TABLE projects DELETE WHERE id = '{projectId}'";
            await _clickHouse.ExecuteAsync(sql);

            // Remove from cache
            var cached = _cache.FirstOrDefault(kvp => kvp.Value.Id == projectId);
            if (cached.Key != null)
            {
                _cache.TryRemove(cached.Key, out _);
            }

            _logger.LogInformation("Deleted project {ProjectId}", projectId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete project {ProjectId}", projectId);
            return false;
        }
    }

    public async Task RefreshCacheAsync()
    {
        try
        {
            var projects = await _clickHouse.QueryAsync(
                "SELECT id, name, api_key, created_at, expires_at FROM projects",
                MapProject);

            _cache.Clear();
            foreach (var p in projects)
            {
                _cache[p.ApiKey] = p;
            }
            _lastRefresh = DateTime.UtcNow;
            _logger.LogDebug("Project cache refreshed with {Count} projects", projects.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh project cache");
        }
    }

    private static Project MapProject(System.Data.IDataReader reader)
    {
        DateTime? expiresAt = null;
        if (!reader.IsDBNull(4))
        {
            expiresAt = reader.GetDateTime(4);
        }

        return new Project
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            ApiKey = reader.GetString(2),
            CreatedAt = reader.GetDateTime(3),
            ExpiresAt = expiresAt
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
}
