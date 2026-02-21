using System.Security.Cryptography;
using System.Data;
using ClickHouse.Client.ADO;
using LogForDev.Models;
using Microsoft.Extensions.Options;
using OtpNet;
using QRCoder;

namespace LogForDev.Services;

public interface IUserService
{
    Task<User> CreateUserAsync(string email, string password, string? connectionString = null);
    Task<User?> ValidateCredentialsAsync(string email, string password, string totpCode);
    Task<User?> GetUserByEmailAsync(string email);
    Task UpdateLastLoginAsync(Guid userId);
    Task IncrementFailedLoginAsync(Guid userId);
    string GenerateTotpSecret();
    string GenerateQrCodeDataUri(string email, string totpSecret);
    bool VerifyTotpCode(string secret, string code);
}

public class UserService : IUserService
{
    private readonly ClickHouseOptions _clickHouseOptions;
    private readonly LogForDevOptions _options;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IOptions<ClickHouseOptions> clickHouseOptions,
        IOptions<LogForDevOptions> logForDevOptions,
        ILogger<UserService> logger)
    {
        _clickHouseOptions = clickHouseOptions.Value;
        _options = logForDevOptions.Value;
        _logger = logger;
    }

    private ClickHouseConnection GetConnection(string? connectionString = null)
    {
        var connStr = connectionString ?? _clickHouseOptions.ConnectionString;
        return new ClickHouseConnection(connStr);
    }

    public async Task<User> CreateUserAsync(string email, string password, string? connectionString = null)
    {
        // Validate email
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new ArgumentException("Geçerli bir e-posta adresi giriniz");
        }

        // Validate password
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Şifre boş olamaz");
        }

        // Check if user already exists (skip during setup)
        if (connectionString == null)
        {
            var existingUser = await GetUserByEmailAsync(email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("Bu e-posta adresi zaten kullanılıyor");
            }
        }

        // Generate password hash
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 11);

        // Generate TOTP secret
        var totpSecret = GenerateTotpSecret();

        // Create user
        var userId = Guid.NewGuid();
        await using var connection = GetConnection(connectionString);
        await connection.OpenAsync();

        // ClickHouse doesn't support named parameters in INSERT VALUES, use string formatting
        var sql = $@"
            INSERT INTO users (id, email, password_hash, totp_secret, totp_enabled, created_at, failed_login_attempts)
            VALUES ('{userId}', '{email.Replace("'", "''")}', '{passwordHash.Replace("'", "''")}', '{totpSecret.Replace("'", "''")}', 1, now(), 0)";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();

        _logger.LogInformation("User created: {Email}", email);

        return new User
        {
            Id = userId,
            Email = email,
            PasswordHash = passwordHash,
            TotpSecret = totpSecret,
            TotpEnabled = true,
            CreatedAt = DateTime.UtcNow,
            FailedLoginAttempts = 0
        };
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password, string totpCode)
    {
        var user = await GetUserByEmailAsync(email);
        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Email}", email);
            return null;
        }

        // Check if account is locked
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            _logger.LogWarning("Login attempt for locked account: {Email}, locked until {LockedUntil}",
                email, user.LockedUntil.Value);
            return null;
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Invalid password for user: {Email}", email);
            await IncrementFailedLoginAsync(user.Id);
            return null;
        }

        // Verify TOTP code
        if (user.TotpEnabled && !VerifyTotpCode(user.TotpSecret, totpCode))
        {
            _logger.LogWarning("Invalid TOTP code for user: {Email}", email);
            await IncrementFailedLoginAsync(user.Id);
            return null;
        }

        // Reset failed attempts and update last login
        await UpdateLastLoginAsync(user.Id);

        _logger.LogInformation("Successful login for user: {Email}", email);
        return user;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync();

        var sql = $@"
            SELECT id, email, password_hash, totp_secret, totp_enabled,
                   created_at, last_login_at, failed_login_attempts, locked_until
            FROM users
            WHERE email = '{email.Replace("'", "''")}'
            LIMIT 1";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new User
        {
            Id = reader.GetGuid(0),
            Email = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            TotpSecret = reader.GetString(3),
            TotpEnabled = reader.GetByte(4) == 1,
            CreatedAt = reader.GetDateTime(5),
            LastLoginAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            FailedLoginAttempts = Convert.ToInt32(reader.GetValue(7)),
            LockedUntil = reader.IsDBNull(8) ? null : reader.GetDateTime(8)
        };
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync();

        var sql = $@"
            ALTER TABLE users
            UPDATE
                last_login_at = now(),
                failed_login_attempts = 0,
                locked_until = NULL
            WHERE id = '{userId}'";

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();

        _logger.LogDebug("Updated last login for user: {UserId}", userId);
    }

    public async Task IncrementFailedLoginAsync(Guid userId)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync();

        // Get current failed attempts
        int currentAttempts = 0;
        await using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.CommandText = $"SELECT failed_login_attempts FROM users WHERE id = '{userId}' LIMIT 1";
            var result = await selectCmd.ExecuteScalarAsync();
            if (result != null)
            {
                currentAttempts = Convert.ToInt32(result);
            }
        }

        var newAttempts = currentAttempts + 1;
        string lockedUntilValue = "NULL";

        // Lock account after 5 failed attempts
        if (newAttempts >= 5)
        {
            var lockedUntil = DateTime.UtcNow.AddMinutes(15);
            lockedUntilValue = $"'{lockedUntil:yyyy-MM-dd HH:mm:ss}'";
            _logger.LogWarning("Account locked due to failed login attempts: {UserId}, locked until {LockedUntil}",
                userId, lockedUntil);
        }

        var sql = $@"
            ALTER TABLE users
            UPDATE
                failed_login_attempts = {newAttempts},
                locked_until = {lockedUntilValue}
            WHERE id = '{userId}'";

        await using var updateCmd = connection.CreateCommand();
        updateCmd.CommandText = sql;
        await updateCmd.ExecuteNonQueryAsync();
    }

    public string GenerateTotpSecret()
    {
        // Generate 20 bytes (160 bits) of random data
        var secretBytes = new byte[20];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(secretBytes);
        }

        // Encode as Base32 for TOTP compatibility
        return Base32Encoding.ToString(secretBytes);
    }

    public string GenerateQrCodeDataUri(string email, string totpSecret)
    {
        // Create OTP Auth URI
        var otpAuthUri = $"otpauth://totp/LogForDev:{Uri.EscapeDataString(email)}?secret={totpSecret}&issuer=LogForDev";

        // Generate QR code
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeImage = qrCode.GetGraphic(20);

        // Convert to data URI
        var base64Image = Convert.ToBase64String(qrCodeImage);
        return $"data:image/png;base64,{base64Image}";
    }

    public bool VerifyTotpCode(string secret, string code)
    {
        try
        {
            if (_options.TestMode && code == "000000")
            {
                _logger.LogWarning("TOTP test bypass used — TestMode is enabled");
                return true;
            }

            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);

            // Verify with ±1 time step window (90 seconds total)
            return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying TOTP code");
            return false;
        }
    }
}
