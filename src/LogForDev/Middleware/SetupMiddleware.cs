using LogForDev.Core;
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

        // Always allow static files and setup-related requests
        if (path.StartsWith("/css") || path.StartsWith("/js") || path.StartsWith("/lib") ||
            path.StartsWith("/favicon"))
        {
            await _next(context);
            return;
        }

        var isSetupRoute = path.StartsWith(AppConstants.Paths.Setup) || path.StartsWith(AppConstants.Paths.ApiSetup);
        var isAuthRoute = path.StartsWith(AppConstants.Paths.Login) || path.StartsWith(AppConstants.Paths.ApiAuth);

        if (!_setupState.IsSetupComplete())
        {
            // Setup not done - redirect non-setup/non-auth requests to /setup
            if (!isSetupRoute && !isAuthRoute)
            {
                context.Response.Redirect("/setup");
                return;
            }
        }
        else
        {
            // Setup done - redirect /setup page to dashboard
            if (path == AppConstants.Paths.Setup)
            {
                context.Response.Redirect("/");
                return;
            }
        }

        await _next(context);
    }
}
