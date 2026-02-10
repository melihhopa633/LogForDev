using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LogForDev.Services;
using LogForDev.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace LogForDev.Controllers;

public class SetupController : Controller
{
    private readonly ISetupStateService _setupState;
    private readonly IClickHouseService _clickHouseService;
    private readonly ILogBufferService _buffer;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SetupController> _logger;

    public SetupController(
        ISetupStateService setupState,
        IClickHouseService clickHouseService,
        ILogBufferService buffer,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<SetupController> logger)
    {
        _setupState = setupState;
        _clickHouseService = clickHouseService;
        _buffer = buffer;
        _configuration = configuration;
        _env = env;
        _logger = logger;
    }

    [HttpGet("/setup")]
    public IActionResult Index()
    {
        ViewBag.ClickHouseHost = _configuration["ClickHouse:Host"] ?? "localhost";
        ViewBag.ClickHousePort = _configuration["ClickHouse:Port"] ?? "8123";
        ViewBag.ClickHouseDatabase = _configuration["ClickHouse:Database"] ?? "logfordev";
        ViewBag.ClickHouseUsername = _configuration["ClickHouse:Username"] ?? "";
        ViewBag.ClickHousePassword = _configuration["ClickHouse:Password"] ?? "";
        ViewBag.ApiKey = _configuration["LogForDev:ApiKey"] ?? "change-me";
        ViewBag.RetentionDays = _configuration["LogForDev:RetentionDays"] ?? "30";
        ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";

        return View();
    }

    [HttpPost("/api/setup/test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] ConnectionTestRequest request)
    {
        try
        {
            var options = new ClickHouseOptions
            {
                Host = request.Host ?? "localhost",
                Port = request.Port > 0 ? request.Port : 8123,
                Database = request.Database ?? "logfordev",
                Username = request.Username,
                Password = request.Password
            };

            using var connection = new ClickHouse.Client.ADO.ClickHouseConnection(options.DefaultConnectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();

            return Ok(new { success = true, message = "Baglanti basarili!" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Setup connection test failed");
            return Ok(new { success = false, message = $"Baglanti hatasi: {ex.Message}" });
        }
    }

    [HttpPost("/api/setup/generate-key")]
    public IActionResult GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var key = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 32);

        return Ok(new { key = $"lfd_{key}" });
    }

    [HttpPost("/api/setup/send-test-log")]
    public IActionResult SendTestLog([FromBody] TestLogRequest request)
    {
        try
        {
            var logEntry = new LogEntry
            {
                Level = Models.LogLevel.Info,
                Message = "Setup wizard test log - LogForDev is working!",
                AppName = "logfordev-setup",
                Environment = "production",
                Host = "setup-wizard"
            };

            _buffer.Enqueue(logEntry);

            return Ok(new { success = true, message = "Test logu gonderildi!", id = logEntry.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test log");
            return Ok(new { success = false, message = $"Test logu gonderilemedi: {ex.Message}" });
        }
    }

    [HttpPost("/api/setup/complete")]
    public async Task<IActionResult> Complete([FromBody] SetupCompleteRequest request)
    {
        try
        {
            // Update appsettings.json
            var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");
            var json = await System.IO.File.ReadAllTextAsync(appSettingsPath);
            var doc = JsonDocument.Parse(json);

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    if (property.Name == "ClickHouse")
                    {
                        writer.WritePropertyName("ClickHouse");
                        writer.WriteStartObject();
                        writer.WriteString("Host", request.ClickHouseHost ?? "localhost");
                        writer.WriteNumber("Port", request.ClickHousePort > 0 ? request.ClickHousePort : 8123);
                        writer.WriteString("Database", request.ClickHouseDatabase ?? "logfordev");
                        writer.WriteString("Username", request.ClickHouseUsername ?? "");
                        writer.WriteString("Password", request.ClickHousePassword ?? "");
                        writer.WriteEndObject();
                    }
                    else if (property.Name == "LogForDev")
                    {
                        writer.WritePropertyName("LogForDev");
                        writer.WriteStartObject();
                        writer.WriteString("ApiKey", request.ApiKey ?? "change-me");
                        writer.WriteNumber("RetentionDays", request.RetentionDays > 0 ? request.RetentionDays : 30);
                        writer.WriteEndObject();
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }
                writer.WriteEndObject();
            }

            var updatedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            await System.IO.File.WriteAllTextAsync(appSettingsPath, updatedJson);

            // Initialize database with new settings
            try
            {
                var options = new ClickHouseOptions
                {
                    Host = request.ClickHouseHost ?? "localhost",
                    Port = request.ClickHousePort > 0 ? request.ClickHousePort : 8123,
                    Database = request.ClickHouseDatabase ?? "logfordev",
                    Username = request.ClickHouseUsername,
                    Password = request.ClickHousePassword
                };

                using var connection = new ClickHouse.Client.ADO.ClickHouseConnection(options.DefaultConnectionString);
                await connection.OpenAsync();

                using var createDbCmd = connection.CreateCommand();
                createDbCmd.CommandText = $"CREATE DATABASE IF NOT EXISTS {options.Database}";
                await createDbCmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database initialization during setup completion failed, will retry on restart");
            }

            // Mark setup as complete
            _setupState.CompleteSetup();

            return Ok(new { success = true, message = "Kurulum tamamlandi!" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete setup");
            return Ok(new { success = false, message = $"Kurulum tamamlanamadi: {ex.Message}" });
        }
    }
}

public class ConnectionTestRequest
{
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Database { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class TestLogRequest
{
    public string? ApiKey { get; set; }
}

public class SetupCompleteRequest
{
    public string? ClickHouseHost { get; set; }
    public int ClickHousePort { get; set; }
    public string? ClickHouseDatabase { get; set; }
    public string? ClickHouseUsername { get; set; }
    public string? ClickHousePassword { get; set; }
    public string? ApiKey { get; set; }
    public int RetentionDays { get; set; }
}
