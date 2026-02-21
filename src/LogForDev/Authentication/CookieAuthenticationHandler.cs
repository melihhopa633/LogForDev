using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using LogForDev.Core;
using LogForDev.Services;

namespace LogForDev.Authentication;

public class CookieAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = AppConstants.Auth.CookieScheme;
    public const string CookieName = AppConstants.Auth.CookieName;
}

public class CookieAuthenticationHandler : AuthenticationHandler<CookieAuthenticationOptions>
{
    private readonly IUserService _userService;
    private readonly IDataProtectionProvider _dataProtectionProvider;

    public CookieAuthenticationHandler(
        IOptionsMonitor<CookieAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserService userService,
        IDataProtectionProvider dataProtectionProvider)
        : base(options, logger, encoder)
    {
        _userService = userService;
        _dataProtectionProvider = dataProtectionProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if cookie exists
        if (!Request.Cookies.TryGetValue(CookieAuthenticationOptions.CookieName, out var encryptedCookie))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            // Decrypt cookie
            var protector = _dataProtectionProvider.CreateProtector(AppConstants.Auth.DataProtectorPurpose);
            var cookieJson = protector.Unprotect(encryptedCookie);
            var cookieData = JsonSerializer.Deserialize<CookieData>(cookieJson);

            if (cookieData == null || cookieData.ExpiresAt < DateTime.UtcNow)
            {
                return AuthenticateResult.Fail("Cookie expired");
            }

            // Validate user still exists and is not locked
            var user = await _userService.GetUserByEmailAsync(cookieData.Email);
            if (user == null)
            {
                return AuthenticateResult.Fail("User not found");
            }

            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                return AuthenticateResult.Fail("Account locked");
            }

            // Create claims
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Email)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to authenticate cookie");
            return AuthenticateResult.Fail("Invalid cookie");
        }
    }

    private class CookieData
    {
        public string Email { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
