using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using LogForDev.Core;
using LogForDev.Models;
using LogForDev.Services;
using LogForDev.Authentication;

namespace LogForDev.Controllers;

public class AuthController : Controller
{
    private readonly IUserService _userService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly LogForDevOptions _options;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<LogForDevOptions> logForDevOptions,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _dataProtectionProvider = dataProtectionProvider;
        _options = logForDevOptions.Value;
        _logger = logger;
    }

    [HttpGet("/login")]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect("/");

        ViewBag.TestMode = _options.TestMode;
        return View();
    }

    [HttpPost("/api/auth/login")]
    public async Task<IActionResult> LoginPost([FromBody] LoginRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.TotpCode))
            {
                return BadRequest(new { success = false, error = "Tüm alanları doldurunuz" });
            }

            // Validate credentials
            var user = await _userService.ValidateCredentialsAsync(
                request.Email,
                request.Password,
                request.TotpCode);

            if (user == null)
            {
                return BadRequest(new { success = false, error = "E-posta, şifre veya TOTP kodu hatalı" });
            }

            // Create encrypted cookie
            var cookieData = new
            {
                Email = user.Email,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            var protector = _dataProtectionProvider.CreateProtector(AppConstants.Auth.DataProtectorPurpose);
            var cookieJson = JsonSerializer.Serialize(cookieData);
            var encryptedCookie = protector.Protect(cookieJson);

            // Set cookie
            Response.Cookies.Append(CookieAuthenticationOptions.CookieName, encryptedCookie, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });

            _logger.LogInformation("User logged in successfully: {Email}", user.Email);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return StatusCode(500, new { success = false, error = "Bir hata oluştu" });
        }
    }

    [HttpPost("/api/auth/logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieAuthenticationOptions.CookieName);
        _logger.LogInformation("User logged out");
        return Ok(new { success = true });
    }

    [HttpPost("/api/auth/change-password")]
    [Microsoft.AspNetCore.Authorization.Authorize(AuthenticationSchemes = CookieAuthenticationOptions.Scheme)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { success = false, error = "Tüm alanları doldurunuz" });

        if (request.NewPassword.Length < 8)
            return BadRequest(new { success = false, error = "Yeni şifre en az 8 karakter olmalıdır" });

        var email = User.Identity?.Name;
        if (string.IsNullOrEmpty(email))
            return Unauthorized(new { success = false, error = "Oturum bulunamadı" });

        var ok = await _userService.ChangePasswordAsync(email, request.CurrentPassword, request.NewPassword);
        if (!ok)
            return BadRequest(new { success = false, error = "Mevcut şifre hatalı" });

        return Ok(new { success = true, message = "Şifre güncellendi" });
    }
}
