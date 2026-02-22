using System.Security.Claims;
using System.Text.Encodings.Web;
using LogForDev.Core;
using LogForDev.Models;
using LogForDev.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LogForDev.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string Scheme = AppConstants.Auth.ApiKeyScheme;
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IProjectService _projectService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IProjectService projectService)
        : base(options, logger, encoder)
    {
        _projectService = projectService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Extract API key from header only (query string removed for security - keys leak into logs)
        string? apiKey = null;

        if (Request.Headers.TryGetValue("X-API-Key", out var headerKey))
            apiKey = headerKey.ToString();

        if (string.IsNullOrEmpty(apiKey))
            return AuthenticateResult.NoResult();

        // Validate against ProjectService
        var project = await _projectService.ValidateApiKeyAsync(apiKey);
        if (project == null)
            return AuthenticateResult.Fail("Invalid or expired API key");

        // Build claims principal with project info
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, project.Id.ToString()),
            new Claim(ClaimTypes.Name, project.Name),
            new Claim("ProjectId", project.Id.ToString()),
            new Claim("ProjectName", project.Name),
            new Claim(ClaimTypes.AuthenticationMethod, "apikey")
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.Scheme);
        var principal = new ClaimsPrincipal(identity);

        // Store the full Project object in HttpContext.Items for easy access
        Context.Items[AppConstants.Database.HttpContextProjectKey] = project;

        return AuthenticateResult.Success(
            new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { success = false, error = "Missing or invalid API key" }));
    }
}
