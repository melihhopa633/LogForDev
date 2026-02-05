using LogForDev.Services;
using LogForDev.Middleware;
using LogForDev.Data;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "LogForDev API",
        Description = "Self-hosted, real-time logging system for developers",
        Contact = new OpenApiContact
        {
            Name = "GitHub",
            Url = new Uri("https://github.com/melihhopa633/LogForDev")
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // API Key authentication
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API key for authentication"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });

    // XML comments
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

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

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var clickHouse = scope.ServiceProvider.GetRequiredService<IClickHouseService>();
    await clickHouse.InitializeAsync();
}

// Swagger UI - always enabled
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "LogForDev API v1");
    options.RoutePrefix = "swagger";
    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    options.DefaultModelsExpandDepth(2);
    options.EnableFilter();
    options.EnableDeepLinking();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
