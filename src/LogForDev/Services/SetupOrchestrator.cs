using LogForDev.Models;
using System.Text.Json;

namespace LogForDev.Services;

public record SetupResult(bool Success, string Message, string? QrCodeDataUri = null, string? TotpSecret = null);

public interface ISetupOrchestrator
{
    Task<SetupResult> CompleteAsync(SetupCompleteRequest request, string contentRootPath);
}

public class SetupOrchestrator : ISetupOrchestrator
{
    private readonly IClickHouseService _clickHouseService;
    private readonly IProjectService _projectService;
    private readonly IUserService _userService;
    private readonly ISetupStateService _setupState;
    private readonly ILogger<SetupOrchestrator> _logger;

    public SetupOrchestrator(
        IClickHouseService clickHouseService,
        IProjectService projectService,
        IUserService userService,
        ISetupStateService setupState,
        ILogger<SetupOrchestrator> logger)
    {
        _clickHouseService = clickHouseService;
        _projectService = projectService;
        _userService = userService;
        _setupState = setupState;
        _logger = logger;
    }

    public async Task<SetupResult> CompleteAsync(SetupCompleteRequest request, string contentRootPath)
    {
        // Validate admin password strength before proceeding
        if (!string.IsNullOrEmpty(request.AdminPassword))
        {
            var pwError = ValidatePasswordStrength(request.AdminPassword);
            if (pwError != null)
                return new SetupResult(false, pwError);
        }

        await WriteAppSettingsAsync(request, contentRootPath);
        await InitializeDatabaseAsync(request);

        if (!string.IsNullOrEmpty(request.ProjectName) && !string.IsNullOrEmpty(request.ApiKey))
        {
            await CreateInitialProjectAsync(request);
        }

        var (qrCodeDataUri, totpSecret) = await CreateAdminUserAsync(request);
        if (qrCodeDataUri == null && !string.IsNullOrEmpty(request.AdminEmail))
        {
            return new SetupResult(false, "Admin kullanici olusturulamadi");
        }

        _setupState.CompleteSetup();

        return new SetupResult(true, "Kurulum tamamlandi!", qrCodeDataUri, totpSecret);
    }

    private async Task WriteAppSettingsAsync(SetupCompleteRequest request, string contentRootPath)
    {
        var appSettingsPath = Path.Combine(contentRootPath, "appsettings.json");
        var json = await File.ReadAllTextAsync(appSettingsPath);
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
                    writer.WriteNumber("RetentionDays", request.RetentionDays >= 0 ? request.RetentionDays : 30);
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
        await File.WriteAllTextAsync(appSettingsPath, updatedJson);
    }

    private async Task InitializeDatabaseAsync(SetupCompleteRequest request)
    {
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

            await _clickHouseService.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database initialization during setup completion failed, will retry on restart");
        }
    }

    private async Task CreateInitialProjectAsync(SetupCompleteRequest request)
    {
        try
        {
            int? expiryDays = request.KeyExpiryDays > 0 ? request.KeyExpiryDays : null;
            await _projectService.CreateProjectAsync(request.ProjectName!, request.ApiKey!, expiryDays);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create initial project during setup");
        }
    }

    private static string? ValidatePasswordStrength(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return "Sifre bos olamaz";
        if (password.Length < 12) return "Sifre en az 12 karakter olmalidir";
        if (!password.Any(char.IsUpper)) return "Sifre en az 1 buyuk harf icermelidir";
        if (!password.Any(char.IsLower)) return "Sifre en az 1 kucuk harf icermelidir";
        if (!password.Any(char.IsDigit)) return "Sifre en az 1 rakam icermelidir";
        if (!password.Any(c => !char.IsLetterOrDigit(c))) return "Sifre en az 1 ozel karakter icermelidir";
        return null;
    }

    private async Task<(string? QrCodeDataUri, string? TotpSecret)> CreateAdminUserAsync(SetupCompleteRequest request)
    {
        if (string.IsNullOrEmpty(request.AdminEmail) || string.IsNullOrEmpty(request.AdminPassword))
            return (null, null);

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

            var user = await _userService.CreateUserAsync(
                request.AdminEmail,
                request.AdminPassword,
                options.ConnectionString);

            var qrCodeDataUri = _userService.GenerateQrCodeDataUri(request.AdminEmail, user.TotpSecret);
            _logger.LogInformation("Admin user created during setup: {Email}", request.AdminEmail);
            return (qrCodeDataUri, user.TotpSecret);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create admin user during setup");
            return (null, null);
        }
    }
}
