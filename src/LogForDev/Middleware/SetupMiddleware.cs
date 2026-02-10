using LogForDev.Services;

namespace LogForDev.Middleware;

public class SetupMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ISetupStateService _setupState;

    public SetupMiddleware(RequestDelegate next, ISetupStateService setupState)
    {
        _next = next;
        _setupState = setupState;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Always allow static files, swagger, and setup-related requests
        if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") ||
            path.StartsWith("/favicon") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        var isSetupRoute = path.StartsWith("/setup") || path.StartsWith("/api/setup");

        if (!_setupState.IsSetupComplete())
        {
            // Setup not done - redirect non-setup requests to /setup
            if (!isSetupRoute)
            {
                context.Response.Redirect("/setup");
                return;
            }
        }
        else
        {
            // Setup done - redirect /setup page to dashboard
            if (path == "/setup")
            {
                context.Response.Redirect("/");
                return;
            }
        }

        await _next(context);
    }
}
