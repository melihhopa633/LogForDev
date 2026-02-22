using LogForDev.Services;
using LogForDev.Middleware;
using LogForDev.Data;
using LogForDev.Authentication;
using LogForDev.Core;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .WriteTo.Console());

// Add services
builder.Services.AddControllersWithViews();

// Add LogForDev services
builder.Services.Configure<LogForDevOptions>(builder.Configuration.GetSection("LogForDev"));
builder.Services.Configure<ClickHouseOptions>(builder.Configuration.GetSection("ClickHouse"));
builder.Services.AddSingleton<IClickHouseService, ClickHouseService>();
builder.Services.AddScoped<ILogRepository, LogRepository>();
builder.Services.AddSingleton<LogBufferService>();
builder.Services.AddSingleton<ILogBufferService>(sp => sp.GetRequiredService<LogBufferService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogBufferService>());

// Internal app logging
builder.Services.AddSingleton<AppLogService>();
builder.Services.AddSingleton<IAppLogService>(sp => sp.GetRequiredService<AppLogService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<AppLogService>());

// Setup wizard
builder.Services.AddSingleton<ISetupStateService, SetupStateService>();

// Project service
builder.Services.AddSingleton<IProjectService, ProjectService>();

// User service
builder.Services.AddSingleton<IUserService, UserService>();

// Setup orchestrator
builder.Services.AddScoped<ISetupOrchestrator, SetupOrchestrator>();

// Data Protection for cookie encryption
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("./keys"))
    .SetApplicationName("LogForDev");

// Dual authentication (Cookie for dashboard, ApiKey for log ingestion)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationOptions.Scheme;
    options.DefaultChallengeScheme = CookieAuthenticationOptions.Scheme;
})
.AddScheme<CookieAuthenticationOptions, CookieAuthenticationHandler>(
    CookieAuthenticationOptions.Scheme, null)
.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
    ApiKeyAuthenticationOptions.Scheme, null);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppConstants.Auth.DashboardOnlyPolicy, policy =>
        policy.AddAuthenticationSchemes(CookieAuthenticationOptions.Scheme)
              .RequireAuthenticatedUser());
});

// Antiforgery for CSRF protection on cookie-authenticated endpoints
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = ".LogForDev.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Rate limiting for login endpoint
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("login", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

// Initialize database (skip failure if setup not complete)
var setupState = app.Services.GetRequiredService<ISetupStateService>();
try
{
    using (var scope = app.Services.CreateScope())
    {
        var clickHouse = scope.ServiceProvider.GetRequiredService<IClickHouseService>();
        await clickHouse.InitializeAsync();
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    if (setupState.IsSetupComplete())
    {
        logger.LogError(ex, "Failed to initialize ClickHouse");
        throw;
    }
    logger.LogWarning("ClickHouse initialization skipped - setup not complete yet");
}

// Initialize project cache
try
{
    var projectService = app.Services.GetRequiredService<IProjectService>();
    await projectService.RefreshCacheAsync();
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Project cache initialization skipped");
}

// Security warnings at startup
{
    var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var testMode = builder.Configuration.GetValue<bool>("LogForDev:TestMode");
    if (testMode)
        startupLogger.LogWarning("*** UYARI: TestMode AKTIF! TOTP bypass mumkun. Production icin TestMode'u kapatin. ***");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://fonts.googleapis.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'");
    await next();
});

app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseMiddleware<SetupMiddleware>();
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<AuthenticationMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
