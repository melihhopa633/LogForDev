using LogForDev.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();

// Add LogForDev services
builder.Services.Configure<LogForDevOptions>(builder.Configuration.GetSection("LogForDev"));
builder.Services.Configure<ClickHouseOptions>(builder.Configuration.GetSection("ClickHouse"));
builder.Services.AddSingleton<IClickHouseService, ClickHouseService>();
builder.Services.AddScoped<ILogService, LogService>();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var clickHouse = scope.ServiceProvider.GetRequiredService<IClickHouseService>();
    await clickHouse.InitializeAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers();

app.Run();
