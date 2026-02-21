using LogForDev.Services;
using LogForDev.Middleware;
using LogForDev.Data;
using LogForDev.Authentication;
using Microsoft.AspNetCore.DataProtection;
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
builder.Services.AddScoped<ILogService, LogService>();
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
builder.Services.AddAuthorization();

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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSerilogRequestLogging();
app.UseMiddleware<SetupMiddleware>();
app.UseRouting();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthentication();
app.UseMiddleware<AuthenticationMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
