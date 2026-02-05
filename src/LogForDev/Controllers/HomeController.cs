using Microsoft.AspNetCore.Mvc;
using LogForDev.Services;
using LogForDev.Models;

namespace LogForDev.Controllers;

public class HomeController : Controller
{
    private readonly ILogService _logService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogService logService, ILogger<HomeController> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var stats = await _logService.GetStatsAsync();
            var apps = await _logService.GetAppNamesAsync();
            var environments = await _logService.GetEnvironmentsAsync();

            ViewBag.Stats = stats;
            ViewBag.Apps = apps;
            ViewBag.Environments = environments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard");
            ViewBag.Stats = new LogStats();
            ViewBag.Apps = new List<string>();
            ViewBag.Environments = new List<string>();
        }
        
        return View();
    }

    public IActionResult Docs()
    {
        return View();
    }

    public IActionResult AppLogs()
    {
        return View();
    }

    public IActionResult Error()
    {
        return View();
    }
}
