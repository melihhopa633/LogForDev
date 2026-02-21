using Microsoft.AspNetCore.Mvc;
using LogForDev.Services;
using LogForDev.Models;
using System.Security.Cryptography;

namespace LogForDev.Controllers;

public class SetupController : Controller
{
    private readonly ISetupStateService _setupState;
    private readonly ILogBufferService _buffer;
    private readonly ISetupOrchestrator _orchestrator;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SetupController> _logger;

    public SetupController(
        ISetupStateService setupState,
        ILogBufferService buffer,
        ISetupOrchestrator orchestrator,
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<SetupController> logger)
    {
        _setupState = setupState;
        _buffer = buffer;
        _orchestrator = orchestrator;
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
            var result = await _orchestrator.CompleteAsync(request, _env.ContentRootPath);
            if (!result.Success)
                return Ok(new { success = false, message = result.Message });

            return Ok(new
            {
                success = true,
                message = result.Message,
                qrCodeDataUri = result.QrCodeDataUri,
                totpSecret = result.TotpSecret
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete setup");
            return Ok(new { success = false, message = $"Kurulum tamamlanamadi: {ex.Message}" });
        }
    }
}
