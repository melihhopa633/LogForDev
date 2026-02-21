using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using LogForDev.Models;
using LogForDev.Services;
using LogForDev.Authentication;

namespace LogForDev.Controllers;

public class AuthController : Controller
{
    private readonly IUserService _userService;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserService userService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    [HttpGet("/login")]
    public IActionResult Login()
    {
        // If already authenticated, redirect to home
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect("/");
        }

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

            var protector = _dataProtectionProvider.CreateProtector("Auth.Cookie");
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
        // Delete cookie
        Response.Cookies.Delete(CookieAuthenticationOptions.CookieName);

        _logger.LogInformation("User logged out");

        return Ok(new { success = true });
    }
}
