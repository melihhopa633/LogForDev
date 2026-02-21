using LogForDev.Core;

namespace LogForDev.Middleware;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        AppConstants.Paths.Login,
        AppConstants.Paths.ApiAuth + "/login",
        AppConstants.Paths.ApiAuth + "/logout",
        AppConstants.Paths.Setup,
        AppConstants.Paths.ApiSetup
    };

    public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Allow public paths
        if (IsPublicPath(path))
        {
            await _next(context);
            return;
        }

        // Allow static files
        if (path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Check if user is authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            // Redirect to login for HTML requests
            if (context.Request.Headers["Accept"].ToString().Contains("text/html"))
            {
                context.Response.Redirect("/login");
                return;
            }

            // Return 401 for API requests
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized" });
            return;
        }

        await _next(context);
    }

    private static bool IsPublicPath(string path)
    {
        foreach (var publicPath in PublicPaths)
        {
            if (path.Equals(publicPath, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(publicPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
